"""Stage 4 of docs/plan-python-scripting.md (decision 489): ``lapin.ui``. A question's fields as the window gets them and
its answer as Python values; ``lapin.ui.tool`` registering a script's tools; the kernel running a tool (the script as a
module, then the tool's function) and saying back the tools a Run registered; against a real engine, a question asked by
a script (from a terminal, and from the Script panel's own Python inside its run) answered by a window of the test's own,
the first answer coming back, a cancel giving None, and a tool's whole run one undo step."""

from __future__ import annotations

import queue
import threading
import time
import unittest

import lapinbim
from lapinbim import Lapin, _ui

from .engine import require
from .test_kernel import Kernel


class FieldTests(unittest.TestCase):
    def test_plain_values_and_fields_go_as_the_windows_fields_in_order(self):
        q = _ui.question("Perimeter walls", {
            "width": lapinbim.ui.number(8, unit="m", min=0.1),
            "count": 4,
            "height": 2.8,
            "name": "Room",
            "closed": True,
            "kind": ["brick", "concrete"],
            "wall_type": lapinbim.ui.type_of("IfcWallType"),
            "walls": lapinbim.ui.element("IfcWall", multiple=True),
            "storey": lapinbim.ui.storey(),
        }, text="Four walls round a rectangle.")
        self.assertEqual(q["title"], "Perimeter walls")
        self.assertEqual(q["text"], "Four walls round a rectangle.")
        f = {x["key"]: x for x in q["fields"]}
        self.assertEqual([x["key"] for x in q["fields"]], ["width", "count", "height", "name", "closed", "kind", "wall_type", "walls", "storey"])
        self.assertEqual(f["width"], {"key": "width", "kind": "number", "label": "Width", "value": 8.0, "unit": "m", "min": 0.1})
        self.assertEqual(f["count"], {"key": "count", "kind": "number", "label": "Count", "value": 4, "integer": True})
        self.assertEqual(f["name"]["kind"], "text")
        self.assertEqual(f["closed"], {"key": "closed", "kind": "bool", "label": "Closed", "value": True})
        self.assertEqual(f["kind"]["choices"], [{"value": "brick", "label": "brick"}, {"value": "concrete", "label": "concrete"}])
        self.assertEqual(f["kind"]["value"], "brick")
        self.assertEqual(f["wall_type"]["label"], "Wall type")
        self.assertEqual(f["wall_type"]["cls"], "IfcWallType")
        self.assertEqual(f["walls"]["classes"], ["IfcWall"])
        self.assertTrue(f["walls"]["multiple"])
        self.assertTrue(f["walls"]["selection"])
        self.assertEqual(f["storey"]["kind"], "storey")

    def test_what_a_question_cannot_hold_is_refused_before_it_is_asked(self):
        for bad in ({}, {"x": object()}, {"x": float("nan")}):
            with self.assertRaises((ValueError, TypeError)):
                _ui.question("x", bad)
        with self.assertRaises(ValueError):
            lapinbim.ui.choice([])
        with self.assertRaises(ValueError):
            lapinbim.ui.choice(["a", "b"], value="c")
        self.assertEqual(lapinbim.ui.choice({"b": "Brick", "c": "Concrete"}, "c").options["choices"][1], {"value": "c", "label": "Concrete"})

    def test_the_answer_comes_back_as_python_values(self):
        fields = {
            "width": lapinbim.ui.number(8.0, unit="m"), "count": 3, "name": "a", "closed": False,
            "wall": lapinbim.ui.element("IfcWall"), "walls": lapinbim.ui.element(multiple=True), "storey": lapinbim.ui.storey(),
        }
        model = {"w1": "<wall w1>", "s1": "<storey s1>"}
        v = _ui.values(fields, {"width": 10, "count": 5.0, "name": "b", "closed": True, "wall": "w1", "walls": ["w1", "gone"], "storey": "s1"}, lambda: model)
        self.assertEqual(v, {"width": 10.0, "count": 5, "name": "b", "closed": True, "wall": "<wall w1>", "walls": ["<wall w1>", "gone"], "storey": "<storey s1>"})
        self.assertIsInstance(v["width"], float)
        self.assertIsInstance(v["count"], int)
        # Nothing chosen: None, or an empty list for a multiple one; a field the window left out keeps its first value.
        v = _ui.values(fields, {"wall": None, "walls": []}, lambda: model)
        self.assertIsNone(v["wall"])
        self.assertEqual(v["walls"], [])
        self.assertEqual(v["width"], 8.0)


