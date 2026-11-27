"""Find the MG400's real reach without moving it: ask InverseSolution() over a grid (#49).

Run from `Dobot/` with the Teleop Bridge stopped (both use the dashboard port):

    python -m hub.teleop.probe_reach --ip 192.168.1.6

It sends no move, enable or DO command: InverseSolution() only computes joint angles.
For each height from Z_MIN_MM to Z_MAX_MM it prints the reach band (distance of x, y from the
base) where every point on the line y = 0 has a solution inside JOINT_LIMITS_DEG. J1 only
turns the arm, so the band holds for any direction within the J1 limit. IK knows nothing of
the table or the base: check the new MIN_REACH_MM and MAX_REACH_MM at the station.
"""

import argparse
from collections.abc import Callable, Iterable, Sequence

from hub.dashboard import TcpDashboard
from hub.teleop.core import JOINT_LIMITS_DEG, Z_MAX_MM, Z_MIN_MM
from hub.teleop.mg400_arm import is_ok_reply, parse_values

DASHBOARD_PORT = 29999
REACH_FROM_MM, REACH_TO_MM = 100.0, 460.0  # probed range, wider than any MG400 band
Ask = Callable[[float, float], bool]  # (reach_mm, z_mm) -> reachable, on y = 0 with r = 0


class UnsupportedError(Exception):
    """The firmware has no InverseSolution(): the probe cannot tell anything."""


def is_reachable(reply: str) -> bool:
    """True when an InverseSolution() reply is OK and its joints are inside JOINT_LIMITS_DEG."""
    if not reply or "not" in reply.lower() or "unknown" in reply.lower():
        raise UnsupportedError(f"InverseSolution() is not supported here: {reply!r}")
    joints = parse_values(reply)
    if not is_ok_reply(reply) or not joints or len(joints) < 4:
        return False
    limits = (JOINT_LIMITS_DEG[name] for name in ("j1", "j2", "j3", "j4"))
    return all(low <= value <= high for value, (low, high) in zip(joints, limits, strict=False))


def reach_runs(ask: Ask, z_mm: float, reaches_mm: Sequence[float]) -> list[tuple[float, float]]:
    """Return every unbroken run of reachable distances at this height, nearest first."""
    runs: list[tuple[float, float]] = []
    start_mm: float | None = None
    previous_mm: float | None = None
    for reach_mm in [*reaches_mm, None]:  # None closes the last run
        if reach_mm is not None and ask(reach_mm, z_mm):
            start_mm = reach_mm if start_mm is None else start_mm
            previous_mm = reach_mm
            continue
        if start_mm is not None and previous_mm is not None:
            runs.append((start_mm, previous_mm))
        start_mm = previous_mm = None
    return runs


def scan(ask: Ask, heights_mm: Iterable[float], reaches_mm: Sequence[float]) -> None:
    """Print the reach band per height, then the band that holds at every height."""
    lows_mm, highs_mm = [], []
    is_complete = True
    for z_mm in heights_mm:
        runs = reach_runs(ask, z_mm, reaches_mm)
        if not runs:
            print(f"z {z_mm:6.1f} mm: no reachable point")
            is_complete = False
            continue
        low_mm, high_mm = max(runs, key=lambda run: run[1] - run[0])
        notes = []
        if len(runs) > 1:
            notes.append(f"holes: {', '.join(f'{a:.0f}-{b:.0f}' for a, b in runs)}")
        if low_mm == reaches_mm[0] or high_mm == reaches_mm[-1]:
            notes.append("touches the probed range")
        suffix = f"  ({'; '.join(notes)})" if notes else ""
        print(f"z {z_mm:6.1f} mm: reach {low_mm:.0f} - {high_mm:.0f} mm{suffix}")
        lows_mm.append(low_mm)
        highs_mm.append(high_mm)
    if not is_complete or not lows_mm or max(lows_mm) > min(highs_mm):
        print("every height: none")
    else:
        print(f"every height: reach {max(lows_mm):.0f} - {min(highs_mm):.0f} mm (add a margin)")


def main(argv: Sequence[str] | None = None) -> None:
    """Probe the real arm over the dashboard port; nothing moves."""
    parser = argparse.ArgumentParser(prog="python -m hub.teleop.probe_reach", description=__doc__)
    parser.add_argument("--ip", default="192.168.1.6", help="MG400 address")
    parser.add_argument("--z-step-mm", type=float, default=10.0, help="height step")
    parser.add_argument("--reach-step-mm", type=float, default=5.0, help="reach step")
    args = parser.parse_args(argv)

    dashboard = TcpDashboard(args.ip, DASHBOARD_PORT)
    dashboard.connect()

    def ask(reach_mm: float, z_mm: float) -> bool:
        return is_reachable(dashboard.send(f"InverseSolution({reach_mm},0,{z_mm},0,0,0)"))

    try:
        heights_mm = _steps(Z_MIN_MM, Z_MAX_MM, args.z_step_mm)
        scan(ask, heights_mm, _steps(REACH_FROM_MM, REACH_TO_MM, args.reach_step_mm))
    except UnsupportedError as error:
        print(error)
    finally:
        dashboard.close()


def _steps(start_mm: float, stop_mm: float, step_mm: float) -> list[float]:
    # Inclusive of both ends.
    count = int(round((stop_mm - start_mm) / step_mm))
    return [start_mm + i * step_mm for i in range(count + 1)]


if __name__ == "__main__":
    main()
