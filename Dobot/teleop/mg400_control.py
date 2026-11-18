"""
mg400_control.py
ควบคุมแขนกล Dobot MG400 ผ่าน TCP/IP (ไม่ต้องใช้ไลบรารีเสริม)

พอร์ตของ MG400:
  29999 = Dashboard  (Enable/Disable, ClearError, SpeedFactor, RobotMode, GetPose, DO ...)
  30003 = Move       (MovJ, MovL, JointMovJ, RelMovL ...)

เวอร์ชันนี้ทนต่อ error:
  - ไม่ใช้ Sync() รอแล้ว (ตอนแขน error มันจะไม่ตอบ -> ค้างจน timeout)
    แต่ถาม RobotMode() เป็นระยะแทน ถ้าเจอ ERROR/PAUSE จะรู้ทันที
  - เจอ error -> ClearError + Continue + Enable ใหม่ + ต่อพอร์ต move ใหม่ให้อัตโนมัติ
    แล้วโยน RobotError ออกมาให้โปรแกรมข้ามจุดนั้นไป ไม่ค้าง
  - socket timeout / หลุด -> ต่อใหม่แล้วลองซ้ำ 1 ครั้ง
  - ล้างคำตอบเก่าที่ค้างใน buffer ก่อนส่งทุกคำสั่ง (กันคำตอบสลับกัน)

วิธีใช้:
  1) ต่อสาย LAN เข้าพอร์ต LAN1 ของ MG400, ตั้ง IP คอมวงเดียวกัน (เช่น 192.168.1.100)
  2) ปิด DobotStudio / DobotSCStudio ก่อน
  3) python mg400_control.py [--ip 192.168.1.6]
"""

import argparse
import re
import socket
import threading
import time

JOINT_LIMITS = {
    "j1": (-160.0, 160.0),
    "j2": (-25.0, 85.0),
    "j3": (-25.0, 105.0),
    "j4": (-360.0, 360.0),
}
MAX_REACH_MM = 440.0

# ตรวจมุมข้อต่อก่อนสั่ง MovJ/MovL ด้วย InverseSolution() ของแขนเอง
# (กัน error joint limit ไม่ให้เกิดตั้งแต่แรก) — ถ้า firmware ไม่รองรับ จะปิดตัวเองอัตโนมัติ
USE_IK_CHECK = True

# RobotMode() ของ MG400
MODE_NAMES = {1: "INIT", 2: "BRAKE_OPEN", 4: "DISABLED", 5: "ENABLED(ว่าง)",
              6: "BACKDRIVE", 7: "RUNNING", 8: "SINGLE_MOVE", 9: "ERROR",
              10: "PAUSE", 11: "JOG"}
MODE_DISABLED, MODE_IDLE, MODE_RUNNING, MODE_ERROR, MODE_PAUSE = 4, 5, 7, 9, 10


class RobotError(Exception):
    """แขนปฏิเสธคำสั่ง หรือเกิด alarm ระหว่างเคลื่อนที่ (กู้คืนให้แล้ว แขนพร้อมรับคำสั่งต่อ)"""