class ToolTests(unittest.TestCase):
    def tearDown(self):
        _ui._registered = None
        _ui._functions = {}

    def test_outside_the_panel_a_tool_is_the_function_given_back(self):
        def walls():
            return 1

        self.assertIs(lapinbim.ui.tool("Walls", run=walls), walls)
        self.assertIs(lapinbim.ui.tool("Walls")(walls), walls)
        self.assertIs(_ui._functions["Walls"], walls)

    def test_while_the_panel_runs_a_script_its_tools_are_registered(self):
        _ui._registered = []

        @lapinbim.ui.tool("Perimeter walls", icon="square", tooltip="Four walls")
        def perimeter():
            pass

        lapinbim.ui.tool("Whole script", icon="W")
        lapinbim.ui.tool("Lambda", run=lambda: None)
        self.assertEqual(_ui._registered, [
            {"name": "Perimeter walls", "icon": "square", "tooltip": "Four walls", "call": "perimeter", "main": False},
            {"name": "Whole script", "icon": "W", "tooltip": None, "call": None, "main": True},
            {"name": "Lambda", "icon": None, "tooltip": None, "call": None, "main": False},
        ])
        with self.assertRaises(ValueError):
            lapinbim.ui.tool("  ")


class PanelTests(unittest.TestCase):
    """Decision 516: panels, sizes, menus and palettes, as registered for the window."""

    def setUp(self):
        _ui._registered = []

    def tearDown(self):
        _ui._registered = None
        _ui._functions = {}
        _ui._palettes = {}
        _ui._within = []

    def test_a_panels_tools_and_menus_name_it_and_a_menus_tools_name_their_menu(self):
        with lapinbim.ui.panel("Walls") as walls:
            lapinbim.ui.tool("Perimeter", run=lambda: None, size="small")
            with lapinbim.ui.menu("Openings", icon="door", tooltip="Doors and windows"):
                @lapinbim.ui.tool("Door")
                def door():
                    pass
            walls.tool("Window", run=door, menu="Openings")
        lapinbim.ui.tool("Loose", run=door)
        roofs = lapinbim.ui.panel("Roofs")
        roofs.tool("Gable", run=door, size="large")
        by = {s["name"]: s for s in _ui._registered}
        self.assertEqual([s["name"] for s in _ui._registered], ["Perimeter", "Openings", "Door", "Window", "Loose", "Gable"])
        self.assertEqual(by["Perimeter"], {"name": "Perimeter", "icon": None, "tooltip": None, "call": None, "main": False, "panel": "Walls", "size": "small"})
        self.assertEqual(by["Openings"], {"kind": "menu", "name": "Openings", "icon": "door", "tooltip": "Doors and windows", "panel": "Walls"})
        self.assertEqual(by["Door"], {"name": "Door", "icon": None, "tooltip": None, "call": "door", "main": False, "panel": "Walls", "menu": "Openings"})
        self.assertEqual(by["Window"]["menu"], "Openings")
        self.assertEqual(by["Window"]["panel"], "Walls")
        self.assertNotIn("panel", by["Loose"])
        self.assertEqual((by["Gable"]["panel"], by["Gable"]["size"]), ("Roofs", "large"))
        self.assertEqual(_ui._within, [])
        with self.assertRaises(ValueError):
            lapinbim.ui.tool("Big", run=door, size="huge")
        with self.assertRaises(ValueError):
            lapinbim.ui.panel("")

    def test_a_palette_says_its_rows_in_order(self):
        with lapinbim.ui.panel("Walls"):
            p = lapinbim.ui.palette("Wall tools", icon="wall", tooltip="Make walls")
        p.heading("New wall").text("Drawn on the active storey.").separator()
        p.field("height", lapinbim.ui.number(2.8, unit="m", min=0.1))
        p.field("kind", ["brick", "concrete"])

        @p.button("Make", icon="hammer", tooltip="Make it")
        def make(v):
            return v

        with p.row():
            p.button("One", run=lambda: 1)
            p.button("Two", run=lambda: 2)
        spec = _ui._registered[0]
        self.assertEqual({k: spec[k] for k in ("kind", "name", "icon", "tooltip", "panel")},
                         {"kind": "palette", "name": "Wall tools", "icon": "wall", "tooltip": "Make walls", "panel": "Walls"})
        self.assertEqual(spec["rows"], [
            {"row": "heading", "text": "New wall"},
            {"row": "text", "text": "Drawn on the active storey."},
            {"row": "separator"},
            {"row": "field", "field": {"key": "height", "kind": "number", "label": "Height", "value": 2.8, "unit": "m", "min": 0.1}},
            {"row": "field", "field": {"key": "kind", "kind": "choice", "label": "Kind", "value": "brick",
                                       "choices": [{"value": "brick", "label": "brick"}, {"value": "concrete", "label": "concrete"}]}},
            {"row": "button", "name": "Make", "icon": "hammer", "tooltip": "Make it", "call": "make"},
            {"row": "buttons", "buttons": [{"name": "One", "icon": None, "tooltip": None, "call": None},
                                           {"name": "Two", "icon": None, "tooltip": None, "call": None}]},
        ])
        with self.assertRaises(ValueError):
            p.field("height", 3.0)

    def test_a_button_is_given_the_palettes_values_when_it_takes_them(self):
        p = lapinbim.ui.palette("Wall tools")
        p.field("height", lapinbim.ui.number(2.8, unit="m"))
        p.field("count", 2)
        got = []
        p.button("With", run=lambda v: got.append(v))
        p.button("Without", run=lambda: got.append("none"))
        _ui.press("Wall tools", "With", {"height": 3, "count": 4.0})
        _ui.press("Wall tools", "With", None)
        _ui.press("Wall tools", "Without", {"height": 3})
        self.assertEqual(got, [{"height": 3.0, "count": 4}, {"height": 2.8, "count": 2}, "none"])
        self.assertIsInstance(got[0]["height"], float)
        with self.assertRaises(LookupError):
            _ui.press("Wall tools", "Missing", {})
        with self.assertRaises(LookupError):
            _ui.press("Nope", "With", {})

    def test_outside_the_panel_nothing_is_registered_and_the_functions_come_back(self):
        _ui._registered = None
        p = lapinbim.ui.palette("P")

        def f():
            pass

        self.assertIs(p.button("B", run=f), f)
        self.assertIs(lapinbim.ui.menu("M").tool("T")(f), f)


