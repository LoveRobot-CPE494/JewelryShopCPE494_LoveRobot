from collections.abc import Iterator

import pytest

from hub.dashboard import TcpDashboard
from hub.teleop.core import RobotError
from hub.teleop.mg400_arm import MG400Arm
from tests.fakes.mg400 import FakeMG400

IDLE, RUNNING, DISABLED, ERROR = (f"0,{{{mode}}},RobotMode();" for mode in (5, 7, 4, 9))
INVERSE_OK = "0,{10,20,30,0},InverseSolution();"


class FakeTime:
    def __init__(self) -> None:
        self.now = 0.0
        self.sleeps: list[float] = []

    def clock(self) -> float:
        return self.now

    def sleep(self, seconds: float) -> None:
        self.sleeps.append(seconds)
        self.now += seconds


@pytest.fixture
def fake_motion() -> Iterator[FakeMG400]:
    fake = FakeMG400()
    yield fake
    fake.close()


@pytest.fixture
def fake_time() -> FakeTime:
    return FakeTime()


@pytest.fixture
def arm(fake_mg400: FakeMG400, fake_motion: FakeMG400, fake_time: FakeTime) -> Iterator[MG400Arm]:
    fake_mg400.script("RobotMode", IDLE)
    fake_mg400.script("InverseSolution", INVERSE_OK)
    arm = MG400Arm(
        TcpDashboard(fake_mg400.host, fake_mg400.port),
        TcpDashboard(fake_motion.host, fake_motion.port),
        clock=fake_time.clock,
        sleep=fake_time.sleep,
    )
    yield arm
    arm.close()


def test_move_to_checks_ik_sends_movj_and_waits_by_polling_robot_mode(
    arm: MG400Arm, fake_mg400: FakeMG400, fake_motion: FakeMG400
) -> None:
    fake_mg400.script("RobotMode", IDLE, RUNNING, RUNNING, IDLE)

    arm.move_to(300, 0, 50, 0)

    assert fake_motion.received == ["MovJ(300.00,0.00,50.00,0.00)"]
    assert fake_mg400.received == [
        "InverseSolution(300,0,50,0,0,0)",
        "RobotMode()",  # pre-move check
        "RobotMode()",
        "RobotMode()",
        "RobotMode()",
    ]
    assert "Sync()" not in fake_motion.received


@pytest.mark.parametrize(("mode", "enables"), [(ERROR, 0), (DISABLED, 1)])
def test_alarm_during_a_move_recovers_and_raises(
    arm: MG400Arm,
    fake_mg400: FakeMG400,
    fake_motion: FakeMG400,
    mode: str,
    enables: int,
) -> None:
    fake_mg400.script("RobotMode", IDLE, mode, mode, IDLE)

    with pytest.raises(RobotError):
        arm.move_to(300, 0, 50, 0)

    assert {"GetErrorID()", "ClearError()", "Continue()"} <= set(fake_mg400.received)
    assert fake_mg400.received.count("EnableRobot()") == enables
    arm.move_joints(0, 0, 0, 0, is_blocking=False)  # served on the new connection
    assert fake_motion.connection_count == 2  # motion port reconnected


def test_move_that_never_finishes_is_reset_after_the_timeout(
    arm: MG400Arm, fake_mg400: FakeMG400, fake_time: FakeTime
) -> None:
    fake_mg400.script("RobotMode", IDLE, RUNNING)

    with pytest.raises(RobotError, match="30"):
        arm.move_to(300, 0, 50, 0)

    assert fake_mg400.received[-1] == "ResetRobot()"
    assert 30.0 < fake_time.now < 31.0


def test_arm_already_in_error_is_recovered_before_the_move(
    arm: MG400Arm, fake_mg400: FakeMG400, fake_motion: FakeMG400
) -> None:
    fake_mg400.script("RobotMode", ERROR, IDLE)

    arm.move_to(300, 0, 50, 0)

    assert "ClearError()" in fake_mg400.received
    assert fake_motion.received[-1] == "MovJ(300.00,0.00,50.00,0.00)"


def test_refused_move_recovers_and_raises(
    arm: MG400Arm, fake_mg400: FakeMG400, fake_motion: FakeMG400
) -> None:
    fake_motion.script("MovJ", "-1,{},MovJ();")

    with pytest.raises(RobotError, match="refused"):
        arm.move_to(300, 0, 50, 0)

    assert "ClearError()" in fake_mg400.received


def test_inverse_solution_joint_limit_stops_the_move_before_it_is_sent(
    arm: MG400Arm, fake_mg400: FakeMG400, fake_motion: FakeMG400
) -> None:
    fake_mg400.script("InverseSolution", "0,{10,100,30,0},InverseSolution();")  # j2 > 85

    with pytest.raises(ValueError, match="j2"):
        arm.move_to(300, 0, 50, 0)

    assert fake_motion.received == []