class MG400:
    def __init__(self, ip="192.168.1.6", dashboard_port=29999, move_port=30003,
                 timeout=3.0, move_timeout=30.0):
        self.ip = ip
        self.ports = {"dash": dashboard_port, "move": move_port}
        self.timeout = timeout
        self.move_timeout = move_timeout     # เวลาสูงสุดของการเคลื่อนที่ 1 ครั้ง
        self.socks = {}
        # bridge มีเธรดรับ UDP ที่อาจสั่ง stop() แทรกระหว่างลูปหลักกำลังรอแขนขยับ
        # ล็อกไว้ไม่ให้ 2 เธรดเขียน/อ่าน socket เดียวกันพร้อมกัน
        self.locks = {"dash": threading.RLock(), "move": threading.RLock()}
        self.ik_check = USE_IK_CHECK
        self._connect("dash")
        self._connect("move")
        print(f"[OK] เชื่อมต่อ MG400 ที่ {ip} แล้ว")

    # ================= ระดับล่าง: socket =================
    def _connect(self, name):
        old = self.socks.get(name)
        if old:
            try:
                old.close()
            except OSError:
                pass
        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        s.settimeout(self.timeout)
        s.connect((self.ip, self.ports[name]))
        self.socks[name] = s

    def _drain(self, sock):
        """ทิ้งคำตอบเก่าที่ค้างใน buffer (เช่นคำตอบที่มาช้าหลัง timeout)"""
        sock.setblocking(False)
        try:
            while sock.recv(4096):
                pass
        except (BlockingIOError, OSError):
            pass
        finally:
            sock.settimeout(self.timeout)

    def _recv_reply(self, sock):
        data = sock.recv(1024).decode("utf-8", errors="ignore")
        # คำตอบจบด้วย ';' ถ้ายังไม่ครบ รออ่านต่ออีกนิด
        end = time.time() + 0.3
        while not data.rstrip().endswith(";") and time.time() < end:
            try:
                sock.settimeout(0.05)
                more = sock.recv(1024).decode("utf-8", errors="ignore")
                if not more:
                    break
                data += more
            except socket.timeout:
                pass
        sock.settimeout(self.timeout)
        return data.strip()

    def _send(self, name, cmd, verbose=True, retry=True):
        with self.locks[name]:
            return self._send_locked(name, cmd, verbose, retry)

    def _send_locked(self, name, cmd, verbose, retry):
        sock = self.socks[name]
        try:
            self._drain(sock)
            sock.sendall(cmd.encode("utf-8"))
            reply = self._recv_reply(sock)
            if not reply:
                raise ConnectionError("แขนปิดการเชื่อมต่อ")
        except (socket.timeout, OSError, ConnectionError) as e:
            if not retry:
                raise
            print(f"  [!] {cmd}: {e} -> ต่อพอร์ต {name} ใหม่แล้วลองอีกครั้ง")
            self._connect(name)
            return self._send_locked(name, cmd, verbose, False)
        if verbose:
            print(f"  >> {cmd:<45} << {reply}")
        return reply

    @staticmethod
    def _ok(reply):
        return reply.split(",", 1)[0].strip() == "0"

    @staticmethod
    def _parse_values(reply):
        m = re.search(r"\{([^}]*)\}", reply)
        if not m:
            return None
        try:
            return [float(v) for v in m.group(1).split(",") if v.strip()]
        except ValueError:
            return None

    def dashboard(self, cmd, verbose=True):
        return self._send("dash", cmd, verbose)

    def move_cmd(self, cmd):
        return self._send("move", cmd)

    # ================= สถานะ / กู้คืน =================
    def robot_mode(self):
        v = self._parse_values(self.dashboard("RobotMode()", verbose=False))
        return int(v[0]) if v else None

    def error_info(self):
        return self.dashboard("GetErrorID()", verbose=False)

    def recover(self):
        """เคลียร์ alarm แล้วทำให้แขนพร้อมรับคำสั่งใหม่"""
        print("  [กู้คืน] error:", self.error_info())
        self.dashboard("ClearError()")
        self.dashboard("Continue()")          # ใช้หลังชน (firmware เก่าอาจไม่มี ไม่เป็นไร)
        time.sleep(0.3)
        if self.robot_mode() == MODE_DISABLED:
            self.dashboard("EnableRobot()")
            time.sleep(1.0)
        self._connect("move")                 # ล้างคิว/สถานะค้างของพอร์ต move
        print(f"  [กู้คืน] เสร็จ RobotMode = {MODE_NAMES.get(self.robot_mode(), '?')}")

    def wait_done(self, start_grace=0.3):
        """รอจนเคลื่อนที่เสร็จ โดยถาม RobotMode() แทน Sync()"""
        t0 = time.time()
        seen_running = False
        while True:
            mode = self.robot_mode()
            if mode in (MODE_ERROR, MODE_PAUSE):
                self.recover()
                raise RobotError(f"แขนเกิด {MODE_NAMES[mode]} ระหว่างเคลื่อนที่ (ชน/เกินลิมิต)")
            if mode == MODE_RUNNING:
                seen_running = True
            elif mode == MODE_IDLE and (seen_running or time.time() - t0 > start_grace):
                return
            elif mode == MODE_DISABLED:
                self.recover()
                raise RobotError("มอเตอร์ถูกปิด (DISABLED) ระหว่างเคลื่อนที่")
            if time.time() - t0 > self.move_timeout:
                self.dashboard("ResetRobot()")    # หยุดการเคลื่อนที่
                raise RobotError(f"เคลื่อนที่นานเกิน {self.move_timeout} วินาที")
            time.sleep(0.05)

    def _run_move(self, cmd, wait):
        # แขนค้าง error จากรอบก่อนอยู่ -> เคลียร์ก่อน ไม่งั้นคำสั่งจะไม่ทำงาน
        if self.robot_mode() in (MODE_ERROR, MODE_PAUSE, MODE_DISABLED):
            self.recover()
        reply = self.move_cmd(cmd)
        if not self._ok(reply):
            self.recover()
            raise RobotError(f"แขนปฏิเสธคำสั่ง {cmd}: {reply}")
        if wait:
            self.wait_done()

    # ================= คำสั่งพื้นฐาน =================
    def enable(self):
        self.dashboard("ClearError()")
        r = self.dashboard("EnableRobot()")
        time.sleep(1.0)
        return r

    def disable(self):
        return self.dashboard("DisableRobot()")

    def clear_error(self):
        return self.recover()

    def reset(self):
        return self.dashboard("ResetRobot()")

    def speed(self, percent):
        percent = int(max(1, min(100, percent)))
        return self.dashboard(f"SpeedFactor({percent})")

    def do(self, index, on):
        return self.dashboard(f"DO({index},{1 if on else 0})")

    def get_pose(self):
        return self._parse_values(self.dashboard("GetPose()"))

    def get_angle(self):
        return self._parse_values(self.dashboard("GetAngle()"))

    # ================= ตรวจค่าก่อนสั่ง =================
    @staticmethod
    def check_xyz(x, y, z):
        reach = (x ** 2 + y ** 2) ** 0.5
        if reach > MAX_REACH_MM:
            raise ValueError(f"ระยะ XY = {reach:.1f} mm เกิน {MAX_REACH_MM} mm")

    @staticmethod
    def check_joints(j1, j2, j3, j4):
        for name, val in zip(("j1", "j2", "j3", "j4"), (j1, j2, j3, j4)):
            lo, hi = JOINT_LIMITS[name]
            if not lo <= val <= hi:
                raise ValueError(f"{name} = {val:.1f}° อยู่นอกช่วง {lo}..{hi}°")

    def check_ik(self, x, y, z, r):
        """ให้แขนคำนวณมุมข้อต่อของจุดนั้นก่อน ถ้าไปไม่ได้/เกินลิมิตจะไม่สั่งเลย"""
        if not self.ik_check:
            return
        reply = self.dashboard(f"InverseSolution({x},{y},{z},{r},0,0)", verbose=False)
        joints = self._parse_values(reply)
        if self._ok(reply) and joints and len(joints) >= 4:
            self.check_joints(*joints[:4])
        elif "not" in reply.lower() or "unknown" in reply.lower() or not reply:
            print("  [i] firmware นี้ไม่มี InverseSolution() -> ปิดการเช็ก IK")
            self.ik_check = False
        else:
            raise ValueError(f"จุด ({x}, {y}, {z}, {r}) แขนไปไม่ได้: {reply}")

    # ================= คำสั่งเคลื่อนที่ =================
    def movj(self, x, y, z, r=0.0, wait=True):
        self.check_xyz(x, y, z)
        self.check_ik(x, y, z, r)
        self._run_move(f"MovJ({x:.2f},{y:.2f},{z:.2f},{r:.2f})", wait)

    def movl(self, x, y, z, r=0.0, wait=True):
        self.check_xyz(x, y, z)
        self.check_ik(x, y, z, r)
        self._run_move(f"MovL({x:.2f},{y:.2f},{z:.2f},{r:.2f})", wait)

    def joint_movj(self, j1, j2, j3, j4=0.0, wait=True):
        self.check_joints(j1, j2, j3, j4)
        self._run_move(f"JointMovJ({j1:.2f},{j2:.2f},{j3:.2f},{j4:.2f})", wait)

    def rel_movl(self, dx, dy, dz, dr=0.0, wait=True):
        self._run_move(f"RelMovL({dx:.2f},{dy:.2f},{dz:.2f},{dr:.2f})", wait)

    def sync(self):
        self.wait_done()

    def close(self):
        for s in self.socks.values():
            try:
                s.close()
            except OSError:
                pass
        print("[OK] ปิดการเชื่อมต่อแล้ว")


