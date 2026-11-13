"""Where the open Lapins' engines listen (docs/python.md, How a program finds Lapin).

An engine started without a port of its own (the desktop app's) listens on the first free of 14912 (L A P I N), 14913,
… 15011, on 127.0.0.1 only; ``pnpm dev``'s on 47810. A program finds them, in this order:

1. ``LAPIN_PORT``: that port, and no other (the Script panel's Python is given it).
2. The host files the engines write while they run: ``<user-dir>/hosts/host-<pid>.json`` (one per engine) and
   ``<user-dir>/host.json`` (the latest started; what engines before 14912 wrote). A file whose process is gone (killed)
   is stale and skipped. ``LAPIN_USER_DIR`` names the one folder to read, and then nothing is probed.
3. The ports 14912–15011 and 47810, probed: a fast connect and ``GET /health``, which a Lapin engine answers with
   ``{"engine": "lapin", "product", "version", "pid", "port", "project"}`` (the WebSocket call ``engine.hello`` answers the
   same), so another program listening on one of them is never taken for Lapin.

The same as the MCP bridge's (scripts/lapin-mcp.mjs, docs/mcp.md).
"""

from __future__ import annotations

import http.client
import json
import os
import sys
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

#: The first port of an engine without one of its own: L A P I N = 12 1 16 9 14 → 1 4 9 1 2.
FIRST_PORT = 14912
#: How many ports such an engine tries: 14912–15011.
PORT_COUNT = 100
#: The port ``pnpm dev`` gives its engine.
DEV_PORT = 47810
#: How long a probe waits for a port to answer (seconds).
PROBE_TIMEOUT = 0.5


def ports() -> list[int]:
    """The ports probed for Lapin: 14912–15011, then ``pnpm dev``'s."""
    return list(range(FIRST_PORT, FIRST_PORT + PORT_COUNT)) + [DEV_PORT]


def user_dirs() -> list[Path]:
    """The folders an engine may have written its host files in, the likeliest first."""
    home = Path.home()
    dirs = [
        os.environ.get("LAPIN_USER_DIR"),
        home / "Library" / "Application Support" / "ro.ciuntucbimstudio.lapin",
        home / ".local" / "share" / "Lapin",
        home / "AppData" / "Local" / "Lapin",
        os.environ.get("LOCALAPPDATA") and Path(os.environ["LOCALAPPDATA"]) / "Lapin",
    ]
    out: list[Path] = []
    for d in dirs:
        if d and Path(d) not in out:
            out.append(Path(d))
    return out


def alive(pid: int) -> bool:
    """Whether the process ``pid`` runs (on Windows without signals: os.kill there would end it)."""
    if pid <= 0:
        return False
    if sys.platform == "win32":
        import ctypes

        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)  # type: ignore[attr-defined]
        handle = kernel32.OpenProcess(0x1000, False, pid)  # PROCESS_QUERY_LIMITED_INFORMATION
        if not handle:
            return ctypes.get_last_error() == 5  # access denied: it exists
        try:
            code = ctypes.c_ulong()
            return bool(kernel32.GetExitCodeProcess(handle, ctypes.byref(code))) and code.value == 259  # STILL_ACTIVE
        finally:
            kernel32.CloseHandle(handle)
    try:
        os.kill(pid, 0)
        return True
    except ProcessLookupError:
        return False
    except PermissionError:
        return True
    except OSError:
        return False


