"""``lapin.ui``: a script's tools and questions (docs/python.md, stage 4 of docs/plan-python-scripting.md; decision 489).

::

    import lapinbim as lapin

    @lapin.ui.tool("Perimeter walls", icon="square", tooltip="Four walls round a rectangle")
    def perimeter():
        v = lapin.ui.ask("Perimeter walls", {
            "width": lapin.ui.number(8.0, unit="m", min=0.1),
            "depth": lapin.ui.number(6.0, unit="m", min=0.1),
            "storey": lapin.ui.storey(),
        })
        if v is None:            # Cancel
            return
        ...

- ``ask(title, fields)`` shows a dialog of Lapin's own made from the fields (a number with its unit, a text, a choice, a
  checkbox, an element picked in the model or the selection, a storey, a type of a class) and returns a dict of the
  values, or None when it is cancelled. Nothing of the script is drawn: no HTML, no JavaScript. It is a host call
  (``script.ask``), answered by the window of this computer, so a script run from a terminal asks too.
- ``tool(name, run=None, icon=…, tooltip=…)`` registers a tool: when the Script panel runs the script (Run), the tools it
  registers appear in My tools; a click runs the script again (as a module, ``__name__ == "__lapin_tool__"``) and calls
  the tool's function, all of it one undo step. A script's header can name a tool too (``# lapin: tool="…"``), read
  without running it. Outside the panel (a terminal) ``tool`` only gives the function back, so a script runs there as
  it is.
"""

from __future__ import annotations

import keyword
import math
from typing import Any, Callable

KINDS = ("number", "text", "choice", "bool", "element", "storey", "type")


class Field:
    """One field of a question: its kind, its first value and what the dialog shows beside it."""

    def __init__(self, kind: str, value: Any = None, label: str | None = None, **options: Any):
        if kind not in KINDS:
            raise ValueError(f"a field is one of {', '.join(KINDS)}, not {kind!r}")
        self.kind = kind
        self.value = value
        self.label = label
        self.options = {k: v for k, v in options.items() if v is not None}

    def _json(self, key: str) -> dict:
        o: dict[str, Any] = {"key": key, "kind": self.kind, "label": self.label or _label(key)}
        if self.value is not None:
            v = self.value
            if self.kind in ("element", "storey", "type"):
                v = [_id(x) for x in v] if isinstance(v, (list, tuple)) else _id(v)
            o["value"] = v
        o.update(self.options)
        return o

    def __repr__(self) -> str:
        return f"<lapin.ui field {self.kind} {self.value!r}>"


def _label(key: str) -> str:
    """A key as a label: ``wall_height`` → ``Wall height``."""
    text = str(key).replace("_", " ").strip()
    return text[:1].upper() + text[1:] if text else str(key)


def _id(x: Any) -> Any:
    return getattr(x, "id", x)


def number(value: float = 0.0, *, unit: str | None = None, label: str | None = None, min: float | None = None,
           max: float | None = None, step: float | None = None, integer: bool = False) -> Field:
    """A number, with its unit written beside it (``unit="m"``: the engine's metres, as the script uses them)."""
    for name, v in (("value", value), ("min", min), ("max", max), ("step", step)):
        if v is not None and (isinstance(v, bool) or not isinstance(v, (int, float)) or not math.isfinite(v)):
            raise ValueError(f"a number's {name} is a finite number")
    return Field("number", int(value) if integer else float(value), label, unit=unit, min=min, max=max, step=step, integer=integer or None)


def text(value: str = "", *, label: str | None = None, multiline: bool = False) -> Field:
    return Field("text", str(value), label, multiline=multiline or None)


def choice(options: Any, value: Any = None, *, label: str | None = None) -> Field:
    """One of a list: ``choice(["brick", "concrete"])``, ``choice({"b": "Brick", "c": "Concrete"})`` (value: label) or
    pairs ``[("b", "Brick"), …]``; the value returned is the option's value. The first is chosen unless ``value`` says."""
    if isinstance(options, dict):
        pairs = list(options.items())
    else:
        pairs = [(o[0], o[1]) if isinstance(o, (list, tuple)) and len(o) == 2 else (o, o) for o in options]
    if not pairs:
        raise ValueError("a choice has its options")
    for v, _ in pairs:
        if not isinstance(v, (str, int, float, bool)):
            raise ValueError("a choice's values are text, numbers or booleans")
    values = [v for v, _ in pairs]
    if value is None:
        value = values[0]
    elif value not in values:
        raise ValueError(f"{value!r} is not one of the choice's options")
    return Field("choice", value, label, choices=[{"value": v, "label": str(t)} for v, t in pairs])


def checkbox(value: bool = False, *, label: str | None = None) -> Field:
    return Field("bool", bool(value), label)


def element(*classes: str, label: str | None = None, multiple: bool = False, selection: bool = True,
            required: bool = True, value: Any = None) -> Field:
    """An element of the model: picked in the window (its selection, or one of the list), of these IFC classes (any when
    none is named). ``multiple=True`` returns a list. ``selection=True`` starts from what the window has selected."""
    return Field("element", value, label, classes=list(classes) or None, multiple=multiple or None,
                 selection=selection, required=required)


def storey(value: Any = None, *, label: str | None = None) -> Field:
    """A storey of the model (the window's active one first)."""
    return Field("storey", value, label)


def type_of(cls: str, value: Any = None, *, label: str | None = None, required: bool = True) -> Field:
    """A type of a class (``type_of("IfcWallType")``): the project's types of it, by name."""
    return Field("type", value, label, cls=cls, required=required)


