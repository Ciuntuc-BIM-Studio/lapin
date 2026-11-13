"""The Script panel's kernel (docs/plan-python-scripting.md §3, stage 2; docs/python.md, The Script panel).

Lapin's engine starts it with the user's own Python, isolated and unbuffered::

    python -I -B -u -c "import sys; sys.path.insert(0, sys.argv[1]); from lapinbim.kernel import main; main()" <package dir>

It reads one JSON object per line on stdin and writes one per line on stdout:

- in: ``{"op": "run", "id": n, "code": "...", "name": "walls.py", "console": false}``; ``{"op": "interrupt"}`` (Stop:
  ``KeyboardInterrupt`` in the running code); ``{"op": "exit"}``. The end of stdin ends it too.
- out: ``{"type": "ready", "python", "lapinbim"}`` once; ``{"type": "out"|"err", "id", "text"}`` for what the code
  prints; ``{"type": "done", "id", "ok", "interrupted", "kept", "error", "line", "traceback", "seconds"}`` when a run ends.

The code runs in one namespace kept from run to run (the console's names stay), with ``lapin`` bound to this package.
With an engine to connect to (``LAPIN_PORT``) each run is one step held open on it (``doc.begin {label: name}``):
``doc.commit`` when it ends well (one undo step named after the script, none when it changed nothing),
``doc.rollback`` when it raises or is stopped. A console run's last expression is shown as the Python prompt shows it.

Stage 4: a run may be a script's tool's (``"tool": {"name", "call", "main"}``): the script runs in a namespace of its
own, as ``__main__`` when the tool is the whole script (a header's ``tool=`` with no ``run=``), else as a module
(``__name__ == "__lapin_tool__"``), and then the tool's function is called (``call``, a function of the script by its
name, else the one ``lapin.ui.tool`` registered under the tool's name as the script loaded). A Run from the panel
(``"source"``: the window's key for the script) says back the tools the script registered with ``lapin.ui.tool``:
``{"type": "tools", "id", "source", "tools": [{"name", "icon", "tooltip", "call", "main"}]}``, before its ``done``.
Decision 526: an entry may name its ``panel``, ``size`` and ``menu``; ``{"kind": "menu"}`` and ``{"kind": "palette",
"rows"}`` entries are a script's pulldowns and palettes. A palette's button run is ``"tool": {"name", "call", "palette",
"values"}``: the script loaded as a module, then the button's function, given the values when it takes a parameter.

The engine gives the kernel a token (``LAPIN_SCRIPT_TOKEN``, taken out of the environment as lapinbim loads): every
connection lapinbim opens to that engine shows it (``script.join``), so one a script opens itself joins the run's step
(stage 3), and ``lapin.step()`` inside a run joins it too.
"""

from __future__ import annotations

import ast
import builtins
import json
import linecache
import os
import queue
import signal
import site
import sys
import threading
import time
import traceback
from typing import Any

import lapinbim
from lapinbim import _ui

_lock = threading.Lock()
_out = None
_current: int | None = None
# Whether the script's own code is running: a Stop interrupts only that, never the kernel's protocol or the step's end.
_armed = False
# A Stop that came while a run was starting (before its code ran): honoured as soon as the code would start.
_stop_asked = False


def _on_sigint(signum, frame) -> None:
    if _armed:
        raise KeyboardInterrupt


def _emit(msg: dict) -> None:
    line = json.dumps(msg, ensure_ascii=False, default=str)
    with _lock:
        _out.write(line + "\n")
        _out.flush()


class _Stream:
    """sys.stdout / sys.stderr while code runs: each write a line of the protocol, with the run it belongs to."""

    def __init__(self, kind: str):
        self.kind = kind
        self.encoding = "utf-8"
        self.errors = "replace"

    def write(self, text: str) -> int:
        if text:
            _emit({"type": self.kind, "id": _current, "text": text})
        return len(text)

    def flush(self) -> None:
        pass

    def isatty(self) -> bool:
        return False

    def writable(self) -> bool:
        return True


def _no_input(prompt: Any = "") -> str:
    raise RuntimeError("input() is not available in the Script panel: give the values in the script.")


def _interrupt() -> None:
    """KeyboardInterrupt in the main thread, even while it waits (a sleep, a socket): a real SIGINT where there are signals."""
    if os.name == "posix":
        os.kill(os.getpid(), signal.SIGINT)
    else:
        import _thread

        _thread.interrupt_main()


def _reader(jobs: queue.Queue) -> None:
    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            msg = json.loads(line)
        except ValueError:
            continue
        op = msg.get("op")
        if op == "run":
            jobs.put(msg)
        elif op == "interrupt":
            global _stop_asked
            if _armed:
                _interrupt()
            elif _current is not None or not jobs.empty():
                _stop_asked = True
        elif op == "exit":
            break
    jobs.put(None)


def _user_frames(tb, filename: str):
    """The traceback from the first frame of the script itself: the kernel's own frames left out."""
    while tb is not None and tb.tb_frame.f_code.co_filename != filename:
        tb = tb.tb_next
    return tb


def _failure(e: BaseException, filename: str) -> dict:
    line = None
    if isinstance(e, SyntaxError) and e.filename == filename:
        line = e.lineno
        text = "".join(traceback.format_exception_only(type(e), e))
    else:
        tb = _user_frames(e.__traceback__, filename)
        te = traceback.TracebackException(type(e), e, tb)
        # The kernel's own frames (input() refused) are not the script's.
        te.stack = traceback.StackSummary.from_list([f for f in te.stack if f.filename != __file__])
        text = "".join(te.format())
        for frame, lineno in traceback.walk_tb(tb):
            if frame.f_code.co_filename == filename:
                line = lineno
    return {"error": "".join(traceback.format_exception_only(type(e), e)).strip(), "line": line, "traceback": text}


