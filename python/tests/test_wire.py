"""The wire without an engine: LPF1 frames, the WebSocket client against a small server of the test's own, and names."""

from __future__ import annotations

import base64
import hashlib
import os
import socket
import struct
import threading
import unittest

from lapinbim._frame import Obj, decode, encode
from lapinbim._names import camel, ident, plural, snake
from lapinbim._catalogue import Catalogue, Entry, merge
from lapinbim._ws import BINARY, TEXT, ConnectionClosed, WebSocket, accept_key, mask


class FrameTests(unittest.TestCase):
    def test_a_frames_arrays_are_views_on_it_and_its_objects_read_as_attributes(self):
        floats = struct.pack("<3f", 1.5, -2.0, 3.25)
        ints = struct.pack("<2i", 7, -9)
        data = encode(
            {"id": 3, "result": {"nodeId": "e1", "positions": {"$buf": 0, "n": 3, "t": "f32"}, "indices": {"$buf": 12, "n": 2, "t": "i32"}, "none": {"$buf": 20, "n": 0, "t": "f32"}}},
            [floats, ints],
        )
        msg = decode(data)
        self.assertIsInstance(msg, Obj)
        r = msg.result
        self.assertEqual(r.node_id, "e1")  # snake_case reads the engine's camelCase
        self.assertEqual(r.nodeId, "e1")
        self.assertIsInstance(r.positions, memoryview)
        self.assertEqual(r.positions.format, "f")
        self.assertEqual(r.positions.tolist(), [1.5, -2.0, 3.25])
        self.assertEqual(list(r.indices), [7, -9])
        self.assertEqual(len(r.none), 0)
        with self.assertRaises(AttributeError):
            r.missing

    def test_a_text_message_is_json_and_an_unknown_buffer_type_is_refused(self):
        self.assertEqual(decode('{"event":"presence","data":{"participants":[]}}').data.participants, [])
        self.assertEqual(decode(b'{"id":1,"result":null}').id, 1)
        with self.assertRaises(ValueError):
            decode(encode({"x": {"$buf": 0, "n": 1, "t": "f64"}}, [b"\0" * 8]))


class NameTests(unittest.TestCase):
    def test_the_engines_names_in_python(self):
        self.assertEqual(snake("addWall"), "add_wall")
        self.assertEqual(snake("storeyId"), "storey_id")
        self.assertEqual(snake("cutOrder"), "cut_order")
        self.assertEqual(snake("exportIFC"), "export_ifc")
        self.assertEqual(snake("x1"), "x1")
        self.assertEqual(snake("planning-pro"), "planning_pro")
        self.assertEqual(camel("storey_id"), "storeyId")
        self.assertEqual(camel("x1"), "x1")
        self.assertEqual(camel("from_"), "from")
        self.assertEqual(ident("from"), "from_")
        self.assertEqual(ident("type"), "type")
        self.assertEqual(ident("3d"), "_3d")
        self.assertEqual(plural("wall"), "walls")
        self.assertEqual(plural("storey"), "storeys")
        self.assertEqual(plural("property"), "properties")
        self.assertEqual(plural("box"), "boxes")
        self.assertEqual(plural("productivity_norms"), "productivity_norms")
        self.assertEqual(plural("stages"), "stages")
        self.assertEqual(plural("furniture"), "furniture")
        self.assertEqual(plural("glass"), "glasses")

    def test_typed_arguments_go_under_the_engines_names_and_none_is_left_out(self):
        e = Entry({"name": "architecture.addWall", "label": "Wall", "args": [{"name": "storeyId", "type": "str"}, {"name": "x1", "type": "float"}, {"name": "from"}]}, "command")
        self.assertEqual(e.to_args(None, {"storey_id": "s1", "x1": 0, "from_": 2, "height": None, "join_style": "butt"}),
                         {"storeyId": "s1", "x1": 0, "from": 2, "joinStyle": "butt"})
        self.assertEqual(e.to_args({"raw": None}, {"storeyId": "s2"}), {"raw": None, "storeyId": "s2"})
        self.assertIn("Wall (command architecture.addWall)", e.doc())

    def test_the_catalogue_names_namespaces_and_plurals_and_a_query_named_as_a_command(self):
        info = {"product": {"id": "lapin"}, "engineVersion": "0.1.5",
                "commands": [{"name": "architecture.addWall", "label": "Wall"}, {"name": "cutOrder.reset", "label": "Reset"}, {"name": "project.location", "label": "Location"}],
                "nodeTypes": [{"type": "IfcWall", "label": "Wall", "module": "architecture", "category": "element", "fields": [{"key": "height", "label": "Height", "kind": "length"}]},
                              {"type": "IfcBuildingStorey", "label": "Storey", "module": "core", "fields": []}]}
        served = {"version": "0.1.5", "commands": [{"name": "architecture.addWall", "module": "architecture", "label": "Wall", "args": [{"name": "x1", "type": "float"}]}],
                  "queries": [{"name": "project.location", "module": "core", "args": []}]}
        c = Catalogue(merge(info, served))
        self.assertEqual(set(c.namespaces), {"architecture", "cut_order", "project"})
        self.assertIs(c.namespace("cutOrder"), c.namespaces["cut_order"])
        self.assertEqual(c.namespaces["architecture"]["add_wall"].args, {"x1": "float"})
        self.assertEqual(c.namespaces["project"]["location"].kind, "command")
        self.assertEqual(c.namespaces["project"]["location_query"].kind, "query")
        self.assertEqual(c.plurals, {"storeys": "IfcBuildingStorey", "walls": "IfcWall"})
        self.assertEqual(c.entry("architecture.addWall").label, "Wall")


