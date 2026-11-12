"""An engine of the tests' own: the built host (engine/src/Host/Lapin.Host, Debug) on a free port with a temporary user
folder, as scripts/lapin-mcp-check.mjs starts it; never the engine of ``pnpm dev``. One for the whole run, stopped at exit.

What it needs (dotnet, node for the product's manifest, a built engine) missing: ``require()`` skips the test, saying why.
"""

from __future__ import annotations

import atexit
import json
import os
import shutil
import socket
import subprocess
import tempfile
import threading
import time
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
HOST = ROOT / "engine/src/Host/Lapin.Host/bin/Debug/net10.0/Lapin.Host.dll"
MODULES = ROOT / "engine/artifacts/modules"

_engine: "Engine | None" = None
_why: str | None = None


def free_port() -> int:
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]


class Engine:
    def __init__(self, product: str = "lapin"):
        self.dir = Path(tempfile.mkdtemp(prefix="lapinbim-test-"))
        self.user_dir = self.dir / "user"
        manifest = self.dir / "product.json"
        script = (
            f"import {{ resolveProduct }} from {json.dumps((ROOT / 'scripts/product.mjs').as_uri())};"
            f"process.stdout.write(JSON.stringify(resolveProduct({json.dumps(str(ROOT))}, {json.dumps(product)})));"
        )
        manifest.write_text(subprocess.run(["node", "--input-type=module", "-e", script], check=True, capture_output=True, text=True).stdout)
        self.port = free_port()
        self.proc = subprocess.Popen(
            ["dotnet", str(HOST), "--product", str(manifest), "--modules-dir", str(MODULES), "--user-dir", str(self.user_dir),
             "--port", str(self.port), "--parent-pid", str(os.getpid())],
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True,
        )
        seen: list[str] = []
        ready = threading.Event()

        def pump() -> None:
            for line in self.proc.stdout:
                seen.append(line)
                if "LAPIN_HOST_READY" in line:
                    ready.set()

        threading.Thread(target=pump, daemon=True).start()
        deadline = time.time() + 90
        while not ready.wait(0.2):
            if self.proc.poll() is not None or time.time() > deadline:
                self.stop()
                raise RuntimeError("the engine did not start: " + "".join(seen)[-800:])

    def stop(self) -> None:
        if self.proc.poll() is None:
            self.proc.terminate()
            try:
                self.proc.wait(10)
            except subprocess.TimeoutExpired:
                self.proc.kill()
        shutil.rmtree(self.dir, ignore_errors=True)


def require() -> Engine:
    """The tests' engine, started at the first call; the test skipped when it cannot be."""
    global _engine, _why
    if _engine is not None:
        return _engine
    if _why is None:
        if os.environ.get("LAPINBIM_NO_ENGINE"):
            _why = "LAPINBIM_NO_ENGINE is set"
        elif not shutil.which("dotnet"):
            _why = "dotnet is not installed"
        elif not shutil.which("node"):
            _why = "node is not installed"
        elif not HOST.exists() or not (MODULES / "architecture").exists():
            _why = f"the engine is not built ({HOST.relative_to(ROOT)}): pnpm build:engine"
        else:
            try:
                _engine = Engine()
                atexit.register(_engine.stop)
                return _engine
            except (RuntimeError, OSError, subprocess.CalledProcessError) as e:
                _why = f"the engine did not start: {e}"
    raise unittest.SkipTest(f"no engine for the tests: {_why}")
