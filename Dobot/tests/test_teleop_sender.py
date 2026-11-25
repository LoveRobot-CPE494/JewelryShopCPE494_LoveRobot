import pytest

from hub.teleop.sender import build_message


@pytest.mark.parametrize(
    ("line", "message"),
    [
        ("clear", {"type": "clear_error"}),
        ("resume", {"type": "resume"}),
        ("jog z -10", {"type": "jog", "axis": "z", "step": -10.0}),
        ("move 300 0 50 0", {"type": "move_to", "x": 300.0, "y": 0.0, "z": 50.0, "r": 0.0}),
        ("suck 1", {"type": "grip", "value": 1}),
        ("p 300 0 50", {"type": "pose", "frame": "robot", "x": 300.0, "y": 0.0, "z": 50.0}),
        ("j 10 20 30", {"type": "joint", "j1": 10.0, "j2": 20.0, "j3": 30.0}),
    ],
)
def test_typed_line_becomes_a_teleop_message(line: str, message: dict) -> None:
    assert build_message(line.split()) == message


@pytest.mark.parametrize("line", ["dance", "move 300 0", "jog x", "speed fast", "enable now"])
def test_bad_line_is_refused(line: str) -> None:
    with pytest.raises(ValueError):
        build_message(line.split())