SCRIPT = (
    "# lapin: name=\"Tools\", tool=\"Hello\", run=\"hello\"\n"
    "print('loaded as', __name__)\n"
    "def hello():\n"
    "    print('hello tool')\n"
    "@lapin.ui.tool('Registered', icon='square')\n"
    "def registered():\n"
    "    print('registered tool')\n"
    "lapin.ui.tool('By name', run=lambda: print('by name'))\n"
    "p = lapin.ui.palette('Palette', panel='Walls')\n"
    "p.field('height', lapin.ui.number(2.5, unit='m'))\n"
    "@p.button('Make')\n"
    "def make(v):\n"
    "    print('make', v['height'])\n"
)


class KernelToolTests(unittest.TestCase):
    """The kernel alone (no engine): a tool's run, and the tools a Run says back."""

    @classmethod
    def setUpClass(cls):
        cls.k = Kernel()

    @classmethod
    def tearDownClass(cls):
        cls.k.close()

    def run_job(self, **job):
        self.k.n += 1
        self.k.send({"op": "run", "id": self.k.n, "code": SCRIPT, "name": "tools.py", **job})
        out, tools = [], None
        while True:
            msg = self.k.next(timeout=30)
            if msg.get("type") in ("out", "err"):
                out.append(msg["text"])
            if msg.get("type") == "tools":
                tools = msg
            if msg.get("type") == "done":
                return "".join(out), tools, msg

    def test_a_run_from_the_panel_says_back_the_tools_its_script_registered(self):
        out, tools, done = self.run_job(source="mine:tools.py")
        self.assertTrue(done["ok"], done)
        self.assertEqual(out, "loaded as __main__\n")
        self.assertEqual(tools["source"], "mine:tools.py")
        self.assertEqual([t["name"] for t in tools["tools"]], ["Registered", "By name", "Palette"])
        self.assertEqual(tools["tools"][2]["kind"], "palette")
        self.assertEqual(tools["tools"][2]["rows"][1]["name"], "Make")
        self.assertEqual(tools["tools"][0]["call"], "registered")
        # Without a source (the console, a script not saved), nothing is said back.
        _, none, _ = self.run_job()
        self.assertIsNone(none)

    def test_a_tool_runs_the_script_as_a_module_then_its_function(self):
        out, tools, done = self.run_job(tool={"name": "Hello", "call": "hello", "main": False}, source="mine:tools.py")
        self.assertTrue(done["ok"], done)
        self.assertEqual(out, "loaded as __lapin_tool__\nhello tool\n")
        self.assertIsNone(tools)   # a tool's run changes no registration
        out, _, done = self.run_job(tool={"name": "By name", "call": None, "main": False})
        self.assertEqual(out, "loaded as __lapin_tool__\nby name\n")
        out, _, done = self.run_job(tool={"name": "Whole", "main": True})
        self.assertEqual(out, "loaded as __main__\n")
        _, _, done = self.run_job(tool={"name": "Missing", "call": "nope"})
        self.assertFalse(done["ok"])
        self.assertIn("no tool „Missing”", done["error"])

    def test_a_palettes_button_runs_its_function_with_the_palettes_values(self):
        out, tools, done = self.run_job(tool={"name": "Make", "call": "make", "palette": "Palette", "values": {"height": 3}})
        self.assertTrue(done["ok"], done)
        self.assertEqual(out, "loaded as __lapin_tool__\nmake 3.0\n")
        self.assertIsNone(tools)
        _, _, done = self.run_job(tool={"name": "Gone", "palette": "Palette", "values": {}})
        self.assertFalse(done["ok"])
        self.assertIn("no button „Gone”", done["error"])

    def test_a_tools_run_leaves_the_consoles_names_alone(self):
        self.k.run("x = 'console'", console=True)
        self.run_job(tool={"name": "Hello", "call": "hello"})
        out, done = self.k.run("x", console=True)
        self.assertEqual(out, "'console'\n")


