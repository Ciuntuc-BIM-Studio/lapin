"""``python -m lapinbim``: what the open Lapin has, its catalogue, and stubs for editors.

::

    python -m lapinbim info                       the product, its version, how many commands, queries and classes
    python -m lapinbim stubs [--out DIR]          __init__.pyi from the open Lapin's catalogue (default: beside the package)
    python -m lapinbim stubs --catalogue FILE     … from a catalogue file instead
    python -m lapinbim catalogue [--out FILE]     the open Lapin's catalogue as JSON (commands, queries, classes)
    python -m lapinbim engines                    the open Lapins: port, product, project
    python -m lapinbim version

``--port N`` names the engine (else LAPIN_PORT, else the one open: docs/python.md, How a program finds Lapin);
``--project NAME`` or ``--product ID`` picks one of several.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from . import __version__
from . import _discovery as discovery
from ._catalogue import Catalogue
from ._client import Lapin, LapinError
from ._stubs import generate


def _live(a: argparse.Namespace) -> dict:
    with _open(a) as lapin:
        return lapin.catalogue().data


def _open(a: argparse.Namespace) -> Lapin:
    return Lapin(a.port, project=a.project, product=a.product)


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(prog="python -m lapinbim", description="Drive Lapin from Python (docs/python.md).")
    sub = p.add_subparsers(dest="what", required=True)
    for name in ("info", "stubs", "catalogue", "engines", "version"):
        s = sub.add_parser(name)
        s.add_argument("--port", type=int)
        s.add_argument("--project")
        s.add_argument("--product")
        if name in ("stubs", "catalogue"):
            s.add_argument("--out")
        if name == "stubs":
            s.add_argument("--catalogue", dest="source")
    a = p.parse_args(argv)
    try:
        if a.what == "version":
            print(__version__)
        elif a.what == "engines":
            found = discovery.find_engines()
            for e in found:
                print(f"{discovery.describe(e)} (Lapin {e.get('version', '?')}, pid {e.get('pid', '?')})")
            if not found:
                print("No open Lapin found.", file=sys.stderr)
                return 1
        elif a.what == "info":
            with _open(a) as lapin:
                c = lapin.catalogue()
                print(f"Lapin {c.version} ({c.data.get('product')}) on port {lapin.port}: "
                      f"{len(c.data['commands'])} commands, {len(c.data['queries'])} queries, {len(c.classes)} classes, "
                      f"{len(c.namespaces)} namespaces")
        elif a.what == "catalogue":
            text = json.dumps(_live(a), ensure_ascii=False, indent=1) + "\n"
            if a.out:
                Path(a.out).write_text(text, encoding="utf-8")
                print(f"catalogue → {a.out}")
            else:
                sys.stdout.write(text)
        elif a.what == "stubs":
            data = json.loads(Path(a.source).read_text(encoding="utf-8")) if a.source else _live(a)
            out = Path(a.out) if a.out else Path(__file__).parent
            out.mkdir(parents=True, exist_ok=True)
            target = out / "__init__.pyi"
            target.write_text(generate(Catalogue(data)), encoding="utf-8")
            print(f"stubs → {target}")
    except LapinError as e:
        print(f"lapinbim: {e}", file=sys.stderr)
        return 1
    except OSError as e:
        print(f"lapinbim: {e}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