def _run(job: dict, namespace: dict, step: bool) -> dict:
    global _armed
    code = str(job.get("code") or "")
    name = str(job.get("name") or "Script")
    console = bool(job.get("console"))
    tool = job.get("tool") if isinstance(job.get("tool"), dict) and not console else None
    source = job.get("source") if isinstance(job.get("source"), str) else None
    filename = name if not console else "<console>"
    linecache.cache[filename] = (len(code), None, code.splitlines(True), filename)
    result: dict = {"ok": True, "interrupted": False, "kept": False, "error": None, "line": None, "traceback": None}
    began = False
    try:
        if step:
            lapinbim.call("doc.begin", {"label": name})
            began = True
        tree = ast.parse(code, filename, "exec")
        echo = None
        if console and tree.body and isinstance(tree.body[-1], ast.Expr):
            echo = ast.Expression(tree.body.pop().value)
        compiled = compile(tree, filename, "exec")
        shown = compile(echo, filename, "eval") if echo is not None else None
        # What lapin.ui.tool registers while the script loads: a Run from the panel says it back; a tool's run finds its function there.
        _ui._functions = {}
        _ui._palettes = {}
        _ui._within = []
        _ui._registered = [] if not console else None
        if tool is not None:
            # A tool's run: the script in a namespace of its own, never the console's.
            namespace = {"__name__": "__main__" if tool.get("main") else "__lapin_tool__", "__builtins__": builtins, "lapin": lapinbim}
        _armed = True
        if _stop_asked:
            raise KeyboardInterrupt
        exec(compiled, namespace)
        if tool is not None and isinstance(tool.get("palette"), str):
            # A palette's button: its function, given the palette's values when it takes them.
            _ui.press(tool["palette"], str(tool.get("name") or ""), tool.get("values"), lambda: lapinbim.client()._elements())
        elif tool is not None and not tool.get("main"):
            call = tool.get("call")
            target = namespace.get(call) if call else None
            if target is None:
                target = _ui._functions.get(str(tool.get("name") or ""))
            if not callable(target):
                raise LookupError(f"The script has no tool „{tool.get('name')}”: name its function in the header (run=…) or register it with lapin.ui.tool.")
            target()
        if shown is not None:
            value = eval(shown, namespace)
            _armed = False
            if value is not None:
                namespace["_"] = value
                print(repr(value))
    except KeyboardInterrupt:
        result.update(ok=False, interrupted=True, error="Stopped.")
    except SystemExit as e:
        if e.code not in (None, 0):
            result.update(ok=False, error=f"SystemExit: {e.code}")
    except BaseException as e:  # noqa: BLE001 - the script's own error, shown in the panel
        result.update(ok=False, **_failure(e, filename))
    finally:
        _armed = False
        if began:
            try:
                if result["ok"]:
                    lapinbim.call("doc.commit")
                    result["kept"] = True
                else:
                    lapinbim.call("doc.rollback")
            except KeyboardInterrupt:
                # A Stop arriving as the step ends: taken back by the engine when the connection goes, or rolled back now.
                result.update(ok=False, interrupted=True, kept=False)
                try:
                    lapinbim.call("doc.rollback")
                except Exception:  # noqa: BLE001
                    pass
            except Exception as e:  # noqa: BLE001 - a commit the engine refused, or a lost engine
                result.update(ok=False, kept=False, error=result["error"] or str(e))
        registered, _ui._registered = _ui._registered, None
        # A Run from the panel that ended well says which tools the script has now (none: its tools go).
        if result["ok"] and tool is None and source is not None and registered is not None:
            _emit({"type": "tools", "id": _current, "source": source, "tools": registered})
    return result


def main() -> None:
    global _out, _current, _stop_asked
    # The real stdout carries the protocol; what code prints goes through it as lines.
    _out = open(sys.stdout.fileno(), "w", encoding="utf-8", buffering=1, closefd=False)
    try:
        sys.stdin.reconfigure(encoding="utf-8")
    except (AttributeError, ValueError):
        pass
    # Isolated mode leaves out the user's own site-packages (pip install --user): put back, so their packages import.
    try:
        user_site = site.getusersitepackages()
        if user_site and os.path.isdir(user_site) and user_site not in sys.path:
            site.addsitedir(user_site)
    except (AttributeError, OSError):
        pass
    signal.signal(signal.SIGINT, _on_sigint)
    namespace: dict = {"__name__": "__main__", "__builtins__": builtins, "lapin": lapinbim}
    sys.stdout = _Stream("out")
    sys.stderr = _Stream("err")
    builtins.input = _no_input
    step = bool(os.environ.get("LAPIN_PORT"))
    jobs: queue.Queue = queue.Queue()
    threading.Thread(target=_reader, args=(jobs,), name="lapinbim-kernel-stdin", daemon=True).start()
    _emit({"type": "ready", "python": "%d.%d.%d" % sys.version_info[:3], "lapinbim": lapinbim.__version__})
    while True:
        try:
            job = jobs.get()
        except KeyboardInterrupt:
            continue
        if job is None:
            break
        run_id = job.get("id")
        t0 = time.monotonic()
        try:
            _current = run_id
            result = _run(job, namespace, step)
        except KeyboardInterrupt:
            result = {"ok": False, "interrupted": True, "kept": False, "error": "Stopped.", "line": None, "traceback": None}
        finally:
            _current = None
            _stop_asked = False
        result.update(type="done", id=run_id, console=bool(job.get("console")), seconds=round(time.monotonic() - t0, 3))
        _emit(result)
    try:
        lapinbim.close()
    except Exception:  # noqa: BLE001
        pass


if __name__ == "__main__":
    main()
