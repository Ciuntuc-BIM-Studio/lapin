"""The package against a real engine (tests/engine.py): connecting, typed commands, a batch as one undo step, undo and
redo, binary frames, a refused command, the model, quantities, diff events, saving, and the stubs."""

from __future__ import annotations

import ast
import contextlib
import io
import os
import tempfile
import threading
import unittest
from pathlib import Path
from unittest import mock

import lapinbim
from lapinbim import Lapin, LapinError
from lapinbim._catalogue import Catalogue
from lapinbim._stubs import generate

from .engine import require


class EngineTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.engine = require()
        cls.lapin = Lapin(cls.engine.port)

    @classmethod
    def tearDownClass(cls):
        cls.lapin.close()

    def setUp(self):
        self.lapin.call("project.new")
        self.storey = self.lapin.model.storeys[0].id

    def walls(self):
        return self.lapin.model.walls

    def test_connects_by_lapin_port_and_by_the_engines_host_file(self):
        with mock.patch.dict(os.environ, {"LAPIN_PORT": str(self.engine.port)}):
            with Lapin() as l:
                self.assertEqual(l.info().product.id, "lapin")
        env = {k: v for k, v in os.environ.items() if k != "LAPIN_PORT"}
        env["LAPIN_USER_DIR"] = str(self.engine.user_dir)
        with mock.patch.dict(os.environ, env, clear=True):
            with Lapin() as l:
                self.assertEqual(l.port, self.engine.port)
        with self.assertRaises(LapinError):
            Lapin(1)  # nothing listens there

    def test_a_typed_command_adds_a_wall_in_one_undo_step_and_reads_it_back(self):
        r = self.lapin.architecture.add_wall(storey_id=self.storey, x1=0, y1=0, x2=5, y2=0, height=2.8)
        self.assertTrue(r.id)
        walls = self.walls()
        self.assertEqual([w.id for w in walls], [r.id])
        w = self.lapin.model[r.id]
        self.assertEqual(w.type, "IfcWall")
        self.assertEqual(w.storey, self.storey)
        self.assertAlmostEqual(w.height, 2.8)
        self.assertEqual(self.lapin.state().undo_label, "Wall")
        self.lapin.undo()
        self.assertEqual(self.walls(), [])
        self.lapin.redo()
        self.assertEqual(len(self.walls()), 1)
        # The raw call, the engine's own names, and a command by its full name do the same.
        self.lapin.execute("architecture.addWall", {"storeyId": self.storey, "x1": 0, "y1": 2, "x2": 5, "y2": 2})
        self.lapin.command("architecture.addWall")(storeyId=self.storey, x1=0, y1=4, x2=5, y2=4)
        self.assertEqual(len(self.walls()), 3)
        self.assertIn("Wall (command architecture.addWall)", self.lapin.architecture.add_wall.__doc__)

    def test_a_batch_is_one_undo_step_named_by_its_label(self):
        with self.lapin.batch("Ring of walls") as b:
            ds = [self.lapin.architecture.add_wall(storey_id=self.storey, x1=x1, y1=y1, x2=x2, y2=y2)
                  for x1, y1, x2, y2 in [(0, 0, 10, 0), (10, 0, 10, 8), (10, 8, 0, 8), (0, 8, 0, 0)]]
            with self.assertRaises(LapinError):
                ds[0].id  # known when the batch ends
            self.assertEqual(self.walls(), [])  # nothing sent yet
            with self.lapin.batch("inner"):  # joins the outer one
                self.lapin.architecture.add_wall(storey_id=self.storey, x1=2, y1=2, x2=4, y2=2)
        self.assertEqual(len(b.results), 5)
        self.assertEqual({w.id for w in self.walls()}, {d.id for d in ds} | {b.results[4]["id"]})
        self.assertEqual(self.lapin.state().undo_label, "Ring of walls")
        self.lapin.undo()
        self.assertEqual(self.walls(), [])
        # An exception in the block sends nothing; a step refused undoes the whole batch.
        with self.assertRaises(ZeroDivisionError):
            with self.lapin.batch("never"):
                self.lapin.architecture.add_wall(storey_id=self.storey, x1=0, y1=0, x2=1, y2=0)
                1 / 0
        with self.assertRaises(LapinError):
            with self.lapin.batch("half"):
                self.lapin.architecture.add_wall(storey_id=self.storey, x1=0, y1=0, x2=1, y2=0)
                self.lapin.architecture.update_wall(id="nope", x1=3)
        self.assertEqual(self.walls(), [])
        with self.assertRaises(LapinError):
            with self.lapin.batch():
                self.lapin.undo()

    def test_a_refused_command_raises_with_the_engines_message_and_a_wrong_name_says_so(self):
        with self.assertRaises(LapinError) as e:
            self.lapin.architecture.update_wall(id="nope", x1=1)
        self.assertEqual(e.exception.name, "architecture.updateWall")
        self.assertTrue(str(e.exception))
        with self.assertRaises(AttributeError) as a:
            self.lapin.architecture.add_wal
        self.assertIn("add_wall", str(a.exception))
        with self.assertRaises(AttributeError):
            self.lapin.nosuchmodule

    def test_binary_frames_queries_quantities_and_elements(self):
        w = self.lapin.architecture.add_wall(storey_id=self.storey, x1=0, y1=0, x2=4, y2=0, height=3)
        render = next(r for r in self.lapin.snapshot().render if r.node_id == w.id)
        # The meshes come as arrays in the frame, read without copying.
        part = render.parts[0] if render.get("parts") else render
        self.assertIsInstance(part.positions, memoryview)
        self.assertEqual(part.positions.format, "f")
        self.assertEqual(len(part.positions) % 3, 0)
        self.assertGreater(len(part.positions), 0)
        wall = self.lapin.model[w.id]
        q = wall.quantities()
        self.assertTrue(q)
        self.assertAlmostEqual(wall.quantity("Length"), 4)
        self.assertIsNone(wall.quantity("Nothing"))
        self.assertTrue(self.lapin.query("params.standardSets"))
        wall.update(height=3.5)
        self.assertAlmostEqual(self.lapin.model[w.id].height, 3.5)
        wall.rename("North wall")
        self.assertEqual(self.lapin.model[w.id].name, "North wall")
        self.assertIn(w.id, self.lapin.model)
        self.assertIn("walls", self.lapin.model.classes)
        self.lapin.element.delete(ids=[wall])  # an element goes as its id
        self.assertEqual(self.walls(), [])

    def test_diff_events_reach_a_handler(self):
        seen = threading.Event()
        got = []

        def on_diff(d):
            got.append(d)
            seen.set()

        off = self.lapin.on("diff", on_diff)
        try:
            self.lapin.architecture.add_wall(storey_id=self.storey, x1=0, y1=0, x2=1, y2=0)
            self.assertTrue(seen.wait(10))
            self.assertTrue(any(n.type == "IfcWall" for d in got for n in d.nodes))
        finally:
            off()

    def test_save_writes_the_project(self):
        self.lapin.architecture.add_wall(storey_id=self.storey, x1=0, y1=0, x2=1, y2=0)
        with tempfile.TemporaryDirectory() as d:
            path = Path(d) / "script.lapin"
            self.lapin.save(str(path))
            self.assertTrue(path.exists() and path.stat().st_size > 0)

    def test_the_module_functions_use_one_connection(self):
        with mock.patch.dict(os.environ, {"LAPIN_PORT": str(self.engine.port)}):
            try:
                lapinbim.execute("architecture.addWall", {"storeyId": self.storey, "x1": 0, "y1": 0, "x2": 2, "y2": 0})
                self.assertEqual(len(lapinbim.model.walls), 1)
                self.assertIs(lapinbim.client(), lapinbim.client())
                lapinbim.architecture.add_wall(storey_id=self.storey, x1=0, y1=1, x2=2, y2=1)
                self.assertEqual(len(self.walls()), 2)
            finally:
                lapinbim.close()

    def test_the_catalogue_has_arguments_and_the_stubs_complete_them(self):
        c = self.lapin.catalogue()
        add = c.namespaces["architecture"]["add_wall"]
        self.assertEqual(add.args["x1"], "float")
        self.assertEqual(add.py["storey_id"], "storeyId")
        self.assertIn("IfcWall", c.classes)
        text = generate(c)
        tree = ast.parse(text)  # valid Python
        self.assertIn("def add_wall(self, _raw: _dict[_str, Any] | None = None, /, *, storey_id: _str | None = None", text)
        self.assertIn("def walls(self) -> list[Element]", text)
        declared = {n.name for n in tree.body if isinstance(n, (ast.FunctionDef, ast.ClassDef))} | {
            t.id for n in tree.body if isinstance(n, ast.AnnAssign) for t in [n.target] if isinstance(t, ast.Name)} | {
            a.asname or a.name for n in tree.body if isinstance(n, ast.ImportFrom) for a in n.names}
        for name in lapinbim.__all__:
            self.assertIn(name, declared, f"{name} is public in lapinbim but not in its stubs")
        self.assertIn("architecture", declared)
        # The command line writes them, and the catalogue, from the open engine.
        from lapinbim.__main__ import main
        with tempfile.TemporaryDirectory() as d, contextlib.redirect_stdout(io.StringIO()) as said:
            self.assertEqual(main(["catalogue", "--port", str(self.engine.port), "--out", f"{d}/c.json"]), 0)
            self.assertEqual(main(["stubs", "--catalogue", f"{d}/c.json", "--out", d]), 0)
            self.assertIn("__init__.pyi", said.getvalue())
            self.assertIn("def add_wall(", Path(d, "__init__.pyi").read_text())
            self.assertEqual(Catalogue(__import__("json").loads(Path(d, "c.json").read_text())).namespaces.keys(), c.namespaces.keys())


if __name__ == "__main__":
    unittest.main()
