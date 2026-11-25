"""MG400 port seam: one `Dashboard` per TCP port (29999 dashboard, 30003 motion)."""

import socket
import time
from typing import Protocol

_REPLY_BUFFER_BYTES = 4096
_REPLY_TAIL_WAIT_S = 0.3  # how long to wait for the rest of a reply that has no `;` yet
_REPLY_TAIL_POLL_S = 0.05


class Dashboard(Protocol):
    """Connect, send one command, read its reply."""

    def connect(self) -> None:
        """Open the connection; when already open, reconnect."""

    def send(self, command: str) -> str:
        """Send one command such as `EnableRobot()` and return the reply, or "" if closed."""

    def close(self) -> None:
        """Close the connection; safe to call twice."""


class TcpDashboard:
    """One MG400 port over plain TCP."""

    # shortcut: raw socket instead of the official Dobot SDK (coder/DOBOT.md); it carries the
    # teammate's proven wire handling (teleop/mg400_control.py). Swap the SDK in behind
    # `Dashboard` if this ever falls short.

    def __init__(self, host: str, port: int = 29999, timeout_s: float = 3.0) -> None:
        """Remember where to connect; nothing is opened yet."""
        self._address = (host, port)
        self._timeout_s = timeout_s
        self._sock: socket.socket | None = None

    def connect(self) -> None:
        """Open the connection; when already open, reconnect."""
        self.close()
        self._sock = socket.create_connection(self._address, timeout=self._timeout_s)

    def send(self, command: str) -> str:
        """Drop stale replies, send one command and return its reply up to the `;`."""
        if self._sock is None:
            raise ConnectionError("dashboard is not connected")
        self._drain(self._sock)
        # No newline, exactly as the teammate's working client sends it.
        self._sock.sendall(command.encode("utf-8"))
        return self._read_reply(self._sock)

    def close(self) -> None:
        """Close the connection; safe to call twice."""
        if self._sock is not None:
            self._sock.close()
            self._sock = None

    def _drain(self, sock: socket.socket) -> None:
        # A reply that came late (after a timeout) would otherwise answer the next command.
        sock.setblocking(False)
        try:
            while sock.recv(_REPLY_BUFFER_BYTES):
                pass
        except OSError:
            pass  # nothing left to read
        finally:
            sock.settimeout(self._timeout_s)

    def _read_reply(self, sock: socket.socket) -> str:
        reply = sock.recv(_REPLY_BUFFER_BYTES).decode("utf-8", errors="ignore")
        deadline = time.monotonic() + _REPLY_TAIL_WAIT_S
        try:
            while reply and not reply.rstrip().endswith(";") and time.monotonic() < deadline:
                sock.settimeout(_REPLY_TAIL_POLL_S)
                try:
                    more = sock.recv(_REPLY_BUFFER_BYTES)
                except TimeoutError:
                    continue
                if not more:
                    break
                reply += more.decode("utf-8", errors="ignore")
        finally:
            sock.settimeout(self._timeout_s)
        return reply.strip()
