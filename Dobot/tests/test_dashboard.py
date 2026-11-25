import threading
import time

import pytest

from hub.dashboard import TcpDashboard
from tests.fakes.mg400 import FakeMG400


def test_enable_robot_returns_the_dashboard_reply(fake_mg400: FakeMG400) -> None:
    dashboard = TcpDashboard(fake_mg400.host, fake_mg400.port)
    dashboard.connect()
    try:
        reply = dashboard.send("EnableRobot()")
    finally:
        dashboard.close()

    assert reply == "0,{},EnableRobot();"
    assert fake_mg400.received == ["EnableRobot()"]


def test_send_before_connect_raises(fake_mg400: FakeMG400) -> None:
    dashboard = TcpDashboard(fake_mg400.host, fake_mg400.port)

    with pytest.raises(ConnectionError):
        dashboard.send("EnableRobot()")


def test_stale_reply_is_drained_before_the_next_command(fake_mg400: FakeMG400) -> None:
    dashboard = TcpDashboard(fake_mg400.host, fake_mg400.port)
    dashboard.connect()
    try:
        dashboard.send("RobotMode()")
        fake_mg400.push("0,{},LateReply();")
        time.sleep(0.1)  # let the late reply land in the client's buffer
        reply = dashboard.send("GetPose()")
    finally:
        dashboard.close()

    assert reply == "0,{},GetPose();"


def test_reply_split_across_packets_is_read_up_to_the_semicolon(fake_mg400: FakeMG400) -> None:
    fake_mg400.script("GetPose", "0,{1,2,3,4}")  # no `;` yet: the rest is pushed below
    dashboard = TcpDashboard(fake_mg400.host, fake_mg400.port)
    dashboard.connect()
    try:
        threading.Timer(0.05, fake_mg400.push, [",GetPose();"]).start()
        reply = dashboard.send("GetPose()")
    finally:
        dashboard.close()

    assert reply == "0,{1,2,3,4},GetPose();"


def test_connect_again_reconnects(fake_mg400: FakeMG400) -> None:
    dashboard = TcpDashboard(fake_mg400.host, fake_mg400.port)
    dashboard.connect()
    dashboard.connect()
    try:
        assert dashboard.send("EnableRobot()") == "0,{},EnableRobot();"
    finally:
        dashboard.close()

    assert fake_mg400.connection_count == 2
