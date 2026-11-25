"""Teleop Bridge shell: UDP on 9000 in, MG400 out (ADR-0017).

    python -m hub.teleop --dry-run          # no arm: log each command instead
    python -m hub.teleop --ip 192.168.1.6   # the real arm (Robot pen)
"""

import argparse
import logging
import socket
import threading
from collections.abc import Sequence

from hub.dashboard import TcpDashboard
from hub.teleop.core import SUCTION_DO_INDEX, UDP_PORT, TeleopCore
from hub.teleop.mg400_arm import MG400Arm

logger = logging.getLogger("hub.teleop")

DASHBOARD_PORT = 29999
MOTION_PORT = 30003
_DATAGRAM_BYTES = 4096


def _log_call(name: str, *args: object) -> None:
    shown = tuple(round(a, 1) if isinstance(a, float) else a for a in args)
    logger.info("  [dry-run] %s%s", name, shown)


class DryRunArm:
    """Logs each Arm call instead of moving; it has no pose, so engage uses the default."""

    def reset(self) -> None:
        """Log ResetRobot."""
        _log_call("reset")

    def get_pose(self) -> None:
        """Return None: the pose is unknown."""
        return None

    def set_digital_output(self, index: int, is_on: bool) -> None:
        """Log DO."""
        _log_call("set_digital_output", index, is_on)

    def move_to(self, x_mm: float, y_mm: float, z_mm: float, r_deg: float) -> None:
        """Log MovJ."""
        _log_call("move_to", x_mm, y_mm, z_mm, r_deg)

    def move_joints(self, j1_deg: float, j2_deg: float, j3_deg: float, j4_deg: float) -> None:
        """Log JointMovJ."""
        _log_call("move_joints", j1_deg, j2_deg, j3_deg, j4_deg)

    def set_speed(self, percent: int) -> None:
        """Log SpeedFactor."""
        _log_call("set_speed", percent)


def _receive_forever(sock: socket.socket, core: TeleopCore) -> None:
    senders: set[str] = set()
    while True:
        data, address = sock.recvfrom(_DATAGRAM_BYTES)
        if address[0] not in senders:
            senders.add(address[0])
            logger.info("[UDP] receiving from %s", address[0])
        core.receive(data)


def main(argv: Sequence[str] | None = None) -> None:
    """Run the Teleop Bridge until Ctrl+C."""
    parser = argparse.ArgumentParser(
        prog="python -m hub.teleop",
        description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument("--ip", default="192.168.1.6", help="MG400 address")
    parser.add_argument("--port", type=int, default=UDP_PORT, help="UDP port to listen on")
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
    threading.Thread(target=_receive_forever, args=(sock, core), daemon=True).start()
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
