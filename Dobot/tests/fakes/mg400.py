"""A local fake MG400 dashboard server; never talks to a real controller."""

import socketserver
import threading


class _Server(socketserver.ThreadingTCPServer):
    daemon_threads = True

    def __init__(self) -> None:
        super().__init__(("127.0.0.1", 0), _Handler)
        self.received: list[str] = []


class _Handler(socketserver.StreamRequestHandler):
    server: _Server

    def handle(self) -> None:
        for line in self.rfile:
            command = line.decode("ascii").strip()
            self.server.received.append(command)
            self.wfile.write(f"0,{{}},{command};".encode("ascii"))


class FakeMG400:
    """Echoes each command as a success reply, like the dashboard port does."""

    def __init__(self) -> None:
        """Bind 127.0.0.1 on an ephemeral port and serve in a thread."""
        self._server = _Server()
        self.host, self.port = self._server.server_address[:2]
        threading.Thread(target=self._server.serve_forever, daemon=True).start()

    @property
    def received(self) -> list[str]:
        """Commands the fake has seen, in order."""
        return self._server.received

    def close(self) -> None:
        """Stop serving."""
        self._server.shutdown()
        self._server.server_close()
