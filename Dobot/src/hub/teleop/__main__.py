"""Teleop Bridge shell: UDP on 9000 in, MG400 out, Robot Status on 9001 back (ADR-0017, 0024).

    python -m hub.teleop --dry-run          # no arm: log each command instead
    python -m hub.teleop --ip 192.168.1.6   # the real arm (Robot pen)
"""

import argparse
import json
import logging
import socket
import threading
import time
from collections.abc import Sequence

from hub.dashboard import TcpDashboard
from hub.teleop.core import (
    DEFAULT_ROBOT_POSE,
    STATUS_PORT,
    SUCTION_DO_INDEX,
    UDP_PORT,
    Pose,
    TeleopCore,
)
from hub.teleop.mg400_arm import MG400Arm

logger = logging.getLogger("hub.teleop")

DASHBOARD_PORT = 29999
MOTION_PORT = 30003
STATUS_HZ = 4  # Robot Status packets per second; the Robot Panel calls the link dead after 2 s
_DATAGRAM_BYTES = 4096


def _log_call(name: str, *args: object) -> None:
    shown = tuple(round(a, 1) if isinstance(a, float) else a for a in args)
    logger.info("  [dry-run] %s%s", name, shown)


class DryRunArm:
    """Logs each Arm call instead of moving; it pretends to be wherever it was last sent."""

    def __init__(self) -> None:
        """Start enabled, at the default pose."""
        self._pose: Pose = DEFAULT_ROBOT_POSE
        self._is_enabled = True

    def reset(self) -> None:
        """Log ResetRobot."""
        _log_call("reset")

    def get_pose(self) -> Pose:
        """Return the last pose sent."""
        return self._pose

    def get_robot_mode(self) -> str:
        """Return "enabled" or "disabled"; a dry run never alarms."""
        return "enabled" if self._is_enabled else "disabled"

    def get_error_id(self) -> int:
        """Return 0: no error."""
        return 0

    def enable(self) -> None:
        """Log EnableRobot."""
        _log_call("enable")
        self._is_enabled = True

    def disable(self) -> None:
        """Log DisableRobot."""
        _log_call("disable")
        self._is_enabled = False

    def clear_error(self) -> None:
        """Log ClearError."""
        _log_call("clear_error")

    def set_digital_output(self, index: int, is_on: bool) -> None:
        """Log DO."""
        _log_call("set_digital_output", index, is_on)

    def move_to(self, x_mm: float, y_mm: float, z_mm: float, r_deg: float) -> None:
        """Log MovJ."""
        _log_call("move_to", x_mm, y_mm, z_mm, r_deg)
        self._pose = (x_mm, y_mm, z_mm, r_deg)

    def move_joints(self, j1_deg: float, j2_deg: float, j3_deg: float, j4_deg: float) -> None:
        """Log JointMovJ."""
        _log_call("move_joints", j1_deg, j2_deg, j3_deg, j4_deg)

    def set_speed(self, percent: int) -> None:
        """Log SpeedFactor."""
        _log_call("set_speed", percent)


def _receive_forever(sock: socket.socket, core: TeleopCore, last_sender: list[str]) -> None:
    senders: set[str] = set()
    while True:
        data, address = sock.recvfrom(_DATAGRAM_BYTES)
        if address[0] not in senders:
            senders.add(address[0])
            logger.info("[UDP] receiving from %s", address[0])
        last_sender[:] = [address[0]]  # Robot Status goes to whoever spoke last (ADR-0024)
        core.receive(data)


def _send_status_forever(core: TeleopCore, last_sender: list[str], status_port: int) -> None:
    status_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    while True:
        time.sleep(1.0 / STATUS_HZ)
        if not last_sender:
            continue
        try:
            status_socket.sendto(json.dumps(core.status()).encode("utf-8"), (last_sender[0], status_port))
        except OSError as error:
            logger.warning("[status] could not send to %s: %s", last_sender[0], error)


def main(argv: Sequence[str] | None = None) -> None:
    """Run the Teleop Bridge until Ctrl+C."""
    parser = argparse.ArgumentParser(
        prog="python -m hub.teleop",
        description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument("--ip", default="192.168.1.6", help="MG400 address")
    parser.add_argument("--port", type=int, default=UDP_PORT, help="UDP port to listen on")
    parser.add_argument(
        "--status-port", type=int, default=STATUS_PORT, help="UDP port Robot Status goes to"
    )
    parser.add_argument("--speed", type=int, default=30, help="speed factor, 1-100 %%")
    parser.add_argument("--dry-run", action="store_true", help="no arm: log each command")
    args = parser.parse_args(argv)
    logging.basicConfig(level=logging.INFO, format="%(message)s")

    mg400 = None
    if not args.dry_run:
        mg400 = MG400Arm(TcpDashboard(args.ip, DASHBOARD_PORT), TcpDashboard(args.ip, MOTION_PORT))
        mg400.enable()
        mg400.set_speed(args.speed)
    core = TeleopCore(mg400 or DryRunArm())

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind(("0.0.0.0", args.port))
    logger.info("[UDP] listening on port %d", args.port)
    last_sender: list[str] = []
    threading.Thread(target=_receive_forever, args=(sock, core, last_sender), daemon=True).start()
    threading.Thread(
        target=_send_status_forever, args=(core, last_sender, args.status_port), daemon=True
    ).start()
    try:
        core.run()
    except KeyboardInterrupt:
        logger.info("stopped")
    finally:
        if mg400:
            try:
                mg400.set_digital_output(SUCTION_DO_INDEX, False)
                mg400.disable()
            except OSError as error:
                logger.warning("could not switch suction off and disable on exit: %s", error)
            mg400.close()


if __name__ == "__main__":
    main()
