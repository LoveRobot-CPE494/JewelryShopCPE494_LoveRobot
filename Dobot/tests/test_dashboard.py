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
