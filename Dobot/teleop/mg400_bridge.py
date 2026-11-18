"""
mg400_bridge.py  —  รับค่าตำแหน่งทาง UDP แล้วสั่ง Dobot MG400 ให้ขยับตาม (Teleop)

ผู้ส่ง:
  - Unity บน Quest 3 (MG400Teleop.cs) : Controller ขวา
  - pc_sender.py                       : ทดสอบจากคอม ไม่ต้องใช้แว่น

ข้อความ (JSON 1 ก้อนต่อ 1 UDP packet):
  {"type":"engage", "x":..,"y":..,"z":..}   เริ่มบังคับ: ตำแหน่ง Controller ตอนนี้ = ตำแหน่งแขนตอนนี้
  {"type":"pose", "frame":"headset", "x":..,"y":..,"z":..}   ตำแหน่ง Controller (เมตร, แกน Unity)
  {"type":"pose", "frame":"robot",   "x":..,"y":..,"z":..,"r":..}   พิกัดแขนตรง ๆ (mm)
  {"type":"release"}            ปล่อย Grip: แขนหยุดตาม
  {"type":"grip", "value":1|0}  หัวดูด ดูด/ปล่อย
  {"type":"home"}               กลับท่าตั้งต้น
  {"type":"stop"}               หยุดฉุกเฉิน (ทำทันทีแม้แขนกำลังขยับ) — ต้อง engage ใหม่ถึงขยับต่อ
  {"type":"joint", "j1":..,"j2":..,"j3":..,"j4":..}
  {"type":"speed", "value":1-100}
  {"type":"calibrate", "x":..,"y":..,"z":..}   (แบบเก่า) ตำแหน่งนี้ = ROBOT_ORIGIN

รัน:
  python mg400_bridge.py --dry-run          # ไม่ต่อแขน พิมพ์ว่าจะสั่งอะไร
  python mg400_bridge.py --ip 192.168.1.6   # ต่อแขนจริง
"""

import argparse
import json
import math
import socket
import threading
import time

try:
    from mg400_control import RobotError
except ImportError:
    class RobotError(Exception):
        pass

# =====================================================================
# ตั้งค่า
# =====================================================================
UDP_PORT = 9000
CONTROL_HZ = 10          # รอบต่อวินาทีที่สั่งแขน (ใช้ค่าล่าสุดเสมอ ไม่ต่อคิว)
DEADBAND_MM = 3.0        # ขยับน้อยกว่านี้ไม่สั่ง (กันสั่น)
SMOOTHING = 0.5          # 0 = ไม่กรอง, ใกล้ 1 = นุ่มแต่ตามช้า
STALE_SEC = 0.5          # ไม่มีค่าใหม่เกินนี้ -> หยุดค้างที่เดิม
SUCTION_DO_INDEX = 1     # ขา DO ของหัวดูด

# ขอบเขตปลอดภัย (ค่าประมาณ — วัดหน้างานแล้วปรับให้แคบลง)
MIN_REACH_MM = 170.0
MAX_REACH_MM = 420.0
Z_MIN_MM, Z_MAX_MM = -100.0, 150.0   # Z_MIN: ต้อง calibrate ให้หัวดูดไม่ชนโต๊ะ (issue #49)
R_MIN, R_MAX = -170.0, 170.0

# การแปลงแกน Controller -> แขน
HEADSET_SCALE = 1.0      # Controller ขยับ 1 cm -> แขนขยับ 1 cm
HEADSET_YAW_DEG = 0.0    # ถ้าคนยืนหันคนละทิศกับด้านหน้าแขน ให้ใส่มุมชดเชย (องศา)
DEFAULT_ROBOT_POSE = (300.0, 0.0, 50.0, 0.0)   # ใช้เมื่ออ่านตำแหน่งแขนไม่ได้ (dry-run)

# จุดอ้างอิง (ถูกตั้งใหม่ทุกครั้งที่ engage)
HEADSET_ORIGIN = (0.0, 1.0, 0.3)
ROBOT_ORIGIN = DEFAULT_ROBOT_POSE[:3]


def map_headset_to_robot(hx, hy, hz):
    """Unity: X ขวา, Y ขึ้น, Z หน้า (m)  ->  MG400: X หน้า, Y ซ้าย, Z ขึ้น (mm)
       คิดแบบ 'ขยับเท่าไหร่ แขนขยับเท่านั้น' จากจุดที่ engage"""
    dx = (hx - HEADSET_ORIGIN[0]) * 1000 * HEADSET_SCALE
    dy = (hy - HEADSET_ORIGIN[1]) * 1000 * HEADSET_SCALE
    dz = (hz - HEADSET_ORIGIN[2]) * 1000 * HEADSET_SCALE
    fwd, left = dz, -dx
    if HEADSET_YAW_DEG:
        a = math.radians(HEADSET_YAW_DEG)
        fwd, left = fwd * math.cos(a) - left * math.sin(a), fwd * math.sin(a) + left * math.cos(a)
    return ROBOT_ORIGIN[0] + fwd, ROBOT_ORIGIN[1] + left, ROBOT_ORIGIN[2] + dy


