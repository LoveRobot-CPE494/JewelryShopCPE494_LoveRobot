import pytest

from hub.teleop.probe_reach import UnsupportedError, _steps, is_reachable, reach_runs, scan

REACHES = [150.0, 200.0, 300.0, 350.0, 400.0, 450.0]


@pytest.mark.parametrize(
    ("reply", "expected"),
    [
        ("0,{10,20,30,0},InverseSolution();", True),
        ("-1,{0,0,0,0},InverseSolution();", False),  # the arm has no solution
        ("0,{10,100,30,0},InverseSolution();", False),  # j2 above its 85 deg limit
        ("0,{},InverseSolution();", False),
    ],
)
def test_is_reachable_needs_an_ok_reply_inside_the_joint_limits(reply: str, expected: bool) -> None:
    assert is_reachable(reply) is expected


@pytest.mark.parametrize("reply", ["", "Command not found", "-1,{},unknown command;"])
def test_firmware_without_inverse_solution_is_reported_not_read_as_unreachable(
    reply: str,
) -> None:
    with pytest.raises(UnsupportedError):
        is_reachable(reply)


def test_reach_runs_lists_every_unbroken_run() -> None:
    reachable = {150.0, 200.0, 250.0, 300.0, 400.0}

    runs = reach_runs(
        lambda reach, z: reach in reachable, 50.0, [100.0, 150.0, 200.0, 250.0, 300.0, 350.0, 400.0]
    )

    assert runs == [(150.0, 300.0), (400.0, 400.0)]


def test_scan_reports_the_band_that_holds_at_every_height(
    capsys: pytest.CaptureFixture[str],
) -> None:
    def ask(reach_mm: float, z_mm: float) -> bool:
        return 200 <= reach_mm <= (400 if z_mm < 100 else 350)

    scan(ask, [0.0, 100.0], REACHES)

    assert "every height: reach 200 - 350 mm" in capsys.readouterr().out


def test_scan_says_none_when_a_height_has_no_band(capsys: pytest.CaptureFixture[str]) -> None:
    scan(lambda reach, z: z < 100 and 200 <= reach <= 350, [0.0, 100.0], REACHES)

    assert "every height: none" in capsys.readouterr().out


def test_scan_says_none_when_the_bands_do_not_overlap(capsys: pytest.CaptureFixture[str]) -> None:
    scan(lambda reach, z: (reach <= 200) if z < 100 else (reach >= 350), [0.0, 100.0], REACHES)

    assert "every height: none" in capsys.readouterr().out


def test_scan_flags_holes_and_bands_cut_by_the_probed_range(
    capsys: pytest.CaptureFixture[str],
) -> None:
    scan(lambda reach, z: reach in (150.0, 200.0, 350.0), [0.0], REACHES)

    out = capsys.readouterr().out
    assert "holes" in out
    assert "touches the probed range" in out


def test_steps_include_both_ends() -> None:
    assert _steps(0.0, 150.0, 50.0) == [0.0, 50.0, 100.0, 150.0]
