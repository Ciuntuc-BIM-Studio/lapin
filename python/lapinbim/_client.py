"""The connection to Lapin's engine: JSON-RPC over a WebSocket on 127.0.0.1, replies in LPF1 frames.

Every Lapin tool is an engine command with JSON arguments and one undo step (``doc.execute``); questions that change
nothing are queries (``doc.query``). A script's edits go through the same engine as the window's, so they show at once
and undo like the user's own.
"""

from __future__ import annotations

import json
import os
import queue
import sys
import threading
import traceback
from typing import Any, Callable

from . import _discovery as discovery
from ._catalogue import Catalogue, Namespace, bind, bundled, merge
from ._frame import Obj, decode
from ._model import Element, Model
from ._ws import TEXT, ConnectionClosed, WebSocket


# The Script panel's kernel: the engine that started it gives its Python a token (docs/python.md, The kernel's own
# connections). Each connection made here to that engine shows it (script.join), so one a script opens itself
# (lapinbim.Lapin()) joins its run's step. Taken out of the environment as the package loads: a process the script starts
# does not inherit it.
_KERNEL_TOKEN = os.environ.pop("LAPIN_SCRIPT_TOKEN", None)
_KERNEL_PORT = os.environ.get("LAPIN_PORT") if _KERNEL_TOKEN else None


class LapinError(Exception):
    """The engine refused a call (its message is the user's), or there is no engine to call."""

    def __init__(self, message: str, method: str | None = None, name: str | None = None):
        super().__init__(message)
        self.method = method
        self.name = name


def _jsonable(o: Any) -> Any:
    """What json cannot write as it is: an element as its id, an array view as a list, a numpy value as a number."""
    if isinstance(o, Element):
        return o.id
    if isinstance(o, Deferred):
        return o.result
    if isinstance(o, memoryview):
        return o.tolist()
    if isinstance(o, (set, frozenset)):
        return list(o)
    if hasattr(o, "tolist"):
        return o.tolist()
    if hasattr(o, "__float__"):
        return float(o)
    raise TypeError(f"{type(o).__name__} cannot be sent to Lapin.")


class _Slot:
    __slots__ = ("done", "msg", "error")

    def __init__(self) -> None:
        self.done = threading.Event()
        self.msg: Any = None
        self.error: BaseException | None = None


class Deferred:
    """A command's result inside ``with lapin.batch():``, known once the batch has run: ``w.id`` after the block."""

    __slots__ = ("command", "_value", "_ready")

    def __init__(self, command: str):
        self.command = command
        self._value: Any = None
        self._ready = False

    def _set(self, value: Any) -> None:
        self._value, self._ready = value, True

    @property
    def ready(self) -> bool:
        return self._ready

    @property
    def result(self) -> Any:
        if not self._ready:
            raise LapinError(f"{self.command}'s result is known when the batch ends (after the with block).")
        return self._value

    def __getattr__(self, name: str) -> Any:
        return getattr(self.result, name)

    def __getitem__(self, key: Any) -> Any:
        return self.result[key]

    def __repr__(self) -> str:
        return f"<{self.command}: {self._value!r}>" if self._ready else f"<{self.command}: pending until the batch ends>"


class Batch:
    """Commands gathered and sent as one ``edit.batch``: one undo step, named ``label``, all or nothing.

    ::

        with lapin.batch("Facades") as b:
            for x in range(10):
                lapin.architecture.add_wall(x1=x, y1=0, x2=x, y2=3)
        print(b.results)

    Inside the block a command returns a ``Deferred``, filled when the block ends; queries go at once (they see the
    model before the batch). A batch inside a batch joins the outer one. An exception in the block sends nothing.
    """

    def __init__(self, client: "Lapin", label: str | None = None):
        self._client = client
        self.label = label
        self.steps: list[dict] = []
        self.deferred: list[Deferred] = []
        self.results: list[Any] | None = None
        self._outer: Batch | None = None

    def add(self, command: str, args: dict) -> Deferred:
        d = Deferred(command)
        self.steps.append({"command": command, "args": args})
        self.deferred.append(d)
        return d

    def __enter__(self) -> "Batch":
        self._outer = self._client._batch()
        if self._outer is None:
            self._client._local.batch = self
        return self._outer or self

    def __exit__(self, kind, value, tb) -> None:
        if self._outer is not None:
            return
        self._client._local.batch = None
        if kind is not None or not self.steps:
            return
        args: dict[str, Any] = {"steps": self.steps}
        if self.label:
            args["label"] = self.label
        results = self._client.execute("edit.batch", args) or []
        for d, r in zip(self.deferred, results):
            d._set(r)
        self.results = list(results)


