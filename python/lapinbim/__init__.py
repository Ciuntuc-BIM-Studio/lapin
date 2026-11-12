"""lapinbim: drive Lapin, the open BIM modeller on IFC, from Python (docs/python.md).

::

    import lapinbim as lapin

    w = lapin.architecture.add_wall(x1=0, y1=0, x2=5, y2=0, height=2.8)   # one undo step
    with lapin.batch("Ring of walls"):                                    # one undo step for all, sent at the end
        for a, b in [((5, 0), (5, 4)), ((5, 4), (0, 4)), ((0, 4), (0, 0))]:
            lapin.architecture.add_wall(x1=a[0], y1=a[1], x2=b[0], y2=b[1])
    for wall in lapin.model.walls:
        print(wall.name, wall.quantities())

The module's functions use one connection, made at the first call (``LAPIN_PORT``, else the open engine's host.json);
``lapin.connect(port)`` makes it explicitly, ``lapin.Lapin(port)`` makes another. Standard library only.
"""

from __future__ import annotations

from typing import Any, Callable

from ._version import __version__
from ._client import Batch, Deferred, Lapin, LapinError, Step
from ._frame import Obj
from ._model import Element, Model

__all__ = [
    "__version__", "Lapin", "LapinError", "Batch", "Deferred", "Step", "Element", "Model", "Obj",
    "connect", "client", "close", "call", "execute", "query", "undo", "redo", "save", "info", "quantities",
    "snapshot", "state", "batch", "step", "selection", "on", "catalogue", "ns", "command",
]

_default: Lapin | None = None


def connect(port: int | None = None, **options: Any) -> Lapin:
    """The module's connection, made anew (to ``port``, else the open engine)."""
    global _default
    if _default is not None:
        _default.close()
    _default = Lapin(port, **options)
    return _default


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
