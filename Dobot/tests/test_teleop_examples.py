"""The shared Teleop examples (`protocol/teleop_examples.json`) against the core."""

import json
import logging
from pathlib import Path

import pytest

from hub.teleop.__main__ import DryRunArm
from hub.teleop.core import TeleopCore

EXAMPLES = json.loads(
    (Path(__file__).parents[1] / "protocol" / "teleop_examples.json").read_text(encoding="utf-8")
)


@pytest.mark.parametrize("command", EXAMPLES["commands"], ids=lambda c: c["type"])
def test_every_example_command_is_handled_by_the_dry_run_arm(
    command: dict, caplog: pytest.LogCaptureFixture
) -> None:
    core = TeleopCore(DryRunArm())
    with caplog.at_level(logging.WARNING):
        core.receive(json.dumps(command).encode("utf-8"))
        core.tick()

    assert "skipped" not in caplog.text
    assert "refused" not in caplog.text
    if command["type"] != "pose":  # poses stream; they are not commands with a result
        assert core.status()["last_command"] == {
            "type": command["type"],
            "result": "done",
            "reason": "",
        }


def test_example_commands_cover_every_robot_panel_command() -> None:
    kinds = {command["type"] for command in EXAMPLES["commands"]}

    assert {"enable", "disable", "clear_error", "resume", "jog", "move_to"} <= kinds


@pytest.mark.parametrize("packet", EXAMPLES["status"])
def test_example_status_packets_have_the_core_status_shape(packet: dict) -> None:
    status = TeleopCore(DryRunArm()).status()

    assert packet.keys() == status.keys()
    assert packet["mode"] in {"enabled", "disabled", "error", "unknown"}
    if packet["pose"] is not None:
        assert packet["pose"].keys() == {"x", "y", "z", "r"}
    if packet["last_command"] is not None:
        assert packet["last_command"].keys() == {"type", "result", "reason"}
        assert packet["last_command"]["result"] in {"done", "refused"}