# ----------------------------------------------------------------------
# โหมดพิมพ์คำสั่งเอง
# ----------------------------------------------------------------------
HELP = """
คำสั่ง (r/j4 ไม่ใส่ = 0):
  movj  x y z [r]        ไป XYZ แบบ joint (mm, องศา)
  movl  x y z [r]        ไป XYZ แบบเส้นตรง
  joint j1 j2 j3 [j4]    สั่งมุมข้อต่อ (องศา)
  rel   dx dy dz [dr]    ขยับเพิ่มจากจุดเดิม
  speed n                ความเร็ว 1-100 %
  do    index 0|1        เปิด/ปิด digital output
  pose / angle / mode    ดูตำแหน่ง / มุม / สถานะแขน
  home                   ไปท่าตั้งต้น (J = 0,0,0,0)
  enable / disable / clear / stop
  help / quit
"""


def interactive(robot: MG400):
    print(HELP)
    while True:
        try:
            line = input("MG400> ").strip()
        except (EOFError, KeyboardInterrupt):
            print()
            break
        if not line:
            continue
        cmd, *args = line.split()
        cmd = cmd.lower()
        try:
            nums = [float(a) for a in args]
            if cmd == "movj":
                robot.movj(*nums)
            elif cmd == "movl":
                robot.movl(*nums)
            elif cmd == "joint":
                robot.joint_movj(*nums)
            elif cmd == "rel":
                robot.rel_movl(*nums)
            elif cmd == "speed":
                robot.speed(nums[0])
            elif cmd == "do":
                robot.do(int(nums[0]), int(nums[1]))
            elif cmd == "pose":
                print("  XYZR =", robot.get_pose())
            elif cmd == "angle":
                print("  J1-J4 =", robot.get_angle())
            elif cmd == "mode":
                m = robot.robot_mode()
                print(f"  RobotMode = {m} ({MODE_NAMES.get(m, '?')})")
            elif cmd == "home":
                robot.joint_movj(0, 0, 0, 0)
            elif cmd == "enable":
                robot.enable()
            elif cmd == "disable":
                robot.disable()
            elif cmd == "clear":
                robot.clear_error()
            elif cmd == "stop":
                robot.reset()
            elif cmd == "help":
                print(HELP)
            elif cmd in ("quit", "exit", "q"):
                break
            else:
                print("  ไม่รู้จักคำสั่ง — พิมพ์ help")
        except RobotError as e:
            print(f"  [แขน error] {e} — เคลียร์แล้ว สั่งจุดใหม่ได้เลย")
        except (ValueError, TypeError, IndexError) as e:
            print(f"  [ค่าไม่ถูกต้อง] {e}")
        except (socket.timeout, OSError) as e:
            print(f"  [เชื่อมต่อไม่ได้] {e} — เช็กสาย LAN / IP")


def demo(robot: MG400):
    robot.speed(30)
    robot.joint_movj(0, 0, 0, 0)
    robot.movj(300, 0, 50, 0)
    robot.movl(300, 0, 0, 0)
    robot.do(1, 1)
    time.sleep(0.5)
    robot.movl(300, 0, 50, 0)
    robot.movj(250, 150, 50, 30)
    robot.movl(250, 150, 0, 30)
    robot.do(1, 0)
    time.sleep(0.5)
    robot.rel_movl(0, 0, 50)
    print("ตำแหน่งสุดท้าย:", robot.get_pose())


def main():
    ap = argparse.ArgumentParser(description="ควบคุม Dobot MG400")
    ap.add_argument("--ip", default="192.168.1.6")
    ap.add_argument("--speed", type=int, default=30)
    ap.add_argument("--demo", action="store_true")
    a = ap.parse_args()

    robot = MG400(a.ip)
    try:
        robot.enable()
        robot.speed(a.speed)
        if a.demo:
            demo(robot)
        else:
            interactive(robot)
    finally:
        try:
            robot.disable()
        except OSError:
            pass
        robot.close()


if __name__ == "__main__":
    main()
