"""Dashboard-port seam (29999): the teammate's MG400 TCP code can sit behind `Dashboard`."""

import socket
from typing import Protocol

_REPLY_BUFFER_BYTES = 4096


class Dashboard(Protocol):
    """Connect, send one dashboard command, read its reply."""

    def connect(self) -> None:
        """Open the connection."""

    def send(self, command: str) -> str:
        """Send one command such as `EnableRobot()` and return the reply."""

    def close(self) -> None:
        """Close the connection; safe to call twice."""


class TcpDashboard:
    """Dashboard over plain TCP."""

    # shortcut: raw socket instead of the official Dobot SDK (coder/DOBOT.md); swap the SDK in
    # behind `Dashboard` when the teammate's MG400 code lands.

    def __init__(self, host: str, port: int = 29999, timeout_s: float = 3.0) -> None:
        """Remember where to connect; nothing is opened yet."""
        self._address = (host, port)
        self._timeout_s = timeout_s
        self._sock: socket.socket | None = None

    def connect(self) -> None:
        """Open the connection."""
        self._sock = socket.create_connection(self._address, timeout=self._timeout_s)

    def send(self, command: str) -> str:
        """Send one command and return the reply."""
        if self._sock is None:
            raise ConnectionError("dashboard is not connected")
        self._sock.sendall(f"{command}\n".encode("ascii"))
        # shortcut: one recv, assumes the reply fits one packet; upgrade when replies grow.
        return self._sock.recv(_REPLY_BUFFER_BYTES).decode("ascii")

    def close(self) -> None:
        """Close the connection; safe to call twice."""
        if self._sock is not None:
            self._sock.close()
            self._sock = None
