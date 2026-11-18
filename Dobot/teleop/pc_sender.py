"""
pc_sender.py  —  ส่งค่าทดสอบไปที่ mg400_bridge.py แทนแว่น
ข้อความรูปแบบเดียวกับที่ MG400Teleop.cs (Unity) ส่ง

รัน:  python pc_sender.py                     (bridge อยู่เครื่องเดียวกัน)
      python pc_sender.py --host 192.168.0.10
"""

import argparse
import json
import math
import socket
import time

HELP = """
จำลอง Controller (เหมือนแว่นส่ง):
  ctrl [วินาที]      กด Grip -> ขยับ Controller เป็นวงกลมรัศมี 5 cm -> ปล่อย Grip
  up [cm]            กด Grip -> ยก Controller ขึ้นตรง ๆ -> ปล่อย (ค่าเริ่ม 5 cm)
  suck 0|1           Trigger (หัวดูด)
  home               ปุ่ม A
  stop               ปุ่ม B (หยุดฉุกเฉิน)
สั่งตรง (ไม่ผ่านแว่น):
  p x y z [r]        ไปพิกัดแขน (mm)
  j j1 j2 j3 [j4]    ไปมุมข้อต่อ (องศา)
  s n                ความเร็ว %
  help / q
"""


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--host", default="127.0.0.1")
    ap.add_argument("--port", type=int, default=9000)
    a = ap.parse_args()

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    dest = (a.host, a.port)

    def send(msg, show=True):
        sock.sendto(json.dumps(msg).encode("utf-8"), dest)
        if show:
            print("  ->", msg)

    def controller_path(fn, dur):
        """จำลอง Controller ที่ (0, 1.0, 0.3) m แล้วขยับตาม fn(t) -> (dx, dy, dz)"""
        base = (0.0, 1.0, 0.3)
        send({"type": "engage", "x": base[0], "y": base[1], "z": base[2]})
        t0 = time.time()
        try:
            while (t := time.time() - t0) < dur:
                dx, dy, dz = fn(t)
                send({"type": "pose", "frame": "headset",
                      "x": base[0] + dx, "y": base[1] + dy, "z": base[2] + dz}, show=False)
                time.sleep(0.05)                     # 20 Hz เท่ากับ Unity
        finally:
            send({"type": "release"})

    print(f"ส่งไปที่ {a.host}:{a.port}")
    print(HELP)
    while True:
        try:
            line = input("send> ").strip()
        except (EOFError, KeyboardInterrupt):
            break
        if not line:
            continue
        cmd, *args = line.split()
        try:
            v = [float(x) for x in args]
            if cmd == "ctrl":
                dur = v[0] if v else 8
                controller_path(lambda t: (0.05 * math.sin(2 * math.pi * t / 4), 0.0,
                                           0.05 * math.cos(2 * math.pi * t / 4) - 0.05), dur)
            elif cmd == "up":
                cm = v[0] if v else 5
                controller_path(lambda t: (0.0, min(t / 2, 1) * cm / 100, 0.0), 3)
            elif cmd == "suck":
                send({"type": "grip", "value": int(v[0])})
            elif cmd == "home":
                send({"type": "home"})
            elif cmd == "stop":
                for _ in range(3):
                    send({"type": "stop"}, show=False)
                print("  -> stop x3")
            elif cmd == "p":
                send({"type": "pose", "frame": "robot",
                      "x": v[0], "y": v[1], "z": v[2], "r": v[3] if len(v) > 3 else 0})
            elif cmd == "j":
                send({"type": "joint", "j1": v[0], "j2": v[1], "j3": v[2],
                      "j4": v[3] if len(v) > 3 else 0})
            elif cmd == "s":
                send({"type": "speed", "value": int(v[0])})
            elif cmd == "help":
                print(HELP)
            elif cmd in ("q", "quit", "exit"):
                break
            else:
                print("  ไม่รู้จักคำสั่ง — พิมพ์ help")
        except (IndexError, ValueError):
            print("  ค่าไม่ครบ/ไม่ใช่ตัวเลข — พิมพ์ help")
        except KeyboardInterrupt:
            print("\n  หยุดสตรีม")


if __name__ == "__main__":
    main()