def _field(key: str, v: Any) -> Field:
    """A plain value as its field: a bool a checkbox, a number a number, a str a text, a list a choice."""
    if isinstance(v, Field):
        return v
    if isinstance(v, bool):
        return checkbox(v)
    if isinstance(v, int):
        return number(v, integer=True)
    if isinstance(v, float):
        return number(v)
    if isinstance(v, str):
        return text(v)
    if isinstance(v, (list, tuple, dict)):
        return choice(v)
    raise TypeError(f"the field {key!r} is a lapin.ui field, a number, a text, a bool or a list of choices, not {type(v).__name__}")


def question(title: str, fields: dict, *, text: str | None = None, ok: str | None = None) -> dict:
    """What ``script.ask`` is sent: the title, the fields as JSON, in order."""
    if not isinstance(fields, dict) or not fields:
        raise ValueError("lapin.ui.ask takes its fields as a dict: {name: value or lapin.ui field}")
    q: dict[str, Any] = {"title": str(title), "fields": [_field(str(k), v)._json(str(k)) for k, v in fields.items()]}
    if text:
        q["text"] = str(text)
    if ok:
        q["ok"] = str(ok)
    return q


def values(fields: dict, answer: dict, elements: Callable[[], dict] | None = None) -> dict:
    """The window's answer as Python values, each by its field: a number a float (an int when it is one), an element, a
    storey or a type an Element (read from the model) when the model has it, else its id."""
    out: dict[str, Any] = {}
    model: dict | None = None
    for key, v in fields.items():
        f = _field(str(key), v)
        got = answer.get(str(key), f.value)
        if f.kind == "number":
            got = f.value if got is None or isinstance(got, bool) else got
            got = int(got) if f.options.get("integer") else float(got)
        elif f.kind == "bool":
            got = bool(got)
        elif f.kind == "text":
            got = "" if got is None else str(got)
        elif f.kind in ("element", "storey", "type") and got not in (None, "", []):
            if model is None:
                model = elements() if elements else {}
            if isinstance(got, list):
                got = [model.get(i, i) for i in got]
            else:
                got = model.get(got, got)
        elif f.kind in ("element", "storey", "type"):
            got = [] if f.options.get("multiple") else None
        out[key] = got
    return out


# ------------------------------------------------------------------ tools

#: The tools the script being run registers, in order (the kernel sets it round a Run from the panel; None elsewhere).
_registered: list[dict] | None = None
#: The functions of the tools registered by the script loaded now, by the tool's name (what a tool's run calls).
_functions: dict[str, Callable[..., Any]] = {}

ICON_CHARS = 40


def _spec(name: str, icon: str | None, tooltip: str | None, f: Callable[..., Any] | None) -> dict:
    call = getattr(f, "__name__", None) if f is not None else None
    if call is not None and (not call.isidentifier() or keyword.iskeyword(call) or call == "<lambda>"):
        call = None
    return {"name": name, "icon": (icon or None) and str(icon)[:ICON_CHARS], "tooltip": (tooltip or None) and str(tooltip)[:300],
            "call": call, "main": f is None}


def _put(spec: dict) -> None:
    if _registered is None:
        return
    for i, s in enumerate(_registered):
        if s["name"] == spec["name"]:
            _registered[i] = spec
            return
    _registered.append(spec)


def tool(name: str, run: Callable[..., Any] | None = None, *, icon: str | None = None, tooltip: str | None = None):
    """A tool of this script, in My tools (when the Script panel runs it): ``lapin.ui.tool("Walls", run=walls)``, or as a
    decorator, ``@lapin.ui.tool("Walls", icon="square")``. Without a function (and not used as a decorator) the tool runs
    the whole script. ``icon``: an icon of Lapin's or lucide's by its name (``square``, ``wall``, ``ruler``…), else its
    first letter is drawn. Returns the function (or the decorator)."""
    name = str(name).strip()
    if not name or len(name) > 120:
        raise ValueError("a tool has a name of 1 to 120 characters")
    if run is not None:
        if not callable(run):
            raise TypeError("a tool's run is a function")
        _functions[name] = run
        _put(_spec(name, icon, tooltip, run))
        return run
    # Used bare, the tool runs the script; used as a decorator, the function it decorates.
    _put(_spec(name, icon, tooltip, None))

    def decorate(f: Callable[..., Any]) -> Callable[..., Any]:
        return tool(name, f, icon=icon, tooltip=tooltip)

    return decorate


class UI:
    """``lapin.ui``: the questions and tools of a script, on a connection (``lapin.ui`` the module's, ``l.ui`` another's)."""

    number = staticmethod(number)
    text = staticmethod(text)
    choice = staticmethod(choice)
    checkbox = staticmethod(checkbox)
    element = staticmethod(element)
    storey = staticmethod(storey)
    type_of = staticmethod(type_of)
    tool = staticmethod(tool)
    Field = Field

    def __init__(self, client: Callable[[], Any]):
        self._client = client

    def ask(self, title: str, fields: dict, *, text: str | None = None, ok: str | None = None) -> dict | None:
        """A dialog of the window's own, made from ``fields`` ({name: a value or a lapin.ui field}, in order): the values
        as a dict, or None when the user cancels (or the script is stopped). It waits for the answer."""
        c = self._client()
        answer = c.call("script.ask", question(title, fields, text=text, ok=ok)) or {}
        if answer.get("stopped"):
            # The Script panel's Stop took the question away: the script stops, it does not go on as if cancelled.
            raise KeyboardInterrupt
        if answer.get("cancelled") or not isinstance(answer.get("values"), dict):
            return None
        return values(fields, answer["values"], c._elements)

    def __repr__(self) -> str:
        return "<lapin.ui>"
