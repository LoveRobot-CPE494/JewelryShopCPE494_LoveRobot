import json
import logging
import math
from collections.abc import Sequence
from pathlib import Path

import pytest

from hub.teleop.__main__ import DryRunArm
from hub.teleop.core import (
    DEFAULT_ROBOT_POSE,
    MAX_REACH_MM,
    MIN_REACH_MM,
    R_MAX_DEG,
    R_MIN_DEG,
    SUCTION_DO_INDEX,
    Z_MAX_MM,
    Z_MIN_MM,
    RobotError,
    TeleopCore,
)

EXAMPLES = json.loads(
    (Path(__file__).parents[1] / "contract" / "teleop_examples.json").read_text("utf-8")
)


class Clock:
    def __init__(self) -> None:
        self.now = 0.0

    def __call__(self) -> float:
        return self.now


class FakeArm:
    def __init__(self, pose: Sequence[float] | Exception | None = None) -> None:
        self.calls: list[tuple[object, ...]] = []
        self.pose = pose
        self.move_error: Exception | None = None
        self.reset_error: Exception | None = None
        self.mode_error: Exception | None = None
        self.mode = "enabled"
        self.error_id = 0

    def reset(self) -> None:
        self.calls.append(("reset",))
        if self.reset_error:
            raise self.reset_error

    def get_pose(self) -> Sequence[float] | None:
        if isinstance(self.pose, Exception):
            raise self.pose
        return self.pose

    def set_digital_output(self, index: int, is_on: bool) -> None:
        self.calls.append(("set_digital_output", index, is_on))

    def move_to(self, x_mm: float, y_mm: float, z_mm: float, r_deg: float) -> None:
        self.calls.append(("move_to", x_mm, y_mm, z_mm, r_deg))
        if self.move_error:
            raise self.move_error

    def move_joints(self, j1_deg: float, j2_deg: float, j3_deg: float, j4_deg: float) -> None:
        self.calls.append(("move_joints", j1_deg, j2_deg, j3_deg, j4_deg))

    def set_speed(self, percent: int) -> None:
        self.calls.append(("set_speed", percent))

    def enable(self) -> None:
        self.calls.append(("enable",))

    def disable(self) -> None:
        self.calls.append(("disable",))

    def clear_error(self) -> None:
        self.calls.append(("clear_error",))

    def get_mode(self) -> str:
        if self.mode_error:
            raise self.mode_error
        return self.mode

    def get_error_id(self) -> int:
        return self.error_id

    @property
    def moves(self) -> list[tuple[object, ...]]:
        return [call[1:] for call in self.calls if call[0] == "move_to"]


@pytest.fixture
def clock() -> Clock:
    return Clock()


@pytest.fixture
def arm() -> FakeArm:
    return FakeArm()


@pytest.fixture
def core(arm: FakeArm, clock: Clock) -> TeleopCore:
    return TeleopCore(arm, clock)


def send(core: TeleopCore, **message: object) -> None:
    core.receive(json.dumps(message).encode("utf-8"))


def robot_pose(core: TeleopCore, x: float, y: float, z: float, r: float = 0.0) -> None:
    send(core, type="pose", frame="robot", x=x, y=y, z=z, r=r)
    core.tick()


def approx(values: Sequence[float]) -> object:
    return pytest.approx(tuple(values))


@pytest.mark.parametrize(
    ("x", "y", "expected_x", "expected_y"),
    [
        (500.0, 0.0, MAX_REACH_MM, 0.0),
        (100.0, 0.0, MIN_REACH_MM, 0.0),
        (0.0, 0.0, MIN_REACH_MM, 0.0),
        (300.0, 400.0, MAX_REACH_MM * 0.6, MAX_REACH_MM * 0.8),
    ],
)
def test_reach_is_clamped_to_170_to_420_mm(
    core: TeleopCore, arm: FakeArm, x: float, y: float, expected_x: float, expected_y: float
) -> None:
    robot_pose(core, x, y, 50.0)

    assert arm.moves == [approx((expected_x, expected_y, 50.0, 0.0))]


@pytest.mark.parametrize(("z", "expected_z"), [(-500.0, Z_MIN_MM), (400.0, Z_MAX_MM)])
def test_z_is_clamped_to_z_min_to_150_mm(
    core: TeleopCore, arm: FakeArm, z: float, expected_z: float
) -> None:
    robot_pose(core, 300.0, 0.0, z)

    assert arm.moves[0][2] == expected_z


