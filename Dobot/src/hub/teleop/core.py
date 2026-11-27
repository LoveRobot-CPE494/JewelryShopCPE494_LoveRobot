"""Teleop Bridge core: messages and the clock in, `Arm` calls out; no sockets, no threads.

The only safety point for Teleop (ADR-0017): every move is clamped and filtered here.
"""

import json
import logging
import math
import time
from collections import deque
from collections.abc import Callable, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Protocol

logger = logging.getLogger(__name__)

UDP_PORT = 9000
STATUS_PORT = 9001  # Robot Status goes back to the last sender on this port (ADR-0024)
STATUS_HZ = 4
CONTROL_HZ = 10  # ticks per second; a tick sends at most one move, the newest pose wins
DEADBAND_MM = 3.0  # smaller moves are not sent (jitter)
R_DEADBAND_DEG = 2.0
SMOOTHING = 0.5  # 0 = no filter, near 1 = smooth but slow to follow
STALE_S = 0.5  # a pose older than this is not followed
SUCTION_DO_INDEX = 1  # the Suction Cup's digital output (ADR-0002)
JOG_STEPS_MM = (1, 10, 50)
PRESET_MAX_SPEED_PCT = 30  # a Preset never moves faster, whatever the speed setting (ADR-0024)
PRESET_MAX_WAIT_MS = 60_000

# Safe workspace. The one reach limit for Teleop. Reach is the band probe_reach measured on the
# real MG400 on y = 0 (2026-11-27: 215-375 mm at every Z from 0 to 150) less a 10 mm margin.
MIN_REACH_MM = 225.0
MAX_REACH_MM = 365.0
# TODO(#49): calibrate Z_MIN_MM on the real arm so the Suction Cup clears the table.
# Provisional 0 mm until then: a guess on the safe side, not a measured value.
Z_MIN_MM, Z_MAX_MM = 0.0, 150.0
R_MIN_DEG, R_MAX_DEG = -170.0, 170.0
JOINT_LIMITS_DEG = {
    "j1": (-160.0, 160.0),
    "j2": (-25.0, 85.0),
    "j3": (-25.0, 105.0),
    "j4": (-360.0, 360.0),
}

# Controller to arm mapping.
HEADSET_SCALE = 1.0  # 1 cm of Controller movement is 1 cm of arm movement
HEADSET_YAW_DEG = 0.0  # offset when the operator faces away from the arm's front
DEFAULT_ROBOT_POSE = (300.0, 0.0, 50.0, 0.0)  # used when the arm pose cannot be read (dry-run)
_DEFAULT_CONTROLLER_ORIGIN_M = (0.0, 1.0, 0.3)

Pose = tuple[float, float, float, float]


class RobotError(Exception):
    """The arm refused a command or alarmed mid-move; it has been recovered and takes new ones."""


class Arm(Protocol):
    """What the core drives: the MG400 adapter, a dry-run printer, or a test fake."""

    def reset(self) -> None:
        """Stop all motion at once (ResetRobot)."""

    def get_pose(self) -> Sequence[float] | None:
        """Return X, Y, Z (mm), R (deg) and more, or None when unknown."""

    def set_digital_output(self, index: int, is_on: bool) -> None:
        """Switch one digital output."""

    def move_to(self, x_mm: float, y_mm: float, z_mm: float, r_deg: float) -> None:
        """Move to a pose (MovJ) and return when it is done."""

    def move_joints(self, j1_deg: float, j2_deg: float, j3_deg: float, j4_deg: float) -> None:
        """Move to joint angles (JointMovJ) and return when it is done."""

    def set_speed(self, percent: int) -> None:
        """Set the speed factor, 1-100 %."""

    def enable(self) -> None:
        """Clear errors and enable the motors."""

    def disable(self) -> None:
        """Disable the motors."""

    def clear_error(self) -> None:
        """Clear the arm's alarm."""

    def get_mode(self) -> str:
        """Return "enabled", "disabled", "error" or "unknown"."""

    def get_error_id(self) -> int:
        """Return the arm's error id, 0 when none."""


