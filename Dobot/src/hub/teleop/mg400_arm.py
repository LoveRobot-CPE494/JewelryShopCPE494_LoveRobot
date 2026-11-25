"""MG400 Arm adapter: the core's `Arm` over the dashboard (29999) and motion (30003) ports.

Ported from the teammate's `teleop/mg400_control.py` with its error handling unchanged.
"""

import logging
import re
import threading
import time
from collections.abc import Callable

from hub.dashboard import Dashboard
from hub.teleop.core import JOINT_LIMITS_DEG, RobotError

logger = logging.getLogger(__name__)

MODE_NAMES = {
    1: "INIT",
    2: "BRAKE_OPEN",
    4: "DISABLED",
    5: "ENABLED(idle)",
    6: "BACKDRIVE",
    7: "RUNNING",
    8: "SINGLE_MOVE",
    9: "ERROR",
    10: "PAUSE",
    11: "JOG",
}
# Robot Status modes (ADR-0024); PAUSE counts as error because it needs a clear to move again.
# Any other mode (INIT, BRAKE_OPEN, BACKDRIVE, unreadable) is reported as "unknown".
_STATUS_MODES = {
    4: "disabled",
    5: "enabled",
    7: "enabled",
    8: "enabled",
    11: "enabled",
    9: "error",
    10: "error",
}
_MODE_DISABLED, _MODE_IDLE, _MODE_RUNNING, _MODE_ERROR, _MODE_PAUSE = 4, 5, 7, 9, 10
_START_GRACE_S = 0.3  # a move that never shows RUNNING counts as done after this
_RECOVER_SETTLE_S = 0.3
_ENABLE_SETTLE_S = 1.0


def _is_ok(reply: str) -> bool:
    return reply.split(",", 1)[0].strip() == "0"


def _parse_values(reply: str) -> list[float] | None:
    match = re.search(r"\{([^}]*)\}", reply)
    if not match:
        return None
    try:
        return [float(v) for v in match.group(1).split(",") if v.strip()]
    except ValueError:
        return None


def _check_joints(j1_deg: float, j2_deg: float, j3_deg: float, j4_deg: float) -> None:
    for name, value in zip(("j1", "j2", "j3", "j4"), (j1_deg, j2_deg, j3_deg, j4_deg), strict=True):
        low, high = JOINT_LIMITS_DEG[name]
        if not low <= value <= high:
            raise ValueError(f"{name} = {value:.1f} deg is outside {low}..{high} deg")