@pytest.mark.parametrize(("r", "expected_r"), [(200.0, R_MAX_DEG), (-200.0, R_MIN_DEG)])
def test_r_is_clamped(core: TeleopCore, arm: FakeArm, r: float, expected_r: float) -> None:
    robot_pose(core, 300.0, 0.0, 50.0, r)

    assert arm.moves[0][3] == expected_r


def test_move_inside_the_3_mm_deadband_is_not_sent_and_poses_are_smoothed(
    core: TeleopCore, arm: FakeArm
) -> None:
    robot_pose(core, 300.0, 0.0, 50.0)
    robot_pose(core, 305.0, 0.0, 50.0)  # smoothed to 302.5: inside the deadband
    robot_pose(core, 310.0, 0.0, 50.0)  # smoothed to 306.25: 6.25 mm from the last move

    assert arm.moves == [approx((300.0, 0.0, 50.0, 0.0)), approx((306.25, 0.0, 50.0, 0.0))]


def test_r_change_over_2_degrees_is_sent_even_inside_the_xyz_deadband(
    core: TeleopCore, arm: FakeArm
) -> None:
    robot_pose(core, 300.0, 0.0, 50.0, 0.0)
    robot_pose(core, 300.0, 0.0, 50.0, 3.0)  # smoothed to 1.5: inside
    robot_pose(core, 300.0, 0.0, 50.0, 6.0)  # smoothed to 3.75: outside

    assert [move[3] for move in arm.moves] == [0.0, 3.75]


def test_control_loop_sends_at_most_10_moves_per_second(
    core: TeleopCore, arm: FakeArm, clock: Clock
) -> None:
    class OneSecondError(Exception):
        pass

    x = [200.0]

    def sleep(seconds: float) -> None:
        clock.now += seconds
        if clock.now > 0.95:  # ticks at 0.0 .. 0.9 s: one second
            raise OneSecondError
        for _ in range(5):  # a flood of poses between ticks, each 2 mm further on
            x[0] += 2.0
            send(core, type="pose", frame="robot", x=x[0], y=0.0, z=50.0)

    send(core, type="pose", frame="robot", x=x[0], y=0.0, z=50.0)
    with pytest.raises(OneSecondError):
        core.run(sleep)

    assert len(arm.moves) == 10


def test_pose_older_than_half_a_second_is_ignored(
    core: TeleopCore, arm: FakeArm, clock: Clock
) -> None:
    send(core, type="pose", frame="robot", x=300.0, y=0.0, z=50.0)
    clock.now = 0.5
    core.tick()

    assert arm.moves == []

    send(core, type="pose", frame="robot", x=300.0, y=0.0, z=50.0)
    clock.now = 0.9
    core.tick()

    assert arm.moves == [approx((300.0, 0.0, 50.0, 0.0))]


def test_engage_takes_the_controller_position_as_the_arm_position(clock: Clock) -> None:
    arm = FakeArm(pose=(250.0, 10.0, 20.0, 5.0, 0.0, 0.0))
    core = TeleopCore(arm, clock)
    send(core, type="engage", x=0.1, y=1.2, z=0.4)
    core.tick()

    # Unity forward +5 cm, right +1 cm, up +2 cm -> MG400 X +50, Y -10, Z +20; R kept.
    send(core, type="pose", frame="headset", x=0.11, y=1.22, z=0.45)
    core.tick()

    assert arm.moves == [approx((300.0, 0.0, 40.0, 5.0))]


@pytest.mark.parametrize("pose", [None, OSError("down"), RobotError("alarm")])
def test_engage_falls_back_to_the_default_pose_when_the_arm_pose_is_unknown(
    clock: Clock, pose: Exception | None
) -> None:
    arm = FakeArm(pose=pose)
    core = TeleopCore(arm, clock)
    send(core, type="engage", x=0.0, y=1.0, z=0.3)
    send(core, type="pose", frame="headset", x=0.0, y=1.0, z=0.3)
    core.tick()

    assert arm.moves == [approx(DEFAULT_ROBOT_POSE)]


def test_headset_pose_is_ignored_before_engage(core: TeleopCore, arm: FakeArm) -> None:
    send(core, type="pose", frame="headset", x=0.0, y=1.0, z=0.3)
    core.tick()

    assert arm.moves == []


