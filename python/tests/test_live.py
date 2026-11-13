"""External geometry and live scripts against a real engine (docs/plan-external-geometry.md): bodies a program computes
(create_body, set_body)."""

from __future__ import annotations

import threading
import unittest
import uuid

from lapinbim import Lapin, LapinError, global_id
from lapinbim._ids import compress

from .engine import require


def box(a: float, b: float, c: float, x: float = 0, y: float = 0, z: float = 0) -> dict:
    """A closed box as a mesh: 8 points, 12 triangles wound outwards."""
    p = [x, y, z, x + a, y, z, x + a, y + b, z, x, y + b, z, x, y, z + c, x + a, y, z + c, x + a, y + b, z + c, x, y + b, z + c]
    t = [0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7]
    return {"positions": p, "indices": t}


class LiveTests(unittest.TestCase):
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

    def test_a_body_is_created_and_set_keeping_its_global_id(self):
        gid = "0Lapin$Body_Test000001"
        r = self.lapin.create_body("IfcMember", box(1, 1, 1), storey=self.storey, global_id=gid, owner="script:test", attrs={"span": 6})
        self.assertEqual(r.id, gid)
        self.assertTrue(r.closed)
        e = self.lapin.model[gid]
        self.assertEqual(e.type, "IfcMember")
        self.assertEqual(e.owner, "script:test")
        self.assertAlmostEqual(e.quantity("NetVolume"), 1.0, places=6)
        self.lapin.set_body(gid, box(2, 1, 1))
        self.assertAlmostEqual(self.lapin.model[gid].quantity("NetVolume"), 2.0, places=6)
        self.assertEqual(self.lapin.state().undo_label, "Set body")
        with self.assertRaises(LapinError):
            self.lapin.create_body("IfcMember", box(1, 1, 1), global_id=gid)
        self.lapin.undo()
        self.assertAlmostEqual(self.lapin.model[gid].quantity("NetVolume"), 1.0, places=6)

    def test_a_body_in_millimetres_as_rows_of_three(self):
        b = box(1000, 1000, 2000)
        rows = {"vertices": [b["positions"][i:i + 3] for i in range(0, 24, 3)], "faces": [b["indices"][i:i + 3] for i in range(0, 36, 3)]}
        r = self.lapin.create_body("IfcColumn", rows, units="mm", global_id=global_id("column:c1:storey:ground", "tests"))
        self.assertAlmostEqual(self.lapin.model[r.id].quantity("NetVolume"), 2.0, places=6)
        self.assertEqual(r.id, global_id("column:c1:storey:ground", "tests"))


    def test_measures_are_told_when_they_change_and_the_selection_with_what_it_is(self):
        wall = self.lapin.architecture.add_wall(storey_id=self.storey, x1=0, y1=0, x2=5, y2=0, height=3).id
        top = self.lapin.measure(f"wall.{wall}.top")
        self.assertAlmostEqual(top - self.lapin.model[self.storey].elevation, 3.0)
        self.assertAlmostEqual(self.lapin.measure("half", id=wall, expr="attr.height / 2"), 1.5)
        got: list = []
        told = threading.Event()

        def on_measure(d):
            got.append(d)
            if d["name"] == "half":
                told.set()

        off = self.lapin.on("measure", on_measure)
        with Lapin(self.engine.port) as other:
            other.element.update(id=wall, attrs={"height": 4})
            self.assertTrue(told.wait(5))
            self.assertAlmostEqual(self.lapin.measure_value("half"), 2.0)
            # The window's selection (here told by another connection, as the window tells it), with what it is.
            other.call("script.selected", {"ids": [wall]})
        off()
        self.assertIn(f"wall.{wall}.top", [d["name"] for d in got])
        items = self.lapin.selected()
        self.assertEqual(items[0].ifc_class, "IfcWall")
        self.assertAlmostEqual(items[0]["bbox"][1][2], top + 1.0)
        self.assertTrue(self.lapin.remove_measure("half"))


class GlobalIdTests(unittest.TestCase):
    def test_ids_are_ifcs_and_stable(self):
        self.assertEqual(compress(uuid.UUID(int=0)), "0" * 22)
        self.assertEqual(compress(uuid.UUID(int=(1 << 128) - 1)), "3" + "$" * 21)
        a = global_id("wall:north:storey:ground")
        self.assertEqual(len(a), 22)
        self.assertEqual(a, global_id("wall:north:storey:ground"))
        self.assertNotEqual(a, global_id("wall:north:storey:ground", "another program"))


if __name__ == "__main__":
    unittest.main()
