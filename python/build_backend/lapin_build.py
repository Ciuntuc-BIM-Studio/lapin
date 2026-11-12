"""The package's own build backend (PEP 517 and PEP 660), on the standard library: a pure-Python wheel and an sdist,
so building lapinbim needs nothing from PyPI (no setuptools, hatchling or flit). ``pip install ./python``,
``pip install -e ./python`` and ``python -m build python`` use it; scripts/publish-python.mjs calls it directly.

The metadata comes from pyproject.toml's [project] table; the version from lapinbim/_version.py.
"""

from __future__ import annotations

import base64
import hashlib
import io
import os
import re
import tarfile
import time
import tomllib
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PACKAGE = "lapinbim"
# The sdist's and wheel's files are dated alike, so a build of the same source makes the same bytes.
EPOCH = int(os.environ.get("SOURCE_DATE_EPOCH", "1767225600"))  # 2026-01-01


def _project() -> dict:
    return tomllib.loads((ROOT / "pyproject.toml").read_text(encoding="utf-8"))["project"]


def version() -> str:
    text = (ROOT / PACKAGE / "_version.py").read_text(encoding="utf-8")
    m = re.search(r'^__version__\s*=\s*"([^"]+)"', text, re.M)
    if not m:
        raise RuntimeError("lapinbim/_version.py has no __version__.")
    return m.group(1)


def _license() -> Path | None:
    for p in (ROOT / "LICENSE", ROOT.parent / "LICENSE"):
        if p.exists():
            return p
    return None


def metadata() -> str:
    """PKG-INFO / METADATA (core metadata 2.1)."""
    p = _project()
    lines = ["Metadata-Version: 2.1", f"Name: {p['name']}", f"Version: {version()}", f"Summary: {p['description']}"]
    for a in p.get("authors", []):
        if a.get("name"):
            lines.append(f"Author: {a['name']}")
        if a.get("email"):
            lines.append(f"Author-email: {a['name']} <{a['email']}>" if a.get("name") else f"Author-email: {a['email']}")
    lic = p.get("license")
    if isinstance(lic, dict) and lic.get("text"):
        lines.append(f"License: {lic['text']}")
    if p.get("keywords"):
        lines.append("Keywords: " + ",".join(p["keywords"]))
    for c in p.get("classifiers", []):
        lines.append(f"Classifier: {c}")
    for label, url in (p.get("urls") or {}).items():
        lines.append(f"Project-URL: {label}, {url}")
    lines.append(f"Requires-Python: {p['requires-python']}")
    readme = (ROOT / p["readme"]).read_text(encoding="utf-8")
    lines.append("Description-Content-Type: text/markdown; charset=UTF-8")
    return "\n".join(lines) + "\n\n" + readme


def _files() -> list[tuple[str, Path]]:
    """The package's files: (path in the archive, file); caches and editors' leftovers left out."""
    out = []
    for f in sorted((ROOT / PACKAGE).rglob("*")):
        if f.is_dir() or "__pycache__" in f.parts or f.suffix in (".pyc", ".pyo") or f.name.startswith("."):
            continue
        out.append((f.relative_to(ROOT).as_posix(), f))
    return out


def _dist_info() -> str:
    return f"{PACKAGE}-{version()}.dist-info"


def _record_hash(data: bytes) -> str:
    return "sha256=" + base64.urlsafe_b64encode(hashlib.sha256(data).digest()).rstrip(b"=").decode("ascii")


def _wheel(wheel_directory: str, entries: list[tuple[str, bytes]]) -> str:
    name = f"{PACKAGE}-{version()}-py3-none-any.whl"
    info = _dist_info()
    entries = list(entries)
    entries.append((f"{info}/METADATA", metadata().encode("utf-8")))
    entries.append((f"{info}/WHEEL", b"Wheel-Version: 1.0\nGenerator: lapin_build\nRoot-Is-Purelib: true\nTag: py3-none-any\n"))
    lic = _license()
    if lic:
        entries.append((f"{info}/licenses/LICENSE", lic.read_bytes()))
    record = "".join(f"{path},{_record_hash(data)},{len(data)}\n" for path, data in entries) + f"{info}/RECORD,,\n"
    entries.append((f"{info}/RECORD", record.encode("utf-8")))
    Path(wheel_directory).mkdir(parents=True, exist_ok=True)
    date = time.gmtime(EPOCH)[:6]
    with zipfile.ZipFile(Path(wheel_directory) / name, "w", zipfile.ZIP_DEFLATED) as z:
        for path, data in entries:
            zi = zipfile.ZipInfo(path, date)
            zi.external_attr = 0o644 << 16
            zi.compress_type = zipfile.ZIP_DEFLATED
            z.writestr(zi, data)
    return name


# ------------------------------------------------------------------ PEP 517


def get_requires_for_build_wheel(config_settings=None):
    return []


def get_requires_for_build_sdist(config_settings=None):
    return []


def get_requires_for_build_editable(config_settings=None):
    return []


def prepare_metadata_for_build_wheel(metadata_directory, config_settings=None):
    d = Path(metadata_directory) / _dist_info()
    d.mkdir(parents=True, exist_ok=True)
    (d / "METADATA").write_text(metadata(), encoding="utf-8")
    (d / "WHEEL").write_text("Wheel-Version: 1.0\nGenerator: lapin_build\nRoot-Is-Purelib: true\nTag: py3-none-any\n", encoding="utf-8")
    return d.name


prepare_metadata_for_build_editable = prepare_metadata_for_build_wheel


def build_wheel(wheel_directory, config_settings=None, metadata_directory=None):
    return _wheel(wheel_directory, [(path, f.read_bytes()) for path, f in _files()])


def build_editable(wheel_directory, config_settings=None, metadata_directory=None):
    """A wheel that puts this folder on sys.path (a .pth file): the checkout's own code runs."""
    return _wheel(wheel_directory, [(f"_{PACKAGE}_editable.pth", (str(ROOT) + "\n").encode("utf-8"))])


def build_sdist(sdist_directory, config_settings=None):
    base = f"{PACKAGE}-{version()}"
    name = f"{base}.tar.gz"
    Path(sdist_directory).mkdir(parents=True, exist_ok=True)
    members: list[tuple[str, bytes]] = [(f"{base}/PKG-INFO", metadata().encode("utf-8"))]
    for rel in ("pyproject.toml", "README.md", "build_backend/lapin_build.py"):
        members.append((f"{base}/{rel}", (ROOT / rel).read_bytes()))
    lic = _license()
    if lic:
        members.append((f"{base}/LICENSE", lic.read_bytes()))
    members += [(f"{base}/{path}", f.read_bytes()) for path, f in _files()]
    buf = io.BytesIO()
    with tarfile.open(fileobj=buf, mode="w", format=tarfile.PAX_FORMAT) as tar:
        for path, data in members:
            ti = tarfile.TarInfo(path)
            ti.size, ti.mtime, ti.mode = len(data), EPOCH, 0o644
            tar.addfile(ti, io.BytesIO(data))
    import gzip

    (Path(sdist_directory) / name).write_bytes(gzip.compress(buf.getvalue(), mtime=EPOCH))
    return name
