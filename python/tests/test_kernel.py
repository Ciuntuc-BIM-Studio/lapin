"""The Script panel's kernel (lapinbim.kernel, docs/python.md): started as the engine starts it (``python -I -B -u -c …``),
spoken to with JSON lines on its stdin, answering with JSON lines on its stdout. Without an engine (no LAPIN_PORT) it
runs code with no step; with one, each run is one step held open on the engine: kept as one undo step named after the
script when it ends well, rolled back when it fails or is stopped."""

from __future__ import annotations

import json
import os
import queue
import subprocess
import sys
import threading
import time
import unittest
from pathlib import Path

from .engine import require

PACKAGE = Path(__file__).resolve().parents[1]
BOOTSTRAP = "import sys; sys.path.insert(0, sys.argv[1]); from lapinbim.kernel import main; main()"


class Kernel:
    """A kernel process, as ScriptHost starts it."""

    def __init__(self, port: int | None = None):
        env = {k: v for k, v in os.environ.items() if k != "LAPIN_PORT"}
        if port:
            env["LAPIN_PORT"] = str(port)
        env["LAPIN_SCRIPT_KERNEL"] = "1"
        self.proc = subprocess.Popen(
            [sys.executable, "-I", "-B", "-u", "-c", BOOTSTRAP, str(PACKAGE)],
            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, env=env, text=True, encoding="utf-8",
        )
        self.lines: queue.Queue = queue.Queue()
        threading.Thread(target=self._pump, daemon=True).start()
        self.ready = self.next("ready")
        self.n = 0

    def _pump(self):
        for line in self.proc.stdout:
            self.lines.put(json.loads(line))
        self.lines.put(None)

    def send(self, msg: dict):
        self.proc.stdin.write(json.dumps(msg) + "\n")
        self.proc.stdin.flush()

    def next(self, kind: str | None = None, timeout: float = 20):
        deadline = time.time() + timeout
        while True:
            msg = self.lines.get(timeout=max(0.01, deadline - time.time()))
            if msg is None:
                raise AssertionError("the kernel ended: " + self.proc.stderr.read())
            if kind is None or msg.get("type") == kind:
                return msg

    def run(self, code: str, name: str = "test.py", console: bool = False, timeout: float = 30):
        """Runs code: (what it printed, its done message)."""
        self.n += 1
        self.send({"op": "run", "id": self.n, "code": code, "name": name, "console": console})
        out = []
        deadline = time.time() + timeout
        while True:
            msg = self.next(timeout=max(0.01, deadline - time.time()))
            if msg.get("type") in ("out", "err"):
                out.append(msg["text"])
            if msg.get("type") == "done":
                self.assert_id(msg)
                return "".join(out), msg

    def assert_id(self, msg):
        assert msg["id"] == self.n, msg

    def close(self):
        if self.proc.poll() is None:
            self.proc.stdin.close()
            try:
                self.proc.wait(5)
            except subprocess.TimeoutExpired:
                self.proc.kill()
        self.proc.stdout.close()
        self.proc.stderr.close()


class KernelTests(unittest.TestCase):
    """The kernel alone: no engine, no step."""

    @classmethod
    def setUpClass(cls):
        cls.k = Kernel()

    @classmethod
    def tearDownClass(cls):
        cls.k.close()

    def test_it_says_it_is_ready_with_its_versions(self):
        self.assertEqual(self.k.ready["python"], "%d.%d.%d" % sys.version_info[:3])
        self.assertTrue(self.k.ready["lapinbim"])

    def test_print_goes_out_as_lines_and_the_run_ends_well(self):
        out, done = self.k.run("print('hello')\nprint('ă', 2)\nimport sys\nprint('err', file=sys.stderr)")
        self.assertEqual(out, "hello\nă 2\nerr\n")
        self.assertTrue(done["ok"])
        self.assertFalse(done["interrupted"])

    def test_the_console_keeps_its_names_and_shows_an_expressions_value(self):
        self.k.run("kept = 40", console=True)
        out, done = self.k.run("kept + 2", console=True)
        self.assertEqual(out, "42\n")
        out, _ = self.k.run("x = 1\nx + 1", console=True)
        self.assertEqual(out, "2\n")
        out, _ = self.k.run("None", console=True)
        self.assertEqual(out, "")
        # A script's last expression is not echoed.
        out, _ = self.k.run("kept + 2")
        self.assertEqual(out, "")

    def test_lapin_is_the_package(self):
        out, _ = self.k.run("print(lapin.__name__, lapin.__version__ == __import__('lapinbim').__version__)")
        self.assertEqual(out, "lapinbim True\n")

    def test_an_error_says_its_line_and_shows_only_the_scripts_frames(self):
        out, done = self.k.run("a = 1\nb = a / 0\n", name="broken.py")
        self.assertFalse(done["ok"])
        self.assertEqual(done["line"], 2)
        self.assertIn("ZeroDivisionError", done["error"])
        self.assertIn('File "broken.py", line 2', done["traceback"])
        self.assertIn("b = a / 0", done["traceback"])
        self.assertNotIn("kernel.py", done["traceback"])

    def test_a_syntax_error_says_its_line(self):
        _, done = self.k.run("x = 1\nif x\n    pass\n")
        self.assertFalse(done["ok"])
        self.assertEqual(done["line"], 2)
        self.assertIn("SyntaxError", done["error"])

    def test_an_interrupt_stops_a_script_that_waits(self):
        self.k.n += 1
        self.k.send({"op": "run", "id": self.k.n, "code": "import time\nprint('start')\ntime.sleep(30)\nprint('never')", "name": "slow.py"})
        self.assertEqual(self.k.next("out")["text"], "start")
        t0 = time.time()
        self.k.send({"op": "interrupt"})
        done = self.k.next("done", timeout=10)
        self.assertLess(time.time() - t0, 5)
        self.assertFalse(done["ok"])
        self.assertTrue(done["interrupted"])
        # The kernel goes on.
        out, done = self.k.run("print('after')")
        self.assertEqual(out, "after\n")

    def test_system_exit_ends_the_script_not_the_kernel(self):
        _, done = self.k.run("raise SystemExit")
        self.assertTrue(done["ok"])
        _, done = self.k.run("raise SystemExit(3)")
        self.assertFalse(done["ok"])
        out, _ = self.k.run("print('still here')")
        self.assertEqual(out, "still here\n")

    def test_input_is_refused_rather_than_hanging(self):
        _, done = self.k.run("input('name? ')")
        self.assertFalse(done["ok"])
        self.assertIn("input", done["error"])


