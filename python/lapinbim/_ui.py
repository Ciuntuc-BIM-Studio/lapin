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
- ``panel(name)``, ``menu(name)``, ``palette(name)`` (decision 516): a script's own group of My tools (its tools go there
  instead of "Scripts"), a pulldown of tools, and a small floating panel of fields and buttons, all drawn by the window
  from what is registered (JSON), never from the script's own HTML or code::

      with lapin.ui.panel("Walls"):
          @lapin.ui.tool("Perimeter", icon="square", size="small")
          def perimeter(): ...

          with lapin.ui.menu("Openings", icon="door"):
              lapin.ui.tool("Door", run=add_door)
              lapin.ui.tool("Window", run=add_window)

          p = lapin.ui.palette("Wall tools", icon="wall")
          p.heading("New wall")
          p.field("height", lapin.ui.number(2.8, unit="m"))

          @p.button("Make", icon="hammer")
          def make(v):              # v: the palette's values, {"height": 2.8}
              ...
"""

from __future__ import annotations

import inspect
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
#: The palettes of the script loaded now, by name (a palette's button run finds its function and its fields there).
_palettes: dict[str, "Palette"] = {}
#: The panels and menus whose ``with`` block is open, innermost last: a tool registered inside goes into them.
_within: list[Any] = []

ICON_CHARS = 40
NAME_CHARS = 120
SIZES = ("large", "small")
#: A palette's most rows, fields and buttons; the most characters of its texts.
PALETTE_ROWS, PALETTE_FIELDS, PALETTE_BUTTONS, TEXT_CHARS = 200, 64, 50, 2000


def _name(name: Any, what: str = "tool") -> str:
    name = str(name).strip()
    if not name or len(name) > NAME_CHARS:
        raise ValueError(f"a {what} has a name of 1 to {NAME_CHARS} characters")
    return name


def _size(size: str | None) -> str | None:
    if size is not None and size not in SIZES:
        raise ValueError('a size is "large" or "small"')
    return size


def _call(f: Callable[..., Any] | None) -> str | None:
    """A function's name as a tool's run calls it (a name of the script, never a lambda's)."""
    call = getattr(f, "__name__", None) if f is not None else None
    if call is not None and (not call.isidentifier() or keyword.iskeyword(call) or call == "<lambda>"):
        call = None
    return call


def _icon(icon: str | None) -> str | None:
    return (icon or None) and str(icon)[:ICON_CHARS]


def _tip(tooltip: str | None) -> str | None:
    return (tooltip or None) and str(tooltip)[:300]


def _spec(name: str, icon: str | None, tooltip: str | None, f: Callable[..., Any] | None, *, panel: str | None = None,
          size: str | None = None, menu: str | None = None) -> dict:
    s = {"name": name, "icon": _icon(icon), "tooltip": _tip(tooltip), "call": _call(f), "main": f is None}
    if panel:
        s["panel"] = panel
    if size:
        s["size"] = size
    if menu:
        s["menu"] = menu
    return s


def _put(spec: dict) -> None:
    if _registered is None:
        return
    for i, s in enumerate(_registered):
        if s["name"] == spec["name"]:
            _registered[i] = spec
            return
    _registered.append(spec)


def _where(panel: Any, menu: Any) -> tuple[str | None, str | None]:
    """The panel and the menu a tool goes in: the ones named, else the innermost ``with`` block's."""
    if menu is None and panel is None:
        for w in reversed(_within):
            if isinstance(w, Menu):
                menu = w
                break
            if isinstance(w, Panel):
                panel = w
                break
    if isinstance(menu, Menu):
        panel = panel if panel is not None else menu.panel
        menu = menu.name
    if isinstance(panel, Panel):
        panel = panel.name
    if panel is None:
        for w in reversed(_within):
            if isinstance(w, Panel):
                panel = w.name
                break
    return (_name(panel, "panel") if panel is not None else None, _name(menu, "menu") if menu is not None else None)


def tool(name: str, run: Callable[..., Any] | None = None, *, icon: str | None = None, tooltip: str | None = None,
         size: str | None = None, panel: Any = None, menu: Any = None):
    """A tool of this script, in My tools (when the Script panel runs it): ``lapin.ui.tool("Walls", run=walls)``, or as a
    decorator, ``@lapin.ui.tool("Walls", icon="square")``. Without a function (and not used as a decorator) the tool runs
    the whole script. ``icon``: an icon of Lapin's or lucide's by its name (``square``, ``wall``, ``ruler``…), else its
    first letter is drawn. ``size``: ``"large"`` (the default) or ``"small"`` (a line, three stacked). ``panel``: the
    group of My tools it goes in (a name, or a ``lapin.ui.panel``; inside ``with lapin.ui.panel(…):`` that one), else
    "Scripts"; ``menu``: the pulldown it is listed in (a ``lapin.ui.menu``). Returns the function (or the decorator)."""
    name = _name(name)
    size = _size(size)
    in_panel, in_menu = _where(panel, menu)
    if run is not None:
        if not callable(run):
            raise TypeError("a tool's run is a function")
        _functions[name] = run
        _put(_spec(name, icon, tooltip, run, panel=in_panel, size=size, menu=in_menu))
        return run
    # Used bare, the tool runs the script; used as a decorator, the function it decorates.
    _put(_spec(name, icon, tooltip, None, panel=in_panel, size=size, menu=in_menu))

    def decorate(f: Callable[..., Any]) -> Callable[..., Any]:
        return tool(name, f, icon=icon, tooltip=tooltip, size=size, panel=in_panel, menu=in_menu)

    return decorate


class _Block:
    """What a ``with`` block of a panel or a menu does: the tools registered inside go into it."""

    def __enter__(self):
        _within.append(self)
        return self

    def __exit__(self, *exc: Any) -> None:
        for i in range(len(_within) - 1, -1, -1):
            if _within[i] is self:
                del _within[i]
                break


class Panel(_Block):
    """A group of My tools of the script's own (``lapin.ui.panel("Walls")``): its tools, menus and palettes go into a group
    of that name, made when it is missing, instead of "Scripts". ``with lapin.ui.panel("Walls"):`` puts what is registered
    inside it there; ``p.tool(…)``, ``p.menu(…)``, ``p.palette(…)`` the same one by one. A panel registers nothing alone."""

    def __init__(self, name: str, icon: str | None = None):
        self.name = _name(name, "panel")
        # Kept for the day My tools draws its groups with an icon; not sent now.
        self.icon = _icon(icon)

    def tool(self, name: str, run: Callable[..., Any] | None = None, **options: Any):
        return tool(name, run, panel=self, **options)

    def menu(self, name: str, **options: Any) -> "Menu":
        return Menu(name, panel=self, **options)

    def palette(self, name: str, **options: Any) -> "Palette":
        return Palette(name, panel=self, **options)

    def __repr__(self) -> str:
        return f"<lapin.ui panel {self.name!r}>"


def panel(name: str, icon: str | None = None) -> Panel:
    return Panel(name, icon)


class Menu(_Block):
    """A pulldown of My tools (``lapin.ui.menu("Openings", icon="door")``): one button that opens the list of its tools.
    Each of them is still a tool of its own (Commands… finds it, Customize My tools… can place it alone).
    ``with m:`` / ``m.tool(…)`` / ``@m.tool(…)`` put a tool in it."""

    def __init__(self, name: str, *, icon: str | None = None, tooltip: str | None = None, size: str | None = None, panel: Any = None):
        self.name = _name(name, "menu")
        self.panel, _ = _where(panel, None)
        spec: dict[str, Any] = {"kind": "menu", "name": self.name, "icon": _icon(icon), "tooltip": _tip(tooltip)}
        if self.panel:
            spec["panel"] = self.panel
        if _size(size):
            spec["size"] = size
        _put(spec)

    def tool(self, name: str, run: Callable[..., Any] | None = None, **options: Any):
        return tool(name, run, menu=self, **options)

    def __repr__(self) -> str:
        return f"<lapin.ui menu {self.name!r}>"


def menu(name: str, *, icon: str | None = None, tooltip: str | None = None, size: str | None = None, panel: Any = None) -> Menu:
    return Menu(name, icon=icon, tooltip=tooltip, size=size, panel=panel)


def _takes_values(f: Callable[..., Any]) -> bool:
    """Whether a button's function takes a parameter (the palette's values)."""
    try:
        params = inspect.signature(f).parameters.values()
    except (TypeError, ValueError):
        return False
    return any(p.kind in (p.POSITIONAL_ONLY, p.POSITIONAL_OR_KEYWORD, p.VAR_POSITIONAL) for p in params)


class Palette:
    """A small floating panel of the window's own (``lapin.ui.palette("Wall tools", icon="wall")``), opened and closed by its
    button in My tools: headings, texts, separators, fields (``p.field(key, value or lapin.ui field)``, the kinds ``ask``
    has) and buttons (``p.button(name, run=f)`` / ``@p.button(name)``), in order; ``with p.row():`` puts the buttons made
    inside it side by side. Its fields keep their values while it is open (and the next time). A button pressed is one
    Run, one undo step named after it; its function is given the palette's values as a dict (as ``ask`` gives them) when
    it takes a parameter. Nothing of the script is drawn: the window draws it all."""

    def __init__(self, name: str, *, icon: str | None = None, tooltip: str | None = None, size: str | None = None, panel: Any = None):
        self.name = _name(name, "palette")
        self.panel, _ = _where(panel, None)
        self.fields: dict[str, Any] = {}
        self.buttons: dict[str, Callable[..., Any]] = {}
        self._rows: list[dict] = []
        self._into: list[dict] | None = None
        self._spec: dict[str, Any] = {"kind": "palette", "name": self.name, "icon": _icon(icon), "tooltip": _tip(tooltip)}
        if self.panel:
            self._spec["panel"] = self.panel
        if _size(size):
            self._spec["size"] = size
        self._spec["rows"] = self._rows
        _palettes[self.name] = self
        _put(self._spec)

    def _row(self, row: dict) -> None:
        if len(self._rows) >= PALETTE_ROWS:
            raise ValueError(f"a palette has at most {PALETTE_ROWS} rows")
        self._rows.append(row)

    def heading(self, text: str) -> "Palette":
        self._row({"row": "heading", "text": str(text)[:TEXT_CHARS]})
        return self

    def text(self, text: str) -> "Palette":
        self._row({"row": "text", "text": str(text)[:TEXT_CHARS]})
        return self

    def separator(self) -> "Palette":
        self._row({"row": "separator"})
        return self

    def field(self, key: str, value: Any) -> "Palette":
        key = str(key).strip()
        if not key or len(key) > 64:
            raise ValueError("a palette's field has a key of 1 to 64 characters")
        if key in self.fields:
            raise ValueError(f"the palette has the field {key!r} already")
        if len(self.fields) >= PALETTE_FIELDS:
            raise ValueError(f"a palette has at most {PALETTE_FIELDS} fields")
        f = _field(key, value)
        self._row({"row": "field", "field": f._json(key)})
        self.fields[key] = f
        return self

    def button(self, name: str, run: Callable[..., Any] | None = None, *, icon: str | None = None, tooltip: str | None = None):
        """A button of the palette: ``p.button("Make", run=make)``, or ``@p.button("Make")``. Returns the function (or the decorator)."""
        name = _name(name, "button")
        if run is None:
            return lambda f: self.button(name, f, icon=icon, tooltip=tooltip)
        if not callable(run):
            raise TypeError("a button's run is a function")
        if name not in self.buttons and len(self.buttons) >= PALETTE_BUTTONS:
            raise ValueError(f"a palette has at most {PALETTE_BUTTONS} buttons")
        fresh = name not in self.buttons
        self.buttons[name] = run
        b = {"name": name, "icon": _icon(icon), "tooltip": _tip(tooltip), "call": _call(run)}
        if fresh:
            if self._into is not None:
                self._into.append(b)
            else:
                self._row({"row": "button", **b})
        return run

    def row(self) -> "_Row":
        """``with p.row():`` the buttons made inside stand side by side."""
        return _Row(self)

    def __enter__(self) -> "Palette":
        return self

    def __exit__(self, *exc: Any) -> None:
        pass

    def __repr__(self) -> str:
        return f"<lapin.ui palette {self.name!r}>"


class _Row:
    def __init__(self, palette: Palette):
        self.palette = palette

    def __enter__(self) -> Palette:
        buttons: list[dict] = []
        self.palette._row({"row": "buttons", "buttons": buttons})
        self.palette._into = buttons
        return self.palette

    def __exit__(self, *exc: Any) -> None:
        self.palette._into = None


def palette(name: str, *, icon: str | None = None, tooltip: str | None = None, size: str | None = None, panel: Any = None) -> Palette:
    return Palette(name, icon=icon, tooltip=tooltip, size=size, panel=panel)


def press(palette_name: str, button: str, values_in: dict | None, elements: Callable[[], dict] | None = None) -> Any:
    """A palette's button pressed (the kernel's side of a button's run, the script loaded again): its function called,
    given the palette's values (read as ``ask`` reads its answer) when it takes a parameter."""
    pal = _palettes.get(palette_name)
    f = pal.buttons.get(button) if pal is not None else None
    if pal is None or not callable(f):
        raise LookupError(f"The script has no button „{button}” in a palette „{palette_name}”.")
    if _takes_values(f):
        return f(values(pal.fields, values_in if isinstance(values_in, dict) else {}, elements))
    return f()


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
    panel = staticmethod(panel)
    menu = staticmethod(menu)
    palette = staticmethod(palette)
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