class MG400Arm:
    """Drives the MG400; moves wait by polling RobotMode() and recover from alarms."""

    def __init__(
        self,
        dashboard: Dashboard,
        motion: Dashboard,
        *,
        move_timeout_s: float = 30.0,
        poll_s: float = 0.05,
        clock: Callable[[], float] = time.monotonic,
        sleep: Callable[[float], None] = time.sleep,
    ) -> None:
        """Connect both ports; `clock` and `sleep` are injectable so tests never wait."""
        self._dashboard = dashboard
        self._motion = motion
        self._move_timeout_s = move_timeout_s
        self._poll_s = poll_s
        self._clock = clock
        self._sleep = sleep
        # The shell's UDP thread sends stop (ResetRobot) while the control loop waits on a
        # move, so each port is used by one thread at a time.
        self._dashboard_lock = threading.RLock()
        self._motion_lock = threading.RLock()
        self._is_inverse_solution_check_on = True  # switches itself off when the firmware lacks it
        dashboard.connect()
        motion.connect()
        logger.info("[OK] connected to the MG400")

    def reset(self) -> None:
        """Stop all motion at once (ResetRobot)."""
        self._send_dashboard("ResetRobot()")

    def get_pose(self) -> list[float] | None:
        """Return X, Y, Z, R and the rest of GetPose(); empty or None when the reply has none."""
        # Quiet: the Robot Status thread asks a few times a second.
        return _parse_values(self._send_dashboard("GetPose()", is_verbose=False))

    def get_robot_mode(self) -> str:
        """Return "enabled", "disabled", "error" or "unknown" from RobotMode()."""
        return _STATUS_MODES.get(self._robot_mode(), "unknown")

    def get_error_id(self) -> int | None:
        """Return the first active id from GetErrorID(), 0 when none, None when unreadable."""
        reply = self._send_dashboard("GetErrorID()", is_verbose=False)
        match = re.search(r"\{(.*)\}", reply)
        if not _is_ok(reply) or not match:
            return None
        ids = re.findall(r"-?\d+", match.group(1))
        return int(ids[0]) if ids else 0

    def clear_error(self) -> None:
        """Clear the arm's alarm (ClearError)."""
        self._send_dashboard("ClearError()")

    def set_digital_output(self, index: int, is_on: bool) -> None:
        """Switch one digital output."""
        self._send_dashboard(f"DO({index},{1 if is_on else 0})")

    def move_to(
        self, x_mm: float, y_mm: float, z_mm: float, r_deg: float, *, is_blocking: bool = True
    ) -> None:
        """Check the joints with InverseSolution(), MovJ, and by default wait until done."""
        self._check_inverse_solution(x_mm, y_mm, z_mm, r_deg)
        self._run_move(f"MovJ({x_mm:.2f},{y_mm:.2f},{z_mm:.2f},{r_deg:.2f})", is_blocking)

    def move_joints(
        self, j1_deg: float, j2_deg: float, j3_deg: float, j4_deg: float, *, is_blocking: bool = True
    ) -> None:
        """Check the joint limits, JointMovJ, and by default wait until done."""
        _check_joints(j1_deg, j2_deg, j3_deg, j4_deg)
        self._run_move(f"JointMovJ({j1_deg:.2f},{j2_deg:.2f},{j3_deg:.2f},{j4_deg:.2f})", is_blocking)

    def set_speed(self, percent: int) -> None:
        """Set the speed factor, clamped to 1-100 %."""
        self._send_dashboard(f"SpeedFactor({int(max(1, min(100, percent)))})")

    def enable(self) -> None:
        """Clear errors and enable the motors."""
        self._send_dashboard("ClearError()")
        self._send_dashboard("EnableRobot()")
        self._sleep(_ENABLE_SETTLE_S)

    def disable(self) -> None:
        """Disable the motors."""
        self._send_dashboard("DisableRobot()")

    def close(self) -> None:
        """Close both ports."""
        self._dashboard.close()
        self._motion.close()
        logger.info("[OK] connection closed")

    def _send(
        self, port: Dashboard, lock: threading.RLock, command: str, *, is_verbose: bool
    ) -> str:
        with lock:
            try:
                return self._exchange(port, command, is_verbose)
            except OSError as error:  # timeouts and dropped links included
                logger.warning("  [!] %s: %s; reconnecting and retrying once", command, error)
                port.connect()
                return self._exchange(port, command, is_verbose)

    @staticmethod
    def _exchange(port: Dashboard, command: str, is_verbose: bool) -> str:
        reply = port.send(command)
        if not reply:
            raise ConnectionError("the arm closed the connection")
        logger.log(logging.INFO if is_verbose else logging.DEBUG, "  >> %-45s << %s", command, reply)
        return reply

    def _send_dashboard(self, command: str, *, is_verbose: bool = True) -> str:
        return self._send(self._dashboard, self._dashboard_lock, command, is_verbose=is_verbose)

    def _robot_mode(self) -> int | None:
        values = _parse_values(self._send_dashboard("RobotMode()", is_verbose=False))
        return int(values[0]) if values else None

    def _recover(self) -> None:
        logger.warning("  [recover] error: %s", self._send_dashboard("GetErrorID()"))
        self._send_dashboard("ClearError()")
        self._send_dashboard("Continue()")  # after a collision; older firmware may lack it
        self._sleep(_RECOVER_SETTLE_S)
        if self._robot_mode() == _MODE_DISABLED:
            self._send_dashboard("EnableRobot()")
            self._sleep(_ENABLE_SETTLE_S)
        with self._motion_lock:
            self._motion.connect()  # clears the motion port's queue and stuck state
        logger.warning("  [recover] done, RobotMode = %s", MODE_NAMES.get(self._robot_mode(), "?"))

    def _wait_done(self) -> None:
        # Polls RobotMode() instead of Sync(): Sync() never answers once the arm alarms.
        start = self._clock()
        has_run = False
        while True:
            mode = self._robot_mode()
            if mode in (_MODE_ERROR, _MODE_PAUSE):
                self._recover()
                raise RobotError(f"arm went {MODE_NAMES[mode]} during the move (hit or limit)")
            if mode == _MODE_RUNNING:
                has_run = True
            elif mode == _MODE_IDLE and (has_run or self._clock() - start > _START_GRACE_S):
                return
            elif mode == _MODE_DISABLED:
                self._recover()
                raise RobotError("motors were disabled during the move")
            if self._clock() - start > self._move_timeout_s:
                self._send_dashboard("ResetRobot()")
                raise RobotError(f"move took longer than {self._move_timeout_s} s")
            self._sleep(self._poll_s)

    def _run_move(self, command: str, is_blocking: bool) -> None:
        # An alarm left from the last move would make this one do nothing.
        if self._robot_mode() in (_MODE_ERROR, _MODE_PAUSE, _MODE_DISABLED):
            self._recover()
        reply = self._send(self._motion, self._motion_lock, command, is_verbose=True)
        if not _is_ok(reply):
            self._recover()
            raise RobotError(f"arm refused {command}: {reply}")
        if is_blocking:
            self._wait_done()

    def _check_inverse_solution(self, x_mm: float, y_mm: float, z_mm: float, r_deg: float) -> None:
        # Ask the arm for the joint angles first, so a joint-limit alarm never happens.
        if not self._is_inverse_solution_check_on:
            return
        reply = self._send_dashboard(
            f"InverseSolution({x_mm},{y_mm},{z_mm},{r_deg},0,0)", is_verbose=False
        )
        joints = _parse_values(reply)
        if _is_ok(reply) and joints and len(joints) >= 4:
            _check_joints(*joints[:4])
        elif "not" in reply.lower() or "unknown" in reply.lower() or not reply:
            logger.info("  [i] this firmware has no InverseSolution(); InverseSolution check off")
            self._is_inverse_solution_check_on = False
        else:
            raise ValueError(f"({x_mm}, {y_mm}, {z_mm}, {r_deg}) is unreachable: {reply}")