def test_release_stops_following_and_drops_the_pending_pose(core: TeleopCore, arm: FakeArm) -> None:
    send(core, type="engage", x=0.0, y=1.0, z=0.3)
    send(core, type="pose", frame="headset", x=0.0, y=1.0, z=0.3)
    send(core, type="release")
    core.tick()
    send(core, type="pose", frame="headset", x=0.0, y=1.0, z=0.4)
    core.tick()

    assert arm.moves == []


def test_stop_resets_the_arm_at_once_and_blocks_poses_and_home_until_engage(
    core: TeleopCore, arm: FakeArm
) -> None:
    send(core, type="engage", x=0.0, y=1.0, z=0.3)
    send(core, type="pose", frame="headset", x=0.0, y=1.0, z=0.3)
    send(core, type="stop")

    assert arm.calls == [("reset",)]  # before any tick

    core.tick()
    robot_pose(core, 300.0, 0.0, 50.0)
    send(core, type="home")
    core.tick()

    assert arm.calls == [("reset",)]

    send(core, type="engage", x=0.0, y=1.0, z=0.3)
    send(core, type="pose", frame="headset", x=0.0, y=1.0, z=0.3)
    core.tick()

    assert arm.moves == [approx(DEFAULT_ROBOT_POSE)]


def test_engage_queued_before_stop_does_not_undo_it(core: TeleopCore, arm: FakeArm) -> None:
    send(core, type="engage", x=0.0, y=1.0, z=0.3)
    send(core, type="stop")
    send(core, type="pose", frame="headset", x=0.0, y=1.0, z=0.3)
    core.tick()

    assert arm.moves == []


def test_stop_still_blocks_motion_when_the_reset_command_fails(
    core: TeleopCore, arm: FakeArm, caplog: pytest.LogCaptureFixture
) -> None:
    arm.reset_error = OSError("link down")
    send(core, type="stop")
    robot_pose(core, 300.0, 0.0, 50.0)

    assert arm.moves == []
    assert "E-stop" in caplog.text


def test_grip_switches_suction_only_on_change(core: TeleopCore, arm: FakeArm) -> None:
    send(core, type="grip", value=1)
    send(core, type="grip", value=1)
    send(core, type="grip", value=0)
    core.tick()
    send(core, type="pose", frame="robot", x=300.0, y=0.0, z=50.0, grip=1)
    core.tick()

    assert [c for c in arm.calls if c[0] == "set_digital_output"] == [
        ("set_digital_output", SUCTION_DO_INDEX, True),
        ("set_digital_output", SUCTION_DO_INDEX, False),
        ("set_digital_output", SUCTION_DO_INDEX, True),
    ]


def test_home_joint_and_speed_commands(core: TeleopCore, arm: FakeArm) -> None:
    send(core, type="home")
    send(core, type="joint", j1=10, j2=20, j3=30)
    send(core, type="speed", value=40)
    core.tick()

    assert arm.calls == [
        ("move_joints", 0, 0, 0, 0),
        ("move_joints", 10.0, 20.0, 30.0, 0.0),
        ("set_speed", 40),
    ]


def test_home_ends_engage(core: TeleopCore, arm: FakeArm) -> None:
    send(core, type="engage", x=0.0, y=1.0, z=0.3)
    send(core, type="home")
    core.tick()
    send(core, type="pose", frame="headset", x=0.0, y=1.0, z=0.3)
    core.tick()

    assert arm.moves == []


def test_calibrate_maps_the_controller_position_to_the_default_pose(
    core: TeleopCore, arm: FakeArm
) -> None:
    send(core, type="calibrate", x=0.5, y=1.5, z=0.5)
    send(core, type="pose", frame="headset", x=0.5, y=1.5, z=0.5, r=12)
    core.tick()

    assert arm.moves == [approx((*DEFAULT_ROBOT_POSE[:3], 12.0))]


def test_unknown_type_is_logged(core: TeleopCore, caplog: pytest.LogCaptureFixture) -> None:
    with caplog.at_level(logging.WARNING):
        send(core, type="dance")
        core.tick()

    assert "dance" in caplog.text


@pytest.mark.parametrize("data", [b"{not json", b"\xff\xfe", b"[1, 2]", b"42"])
def test_malformed_datagram_is_skipped(
    core: TeleopCore, arm: FakeArm, caplog: pytest.LogCaptureFixture, data: bytes
) -> None:
    core.receive(data)
    core.tick()

    assert arm.calls == []
    assert "skipped" in caplog.text


