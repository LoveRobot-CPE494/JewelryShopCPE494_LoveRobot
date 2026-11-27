import time

import pytest

from hub.teleop import beacon
from hub.teleop.beacon import choose_address, encode_beacon

# HostBeaconCodec doc comment: robot, 192.168.1.42 port 7777
DOCUMENTED_ROBOT = bytes.fromhex("4A53524201C0A8012A1E61")


def test_encode_matches_documented_robot_example() -> None:
    assert encode_beacon("192.168.1.42", 7777) == DOCUMENTED_ROBOT


def test_choose_skips_the_mg400_network() -> None:
    assert choose_address(["192.168.1.5", "10.0.0.7"], "192.168.1.6") == "10.0.0.7"


def test_choose_skips_loopback_and_link_local() -> None:
    assert choose_address(["127.0.0.1", "169.254.3.4", "10.0.0.7"], "192.168.1.6") == "10.0.0.7"


def test_choose_falls_back_to_the_only_address() -> None:
    assert choose_address(["192.168.1.5"], "192.168.1.6") == "192.168.1.5"


def test_choose_none_when_nothing_usable() -> None:
    assert choose_address(["127.0.0.1"], "192.168.1.6") is None


def test_start_beacon_broadcasts_until_stopped(monkeypatch: pytest.MonkeyPatch) -> None:
    sent: list[tuple[bytes, tuple[str, int]]] = []
    bound: list[tuple[str, int]] = []

    class FakeSocket:
        def setsockopt(self, *args: object) -> None:
            pass

        def bind(self, address: tuple[str, int]) -> None:
            bound.append(address)

        def sendto(self, data: bytes, target: tuple[str, int]) -> None:
            sent.append((data, target))

        def __enter__(self) -> "FakeSocket":
            return self

        def __exit__(self, *args: object) -> None:
            pass

    monkeypatch.setattr(beacon.socket, "socket", lambda *args: FakeSocket())
    monkeypatch.setattr(beacon, "INTERVAL_S", 0.01)
    stop = beacon.start_beacon(7777, "192.168.1.6", address="192.168.1.42")
    deadline = time.time() + 2
    while len(sent) < 2 and time.time() < deadline:
        time.sleep(0.01)
    stop.set()
    assert len(sent) >= 2
    assert bound == [("192.168.1.42", 0)]  # leaves on the announced card
    assert sent[0] == (DOCUMENTED_ROBOT, ("255.255.255.255", 7778))
    time.sleep(0.05)
    count = len(sent)
    time.sleep(0.05)
    assert len(sent) == count