def test_unreachable_point_stops_the_move(
    arm: MG400Arm, fake_mg400: FakeMG400, fake_motion: FakeMG400
) -> None:
    fake_mg400.script("InverseSolution", "-1,{},InverseSolution();")

    with pytest.raises(ValueError, match="unreachable"):
        arm.move_to(300, 0, 50, 0)

    assert fake_motion.received == []


def test_inverse_solution_check_switches_itself_off_when_the_firmware_lacks_it(
    arm: MG400Arm, fake_mg400: FakeMG400, fake_motion: FakeMG400
) -> None:
    fake_mg400.script("InverseSolution", "-1,{},command not supported;")

    arm.move_to(300, 0, 50, 0)
    arm.move_to(310, 0, 50, 0)

    assert fake_mg400.received.count("InverseSolution(300,0,50,0,0,0)") == 1
    assert len(fake_motion.received) == 2


def test_move_joints_outside_the_joint_limits_is_refused(
    arm: MG400Arm, fake_motion: FakeMG400
) -> None:
    with pytest.raises(ValueError, match="j1"):
        arm.move_joints(170, 0, 0, 0)

    assert fake_motion.received == []


def test_move_joints_sends_joint_movj(arm: MG400Arm, fake_motion: FakeMG400) -> None:
    arm.move_joints(10, 20, 30, 0)

    assert fake_motion.received == ["JointMovJ(10.00,20.00,30.00,0.00)"]


@pytest.mark.parametrize(("percent", "sent"), [(150, 100), (0, 1), (40, 40)])
def test_speed_factor_is_clamped_to_1_to_100(
    arm: MG400Arm, fake_mg400: FakeMG400, percent: int, sent: int
) -> None:
    arm.set_speed(percent)

    assert fake_mg400.received[-1] == f"SpeedFactor({sent})"


def test_digital_output_enable_disable_and_reset(
    arm: MG400Arm, fake_mg400: FakeMG400, fake_time: FakeTime
) -> None:
    arm.set_digital_output(1, True)
    arm.set_digital_output(1, False)
    arm.enable()
    arm.disable()
    arm.reset()

    assert fake_mg400.received == [
        "DO(1,1)",
        "DO(1,0)",
        "ClearError()",
        "EnableRobot()",
        "DisableRobot()",
        "ResetRobot()",
    ]
    assert fake_time.sleeps == [1.0]


def test_get_pose_parses_the_reply(arm: MG400Arm, fake_mg400: FakeMG400) -> None:
    fake_mg400.script("GetPose", "0,{300.5,-2,50,10,0,0},GetPose();")

    assert arm.get_pose() == [300.5, -2.0, 50.0, 10.0, 0.0, 0.0]


def test_get_pose_without_values_is_empty(arm: MG400Arm, fake_mg400: FakeMG400) -> None:
    fake_mg400.script("GetPose", "0,{},GetPose();")

    assert not arm.get_pose()


def test_dropped_connection_reconnects_and_retries_once(
    arm: MG400Arm, fake_mg400: FakeMG400
) -> None:
    fake_mg400.script("GetPose", None, "0,{1,2,3,4},GetPose();")

    assert arm.get_pose() == [1.0, 2.0, 3.0, 4.0]
    assert fake_mg400.connection_count == 2


def test_second_failure_is_raised(arm: MG400Arm, fake_mg400: FakeMG400) -> None:
    fake_mg400.script("GetPose", None)

    with pytest.raises(ConnectionError):
        arm.get_pose()


def test_clear_error_sends_clear_error(arm: MG400Arm, fake_mg400: FakeMG400) -> None:
    arm.clear_error()

    assert fake_mg400.received == ["ClearError()"]


@pytest.mark.parametrize(
    ("reply", "mode"),
    [
        (IDLE, "enabled"),
        (RUNNING, "enabled"),
        (DISABLED, "disabled"),
        (ERROR, "error"),
        ("0,{10},RobotMode();", "error"),  # PAUSE: stopped after a hit, needs a clear
        ("0,{1},RobotMode();", "unknown"),
        ("0,{},RobotMode();", "unknown"),
    ],
)
def test_get_robot_mode_maps_the_mg400_mode(
    arm: MG400Arm, fake_mg400: FakeMG400, reply: str, mode: str
) -> None:
    fake_mg400.script("RobotMode", reply)

    assert arm.get_robot_mode() == mode


@pytest.mark.parametrize(
    ("reply", "error_id"),
    [
        ("0,{[[],[],[],[],[],[],[]]},GetErrorID();", 0),
        ("0,{[[22,23],[],[],[],[],[],[]]},GetErrorID();", 22),
        ("0,{[[],[],[],[],[],[],[115]]},GetErrorID();", 115),
        ("-1,{},GetErrorID();", None),
        ("0,GetErrorID();", None),
    ],
)
def test_get_error_id_returns_the_first_active_error(
    arm: MG400Arm, fake_mg400: FakeMG400, reply: str, error_id: int | None
) -> None:
    fake_mg400.script("GetErrorID", reply)

    assert arm.get_error_id() == error_id