class Step:
    """A step held open on the engine: every command inside, each run at once, is one undo step named ``label``.

    ::

        with lapin.step("Facades"):
            w = lapin.architecture.add_wall(x1=0, y1=0, x2=5, y2=0)   # its result now: w.id
            lapin.architecture.add_door(wall_id=w.id, offset=1.2)

    Unlike a batch, each command answers at once (a later one may use an id an earlier one made); while it is open no
    other connection edits (the window says a script is editing). It ends with ``doc.commit`` when the block ends well
    and ``doc.rollback`` when it raises (the exception goes on): the model as it was. A step inside a step joins it, as
    the Script panel's Run is one: only the outermost decides. A connection lost rolls it back. A batch may go inside a
    step, not a step inside a batch.
    """

    def __init__(self, client: "Lapin", label: str | None = None):
        self._client = client
        self.label = label or "Script"
        self.joined = False
        self.kept: bool | None = None

    def __enter__(self) -> "Step":
        if self._client._batch() is not None:
            raise LapinError("lapin.step inside a batch: end the batch first (a batch may go inside a step).")
        r = self._client.call("doc.begin", {"label": self.label})
        self.joined = bool(isinstance(r, dict) and r.get("joined"))
        return self

    def __exit__(self, kind, value, tb) -> None:
        if kind is None:
            self._client.call("doc.commit")
            self.kept = not self.joined
            return
        self.kept = False
        try:
            self._client.call("doc.rollback")
        except Exception:  # noqa: BLE001 - the block's own exception is what goes on; a lost engine rolled it back already
            pass


