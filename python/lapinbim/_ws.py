"""A minimal WebSocket client on the standard library (RFC 6455): what Lapin's engine needs and nothing more.

One connection to ``ws://127.0.0.1:<port>/ws``: the opening handshake, masked text frames out, text and binary
messages in (fragments joined), ping answered with pong, close answered and reported. No extensions, no
compression, no TLS (the engine listens on the loopback only).
"""

from __future__ import annotations

import base64
import hashlib
import os
import socket
import struct
import threading

GUID = b"258EAFA5-E914-47DA-95CA-C5AB0DC85B11"

TEXT, BINARY, CLOSE, PING, PONG, CONTINUATION = 0x1, 0x2, 0x8, 0x9, 0xA, 0x0


class ConnectionClosed(ConnectionError):
    """The peer closed the connection (a close frame, or the socket ended)."""

    def __init__(self, code: int | None = None, reason: str = ""):
        super().__init__(f"closed ({code}){': ' + reason if reason else ''}" if code else "closed")
        self.code = code
        self.reason = reason


def accept_key(key: str) -> str:
    """The Sec-WebSocket-Accept a server answers ``key`` with."""
    return base64.b64encode(hashlib.sha1(key.encode("ascii") + GUID).digest()).decode("ascii")


def mask(data: bytes, key: bytes) -> bytes:
    """``data`` XORed with the 4-byte ``key`` repeated (fast for large payloads: one big integer)."""
    n = len(data)
    if n == 0:
        return b""
    repeated = (key * (n // 4 + 1))[:n]
    return (int.from_bytes(data, "little") ^ int.from_bytes(repeated, "little")).to_bytes(n, "little")


def frame(opcode: int, payload: bytes, masked: bool = True, fin: bool = True) -> bytes:
    """One frame: FIN and opcode, the length (7, 16 or 64 bits), the mask and the (masked) payload."""
    head = bytearray([(0x80 if fin else 0) | opcode])
    n = len(payload)
    bit = 0x80 if masked else 0
    if n < 126:
        head.append(bit | n)
    elif n < 1 << 16:
        head.append(bit | 126)
        head += struct.pack(">H", n)
    else:
        head.append(bit | 127)
        head += struct.pack(">Q", n)
    if not masked:
        return bytes(head) + payload
    key = os.urandom(4)
    return bytes(head) + key + mask(payload, key)


class WebSocket:
    """A client connection. ``recv`` from one thread; ``send_text`` and ``close`` from any (one lock)."""

    def __init__(self, host: str, port: int, path: str = "/ws", timeout: float | None = 10.0):
        self._sock = socket.create_connection((host, port), timeout=timeout)
        self._sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        self._send_lock = threading.Lock()
        self._buf = bytearray()
        self._closed = False
        try:
            self._handshake(host, port, path)
        except BaseException:
            self._sock.close()
            raise
        self._sock.settimeout(None)

    def _handshake(self, host: str, port: int, path: str) -> None:
        key = base64.b64encode(os.urandom(16)).decode("ascii")
        request = (
            f"GET {path} HTTP/1.1\r\nHost: {host}:{port}\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
            f"Sec-WebSocket-Key: {key}\r\nSec-WebSocket-Version: 13\r\nUser-Agent: lapinbim\r\n\r\n"
        )
        self._sock.sendall(request.encode("ascii"))
        while b"\r\n\r\n" not in self._buf:
            chunk = self._sock.recv(4096)
            if not chunk:
                raise ConnectionClosed(reason="the server closed during the handshake")
            self._buf += chunk
            if len(self._buf) > 65536:
                raise ConnectionError("The server's handshake is too long.")
        end = self._buf.index(b"\r\n\r\n")
        head = bytes(self._buf[:end]).decode("latin-1").split("\r\n")
        del self._buf[: end + 4]
        status = head[0].split(" ", 2)
        if len(status) < 2 or status[1] != "101":
            raise ConnectionError(f"The server refused the WebSocket: {head[0]}")
        headers = {k.strip().lower(): v.strip() for k, _, v in (line.partition(":") for line in head[1:])}
        if headers.get("sec-websocket-accept") != accept_key(key):
            raise ConnectionError("The server's handshake does not answer ours.")

    # ------------------------------------------------------------------ reading

    def _read(self, n: int) -> bytes:
        """Exactly ``n`` bytes (what the handshake left over first)."""
        if len(self._buf) >= n:
            head = bytes(self._buf[:n])
            del self._buf[:n]
            return head
        out = bytearray(n)
        got = len(self._buf)
        out[:got] = self._buf
        self._buf.clear()
        view = memoryview(out)
        while got < n:
            k = self._sock.recv_into(view[got:], min(n - got, 1 << 20))
            if k == 0:
                raise ConnectionClosed(reason="the connection ended")
            got += k
        return bytes(out)

    def recv(self) -> tuple[int, bytes]:
        """The next message: (TEXT or BINARY, its payload). Raises ConnectionClosed when the peer closes."""
        opcode, parts = None, []
        while True:
            b0, b1 = self._read(2)
            fin, op = b0 & 0x80, b0 & 0x0F
            n = b1 & 0x7F
            if n == 126:
                (n,) = struct.unpack(">H", self._read(2))
            elif n == 127:
                (n,) = struct.unpack(">Q", self._read(8))
            key = self._read(4) if b1 & 0x80 else None
            data = self._read(n) if n else b""
            if key:
                data = mask(data, key)
            if op == CLOSE:
                code = struct.unpack(">H", data[:2])[0] if len(data) >= 2 else None
                try:
                    self._send(CLOSE, data[:2])
                except OSError:
                    pass
                self._shutdown()
                raise ConnectionClosed(code, data[2:].decode("utf-8", "replace"))
            if op == PING:
                self._send(PONG, data)
                continue
            if op == PONG:
                continue
            if op in (TEXT, BINARY):
                opcode, parts = op, [data]
            elif op == CONTINUATION and opcode is not None:
                parts.append(data)
            else:
                raise ConnectionError(f"Unexpected WebSocket frame (opcode {op}).")
            if fin:
                return opcode, parts[0] if len(parts) == 1 else b"".join(parts)

    # ------------------------------------------------------------------ writing

    def _send(self, opcode: int, payload: bytes) -> None:
        with self._send_lock:
            if self._closed:
                raise ConnectionClosed(reason="the connection is closed")
            self._sock.sendall(frame(opcode, payload))

    def send_text(self, text: str) -> None:
        self._send(TEXT, text.encode("utf-8"))

    def close(self, code: int = 1000) -> None:
        """A close frame, then the socket shut (the reading thread ends with ConnectionClosed)."""
        try:
            self._send(CLOSE, struct.pack(">H", code))
        except OSError:
            pass
        self._shutdown()

    def _shutdown(self) -> None:
        with self._send_lock:
            if self._closed:
                return
            self._closed = True
        try:
            self._sock.shutdown(socket.SHUT_RDWR)
        except OSError:
            pass
        self._sock.close()