class _Server:
    """A WebSocket server for one connection: answers the handshake, then runs ``script(conn)``."""

    def __init__(self, script):
        self.sock = socket.socket()
        self.sock.bind(("127.0.0.1", 0))
        self.sock.listen(1)
        self.port = self.sock.getsockname()[1]
        self.got: list = []
        self.error: BaseException | None = None
        self.thread = threading.Thread(target=self._run, args=(script,), daemon=True)
        self.thread.start()

    def _run(self, script):
        try:
            conn, _ = self.sock.accept()
            self.conn = conn
            data = b""
            while b"\r\n\r\n" not in data:
                data += conn.recv(4096)
            key = next(l.split(":", 1)[1].strip() for l in data.decode().split("\r\n") if l.lower().startswith("sec-websocket-key"))
            accept = base64.b64encode(hashlib.sha1(key.encode() + b"258EAFA5-E914-47DA-95CA-C5AB0DC85B11").digest()).decode()
            conn.sendall(f"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\n\r\n".encode())
            script(self, conn)
        except BaseException as e:  # reported by the test
            self.error = e
        finally:
            for s in (getattr(self, "conn", None), self.sock):
                if s:
                    s.close()

    @staticmethod
    def send(conn, opcode, payload, fin=True):
        n = len(payload)
        head = bytes([(0x80 if fin else 0) | opcode])
        head += bytes([n]) if n < 126 else bytes([126]) + struct.pack(">H", n) if n < 65536 else bytes([127]) + struct.pack(">Q", n)
        conn.sendall(head + payload)

    @staticmethod
    def read(conn):
        def exact(n):
            b = b""
            while len(b) < n:
                b += conn.recv(n - len(b))
            return b
        b0, b1 = exact(2)
        n = b1 & 0x7F
        if n == 126:
            (n,) = struct.unpack(">H", exact(2))
        elif n == 127:
            (n,) = struct.unpack(">Q", exact(8))
        assert b1 & 0x80, "a client's frame is masked"
        key = exact(4)
        return b0 & 0x0F, mask(exact(n), key)


class WebSocketTests(unittest.TestCase):
    def test_handshake_ping_fragments_a_big_binary_message_masked_sends_and_close(self):
        big = os.urandom(200_000)

        def script(srv, conn):
            srv.send(conn, 0x9, b"hi")                          # a ping, answered with a pong
            srv.got.append(srv.read(conn))
            srv.send(conn, TEXT, b"hel", fin=False)             # a text in two fragments
            srv.send(conn, 0x0, "lo é".encode())
            srv.send(conn, BINARY, big)                         # 64-bit length
            srv.got.append(srv.read(conn))                      # the client's text, masked
            srv.send(conn, 0x8, struct.pack(">H", 1000) + b"bye")
            srv.got.append(srv.read(conn))                      # the client's close answer

        srv = _Server(script)
        ws = WebSocket("127.0.0.1", srv.port)
        self.assertEqual(ws.recv(), (TEXT, "hello é".encode()))
        self.assertEqual(ws.recv(), (BINARY, big))
        ws.send_text('{"id":1}' * 3000)
        with self.assertRaises(ConnectionClosed) as c:
            ws.recv()
        self.assertEqual(c.exception.code, 1000)
        self.assertEqual(c.exception.reason, "bye")
        srv.thread.join(5)
        self.assertIsNone(srv.error)
        self.assertEqual(srv.got[0], (0xA, b"hi"))
        self.assertEqual(srv.got[1], (TEXT, b'{"id":1}' * 3000))
        self.assertEqual(srv.got[2][0], 0x8)

    def test_a_server_that_answers_wrong_is_refused(self):
        sock = socket.socket()
        sock.bind(("127.0.0.1", 0))
        sock.listen(1)

        def bad():
            conn, _ = sock.accept()
            conn.recv(4096)
            conn.sendall(b"HTTP/1.1 101 Switching Protocols\r\nSec-WebSocket-Accept: nope\r\n\r\n")
            conn.close()

        t = threading.Thread(target=bad, daemon=True)
        t.start()
        with self.assertRaises(ConnectionError):
            WebSocket("127.0.0.1", sock.getsockname()[1])
        t.join(5)
        sock.close()
        self.assertEqual(accept_key("dGhlIHNhbXBsZSBub25jZQ=="), "s3pPLMBiTxaQ9kYGzzhZRbK+xOo=")  # RFC 6455's example


if __name__ == "__main__":
    unittest.main()
