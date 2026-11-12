"""Where the open Lapin's engine listens: ``LAPIN_PORT``, else the port file an engine writes while it runs.

The engine writes ``<user-dir>/host.json`` (``{"port", "pid", "product"}``) and removes it when it stops; a file left by
an engine that is gone (killed) names a dead process and is skipped. The same places as the MCP bridge
(scripts/lapin-mcp.mjs, docs/mcp.md). The engine listens on 127.0.0.1 only.
"""

from __future__ import annotations

import json
import os
import sys
from pathlib import Path

#: The port ``pnpm dev`` gives its engine, tried last.
DEV_PORT = 47810


def user_dirs() -> list[Path]:
    """The folders an engine may have written its host.json in, the likeliest first."""
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


def host_file(dirs: list[Path] | None = None) -> dict | None:
    """The first live engine's host.json, read; None when there is none."""
    for d in dirs if dirs is not None else user_dirs():
        f = d / "host.json"
        try:
            h = json.loads(f.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            continue
        if not isinstance(h, dict) or not isinstance(h.get("port"), int):
            continue
        if h.get("pid") and not alive(int(h["pid"])):
            continue
        return h
    return None


def find_port() -> int | None:
    """``LAPIN_PORT``, else the live engine's port from its host.json; None when neither says."""
    env = os.environ.get("LAPIN_PORT")
    if env:
        try:
            return int(env)
        except ValueError:
            raise ValueError(f"LAPIN_PORT is not a port: {env!r}") from None
    h = host_file()
    return h["port"] if h else None
