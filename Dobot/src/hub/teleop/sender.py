"""Send Robot Panel and Teleop commands to the Teleop Bridge and print its Robot Status.

Tests the bridge without the headset (ADR-0024):

    python -m hub.teleop.sender                     # bridge on this PC
    python -m hub.teleop.sender --host 192.168.1.20
"""

import argparse
import json
import socket
import threading
from collections.abc import Sequence
from typing import Any

from hub.teleop.core import STATUS_PORT, UDP_PORT

HELP = """\
  enable | disable | clear | resume | home | stop
  jog x|y|z STEP       step in mm: +-1, +-10 or +-50
  move X Y Z R         move to a pose (mm, deg)
  suck 0|1             suction off / on
  speed N              speed factor, 1-100 %
  p X Y Z [R]          robot-frame pose (Teleop test message)
  j J1 J2 J3 [J4]      joint angles (deg)
  raw TEXT             send TEXT as it is (e.g. to test a malformed message)
  help | q"""

_NO_ARGUMENT_COMMANDS = {
    "enable": "enable",
    "disable": "disable",
    "clear": "clear_error",
    "resume": "resume",
    "home": "home",
    "stop": "stop",
}


def build_message(words: Sequence[str]) -> dict[str, Any]:
    """Turn one typed line, split into words, into a Teleop message; raise ValueError if bad."""
    name, args = words[0], list(words[1:])
    if name in _NO_ARGUMENT_COMMANDS and not args:
        return {"type": _NO_ARGUMENT_COMMANDS[name]}
    if name == "jog" and len(args) == 2:
        return {"type": "jog", "axis": args[0], "step": float(args[1])}
    numbers = [float(word) for word in args]
    if name == "move" and len(numbers) == 4:
        return dict(zip(("type", "x", "y", "z", "r"), ("move_to", *numbers), strict=True))
    if name == "suck" and len(numbers) == 1:
        return {"type": "grip", "value": int(numbers[0])}
    if name == "speed" and len(numbers) == 1:
        return {"type": "speed", "value": int(numbers[0])}
    if name == "p" and len(numbers) in (3, 4):
        return {"type": "pose", "frame": "robot", **dict(zip("xyzr", numbers, strict=False))}
    if name == "j" and len(numbers) in (3, 4):
        return {"type": "joint", **dict(zip(("j1", "j2", "j3", "j4"), numbers, strict=False))}
    raise ValueError(f"unknown command or wrong arguments: {' '.join(words)}")


def _print_status_forever(send_socket: socket.socket) -> None:
    last = None
    while True:
        data, _ = send_socket.recvfrom(4096)
        if data != last:  # the bridge repeats itself a few times a second; show changes only
            last = data
            print(f"\n  <- {data.decode('utf-8', 'replace')}")


def main(argv: Sequence[str] | None = None) -> None:
    """Read commands from the keyboard until q."""
    parser = argparse.ArgumentParser(
        prog="python -m hub.teleop.sender",
        description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument("--host", default="127.0.0.1", help="Teleop Bridge address")
    parser.add_argument("--port", type=int, default=UDP_PORT, help="bridge UDP port")
    parser.add_argument(
        "--status-port", type=int, default=STATUS_PORT, help="UDP port to hear Robot Status on"
    )
    args = parser.parse_args(argv)

    status_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    status_socket.bind(("0.0.0.0", args.status_port))
    threading.Thread(target=_print_status_forever, args=(status_socket,), daemon=True).start()
    send_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    destination = (args.host, args.port)
    print(f"sending to {args.host}:{args.port}, Robot Status on port {args.status_port}")
    print(HELP)
    while True:
        try:
            line = input("send> ").strip()
        except (EOFError, KeyboardInterrupt):
            break
        if not line:
            continue
        if line in ("q", "quit", "exit"):
            break
        if line == "help":
            print(HELP)
            continue
        if line.startswith("raw "):
            send_socket.sendto(line[4:].encode("utf-8"), destination)
            continue
        try:
            message = build_message(line.split())
        except ValueError as error:
            print(f"  {error}; type help")
            continue
        # stop goes three times, like the headset, so one lost packet cannot drop it.
        for _ in range(3 if message["type"] == "stop" else 1):
            send_socket.sendto(json.dumps(message).encode("utf-8"), destination)
        print(f"  -> {message}")


if __name__ == "__main__":
    main()