def clamp_workspace(x, y, z, r):
    """บีบค่าให้อยู่ในพื้นที่ปลอดภัย (ไม่ทิ้ง เพื่อให้แขนไปหยุดที่ขอบแทน)"""
    reach = math.hypot(x, y)
    if reach < 1e-6:
        x, reach = MIN_REACH_MM, MIN_REACH_MM
    target = min(max(reach, MIN_REACH_MM), MAX_REACH_MM)
    if target != reach:
        x, y = x * target / reach, y * target / reach
    return x, y, min(max(z, Z_MIN_MM), Z_MAX_MM), min(max(r, R_MIN), R_MAX)


# =====================================================================
# รับ UDP — เก็บแค่ตำแหน่งล่าสุด; คำสั่งอื่นต่อคิว; "stop" ทำทันที
# =====================================================================
class Receiver(threading.Thread):
    def __init__(self, port, on_stop):
        super().__init__(daemon=True)
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.sock.bind(("0.0.0.0", port))
        self.lock = threading.Lock()
        self.on_stop = on_stop
        self.latest_pose = None
        self.events = []
        self.senders = set()
        print(f"[UDP] รอรับค่าที่พอร์ต {port}")

    def run(self):
        while True:
            data, addr = self.sock.recvfrom(4096)
            if addr[0] not in self.senders:
                self.senders.add(addr[0])
                print(f"[UDP] ได้รับข้อมูลจาก {addr[0]}")
            try:
                msg = json.loads(data.decode("utf-8"))
            except (UnicodeDecodeError, json.JSONDecodeError):
                print(f"[UDP] ข้อความเสียจาก {addr}: {data[:60]!r}")
                continue
            kind = msg.get("type")
            if kind == "stop":
                self.on_stop()                 # ไม่รอคิว — แทรกได้ระหว่างแขนกำลังขยับ
            with self.lock:
                if kind == "pose":
                    self.latest_pose = (msg, time.time())
                else:
                    if kind in ("release", "stop"):
                        self.latest_pose = None  # ทิ้งตำแหน่งค้าง
                    self.events.append(msg)

    def take(self):
        with self.lock:
            pose, events = self.latest_pose, self.events
            self.events = []
            return pose, events


class FakeRobot:
    """แขนจำลองสำหรับ --dry-run"""
    def __getattr__(self, name):
        def f(*a, **k):
            print(f"  [dry-run] {name}{tuple(round(v, 1) if isinstance(v, float) else v for v in a)}")
        return f

    def get_pose(self):
        return None


