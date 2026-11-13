"""How a program finds Lapin (docs/python.md): LAPIN_PORT, the engines' host files, the ports from 14912 probed, each
confirmed by what it answers. Fake engines here (a /health that says it is Lapin), so no built engine is needed;
test_engine.py asks the real one."""

from __future__ import annotations

import json
import os
import socket
import tempfile
import threading
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from unittest import mock

from lapinbim import _discovery as discovery
from lapinbim import Lapin, LapinError


def _server(body: dict | None, status: int = 200) -> ThreadingHTTPServer:
    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):  # noqa: N802
            data = json.dumps(body).encode()
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            self.wfile.write(data)

        def log_message(self, *a):
            pass

    s = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
    threading.Thread(target=s.serve_forever, daemon=True).start()
    return s


def _lapin(project: str | None = None, product: str = "lapin") -> ThreadingHTTPServer:
    return _server({"engine": "lapin", "ok": True, "product": product, "version": "0.1.0", "pid": os.getpid(), "port": 0,
                    "project": project, "modules": ["core"]})


def _closed_port() -> int:
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]


class Discovery(unittest.TestCase):
    def setUp(self):
        self.servers: list = []
        self.env = mock.patch.dict(os.environ, {k: v for k, v in os.environ.items() if k not in ("LAPIN_PORT", "LAPIN_USER_DIR")}, clear=True)
        self.env.start()

    def tearDown(self):
        self.env.stop()
        for s in self.servers:
            s.shutdown()
            s.server_close()

    def serve(self, s: ThreadingHTTPServer) -> int:
        self.servers.append(s)
        return s.server_address[1]

    def test_lapin_is_14912_and_the_next_hundred(self):
        self.assertEqual(discovery.FIRST_PORT, 14912)
        self.assertEqual(discovery.ports()[:2], [14912, 14913])
        self.assertEqual(discovery.ports()[99], 15011)
        self.assertIn(discovery.DEV_PORT, discovery.ports())

    def test_a_probe_finds_a_live_engine_and_skips_what_is_not_lapin(self):
        lapin = self.serve(_lapin("/work/House.lapin"))
        other = self.serve(_server({"name": "something else"}))
        refused = self.serve(_server({"engine": "lapin"}, status=403))
        # A program that takes the connection and says nothing.
        silent = socket.socket()
        silent.bind(("127.0.0.1", 0))
        silent.listen()
        self.addCleanup(silent.close)
        closed = _closed_port()
        with mock.patch.object(discovery, "ports", return_value=[closed, other, silent.getsockname()[1], refused, lapin]), \
             mock.patch.object(discovery, "user_dirs", return_value=[]):
            found = discovery.find_engines(timeout=0.3)
        self.assertEqual([e["port"] for e in found], [lapin])
        self.assertEqual(found[0]["project"], "/work/House.lapin")
        self.assertIsNone(discovery.identify(other, 0.3))
        self.assertIsNone(discovery.identify(closed, 0.3))

    def test_an_engine_from_before_engine_hello_is_still_found(self):
        old = self.serve(_server({"ok": True, "product": "lapin", "modules": ["core", "architecture"]}))
        e = discovery.identify(old)
        self.assertEqual(e["engine"], "lapin")
        self.assertEqual(e["product"], "lapin")

    def test_stale_host_files_are_ignored_and_each_engine_has_its_own(self):
        a = self.serve(_lapin("/a/One.lapin"))
        b = self.serve(_lapin("/b/Two.lapin"))
        with tempfile.TemporaryDirectory() as d:
            d = Path(d)
            (d / "hosts").mkdir()
            (d / "hosts" / f"host-{os.getpid()}.json").write_text(json.dumps({"port": a, "pid": os.getpid()}))
            (d / "hosts" / "host-1.json").write_text(json.dumps({"port": b, "pid": os.getpid()}))
            # Engines that are gone: a dead pid, in a file of its own and in host.json.
            dead = 2 ** 22 + 12345
            (d / "hosts" / f"host-{dead}.json").write_text(json.dumps({"port": 1, "pid": dead}))
            (d / "host.json").write_text(json.dumps({"port": 2, "pid": dead}))
            (d / "hosts" / "host-x.json").write_text("not json")
            self.assertEqual(sorted(h["port"] for h in discovery.host_files([d])), sorted([a, b]))
            found = discovery.find_engines(dirs=[d], timeout=0.3)
            self.assertEqual(sorted(e["port"] for e in found), sorted([a, b]))
            # The reader from before 14912: host.json alone, still read.
            (d / "host.json").write_text(json.dumps({"port": a, "pid": os.getpid(), "product": "lapin"}))
            self.assertIn(discovery.host_file([d / "nothing", d])["port"], (a, b))
            # LAPIN_USER_DIR names the folder: only its engines, nothing probed.
            with mock.patch.dict(os.environ, {"LAPIN_USER_DIR": str(d)}), mock.patch.object(discovery, "identify", wraps=discovery.identify) as probe:
                self.assertEqual(len(discovery.find_engines(timeout=0.3)), 2)
                self.assertEqual(sorted(c.args[0] for c in probe.call_args_list), sorted([a, b]))

    def test_choose_takes_the_only_one_or_the_one_named_else_lists_them(self):
        one = {"engine": "lapin", "port": 14912, "product": "lapin", "project": "/work/House.lapin"}
        two = {"engine": "lapin", "port": 14913, "product": "structure", "project": "/work/Bridge.lapin"}
        self.assertIs(discovery.choose([one]), one)
        with self.assertRaises(LookupError) as e:
            discovery.choose([one, two])
        self.assertIn("14912", str(e.exception))
        self.assertIn("14913", str(e.exception))
        self.assertIs(discovery.choose([one, two], project="Bridge.lapin"), two)
        self.assertIs(discovery.choose([one, two], project="house"), one)
        self.assertIs(discovery.choose([one, two], project="/work/House.lapin"), one)
        self.assertIs(discovery.choose([one, two], product="structure"), two)
        with self.assertRaises(LookupError) as e:
            discovery.choose([one, two], project="Barn")
        self.assertIn("Barn", str(e.exception))
        with self.assertRaises(LookupError):
            discovery.choose([])

    def test_lapin_port_comes_first_and_lapin_raises_when_several_are_open(self):
        with mock.patch.dict(os.environ, {"LAPIN_PORT": "15000"}):
            self.assertEqual(discovery.find_port(), 15000)
        a = self.serve(_lapin("/a/One.lapin"))
        b = self.serve(_lapin("/b/Two.lapin"))
        with mock.patch.object(discovery, "ports", return_value=[a, b]), mock.patch.object(discovery, "user_dirs", return_value=[]):
            with self.assertRaises(LapinError) as e:
                Lapin(connect_timeout=0.5)
            self.assertIn("2 Lapins are open", str(e.exception))
            self.assertEqual(discovery.find_port(project="Two"), b)
        with mock.patch.object(discovery, "ports", return_value=[_closed_port()]), mock.patch.object(discovery, "user_dirs", return_value=[]):
            with self.assertRaises(LapinError) as e:
                Lapin(connect_timeout=0.5)
            self.assertIn("No open Lapin", str(e.exception))
            self.assertIsNone(discovery.find_port())


if __name__ == "__main__":
    unittest.main()