@pytest.mark.parametrize(
    "message",
    [
        {"type": "pose", "frame": "robot", "x": 300, "y": 0},
        {"type": "pose", "frame": "robot", "x": "far", "y": 0, "z": 0},
        {"type": "pose", "frame": "robot", "x": None, "y": 0, "z": 0},
        {"type": "pose", "frame": "robot", "x": math.nan, "y": 0, "z": 0},
        {"type": "pose", "frame": "robot", "x": math.inf, "y": 0, "z": 0},
        {"type": "engage", "x": 0},
        {"type": "speed", "value": "fast"},
    ],
)
def test_bad_fields_are_skipped(
    core: TeleopCore, arm: FakeArm, caplog: pytest.LogCaptureFixture, message: dict
) -> None:
    send(core, **message)
    core.tick()

    assert arm.moves == []
    assert "skipped" in caplog.text


def test_robot_error_is_skipped_and_restarts_the_filter(
    core: TeleopCore, arm: FakeArm, caplog: pytest.LogCaptureFixture
) -> None:
    arm.move_error = RobotError("joint limit")
    robot_pose(core, 300.0, 0.0, 50.0)
    arm.move_error = None
    robot_pose(core, 310.0, 0.0, 50.0)  # unsmoothed: the filter restarted

    assert arm.moves[-1] == approx((310.0, 0.0, 50.0, 0.0))
    assert "joint limit" in caplog.text


def test_connection_error_is_logged_and_the_loop_goes_on(
    core: TeleopCore, arm: FakeArm, caplog: pytest.LogCaptureFixture
) -> None:
    arm.move_error = OSError("link down")
    robot_pose(core, 300.0, 0.0, 50.0)

    assert "link down" in caplog.text


# --- Robot Panel commands and Robot Status (017-02, ADR-0024) ---


def test_enable_disable_and_clear_error_reach_the_arm(core: TeleopCore, arm: FakeArm) -> None:
    send(core, type="enable")
    send(core, type="clear_error")
    send(core, type="disable")
    core.tick()

    assert arm.calls == [("enable",), ("clear_error",), ("disable",)]


def test_disable_blocks_motion_until_enable(core: TeleopCore, arm: FakeArm) -> None:
    send(core, type="disable")
    send(core, type="move_to", x=300, y=0, z=50, r=0)
    core.tick()
    robot_pose(core, 300.0, 0.0, 50.0)

    assert arm.moves == []
    assert core.status()["reason"] == "disabled"

    send(core, type="enable")
    send(core, type="move_to", x=300, y=0, z=50, r=0)
    core.tick()

    assert arm.moves == [approx((300.0, 0.0, 50.0, 0.0))]


def test_engage_is_refused_while_disabled_and_does_not_follow_after_enable(
    core: TeleopCore, arm: FakeArm
) -> None:
    send(core, type="disable")
    send(core, type="engage", x=0.0, y=1.0, z=0.3)
    core.tick()
    assert core.status()["reason"] == "disabled"

    send(core, type="enable")
    send(core, type="pose", frame="headset", x=0.0, y=1.0, z=0.3)
    core.tick()

    assert arm.moves == []


def test_jog_moves_one_axis_by_the_step_from_the_arm_pose(clock: Clock) -> None:
    arm = FakeArm(pose=(300.0, 0.0, 50.0, 5.0, 0.0, 0.0))
    core = TeleopCore(arm, clock)
    send(core, type="jog", axis="x", step=10)
    send(core, type="jog", axis="y", step=-50)
    send(core, type="jog", axis="z", step=1)
    core.tick()

    assert arm.moves == [
        approx((310.0, 0.0, 50.0, 5.0)),
        approx((300.0, -50.0, 50.0, 5.0)),
        approx((300.0, 0.0, 51.0, 5.0)),
    ]


def test_jog_without_an_arm_pose_builds_on_the_last_target(core: TeleopCore, arm: FakeArm) -> None:
    send(core, type="jog", axis="x", step=10)
    send(core, type="jog", axis="x", step=10)
    core.tick()

    x0 = DEFAULT_ROBOT_POSE[0]
    assert [move[0] for move in arm.moves] == [x0 + 10, x0 + 20]


