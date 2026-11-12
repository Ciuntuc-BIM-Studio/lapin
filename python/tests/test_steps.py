"""Stage 3 of docs/plan-python-scripting.md (decision 489) against a real engine (tests/engine.py): ``lapin.step`` (one
undo step held open on the engine, committed when its block ends well, rolled back when it raises, a step inside a step
joining it), element proxies (an assignment is ``element.update``, ``refresh``, ``model.storey``, ``model.find``),
``lapin.selection()``, the Script panel's own Python whose every connection joins its run's step, and a script the
recorder wrote (tests/fixtures/recorded_walls.py, the one ui/src/modules/scripting/recorder.test.ts makes) run on a new
project."""

from __future__ import annotations

import queue
import threading
import time
import unittest
from pathlib import Path

import lapinbim
from lapinbim import Lapin, LapinError

from .engine import require

RECORDED = (Path(__file__).parent / "fixtures" / "recorded_walls.py").read_text(encoding="utf-8")


class Base(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.engine = require()
        cls.lapin = Lapin(cls.engine.port)

    @classmethod
    def tearDownClass(cls):
        cls.lapin.close()

    def setUp(self):
        self.lapin.call("project.new")
        self.storey = self.lapin.model.storey().id

    def walls(self):
        return [n for n in self.lapin.snapshot().nodes if n.get("type") == "IfcWall"]

    def undo_label(self):
        return self.lapin.snapshot().state.get("undoLabel")

    def wall(self, y, on=None):
        return (on or self.lapin).architecture.add_wall(storey_id=self.storey, x1=0, y1=y, x2=5, y2=y)


class StepTests(Base):
    def test_a_step_is_one_undo_step_and_each_command_answers_at_once(self):
        with self.lapin.step("Room") as s:
            a = self.wall(0)
            # Unlike a batch, the id is there at once: the next command may use it.
            self.lapin.element.rename(id=a.id, name="South")
            self.wall(1)
        self.assertTrue(s.kept)
        self.assertEqual(len(self.walls()), 2)
        self.assertEqual(self.undo_label(), "Room")
        self.assertEqual(self.lapin.model[a.id].name, "South")
        self.lapin.undo()
        self.assertEqual(len(self.walls()), 0)

    def test_an_exception_rolls_the_step_back_and_goes_on(self):
        self.wall(9)
        with self.assertRaises(ValueError):
            with self.lapin.step("Fails"):
                self.wall(0)
                raise ValueError("no")
        self.assertEqual(len(self.walls()), 1)
        self.assertNotEqual(self.undo_label(), "Fails")

    def test_a_step_inside_a_step_joins_it_and_the_outer_decides(self):
        with self.lapin.step("Outer"):
            self.wall(0)
            with self.lapin.step("Inner") as inner:
                self.wall(1)
            self.assertTrue(inner.joined)
            # An inner step whose exception is caught keeps what it did in the outer one.
            try:
                with self.lapin.step("Inner, failing"):
                    self.wall(2)
                    raise KeyError("caught")
            except KeyError:
                pass
            with self.lapin.batch("A batch inside"):
                self.wall(3)
        self.assertEqual(len(self.walls()), 4)
        self.assertEqual(self.undo_label(), "Outer")
        self.lapin.undo()
        self.assertEqual(len(self.walls()), 0)

    def test_a_step_inside_a_batch_is_refused_and_undo_inside_a_step_too(self):
        with self.assertRaises(LapinError):
            with self.lapin.batch():
                with self.lapin.step("x"):
                    pass
        with self.assertRaises(LapinError):
            with self.lapin.step("x"):
                self.wall(0)
                self.lapin.undo()
        self.assertEqual(len(self.walls()), 0)

    def test_while_a_step_is_open_another_connection_is_refused(self):
        with Lapin(self.engine.port) as other:
            with self.lapin.step("Held"):
                self.wall(0)
                with self.assertRaises(LapinError) as e:
                    self.wall(1, on=other)
                self.assertIn("A script is editing the model", str(e.exception))
            self.wall(1, on=other)
        self.assertEqual(len(self.walls()), 2)


class ProxyTests(Base):
    def test_an_assignment_changes_the_element_in_one_undo_step(self):
        w = self.lapin.model[self.wall(0).id]
        w.height = 3.25
        self.assertEqual(w.height, 3.25)
        self.assertEqual(self.lapin.model[w.id].height, 3.25)
        self.assertEqual(self.undo_label(), "Change")
        self.lapin.undo()
        self.assertNotEqual(w.refresh().height, 3.25)
        with self.assertRaises(LapinError):
            w.no_such_field = 1
        with self.assertRaises(AttributeError):
            w._private = 1
        self.assertIn("height", w.fields)

    def test_a_storey_by_its_name_else_its_elevation_and_a_type_by_its_name(self):
        ground = self.lapin.model.storey()
        self.assertEqual(self.lapin.model.storey(ground.name).id, ground.id)
        self.assertEqual(self.lapin.model.storey("A name no project has", float(ground.attrs.get("elevation") or 0)).id, ground.id)
        with self.assertRaises(KeyError):
            self.lapin.model.storey("A name no project has", 1234.5)
        t = self.lapin.architecture.create_wall_type(name="Script type", layers=[{"material": "Brick", "thickness": 0.25, "core": True}])
        self.assertEqual(self.lapin.model.find("IfcWallType", "Script type").id, t.id)
        with self.assertRaises(KeyError):
            self.lapin.model.find("IfcWallType", "None such")


class SelectionTests(Base):
    def test_the_selection_the_window_told_is_read_as_elements(self):
        a, b = self.wall(0).id, self.wall(1).id
        # What the window does as its selection changes (a call wanting no answer there; here, with one).
        self.lapin.call("script.selected", {"ids": [a, "grid#A", b]})
        self.assertEqual([e.id for e in self.lapin.selection()], [a, b])
        self.assertEqual([e.id for e in lapinbim.Lapin(self.engine.port).selection()], [a, b])
        self.lapin.call("script.selected", {"ids": []})
        self.assertEqual(self.lapin.selection(), [])


class PanelTests(Base):
    """Code run by the engine's own kernel (script.run), as the Script panel runs it."""

    def run_script(self, code: str, name: str = "test.py", timeout: float = 60):
        events: queue.Queue = queue.Queue()
        off = self.lapin.on("script.output", events.put)
        try:
            run = self.lapin.call("script.run", {"code": code, "name": name})["run"]
            out, deadline = [], time.time() + timeout
            while True:
                e = events.get(timeout=max(0.01, deadline - time.time()))
                if e.get("id") != run:
                    continue
                if e.get("type") in ("out", "err"):
                    out.append(e["text"])
                if e.get("type") == "done":
                    return "".join(out), e
        finally:
            off()

    def setUp(self):
        super().setUp()
        status = self.lapin.call("script.status", {})
        if not status.get("ok"):
            self.skipTest(f"the engine runs no Python: {status.get('message')}")

    def test_a_connection_the_script_opens_itself_joins_its_run(self):
        code = (
            "import os, lapinbim\n"
            f"with lapinbim.Lapin() as other:\n"
            f"    print(other.joined)\n"
            f"    other.architecture.add_wall(storey_id={self.storey!r}, x1=0, y1=0, x2=5, y2=0)\n"
            f"lapin.architecture.add_wall(storey_id={self.storey!r}, x1=0, y1=1, x2=5, y2=1)\n"
            # The kernel's token is not in what a process the script starts inherits.
            "print(os.environ.get('LAPIN_SCRIPT_TOKEN'))\n"
        )
        out, done = self.run_script(code, name="joined")
        self.assertTrue(done["ok"], done)
        self.assertTrue(done["kept"])
        self.assertEqual(out, "True\nNone\n")
        self.assertEqual(len(self.walls()), 2)
        self.assertEqual(self.undo_label(), "joined")
        self.lapin.undo()
        self.assertEqual(len(self.walls()), 0)

    def test_a_step_in_a_run_joins_it(self):
        code = f"with lapin.step('inner') as s:\n    lapin.architecture.add_wall(storey_id={self.storey!r}, x1=0, y1=0, x2=5, y2=0)\nprint(s.joined)\n"
        out, done = self.run_script(code, name="outer")
        self.assertTrue(done["ok"], done)
        self.assertEqual(out, "True\n")
        self.assertEqual(self.undo_label(), "outer")

    def test_a_recorded_script_runs_as_one_undo_step(self):
        _, done = self.run_script(RECORDED, name="Recorded 1")
        self.assertTrue(done["ok"], done)
        self.assertEqual(len(self.walls()), 2)
        self.assertEqual(self.undo_label(), "Recorded 1")


class RecordedTests(Base):
    def test_the_recorders_script_runs_on_a_new_project_from_a_terminal_too(self):
        lapinbim.connect(self.engine.port)
        try:
            for round_ in (1, 2):
                exec(compile(RECORDED, "recorded_walls.py", "exec"), {"__name__": "__main__"})
                walls = self.walls()
                self.assertEqual(len(walls), 2 * round_)
            names = sorted(w.get("name") for w in walls)
            self.assertEqual(names.count("South"), 2)
            heights = [w.get("props", {}).get("height") for w in walls]
            self.assertEqual(heights.count(3.1), 2)
        finally:
            lapinbim.close()


if __name__ == "__main__":
    unittest.main()