def _read(f: Path) -> dict | None:
    try:
        h = json.loads(f.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None
    if not isinstance(h, dict) or not isinstance(h.get("port"), int):
        return None
    if h.get("pid") and not alive(int(h["pid"])):
        return None  # stale: its engine is gone
    return h


def host_files(dirs: list[Path] | None = None) -> list[dict]:
    """The live engines' host files (each engine's, then host.json), one per port, the likeliest folder first."""
    out: list[dict] = []
    seen: set[int] = set()
    for d in dirs if dirs is not None else user_dirs():
        files = sorted((d / "hosts").glob("host-*.json")) if (d / "hosts").is_dir() else []
        for f in [*files, d / "host.json"]:
            h = _read(f)
            if h and h["port"] not in seen:
                seen.add(h["port"])
                out.append(h)
    return out


def host_file(dirs: list[Path] | None = None) -> dict | None:
    """The first live engine's host file, read; None when there is none."""
    found = host_files(dirs)
    return found[0] if found else None


def identify(port: int, timeout: float = PROBE_TIMEOUT) -> dict | None:
    """What the engine on ``port`` says it is (``GET /health``); None when nothing answers there or it is not Lapin."""
    conn = http.client.HTTPConnection("127.0.0.1", port, timeout=timeout)
    try:
        conn.request("GET", "/health", headers={"Host": f"127.0.0.1:{port}"})
        r = conn.getresponse()
        if r.status != 200:
            return None
        body = json.loads(r.read(65536).decode("utf-8"))
    except (OSError, ValueError, http.client.HTTPException):
        return None
    finally:
        conn.close()
    if not isinstance(body, dict):
        return None
    if body.get("engine") == "lapin":
        body["port"] = port
        return body
    # An engine from before engine.hello: {"ok", "product", "modules"}.
    if body.get("ok") is True and isinstance(body.get("product"), str) and isinstance(body.get("modules"), list):
        return {"engine": "lapin", "product": body["product"], "port": port, "modules": body["modules"], "project": None}
    return None


def find_engines(timeout: float = PROBE_TIMEOUT, dirs: list[Path] | None = None) -> list[dict]:
    """Every live Lapin engine on this computer, by port: the host files' and the probed ports', each confirmed by what
    it answers. With ``LAPIN_USER_DIR`` (or ``dirs``) only that folder's host files, nothing probed."""
    if dirs is None and os.environ.get("LAPIN_USER_DIR"):
        dirs = [Path(os.environ["LAPIN_USER_DIR"])]
        probe = False
    else:
        probe = dirs is None
    candidates: list[int] = [h["port"] for h in host_files(dirs)]
    if probe:
        candidates += [p for p in ports() if p not in candidates]
    with ThreadPoolExecutor(max_workers=32) as pool:
        found = list(pool.map(lambda p: identify(p, timeout), candidates))
    return sorted((e for e in found if e), key=lambda e: e["port"])


def describe(e: dict) -> str:
    """One engine, for a message: its port, product and project."""
    project = e.get("project") or "no project saved"
    return f"port {e['port']}: {e.get('product', '?')}, {project}"


def _matches(e: dict, project: str) -> bool:
    path = e.get("project")
    if not path:
        return False
    want = project.lower()
    p = Path(path)
    return (
        os.path.normcase(os.path.abspath(project)) == os.path.normcase(os.path.abspath(path))
        or p.name.lower() == want
        or p.stem.lower() == want
        or want in path.lower()
    )


def choose(engines: list[dict], project: str | None = None, product: str | None = None) -> dict:
    """The engine to connect to: the only one, or the one whose project (its path, file name or a part of the path)
    and product match. LookupError, naming them, when none or several are left."""
    left = [e for e in engines if (product is None or e.get("product") == product) and (project is None or _matches(e, project))]
    if len(left) == 1:
        return left[0]
    if not engines:
        raise LookupError("No open Lapin found: start the app (or pnpm dev), or set LAPIN_PORT.")
    listed = "; ".join(describe(e) for e in engines)
    if not left:
        wanted = " and ".join(x for x in [project and f"project {project!r}", product and f"product {product!r}"] if x)
        raise LookupError(f"No open Lapin has {wanted}. Open: {listed}.")
    raise LookupError(f"{len(left)} Lapins are open ({'; '.join(describe(e) for e in left)}): name one with "
                      f"Lapin(port), Lapin(project=...), Lapin(product=...) or LAPIN_PORT.")


def env_port() -> int | None:
    """``LAPIN_PORT``, when set."""
    env = os.environ.get("LAPIN_PORT")
    if not env:
        return None
    try:
        return int(env)
    except ValueError:
        raise ValueError(f"LAPIN_PORT is not a port: {env!r}") from None


def find_port(project: str | None = None, product: str | None = None) -> int | None:
    """``LAPIN_PORT``, else the one open engine's port (or the one matching ``project``/``product``); None when there is
    none; LookupError when several are open and nothing tells which."""
    env = env_port()
    if env is not None:
        return env
    engines = find_engines()
    if not engines:
        return None
    return choose(engines, project, product)["port"]