class Lapin:
    """One connection to an open Lapin (the desktop app, or ``pnpm dev``).

    ``Lapin()`` finds the engine (``LAPIN_PORT``, else its ``host.json``); ``Lapin(port)`` names it. Calls are made one
    at a time, from any thread; the engine sends every change as a ``diff`` event, which ``on("diff", f)`` receives.
    """

    def __init__(self, port: int | None = None, *, connect_timeout: float = 5.0):
        tried = port or discovery.find_port()
        candidates = [tried] if tried else [discovery.DEV_PORT]
        last: Exception | None = None
        ws: WebSocket | None = None
        for p in candidates:
            try:
                ws = WebSocket("127.0.0.1", p, timeout=connect_timeout)
                self.port = p
                break
            except OSError as e:
                last = e
        if ws is None:
            if tried:
                raise LapinError(f"Lapin's engine did not answer on port {tried}: {last}")
            raise LapinError("No open Lapin found: start the app (or pnpm dev), or set LAPIN_PORT.")
        self._ws: WebSocket = ws
        self._next = 1
        self._pending: dict[int, _Slot] = {}
        self._pending_lock = threading.Lock()
        self._call_lock = threading.RLock()
        self._local = threading.local()
        self._handlers: dict[str, list[Callable[[Any], Any]]] = {}
        self._events: queue.Queue = queue.Queue()
        self._dispatcher: threading.Thread | None = None
        self._collecting: list | None = None
        self._dirty = True
        self._state: Any = None
        self._elements_cache: dict[str, Element] | None = None
        self._catalogue: Catalogue | None = None
        self.closed = False
        self._reader = threading.Thread(target=self._read, name="lapinbim-reader", daemon=True)
        self._reader.start()
        # In the Script panel: one of its Python's connections, so it edits in the run's step (docs/python.md).
        self.joined = False
        if _KERNEL_TOKEN and _KERNEL_PORT == str(self.port):
            try:
                self.call("script.join", {"token": _KERNEL_TOKEN}, timeout=connect_timeout)
                self.joined = True
            except LapinError:
                pass

    # ------------------------------------------------------------------ the wire

    def _read(self) -> None:
        error: BaseException = LapinError("The engine closed.")
        try:
            while True:
                opcode, data = self._ws.recv()
                try:
                    msg = decode(data.decode("utf-8") if opcode == TEXT else data)
                except ValueError as e:
                    print(f"lapinbim: a message from the engine could not be read: {e}", file=sys.stderr)
                    continue
                if not isinstance(msg, dict):
                    continue
                if "event" in msg:
                    self._event(msg["event"], msg.get("data"))
                    continue
                with self._pending_lock:
                    key = msg.get("id")
                    slot = self._pending.pop(key, None) if isinstance(key, int) else None
                if slot is not None:
                    slot.msg = msg
                    slot.done.set()
        except (ConnectionClosed, OSError) as e:
            if not self.closed:
                error = LapinError(f"The engine closed ({e}).")
        finally:
            self.closed = True
            with self._pending_lock:
                slots = list(self._pending.values())
                self._pending.clear()
            for s in slots:
                s.error = error
                s.done.set()
            self._events.put(None)

    def _event(self, name: str, data: Any) -> None:
        if name == "diff":
            self._dirty = True
            if isinstance(data, dict) and data.get("state") is not None:
                self._state = data["state"]
            if self._collecting is not None:
                self._collecting.append(data)
        if self._handlers.get(name) or self._handlers.get("*"):
            self._events.put((name, data))

    def _dispatch(self) -> None:
        while True:
            item = self._events.get()
            if item is None:
                return
            name, data = item
            calls = [(f, (data,)) for f in self._handlers.get(name, ())] + [(f, (name, data)) for f in self._handlers.get("*", ())]
            for f, args in calls:
                try:
                    f(*args)
                except Exception:
                    traceback.print_exc()

    def call(self, method: str, params: dict | None = None, timeout: float | None = None) -> Any:
        """One protocol call (``doc.execute``, ``doc.query``, ``session.info``…): its result, or LapinError."""
        with self._call_lock:
            if self.closed:
                raise LapinError("The connection to Lapin is closed.", method)
            id = self._next
            self._next += 1
            slot = _Slot()
            with self._pending_lock:
                self._pending[id] = slot
            text = json.dumps({"id": id, "method": method, "params": params or {}}, default=_jsonable, separators=(",", ":"), ensure_ascii=False)
            try:
                self._ws.send_text(text)
            except OSError as e:
                with self._pending_lock:
                    self._pending.pop(id, None)
                raise LapinError(f"The engine closed ({e}).", method) from e
            if not slot.done.wait(timeout):
                with self._pending_lock:
                    self._pending.pop(id, None)
                raise LapinError(f"{method} had no answer in {timeout} s.", method)
            if slot.error is not None:
                raise slot.error
            msg = slot.msg
            if msg.get("error"):
                raise LapinError(msg["error"], method, (params or {}).get("command") or (params or {}).get("name"))
            return msg.get("result")

    # ------------------------------------------------------------------ layer 1: the raw calls

    def _batch(self) -> Batch | None:
        return getattr(self._local, "batch", None)

    def execute(self, command: str, args: dict | None = None) -> Any:
        """One command, one undo step: ``execute("architecture.addWall", {"x1": 0, "y1": 0, "x2": 5, "y2": 0})``.

        Returns what the command returns (often ``{"id": …}``); inside ``with batch():`` a Deferred.
        """
        b = self._batch()
        if b is not None:
            return b.add(command, dict(args or {}))
        return self.call("doc.execute", {"command": command, "args": args or {}})

    def query(self, name: str, args: dict | None = None) -> Any:
        """A question that changes nothing: ``query("schedule.csv", {"id": …})``."""
        return self.call("doc.query", {"name": name, "args": args or {}})

    def undo(self) -> None:
        if self._batch() is not None:
            raise LapinError("Undo inside a batch: end the batch first.")
        self.call("doc.undo")

    def redo(self) -> None:
        if self._batch() is not None:
            raise LapinError("Redo inside a batch: end the batch first.")
        self.call("doc.redo")

    def save(self, path: str | None = None) -> Any:
        """Save the project: to its own file, or to the ``.lapin`` path given (absolute)."""
        return self.call("project.save", {"path": str(path)} if path else {})

    def info(self) -> Obj:
        """The product, its modules, the commands (name, label) and the classes with their fields (``session.info``)."""
        return self.call("session.info")

    def quantities(self, id: str | Element) -> Any:
        """An element's quantity sets."""
        return self.call("quantities.of", {"id": id.id if isinstance(id, Element) else id})

    def snapshot(self) -> Obj:
        """The whole model as the engine sends it to a window (``doc.snapshot``): nodes, relations, render, state.

        A big model comes in parts (the first as events before the reply): they are joined here.
        """
        with self._call_lock:
            self._collecting = []
            try:
                last = self.call("doc.snapshot")
            finally:
                parts, self._collecting = self._collecting, None
        start = max((i for i, d in enumerate(parts) if isinstance(d, dict) and d.get("reset")), default=None)
        chunks = (parts[start:] if start is not None else []) + [last]
        out = Obj(last)
        if last.get("state") is not None:
            self._state = last["state"]
        for key in ("nodes", "relations", "render"):
            out[key] = [x for c in chunks for x in (c.get(key) or [])]
        return out

    def state(self) -> Obj:
        """The document's state: canUndo, canRedo, undoLabel, redoLabel, dirty, path. Every diff carries it (one comes
        before the reply of each edit), so it is the last one's; before any, a snapshot's."""
        if self._state is None:
            self._state = self.snapshot().get("state")
        return self._state or Obj()

    def batch(self, label: str | None = None) -> Batch:
        """``with lapin.batch("Facades"):`` the commands inside as one undo step (see Batch)."""
        return Batch(self, label)

    def step(self, label: str | None = None) -> Step:
        """``with lapin.step("Facades"):`` the commands inside, each answered at once, as one undo step (see Step)."""
        return Step(self, label)

    def selection(self) -> list[Element]:
        """What the window has selected now, as elements (a part selected on its own, a grid's axis, is left out).
        The desktop's Lapin with the scripting module tells it; elsewhere a LapinError says why."""
        ids = (self.call("script.selection") or {}).get("ids") or []
        elements = self._elements()
        return [elements[i] for i in ids if i in elements]

    def on(self, event: str, handler: Callable[[Any], Any]) -> Callable[[], None]:
        """``handler(data)`` for each ``event`` the engine sends ("diff", "progress", "presence", "delivery"); "*" gets
        ``handler(name, data)`` for all. Handlers run one at a time on a thread of their own. Returns what removes it."""
        self._handlers.setdefault(event, []).append(handler)
        if self._dispatcher is None:
            self._dispatcher = threading.Thread(target=self._dispatch, name="lapinbim-events", daemon=True)
            self._dispatcher.start()

        def off() -> None:
            if handler in self._handlers.get(event, []):
                self._handlers[event].remove(handler)

        return off

    def close(self) -> None:
        if self.closed:
            return
        self.closed = True
        self._ws.close()
        self._reader.join(timeout=2)

    def __enter__(self) -> "Lapin":
        return self

    def __exit__(self, *exc) -> None:
        self.close()

    # ------------------------------------------------------------------ layer 2: typed, from the catalogue

    def catalogue(self, refresh: bool = False) -> Catalogue:
        """The commands, queries and classes this engine has, with their arguments (``session.catalogue`` and ``session.info``)."""
        if self._catalogue is None or refresh:
            info = self.info()
            try:
                served = self.call("session.catalogue")
            except LapinError:
                served = bundled()  # an engine from before the catalogue
            self._catalogue = Catalogue(merge(info, served))
        return self._catalogue

    def ns(self, name: str) -> Namespace:
        """A namespace by its engine prefix or Python name (``ns("cutOrder")``, ``ns("architecture")``)."""
        calls = self.catalogue().namespace(name)
        if calls is None:
            raise AttributeError(f"Lapin has no commands under {name!r}.")
        return Namespace(self, name, calls)

    def command(self, name: str) -> Callable[..., Any]:
        """A command or query by its engine name (``command("architecture.addWall")``), typed as the namespaces are."""
        e = self.catalogue().entry(name)
        if e is None:
            raise AttributeError(f"Lapin has no command or query {name!r}.")
        return bind(self, e)

    @property
    def model(self) -> Model:
        """The open model's entities (``lapin.model.walls``)."""
        return Model(self)

    def _elements(self) -> dict[str, Element]:
        if self._elements_cache is None or self._dirty:
            # Cleared before asking: a diff while the snapshot is on its way marks it stale again.
            self._dirty = False
            nodes = self.snapshot().get("nodes") or []
            self._elements_cache = {n["id"]: Element(self, n) for n in nodes}
        return self._elements_cache

    def __getattr__(self, name: str) -> Namespace:
        if name.startswith("_"):
            raise AttributeError(name)
        return self.ns(name)

    def __dir__(self):
        names = list(super().__dir__())
        if self._catalogue is not None:
            names += list(self._catalogue.namespaces)
        return names

    def __repr__(self) -> str:
        return f"<Lapin on 127.0.0.1:{self.port}{' (closed)' if self.closed else ''}>"