@dataclass(frozen=True)
class Preset:
    """One Preset Action; `reason` is non-empty when the load check rejected it (ADR-0024).

    A step is ("move", x, y, z, r), ("suction", on), ("wait", seconds) or ("home",).
    """

    id: int
    name: str
    steps: tuple[tuple[Any, ...], ...] = ()
    reason: str = ""


@dataclass
class _Running:
    preset: Preset
    index: int = 0  # the step being run
    until: float | None = None  # clock time a wait step ends


class RefusedError(Exception):
    """The core refused a command (Stop latched, motors disabled); nothing was sent."""


def clamp_workspace(x_mm: float, y_mm: float, z_mm: float, r_deg: float) -> Pose:
    """Clamp a target into the safe workspace, so the arm stops at the edge instead."""
    reach_mm = math.hypot(x_mm, y_mm)
    if reach_mm < 1e-6:
        x_mm, reach_mm = MIN_REACH_MM, MIN_REACH_MM
    target_mm = min(max(reach_mm, MIN_REACH_MM), MAX_REACH_MM)
    if target_mm != reach_mm:
        x_mm, y_mm = x_mm * target_mm / reach_mm, y_mm * target_mm / reach_mm
    return (
        x_mm,
        y_mm,
        min(max(z_mm, Z_MIN_MM), Z_MAX_MM),
        min(max(r_deg, R_MIN_DEG), R_MAX_DEG),
    )


def _safe_target(x_mm: float, y_mm: float, z_mm: float, r_deg: float) -> Pose:
    if not all(math.isfinite(v) for v in (x_mm, y_mm, z_mm, r_deg)):
        raise ValueError("non-finite coordinate")  # NaN slips through min/max clamps
    return clamp_workspace(x_mm, y_mm, z_mm, r_deg)


def _number(value: Any) -> float:
    if type(value) not in (int, float) or not math.isfinite(value):
        raise ValueError(f"not a finite number: {value!r}")
    return float(value)


def _parse_step(step: Any) -> tuple[Any, ...]:
    kind = step.get("type") if isinstance(step, dict) else None
    if kind == "move":
        x, y, z, r = (_number(step.get(axis)) for axis in "xyzr")
        if not MIN_REACH_MM <= math.hypot(x, y) <= MAX_REACH_MM:
            raise ValueError(
                f"move x={x:g} y={y:g} is outside reach {MIN_REACH_MM:g}-{MAX_REACH_MM:g} mm"
            )
        if not Z_MIN_MM <= z <= Z_MAX_MM:
            raise ValueError(f"move z={z:g} is outside {Z_MIN_MM:g} to {Z_MAX_MM:g} mm")
        if not R_MIN_DEG <= r <= R_MAX_DEG:
            raise ValueError(f"move r={r:g} is outside {R_MIN_DEG:g} to {R_MAX_DEG:g} deg")
        return ("move", x, y, z, r)
    if kind == "suction":
        if not isinstance(step.get("on"), bool):
            raise ValueError("suction needs on: true or false")
        return ("suction", step["on"])
    if kind == "wait":
        ms = _number(step.get("ms"))
        if not 0 <= ms <= PRESET_MAX_WAIT_MS:
            raise ValueError(f"wait ms must be 0-{PRESET_MAX_WAIT_MS}")
        return ("wait", ms / 1000)
    if kind == "home":
        return ("home",)
    raise ValueError(f"unknown step: {step!r}")