# =====================================================================
# ลูปควบคุม
# =====================================================================
class Bridge:
    def __init__(self, robot):
        self.robot = robot
        self.engaged = False       # ต้องได้ engage ก่อนถึงจะตาม pose แบบ headset
        self.stopped = False       # กด stop แล้ว -> ไม่ขยับจนกว่าจะ engage ใหม่
        self.last_sent = None
        self.filt = None
        self.suction = None
        self.origin_r = DEFAULT_ROBOT_POSE[3]

    def emergency_stop(self):
        self.stopped = True
        self.engaged = False
        print("[STOP] หยุดฉุกเฉิน — กด Grip ใหม่เพื่อบังคับต่อ")
        try:
            self.robot.reset()                 # ResetRobot(): หยุดการเคลื่อนที่ทันที
        except OSError as e:
            print(f"[STOP] ส่งคำสั่งหยุดไม่สำเร็จ: {e} — ใช้ E-stop จริง!")

    def current_robot_pose(self):
        try:
            p = self.robot.get_pose()
        except (OSError, RobotError):
            p = None
        if p and len(p) >= 4:
            return tuple(p[:4])
        return self.last_sent or DEFAULT_ROBOT_POSE

    def set_suction(self, value):
        if value != self.suction:
            self.robot.do(SUCTION_DO_INDEX, value)
            self.suction = value

    def handle_event(self, ev):
        global HEADSET_ORIGIN, ROBOT_ORIGIN
        kind = ev.get("type")
        if kind == "engage":
            HEADSET_ORIGIN = (float(ev["x"]), float(ev["y"]), float(ev["z"]))
            pose = self.current_robot_pose()
            ROBOT_ORIGIN, self.origin_r = pose[:3], pose[3]
            self.engaged, self.stopped = True, False
            self.last_sent = self.filt = None
            print(f"[engage] แขนเริ่มตาม Controller จาก {tuple(round(v, 1) for v in ROBOT_ORIGIN)}")
        elif kind == "release":
            if self.engaged:
                print("[release] แขนหยุดตาม")
            self.engaged = False
        elif kind == "calibrate":
            HEADSET_ORIGIN = (float(ev["x"]), float(ev["y"]), float(ev["z"]))
            ROBOT_ORIGIN = DEFAULT_ROBOT_POSE[:3]
            self.engaged, self.stopped = True, False
            self.last_sent = self.filt = None
            print(f"[calibrate] HEADSET_ORIGIN = {HEADSET_ORIGIN}")
        elif kind == "stop":
            pass                                   # ทำไปแล้วใน Receiver
        elif kind == "grip":
            self.set_suction(int(ev["value"]))
        elif kind == "home":
            if self.stopped:
                print("[home] ถูกหยุดฉุกเฉินอยู่ — ไม่ขยับ")
                return
            self.engaged = False
            self.robot.joint_movj(0, 0, 0, 0)
            self.last_sent = self.filt = None
        elif kind == "joint":
            self.robot.joint_movj(float(ev["j1"]), float(ev["j2"]),
                                  float(ev["j3"]), float(ev.get("j4", 0)))
            self.last_sent = self.filt = None
        elif kind == "speed":
            self.robot.speed(int(ev["value"]))
        else:
            print(f"[?] ไม่รู้จัก type: {kind}")

    def handle_pose(self, msg):
        if self.stopped:
            return
        if msg.get("frame", "robot") == "headset":
            if not self.engaged:
                return                              # ยังไม่กด Grip -> ไม่ตาม
            x, y, z = map_headset_to_robot(float(msg["x"]), float(msg["y"]), float(msg["z"]))
            r = float(msg.get("r", self.origin_r))   # ไม่หมุนหัว คงมุมเดิมไว้
        else:
            x, y, z = float(msg["x"]), float(msg["y"]), float(msg["z"])
            r = float(msg.get("r", 0))
        x, y, z, r = clamp_workspace(x, y, z, r)

        if self.filt is None:
            self.filt = (x, y, z, r)
        else:
            a = SMOOTHING
            self.filt = tuple(a * f + (1 - a) * n for f, n in zip(self.filt, (x, y, z, r)))

        if self.last_sent is None or math.dist(self.filt[:3], self.last_sent[:3]) > DEADBAND_MM \
                or abs(self.filt[3] - self.last_sent[3]) > 2:
            self.last_sent = self.filt         # ตั้งก่อนสั่ง: จุดที่ error จะไม่ถูกยิงซ้ำ
            self.robot.movj(*self.filt)

        if "grip" in msg:
            self.set_suction(int(msg["grip"]))

    def loop(self, rx):
        period = 1.0 / CONTROL_HZ
        while True:
            t0 = time.time()
            pose, events = rx.take()
            for ev in events:
                self.safe(self.handle_event, ev)
            if pose and time.time() - pose[1] < STALE_SEC:
                self.safe(self.handle_pose, pose[0])
            time.sleep(max(0.0, period - (time.time() - t0)))

    def safe(self, fn, msg):
        try:
            fn(msg)
        except (KeyError, ValueError, TypeError) as e:
            print(f"[!] ข้อความผิดรูปแบบ/เกินช่วง ข้าม: {msg} ({e})")
        except RobotError as e:
            print(f"[แขน error] {e} — ข้าม รอค่าถัดไป")
            self.filt = None
        except OSError as e:
            print(f"[เชื่อมต่อ] {e} — จะลองใหม่รอบหน้า")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--ip", default="192.168.1.6")
    ap.add_argument("--port", type=int, default=UDP_PORT)
    ap.add_argument("--speed", type=int, default=30)
    ap.add_argument("--dry-run", action="store_true", help="ไม่ต่อแขนจริง แค่พิมพ์คำสั่ง")
    a = ap.parse_args()

    if a.dry_run:
        robot = FakeRobot()
    else:
        from mg400_control import MG400
        robot = MG400(a.ip)
        robot.enable()
        robot.speed(a.speed)

    bridge = Bridge(robot)
    rx = Receiver(a.port, bridge.emergency_stop)
    rx.start()
    try:
        bridge.loop(rx)
    except KeyboardInterrupt:
        print("\nหยุด")
    finally:
        if not a.dry_run:
            try:
                robot.do(SUCTION_DO_INDEX, 0)
                robot.disable()
            except OSError:
                pass
            robot.close()


if __name__ == "__main__":
    main()