def test_jog_ends_engage(core: TeleopCore, arm: FakeArm) -> None:
    send(core, type="engage", x=0.0, y=1.0, z=0.3)
    send(core, type="jog", axis="z", step=10)
    core.tick()
    send(core, type="pose", frame="headset", x=0.0, y=1.2, z=0.3)
    core.tick()

    assert len(arm.moves) == 1


@pytest.mark.parametrize(
    ("pose", "message", "expected"),
    [
        ((415.0, 0.0, 50.0, 0.0), {"axis": "x", "step": 50}, (MAX_REACH_MM, 0.0, 50.0, 0.0)),
        ((300.0, 0.0, Z_MIN_MM, 0.0), {"axis": "z", "step": -50}, (300.0, 0.0, Z_MIN_MM, 0.0)),
        ((300.0, 0.0, Z_MAX_MM, 0.0), {"axis": "z", "step": 10}, (300.0, 0.0, Z_MAX_MM, 0.0)),
    ],
)
def test_jog_is_clamped_to_the_safe_workspace(
    clock: Clock, pose: tuple[float, ...], message: dict, expected: tuple[float, ...]
) -> None:
    arm = FakeArm(pose=pose)
    core = TeleopCore(arm, clock)
    send(core, type="jog", **message)
    core.tick()

    assert arm.moves == [approx(expected)]


def test_move_to_goes_to_the_pose_and_is_clamped(core: TeleopCore, arm: FakeArm) -> None:
    send(core, type="move_to", x=250, y=10, z=20, r=5)
    send(core, type="move_to", x=600, y=0, z=-500, r=400)
    core.tick()

    assert arm.moves == [
        approx((250.0, 10.0, 20.0, 5.0)),
        approx((MAX_REACH_MM, 0.0, Z_MIN_MM, R_MAX_DEG)),
    ]


@pytest.mark.parametrize(
    "message",
    [
        {"type": "jog", "axis": "r", "step": 10},
        {"type": "jog", "axis": "x", "step": 5},
        {"type": "jog", "axis": "x", "step": "10"},
        {"type": "jog", "axis": "x", "step": True},
        {"type": "jog", "axis": "x"},
        {"type": "jog", "step": 10},
        {"type": "move_to", "x": 300, "y": 0, "z": 50},
        {"type": "move_to", "x": math.nan, "y": 0, "z": 50, "r": 0},
        {"type": "move_to", "x": 300, "y": math.inf, "z": 50, "r": 0},
        {"type": "move_to", "x": "far", "y": 0, "z": 50, "r": 0},
    ],
)
def test_bad_jog_and_move_to_are_refused_and_never_move(
    core: TeleopCore, arm: FakeArm, message: dict
) -> None:
    send(core, **message)
    core.tick()

    assert arm.moves == []
    assert core.status()["result"] == "refused"


def test_unknown_type_is_refused_and_never_moves(core: TeleopCore, arm: FakeArm) -> None:
    send(core, type="dance", x=300, y=0, z=50, r=0)
    core.tick()

    assert arm.calls == []
    assert (core.status()["last_command"], core.status()["result"]) == ("dance", "refused")


STOP_BLOCKED = [
    {"type": "pose", "frame": "robot", "x": 300, "y": 0, "z": 50},
    {"type": "jog", "axis": "x", "step": 10},
    {"type": "move_to", "x": 300, "y": 0, "z": 50, "r": 0},
    {"type": "home"},
    {"type": "joint", "j1": 10, "j2": 20, "j3": 30},
    {"type": "calibrate", "x": 0.0, "y": 1.0, "z": 0.3},
]


def _arm_moved(arm: FakeArm) -> bool:
    return any(call[0] in ("move_to", "move_joints") for call in arm.calls)


@pytest.mark.parametrize("message", STOP_BLOCKED, ids=lambda m: m["type"])
def test_stop_latch_blocks_every_motion(core: TeleopCore, arm: FakeArm, message: dict) -> None:
    send(core, type="stop")
    core.tick()
    send(core, **message)
    core.tick()
    # calibrate must not quietly start following either
    send(core, type="pose", frame="headset", x=0.0, y=1.0, z=0.3)
    core.tick()

    assert not _arm_moved(arm)
    assert core.status()["stop_latched"] is True


