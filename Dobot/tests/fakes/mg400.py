"""A local fake MG400 port (dashboard or motion); never talks to a real controller."""

import socket
import socketserver
import threading


class _Server(socketserver.ThreadingTCPServer):
    daemon_threads = True

    def __init__(self) -> None:
        super().__init__(("127.0.0.1", 0), _Handler)
        self.received: list[str] = []
        self.scripts: dict[str, list[str | None]] = {}
        self.connections: list[socket.socket] = []


class _Handler(socketserver.BaseRequestHandler):
    server: _Server

    def handle(self) -> None:
        self.server.connections.append(self.request)
        buffer = ""
        while chunk := self.request.recv(4096):
            buffer += chunk.decode("ascii")
            while ")" in buffer:  # commands are `Name(args)`, with or without a newline
                command, buffer = buffer.split(")", 1)
                command = command.strip() + ")"
                self.server.received.append(command)
                reply = self._reply(command)
                if reply is None:
                    return  # drop the connection
                self.request.sendall(reply.encode("ascii"))

    def _reply(self, command: str) -> str | None:
        queue = self.server.scripts.get(command.split("(", 1)[0])
        if not queue:
            return f"0,{{}},{command};"
        return queue.pop(0) if len(queue) > 1 else queue[0]


class FakeMG400:
    """Echoes each command as a success reply, unless a reply is scripted for it."""

    def __init__(self) -> None:
        """Bind 127.0.0.1 on an ephemeral port and serve in a thread."""
        self._server = _Server()
        self.host, self.port = self._server.server_address[:2]
        threading.Thread(target=self._server.serve_forever, args=(0.05,), daemon=True).start()

    @property
    def received(self) -> list[str]:
        """Commands the fake has seen, in order."""
        return self._server.received

    @property
    def connection_count(self) -> int:
        """How many times a client has connected."""
        return len(self._server.connections)

    def script(self, name: str, *replies: str | None) -> None:
        """Reply to command `name` with `replies` in turn, the last one repeating; None drops."""
        self._server.scripts[name] = list(replies)

    def push(self, text: str) -> None:
        """Send unasked bytes on every open connection, like a late reply."""
        for connection in self._server.connections:
            connection.sendall(text.encode("ascii"))

    def close(self) -> None:
        """Stop serving."""
        self._server.shutdown()
        self._server.server_close()
