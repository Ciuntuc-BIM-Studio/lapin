"""The command catalogue and the typed calls built from it.

The catalogue (decision 489, docs/python.md) lists every command and query with the arguments its handler reads, made
from the engine's source when Lapin is built (scripts/command-catalogue.mjs) and served by the engine as
``session.catalogue``; the classes and their fields come from ``session.info``. A copy taken when the package was
published (``catalogue.json`` beside this file) serves an engine too old to have ``session.catalogue``, and the stubs.

The typed layer: ``lapin.architecture.add_wall(x1=0, y1=0, x2=5, y2=0)`` runs ``architecture.addWall``; a namespace is
what comes before the command's first dot, in snake_case (``cutOrder`` → ``cut_order``); arguments are named only, in
snake_case or as the engine spells them, metres and degrees; a top-level ``None`` is not sent.
"""

from __future__ import annotations

import difflib
import json
from pathlib import Path
from typing import TYPE_CHECKING, Any, Callable

from ._names import camel, ident, plural, snake

if TYPE_CHECKING:
    from ._client import Lapin

BUNDLED = Path(__file__).with_name("catalogue.json")


def bundled() -> dict | None:
    """The catalogue published with the package; None when it has none (a checkout of the repo)."""
    try:
        return json.loads(BUNDLED.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None


def merge(info: dict, served: dict | None) -> dict:
    """One catalogue from ``session.info`` (every command, the classes) and ``session.catalogue`` (arguments, queries)."""
    served = served or {}
    commands = {c["name"]: dict(c) for c in served.get("commands") or []}
    for c in info.get("commands") or []:
        e = commands.setdefault(c["name"], {"name": c["name"], "module": c["name"].split(".")[0], "args": []})
        e.setdefault("label", c.get("label"))
    classes = [
        {
            "class": t.get("type"),
            "label": t.get("label"),
            "module": t.get("module"),
            "kind": t.get("category"),
            "fields": [
                {k: f[k] for k in ("key", "label", "kind", "choices", "readOnly") if f.get(k) not in (None, False)}
                for f in t.get("fields") or []
            ],
        }
        for t in info.get("nodeTypes") or []
    ]
    product = info.get("product") or {}
    return {
        "format": 1,
        "version": served.get("version") or info.get("engineVersion"),
        "product": product.get("id"),
        "commands": sorted(commands.values(), key=lambda c: c["name"]),
        "queries": sorted(served.get("queries") or [], key=lambda q: q["name"]),
        "classes": sorted(classes, key=lambda c: c["class"] or ""),
    }


class Entry:
    """One command or query as a Python call: its engine name, kind, label and arguments (engine name → type)."""

    __slots__ = ("name", "kind", "label", "module", "args", "py")

    def __init__(self, raw: dict, kind: str):
        self.name: str = raw["name"]
        self.kind = kind
        self.label: str | None = raw.get("label")
        self.module: str | None = raw.get("module")
        self.args: dict[str, str | None] = {a["name"]: a.get("type") for a in raw.get("args") or []}
        # The Python name of each argument → the engine's.
        self.py: dict[str, str] = {}
        for a in self.args:
            self.py.setdefault(ident(snake(a)), a)

    def to_args(self, raw: dict | None, kwargs: dict[str, Any]) -> dict[str, Any]:
        """The engine's arguments: ``raw`` as it is, then each keyword under the engine's name; a top-level None is left out."""
        out = dict(raw or {})
        for k, v in kwargs.items():
            if v is None:
                continue
            key = self.py.get(k) or (k if k in self.args else camel(k))
            out[key] = v
        return out

    def doc(self) -> str:
        what = "query" if self.kind == "query" else "command"
        head = f"{self.label} ({what} {self.name})." if self.label else f"The {what} {self.name}."
        if not self.args:
            return head
        return head + "\n\nArguments: " + ", ".join(f"{p}" + (f" ({self.args[a]})" if self.args[a] else "") for p, a in self.py.items()) + "."


class Catalogue:
    """The catalogue read: the namespaces, each with its calls by Python name; the classes by their plural."""

    def __init__(self, data: dict):
        self.data = data
        self.version = data.get("version")
        self.namespaces: dict[str, dict[str, Entry]] = {}
        self.spelled: dict[str, str] = {}  # the engine's prefix → the namespace's Python name
        for kind, key in (("command", "commands"), ("query", "queries")):
            for raw in data.get(key) or []:
                prefix, _, rest = raw["name"].partition(".")
                if not rest:
                    continue
                ns = ident(snake(prefix))
                self.spelled[prefix] = ns
                calls = self.namespaces.setdefault(ns, {})
                name = ident(snake(rest))
                # A query named as a command (project.location) is project.location_query.
                if name in calls:
                    if kind == "command":
                        continue
                    name += "_query"
                calls[name] = Entry(raw, kind)
        self.classes: dict[str, dict] = {c["class"]: c for c in data.get("classes") or [] if c.get("class")}
        self.plurals: dict[str, str] = {}
        for cls, c in sorted(self.classes.items()):
            label = c.get("label") or cls
            p = ident(plural(snake("".join(ch if ch.isalnum() else "_" for ch in label).strip("_"))).replace("__", "_"))
            self.plurals.setdefault(p, cls)

    def namespace(self, name: str) -> dict[str, Entry] | None:
        return self.namespaces.get(name) or self.namespaces.get(self.spelled.get(name, ""))

    def entry(self, name: str) -> Entry | None:
        """A command or query by its engine name (``architecture.addWall``)."""
        prefix, _, rest = name.partition(".")
        for e in (self.namespace(prefix) or {}).values():
            if e.name == name:
                return e
        return None


def bind(client: "Lapin", entry: Entry, name: str | None = None) -> Callable[..., Any]:
    """A command or query of the catalogue as a function bound to ``client``: call it with named arguments
    (``_raw``, a dict passed first, goes as it is: what a keyword cannot say, a top-level null)."""

    def call(_raw: dict | None = None, /, **kwargs: Any) -> Any:
        args = entry.to_args(_raw, kwargs)
        return client.query(entry.name, args) if entry.kind == "query" else client.execute(entry.name, args)

    call.__name__ = name or ident(snake(entry.name.partition(".")[2]))
    call.__qualname__ = f"{entry.name.partition('.')[0]}.{call.__name__}"
    call.__doc__ = entry.doc()
    call.entry = entry  # type: ignore[attr-defined]
    return call


class Namespace:
    """The calls under one prefix (``lapin.architecture``): ``add_wall``, ``update_wall``…"""

    def __init__(self, client: "Lapin", name: str, calls: dict[str, Entry]):
        self._client = client
        self._name = name
        self._calls = calls

    def __getattr__(self, name: str) -> Callable[..., Any]:
        e = self._calls.get(name) or self._calls.get(ident(snake(name)))
        if e is None:
            near = difflib.get_close_matches(name, list(self._calls), n=3)
            hint = f" Did you mean {', '.join(near)}?" if near else ""
            raise AttributeError(f"Lapin has no {self._name}.{name}.{hint}")
        return bind(self._client, e, name)

    def __dir__(self):
        return sorted(self._calls)

    def __repr__(self) -> str:
        return f"<lapin namespace {self._name}: {len(self._calls)} calls>"