def load_presets(path: Path) -> list[Preset]:
    """Read the Presets file; a bad Preset is kept with a reason, never clamped or run.

    A missing or unreadable file gives an empty list and a logged reason. A repeated id rejects
    that id. An entry whose id is not 1-9 is only logged: it has no button to grey out.
    """
    try:
        entries = json.loads(path.read_text("utf-8"))["presets"]
        if not isinstance(entries, list):
            raise TypeError("presets must be a list")
    except (OSError, ValueError, KeyError, TypeError) as error:
        logger.warning("[presets] none loaded from %s: %r", path, error)
        return []
    presets: dict[int, Preset] = {}
    for entry in entries:
        preset_id = entry.get("id") if isinstance(entry, dict) else None
        if type(preset_id) is not int or not 1 <= preset_id <= 9:
            logger.warning("[presets] skipped entry with bad id (1-9): %r", preset_id)
            continue
        if preset_id in presets:
            # Which of the two was meant is unknown, so neither runs.
            logger.warning("[presets] %d rejected: duplicate id", preset_id)
            presets[preset_id] = Preset(preset_id, presets[preset_id].name, reason="duplicate id")
            continue
        name, steps = entry.get("name"), entry.get("steps")
        try:
            if not isinstance(name, str) or not name:
                raise ValueError("name must be a non-empty string")
            if not isinstance(steps, list) or not steps:
                raise ValueError("steps must be a non-empty list")
            presets[preset_id] = Preset(preset_id, name, tuple(_parse_step(s) for s in steps))
        except ValueError as error:
            logger.warning("[presets] %d rejected: %s", preset_id, error)
            shown = name if isinstance(name, str) else ""
            presets[preset_id] = Preset(preset_id, shown, reason=str(error))
    return sorted(presets.values(), key=lambda p: p.id)