class KernelEngineTests(unittest.TestCase):
    """The kernel against a real engine: one run, one undo step."""

    @classmethod
    def setUpClass(cls):
        cls.engine = require()
        from lapinbim import Lapin

        cls.lapin = Lapin(cls.engine.port)
        cls.k = Kernel(cls.engine.port)

    @classmethod
    def tearDownClass(cls):
        cls.k.close()
        cls.lapin.close()

    def setUp(self):
        self.lapin.call("project.new")
        self.storey = self.lapin.model.storeys[0].id

    def walls(self):
        # Read afresh: the kernel's diffs reach this connection on their own pace.
        return sum(1 for n in self.lapin.snapshot().nodes if n.get("type") == "IfcWall")

    def undo_label(self):
        return self.lapin.snapshot().state.get("undoLabel")

    def test_a_run_is_one_undo_step_named_after_the_script(self):
        code = (
            f"for y in range(3):\n"
            f"    lapin.architecture.add_wall(storey_id={self.storey!r}, x1=0, y1=y, x2=5, y2=y)\n"
            f"print(len(lapin.model.walls))\n"
        )
        out, done = self.k.run(code, name="three walls")
        self.assertTrue(done["ok"], done)
        self.assertTrue(done["kept"])
        self.assertEqual(out, "3\n")
        self.assertEqual(self.walls(), 3)
        self.assertEqual(self.undo_label(), "three walls")
        self.lapin.undo()
        self.assertEqual(self.walls(), 0)

    def test_a_failing_run_keeps_nothing(self):
        code = f"lapin.architecture.add_wall(storey_id={self.storey!r}, x1=0, y1=0, x2=5, y2=0)\nraise ValueError('no')\n"
        _, done = self.k.run(code, name="fails")
        self.assertFalse(done["ok"])
        self.assertFalse(done["kept"])
        self.assertEqual(self.walls(), 0)
        self.assertNotEqual(self.undo_label(), "fails")

    def test_while_a_run_is_open_another_connection_cannot_edit(self):
        self.k.n += 1
        code = f"lapin.architecture.add_wall(storey_id={self.storey!r}, x1=0, y1=0, x2=5, y2=0)\nprint('made')\nimport time\ntime.sleep(30)\n"
        self.k.send({"op": "run", "id": self.k.n, "code": code, "name": "holds"})
        self.assertEqual(self.k.next("out")["text"], "made")
        from lapinbim import LapinError

        with self.assertRaises(LapinError) as e:
            self.lapin.execute("architecture.addWall", {"storeyId": self.storey, "x1": 0, "y1": 3, "x2": 5, "y2": 3})
        self.assertIn("A script is editing the model", str(e.exception))
        self.k.send({"op": "interrupt"})
        done = self.k.next("done", timeout=10)
        self.assertTrue(done["interrupted"])
        self.assertFalse(done["kept"])
        self.assertEqual(self.walls(), 0)
        self.lapin.execute("architecture.addWall", {"storeyId": self.storey, "x1": 0, "y1": 3, "x2": 5, "y2": 3})
        self.assertEqual(self.walls(), 1)


if __name__ == "__main__":
    unittest.main()