class Window:
    """A window of the test's own: it answers the questions scripts ask (script.output, type ask) with `answer(question)`."""

    def __init__(self, port: int, answer):
        self.lapin = Lapin(port)
        self.asked: queue.Queue = queue.Queue()
        self.answer = answer
        self.off = self.lapin.on("script.output", self._event)

    def _event(self, e):
        if e.get("type") != "ask":
            return
        self.asked.put(e)
        reply = self.answer(e)
        params = {"ask": e["ask"], "cancel": True} if reply is None else {"ask": e["ask"], "values": reply}
        threading.Thread(target=lambda: self.lapin.call("script.answer", params), daemon=True).start()

    def close(self):
        self.off()
        self.lapin.close()


class AskTests(unittest.TestCase):
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

    def test_a_terminal_scripts_question_is_answered_by_the_window(self):
        def answer(q):
            keys = [f["key"] for f in q["fields"]]
            self.assertEqual(keys, ["width", "closed", "storey"])
            self.assertEqual(q["fields"][0]["unit"], "m")
            return {"width": 10.5, "closed": False, "storey": self.storey}

        w = Window(self.engine.port, answer)
        try:
            v = self.lapin.ui.ask("Room", {"width": lapinbim.ui.number(8.0, unit="m"), "closed": True, "storey": lapinbim.ui.storey()})
            self.assertEqual(v["width"], 10.5)
            self.assertFalse(v["closed"])
            self.assertEqual(v["storey"].id, self.storey)
            self.assertEqual(v["storey"].type, "IfcBuildingStorey")
        finally:
            w.close()

    def test_a_cancelled_question_is_none(self):
        w = Window(self.engine.port, lambda q: None)
        try:
            self.assertIsNone(self.lapin.ui.ask("Room", {"width": 8.0}))
        finally:
            w.close()