class TeleopCore:
    """Turns Teleop messages into safe arm moves."""

    def __init__(
        self,
        arm: Arm,
        clock: Callable[[], float] = time.monotonic,
        presets: Sequence[Preset] = (),
        speed_percent: int = PRESET_MAX_SPEED_PCT,
    ) -> None:
        """Drive `arm`; `clock` gives the current time in seconds.

        `speed_percent` is the arm's current speed setting, so a Preset can cap it and restore it.
        """
        self._arm = arm
        self._clock = clock
        self._presets = {p.id: p for p in presets}
        self._speed = speed_percent
        self._running: _Running | None = None  # one Preset at a time
        self._is_speed_capped = False
        # receive() runs on the shell's UDP thread, tick() on its control loop: deque appends
        # and pops and one reference assignment are atomic, so no lock is needed.
        self._events: deque[dict[str, Any]] = deque()
        self._latest_pose: tuple[dict[str, Any], float] | None = None
        self._is_engaged = False  # headset poses are followed only after engage
        self._is_stopped = False  # after stop nothing moves until resume or the next engage
        self._is_disabled = False  # after disable nothing moves until enable
        self._last_result = ("", "", "")  # last command, "done" / "refused", reason
        self._last_sent: Pose | None = None
        self._filtered: Pose | None = None
        self._is_suction_on: bool | None = None
        self._controller_origin_m = _DEFAULT_CONTROLLER_ORIGIN_M
        self._robot_origin_mm = DEFAULT_ROBOT_POSE[:3]
        self._origin_r_deg = DEFAULT_ROBOT_POSE[3]

    def receive(self, data: bytes) -> None:
        """Take one UDP datagram: `stop` acts at once, a pose replaces the last, others queue."""
        try:
            message = json.loads(data.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError):
            logger.warning("[UDP] malformed message skipped: %r", data[:60])
            return
        if not isinstance(message, dict):
            logger.warning("[UDP] non-object message skipped: %r", data[:60])
            return
        kind = message.get("type")
        if kind == "stop":
            self._emergency_stop()  # at once: it must cut in while a move is running
        if kind == "pose":
            self._latest_pose = (message, self._clock())
        else:
            if kind in ("release", "stop"):
                self._latest_pose = None
            # stop is queued too, so an engage queued before it cannot undo it.
            self._events.append(message)

    def tick(self) -> None:
        """Run the queued events, then follow the newest pose if it is fresh."""
        while self._events:
            event = self._events.popleft()
            reason = self._safe(self._handle_event, event)
            self._last_result = (
                str(event.get("type", "")),
                "refused" if reason is not None else "done",
                reason or "",
            )
        pose = self._latest_pose
        if pose and self._clock() - pose[1] < STALE_S:
            self._safe(self._follow, pose[0])
        reason = self._safe(self._step_preset, {"type": "preset"})
        if reason is not None:
            self._running = None
            self._last_result = ("preset", "refused", reason)
        if self._is_speed_capped and self._running is None:  # a stop cancels from the UDP thread
            self._is_speed_capped = False
            self._safe(lambda _: self._arm.set_speed(self._speed), {"type": "speed"})

    def status(self) -> dict[str, Any]:
        """Return Robot Status as a JSON-ready dict; never raises when the arm is unreachable."""
        try:
            mode = self._arm.get_mode()
            error_id = self._arm.get_error_id() if mode == "error" else 0
        except (OSError, RobotError):
            mode, error_id = "unknown", 0
        x, y, z, r = self._current_robot_pose()
        command, result, reason = self._last_result
        running = self._running
        return {
            "type": "status",
            "mode": mode,
            "error_id": int(error_id),
            "x": float(x),
            "y": float(y),
            "z": float(z),
            "r": float(r),
            "suction": bool(self._is_suction_on),
            "stop_latched": self._is_stopped,
            "last_command": command,
            "result": result,
            "reason": reason,
            "presets": [
                {"id": p.id, "name": p.name, "valid": not p.reason, "reason": p.reason}
                for _, p in sorted(self._presets.items())
            ],
            "running_preset": running
            and {
                "id": running.preset.id,
                "step": running.index + 1,
                "of": len(running.preset.steps),
            },
        }

    def run(self, sleep: Callable[[float], None] = time.sleep) -> None:
        """Tick CONTROL_HZ times a second, forever; this is the move-rate limit."""
        period_s = 1.0 / CONTROL_HZ
        while True:
            start = self._clock()
            self.tick()
            sleep(max(0.0, period_s - (self._clock() - start)))

    def _emergency_stop(self) -> None:
        self._halt()
        logger.warning("[STOP] emergency stop; resume or press Grip again to drive")
        try:
            self._arm.reset()
        except OSError as error:
            logger.error("[STOP] stop command failed: %s; use the real E-stop!", error)

    def _halt(self) -> None:
        self._is_stopped = True
        self._is_engaged = False
        self._running = None  # a Preset stops at once; tick() restores the speed

    def _current_robot_pose(self) -> Pose:
        try:
            pose = self._arm.get_pose()
        except (OSError, RobotError):
            pose = None
        if pose and len(pose) >= 4:
            return (pose[0], pose[1], pose[2], pose[3])
        return self._last_sent or DEFAULT_ROBOT_POSE

    def _set_suction(self, value: Any) -> None:
        is_on = int(value) != 0
        if is_on != self._is_suction_on:
            self._arm.set_digital_output(SUCTION_DO_INDEX, is_on)
            self._is_suction_on = is_on

    def _restart_motion(self) -> None:
        self._last_sent = self._filtered = None

    def _check_can_move(self) -> None:
        if self._is_stopped:
            raise RefusedError("stop latched")
        if self._is_disabled:
            raise RefusedError("disabled")
        if self._running:
            raise RefusedError("preset running")

    def _start_preset(self, preset_id: Any) -> None:
        preset = self._presets.get(preset_id) if type(preset_id) is int else None
        if preset is None:
            raise RefusedError(f"unknown preset {preset_id!r}")
        if preset.reason:
            raise RefusedError(f"preset rejected: {preset.reason}")
        if self._running:
            raise RefusedError("preset running")
        if self._is_stopped:
            raise RefusedError("stop latched")
        if self._is_engaged:
            raise RefusedError("teleop engaged")
        if self._is_disabled:
            raise RefusedError("disabled")
        mode = self._arm.get_mode()
        if mode != "enabled":
            raise RefusedError(f"arm not enabled ({mode})")
        self._restart_motion()
        self._is_speed_capped = True  # set first: tick() restores the setting if the cap fails
        self._arm.set_speed(min(self._speed, PRESET_MAX_SPEED_PCT))  # a failure means no Preset
        self._running = _Running(preset)
        logger.info("[preset] %d %s started", preset.id, preset.name)

    def _step_preset(self, _: dict[str, Any]) -> None:
        # One step per tick; a wait ends on the clock, never a sleep.
        run = self._running
        if run is None:
            return
        if run.until is not None:
            if self._clock() < run.until:
                return
            run.until, run.index = None, run.index + 1
        if run.index >= len(run.preset.steps):
            self._running = None
            return
        step = run.preset.steps[run.index]
        if self._is_stopped:  # a stop may land from the UDP thread between ticks
            return
        if step[0] == "move":
            self._arm.move_to(*_safe_target(*step[1:]))
        elif step[0] == "suction":
            self._set_suction(step[1])
        elif step[0] == "wait":
            run.until = self._clock() + step[1]
            return
        else:
            self._arm.move_joints(0, 0, 0, 0)
        run.index += 1
        if run.index >= len(run.preset.steps):
            self._running = None

    def _move_once(self, x_mm: float, y_mm: float, z_mm: float, r_deg: float) -> None:
        # A Robot Panel move (jog, move_to): ends engage, then one clamped MovJ.
        self._check_can_move()
        target = _safe_target(x_mm, y_mm, z_mm, r_deg)
        self._is_engaged = False
        self._filtered = None
        self._last_sent = target  # set first: the next jog builds on it when the pose is unknown
        self._arm.move_to(*target)

    def _start_following(
        self, event: dict[str, Any], robot_origin_mm: tuple[float, float, float]
    ) -> None:
        # The Controller position right now is the arm position right now (ADR-0017).
        self._controller_origin_m = (float(event["x"]), float(event["y"]), float(event["z"]))
        self._robot_origin_mm = robot_origin_mm
        self._is_engaged, self._is_stopped = True, False
        self._restart_motion()

    def _handle_event(self, event: dict[str, Any]) -> None:
        kind = event.get("type")
        if kind == "engage":
            self._running = None  # engage cancels a Preset
            if self._is_disabled:
                raise RefusedError("disabled")  # engage releases Stop, never Disable
            pose = self._current_robot_pose()
            self._start_following(event, pose[:3])
            self._origin_r_deg = pose[3]
            logger.info("[engage] following the Controller from %s", self._robot_origin_mm)
        elif kind == "release":
            if self._is_engaged:
                logger.info("[release] stopped following")
            self._is_engaged = False
        elif kind == "calibrate":
            self._check_can_move()
            self._start_following(event, DEFAULT_ROBOT_POSE[:3])
            logger.info("[calibrate] Controller origin = %s", self._controller_origin_m)
        elif kind == "stop":
            self._halt()  # the arm was reset in receive()
        elif kind == "grip":
            self._set_suction(event["value"])
        elif kind == "home":
            self._check_can_move()
            self._is_engaged = False
            self._arm.move_joints(0, 0, 0, 0)
            self._restart_motion()
        elif kind == "joint":
            self._check_can_move()
            self._is_engaged = False
            self._arm.move_joints(
                float(event["j1"]),
                float(event["j2"]),
                float(event["j3"]),
                float(event.get("j4", 0)),
            )
            self._restart_motion()
        elif kind == "speed":
            self._speed = int(event["value"])
            self._arm.set_speed(
                min(self._speed, PRESET_MAX_SPEED_PCT) if self._running else self._speed
            )
        elif kind == "resume":
            self._is_stopped = False
            logger.info("[resume] stop released")
        elif kind == "enable":
            self._arm.enable()
            self._is_disabled = False
        elif kind == "disable":
            self._is_disabled, self._is_engaged = True, False
            self._running = None
            self._arm.disable()
        elif kind == "clear_error":
            self._arm.clear_error()
        elif kind == "jog":
            self._jog(event["axis"], event["step"])
        elif kind == "preset":
            self._start_preset(event.get("id"))
        elif kind == "move_to":
            self._move_once(
                float(event["x"]), float(event["y"]), float(event["z"]), float(event["r"])
            )
        else:
            raise ValueError(f"unknown type: {kind}")

    def _jog(self, axis: Any, step_mm: Any) -> None:
        if axis not in ("x", "y", "z"):
            raise ValueError(f"jog axis must be x, y or z, got {axis!r}")
        if type(step_mm) not in (int, float) or abs(step_mm) not in JOG_STEPS_MM:
            raise ValueError(f"jog step must be +/- {JOG_STEPS_MM} mm, got {step_mm!r}")
        x_mm, y_mm, z_mm, r_deg = self._current_robot_pose()
        self._move_once(
            x_mm + (step_mm if axis == "x" else 0),
            y_mm + (step_mm if axis == "y" else 0),
            z_mm + (step_mm if axis == "z" else 0),
            r_deg,
        )

    def _map_controller_to_robot(
        self, x_m: float, y_m: float, z_m: float
    ) -> tuple[float, float, float]:
        # Unity: X right, Y up, Z forward (m) -> MG400: X forward, Y left, Z up (mm), relative
        # to where engage happened.
        origin_m = self._controller_origin_m
        right_mm = (x_m - origin_m[0]) * 1000 * HEADSET_SCALE
        up_mm = (y_m - origin_m[1]) * 1000 * HEADSET_SCALE
        forward_mm = (z_m - origin_m[2]) * 1000 * HEADSET_SCALE
        left_mm = -right_mm
        if HEADSET_YAW_DEG:
            yaw_rad = math.radians(HEADSET_YAW_DEG)
            forward_mm, left_mm = (
                forward_mm * math.cos(yaw_rad) - left_mm * math.sin(yaw_rad),
                forward_mm * math.sin(yaw_rad) + left_mm * math.cos(yaw_rad),
            )
        x0_mm, y0_mm, z0_mm = self._robot_origin_mm
        return x0_mm + forward_mm, y0_mm + left_mm, z0_mm + up_mm

    def _follow(self, message: dict[str, Any]) -> None:
        if self._is_stopped or self._is_disabled or self._running:
            return
        if message.get("frame", "robot") == "headset":
            if not self._is_engaged:
                return  # Grip not held: the Deadman
            x, y, z = self._map_controller_to_robot(
                float(message["x"]), float(message["y"]), float(message["z"])
            )
            r = float(message.get("r", self._origin_r_deg))  # R stays where it was
        else:
            x, y, z = float(message["x"]), float(message["y"]), float(message["z"])
            r = float(message.get("r", 0))
        target = _safe_target(x, y, z, r)

        if self._filtered is None:
            self._filtered = target
        else:
            previous = self._filtered
            self._filtered = (
                SMOOTHING * previous[0] + (1 - SMOOTHING) * target[0],
                SMOOTHING * previous[1] + (1 - SMOOTHING) * target[1],
                SMOOTHING * previous[2] + (1 - SMOOTHING) * target[2],
                SMOOTHING * previous[3] + (1 - SMOOTHING) * target[3],
            )

        last = self._last_sent
        if (
            last is None
            or math.dist(self._filtered[:3], last[:3]) > DEADBAND_MM
            or abs(self._filtered[3] - last[3]) > R_DEADBAND_DEG
        ):
            self._last_sent = self._filtered  # set first: a target that errors is not resent
            self._arm.move_to(*self._filtered)

        if "grip" in message:
            self._set_suction(message["grip"])

    def _safe(
        self, handler: Callable[[dict[str, Any]], None], message: dict[str, Any]
    ) -> str | None:
        # Returns None when done, else why it was refused.
        try:
            handler(message)
        except RefusedError as error:
            logger.warning("[refused] %s: %s", message.get("type"), error)
            return str(error)
        except (KeyError, ValueError, TypeError) as error:
            logger.warning("[!] bad message skipped: %s (%r)", message, error)
            return f"bad message: {error}"
        except RobotError as error:
            logger.warning("[arm error] %s; skipped, waiting for the next value", error)
            self._filtered = None
            return str(error)
        except OSError as error:
            logger.warning("[link] %s; retrying next tick", error)
            return f"link: {error}"
        return None
