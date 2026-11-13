"""lapinbim: drive Lapin, the open BIM modeller on IFC, from Python (docs/python.md).

::

    import lapinbim as lapin

    w = lapin.architecture.add_wall(x1=0, y1=0, x2=5, y2=0, height=2.8)   # one undo step
    with lapin.batch("Ring of walls"):                                    # one undo step for all, sent at the end
        for a, b in [((5, 0), (5, 4)), ((5, 4), (0, 4)), ((0, 4), (0, 0))]:
            lapin.architecture.add_wall(x1=a[0], y1=a[1], x2=b[0], y2=b[1])
    for wall in lapin.model.walls:
        print(wall.name, wall.quantities())

The module's functions use one connection, made at the first call to the open Lapin (``LAPIN_PORT``, else the engines'
host files, else the ports from 14912 probed; docs/python.md); ``lapin.connect(port)`` or ``lapin.connect(project=...)``
makes it explicitly, ``lapin.Lapin(port)`` makes another. Standard library only.
"""

from __future__ import annotations

from typing import Any, Callable

from ._version import __version__
from ._client import Batch, Deferred, Lapin, LapinError, Step
from ._frame import Obj
from ._ids import global_id
from ._model import Element, Model
from ._ui import UI

__all__ = [
    "__version__", "Lapin", "LapinError", "Batch", "Deferred", "Step", "Element", "Model", "Obj",
    "connect", "engines", "client", "close", "call", "execute", "query", "undo", "redo", "save", "info", "quantities",
    "snapshot", "state", "batch", "step", "selection", "on", "catalogue", "ns", "command", "ui", "UI",
    "create_body", "set_body", "global_id", "selected", "measure", "measure_value", "remove_measure", "watch", "unwatch",
]

_default: Lapin | None = None

#: Questions and tools (docs/python.md, stage 4): ``lapin.ui.ask(...)``, ``@lapin.ui.tool(...)``; on the module's connection.
ui = UI(lambda: client())


def connect(port: int | None = None, **options: Any) -> Lapin:
    """The module's connection, made anew: to ``port``, else the open engine (with several open, the one whose
    ``project=`` or ``product=`` matches)."""
    global _default
    if _default is not None:
        _default.close()
    _default = Lapin(port, **options)
    return _default


def engines() -> list[dict]:
    """The open Lapins on this computer, each as its engine says (``engine.hello``): port, product, version, pid, project."""
    from ._discovery import find_engines

    return find_engines()


def client() -> Lapin:
    """The module's connection, made at the first call."""
    if _default is None or _default.closed:
        return connect()
    return _default


def close() -> None:
    global _default
    if _default is not None:
        _default.close()
        _default = None


def call(method: str, params: dict | None = None, timeout: float | None = None) -> Any:
    return client().call(method, params, timeout)


def execute(command: str, args: dict | None = None) -> Any:
    return client().execute(command, args)


def query(name: str, args: dict | None = None) -> Any:
    return client().query(name, args)


def undo() -> None:
    client().undo()


def redo() -> None:
    client().redo()


def save(path: str | None = None) -> Any:
    return client().save(path)


def info() -> Obj:
    return client().info()


def quantities(id: str | Element) -> Any:
    return client().quantities(id)


def create_body(ifc_class: str, body: Any, **options: Any) -> Any:
    """An element whose body this program computes (``element.createBody``): see ``Lapin.create_body``."""
    return client().create_body(ifc_class, body, **options)


def set_body(id: Any, body: Any, **options: Any) -> Any:
    """A new body for an element made by ``create_body`` (``element.setBody``), its GlobalId kept."""
    return client().set_body(id, body, **options)


def snapshot() -> Obj:
    return client().snapshot()


def state() -> Obj:
    return client().state()


def batch(label: str | None = None) -> Batch:
    return client().batch(label)


def step(label: str | None = None) -> Step:
    """``with lapin.step("Facades"):`` one undo step, each command answered at once; rolled back if the block raises."""
    return client().step(label)


def selection() -> list[Element]:
    """What the window has selected now, as elements."""
    return client().selection()


def selected() -> list[Obj]:
    """What the window has selected now, each with its GlobalId, class, name, storey, assembly path and box."""
    return client().selected()


def measure(name: str, **definition: Any) -> Any:
    """A named measure kept by the engine (``measure.define``): see ``Lapin.measure``."""
    return client().measure(name, **definition)


def measure_value(name: str) -> Any:
    return client().measure_value(name)


def remove_measure(name: str) -> bool:
    return client().remove_measure(name)


def watch(ids: Any = None, measures: Any = None, *, script: str | None = None) -> Any:
    """Be told (the ``watch`` event) when elements or measures change; a project script's watch with ``script``."""
    return client().watch(ids, measures, script=script)


def unwatch(watch: str | None = None, *, script: str | None = None) -> Any:
    return client().unwatch(watch, script=script)


def on(event: str, handler: Callable[..., Any]) -> Callable[[], None]:
    return client().on(event, handler)


def catalogue(refresh: bool = False):
    return client().catalogue(refresh)


def ns(name: str):
    return client().ns(name)


def command(name: str):
    return client().command(name)


def __getattr__(name: str) -> Any:
    # lapin.model, lapin.architecture, lapin.element…: the module's connection's.
    if name.startswith("_"):
        raise AttributeError(name)
    return getattr(client(), name)