class PanelToolTests(unittest.TestCase):
    """A tool run by the engine's own kernel (script.run {tool}), as a click in My tools runs it."""

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
        status = self.lapin.call("script.status", {})
        if not status.get("ok"):
            self.skipTest(f"the engine runs no Python: {status.get('message')}")

    def walls(self):
        return [n for n in self.lapin.snapshot().nodes if n.get("type") == "IfcWall"]

    def run_tool(self, code: str, tool: dict, name: str, timeout: float = 60):
        events: queue.Queue = queue.Queue()
        off = self.lapin.on("script.output", events.put)
        try:
            run = self.lapin.call("script.run", {"code": code, "name": name, "tool": tool, "source": "mine:walls.py"})["run"]
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

    CODE = (
        "# lapin: name=\"Walls\", tool=\"Perimeter walls\", icon=\"square\", run=\"perimeter\"\n"
        "def perimeter():\n"
        "    v = lapin.ui.ask('Perimeter walls', {'width': lapin.ui.number(8.0, unit='m'), 'depth': lapin.ui.number(6.0, unit='m'), 'storey': lapin.ui.storey()})\n"
        "    if v is None:\n"
        "        print('cancelled')\n"
        "        return\n"
        "    w, d = v['width'], v['depth']\n"
        "    corners = [(0, 0), (w, 0), (w, d), (0, d)]\n"
        "    for (x1, y1), (x2, y2) in zip(corners, corners[1:] + corners[:1]):\n"
        "        lapin.architecture.add_wall(storey_id=v['storey'].id, x1=x1, y1=y1, x2=x2, y2=y2)\n"
        "    print(len(lapin.model.walls), 'walls')\n"
    )

    def test_a_tool_asks_makes_walls_and_one_undo_takes_them_away(self):
        w = Window(self.engine.port, lambda q: {"width": 5.0, "depth": 4.0, "storey": self.storey})
        try:
            out, done = self.run_tool(self.CODE, {"name": "Perimeter walls", "call": "perimeter"}, name="Perimeter walls")
        finally:
            w.close()
        self.assertTrue(done["ok"], done)
        self.assertEqual(out, "4 walls\n")
        self.assertEqual(len(self.walls()), 4)
        self.assertEqual(self.lapin.snapshot().state.get("undoLabel"), "Perimeter walls")
        self.lapin.undo()
        self.assertEqual(len(self.walls()), 0)

    def test_a_tool_cancelled_changes_nothing(self):
        w = Window(self.engine.port, lambda q: None)
        try:
            out, done = self.run_tool(self.CODE, {"name": "Perimeter walls", "call": "perimeter"}, name="Perimeter walls")
        finally:
            w.close()
        self.assertTrue(done["ok"], done)
        self.assertEqual(out, "cancelled\n")
        self.assertEqual(len(self.walls()), 0)
        # Nothing changed: no undo step.
        self.assertNotEqual(self.lapin.snapshot().state.get("undoLabel"), "Perimeter walls")

    def test_stop_while_a_tool_asks_takes_the_question_away_and_keeps_nothing(self):
        # A wall made before the question; the window shows the question and never answers: the panel's Stop ends the run.
        code = self.CODE.replace("    v = lapin.ui.ask", "    lapin.architecture.add_wall(storey_id=lapin.model.storey().id, x1=0, y1=8, x2=5, y2=8)\n    v = lapin.ui.ask")
        events: queue.Queue = queue.Queue()
        off = self.lapin.on("script.output", events.put)
        try:
            run = self.lapin.call("script.run", {"code": code, "name": "Perimeter walls", "tool": {"name": "Perimeter walls", "call": "perimeter"}})["run"]
            deadline = time.time() + 30
            while True:
                e = events.get(timeout=max(0.01, deadline - time.time()))
                if e.get("type") == "ask":
                    break
            self.lapin.call("script.stop")
            closed = False
            while True:
                e = events.get(timeout=max(0.01, deadline - time.time()))
                closed = closed or e.get("type") == "asked"
                if e.get("type") == "done" and e.get("id") == run:
                    break
        finally:
            off()
        self.assertTrue(closed)
        self.assertFalse(e["ok"])
        self.assertTrue(e["interrupted"])
        self.assertFalse(e.get("kept"))
        self.assertEqual(len(self.walls()), 0)


if __name__ == "__main__":
    unittest.main()