@pytest.mark.parametrize("release", ["resume", "engage"])
@pytest.mark.parametrize("message", STOP_BLOCKED, ids=lambda m: m["type"])
def test_resume_or_engage_releases_the_stop_latch(
    core: TeleopCore, arm: FakeArm, release: str, message: dict
) -> None:
    send(core, type="stop")
    send(core, type=release, x=0.0, y=1.0, z=0.3)
    core.tick()
    assert core.status()["stop_latched"] is False

    send(core, **message)
    core.tick()
    send(core, type="pose", frame="headset", x=0.0, y=1.0, z=0.3)  # calibrate moves on a pose
    core.tick()

    assert _arm_moved(arm)


def test_resume_does_not_engage(core: TeleopCore, arm: FakeArm) -> None:
    send(core, type="stop")
    send(core, type="resume")
    send(core, type="pose", frame="headset", x=0.0, y=1.0, z=0.3)
    core.tick()

    assert arm.moves == []


def test_refused_command_is_reported_with_its_reason(core: TeleopCore) -> None:
    send(core, type="stop")
    send(core, type="home")
    core.tick()

    status = core.status()
    assert (status["last_command"], status["result"], status["reason"]) == (
        "home",
        "refused",
        "stop latched",
    )


def test_robot_error_is_reported_as_refused(core: TeleopCore, arm: FakeArm) -> None:
    arm.move_error = RobotError("arm refused MovJ")
    send(core, type="move_to", x=300, y=0, z=50, r=0)
    core.tick()

    status = core.status()
    assert (status["result"], status["reason"]) == ("refused", "arm refused MovJ")


def test_status_carries_mode_pose_suction_and_last_result(clock: Clock) -> None:
    arm = FakeArm(pose=(310.0, 1.0, 50.0, 2.0, 0.0, 0.0))
    core = TeleopCore(arm, clock)
    send(core, type="grip", value=1)
    send(core, type="jog", axis="x", step=10)
    core.tick()

    assert core.status() == {
        "type": "status",
        "mode": "enabled",
        "error_id": 0,
        "x": 310.0,
        "y": 1.0,
        "z": 50.0,
        "r": 2.0,
        "suction": True,
        "stop_latched": False,
        "last_command": "jog",
        "result": "done",
        "reason": "",
    }


def test_status_reads_the_error_id_only_in_error(core: TeleopCore, arm: FakeArm) -> None:
    arm.error_id = 22
    assert core.status()["error_id"] == 0

    arm.mode = "error"
    assert (core.status()["mode"], core.status()["error_id"]) == ("error", 22)


def test_status_survives_an_unreachable_arm(clock: Clock) -> None:
    arm = FakeArm(pose=OSError("down"))
    arm.mode_error = OSError("down")
    status = TeleopCore(arm, clock).status()

    assert status["mode"] == "unknown"
    assert (status["x"], status["y"], status["z"], status["r"]) == DEFAULT_ROBOT_POSE


def test_status_matches_the_shared_examples(clock: Clock) -> None:
    status = TeleopCore(FakeArm(pose=(300.0, 0.0, 50.0, 0.0)), clock).status()

    for example in EXAMPLES["status"]:
        message = example["message"]
        assert message.keys() == status.keys(), example["name"]
        for key, value in message.items():
            assert type(value) is type(status[key]), (example["name"], key)


@pytest.mark.parametrize("example", EXAMPLES["commands"], ids=lambda e: e["name"])
def test_every_example_command_is_accepted(
    core: TeleopCore, caplog: pytest.LogCaptureFixture, example: dict
) -> None:
    with caplog.at_level(logging.WARNING):
        send(core, **example["message"])
        core.tick()

    assert "skipped" not in caplog.text
    assert "unknown" not in caplog.text
    if example["message"]["type"] != "pose":
        assert core.status()["result"] == "done"


@pytest.mark.parametrize("example", EXAMPLES["commands"], ids=lambda e: e["name"])
def test_dry_run_accepts_every_example_command(clock: Clock, example: dict) -> None:
    core = TeleopCore(DryRunArm(), clock)
    send(core, **example["message"])
    core.tick()

    if example["message"]["type"] != "pose":
        assert core.status()["result"] == "done"


def test_dry_run_status_follows_enable_and_disable(clock: Clock) -> None:
    core = TeleopCore(DryRunArm(), clock)
    assert core.status()["mode"] == "enabled"

    send(core, type="disable")
    core.tick()
    assert core.status()["mode"] == "disabled"
