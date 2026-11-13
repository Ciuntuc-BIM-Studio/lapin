"""An optional sketch: a Lite-STEP model's elements pushed into the open Lapin, kept up to date as the model file changes.

Lite-STEP (https://github.com/ginnung-tech/lite-step, MIT, Copyright (c) 2026 David Arbirk) is a Python DSL that
compiles a building to IFC4X3 with ifcopenshell. It runs as its own process: neither Lapin nor ``lapinbim`` depends on it
or on ifcopenshell, and nothing of it is copied here. This file is Lapin's (MIT) and imports them only when it runs::

    pip install lapinbim
    pip install -r <lite-step checkout>/requirements.txt       # lite_step's own: ifcopenshell, manifold3d, numpy, …
    cd <lite-step checkout>                                     # so that lite_step imports, as its README says
    python /path/to/lapin-pro/python/examples/litestep_bridge.py output.py           # once
    python /path/to/lapin-pro/python/examples/litestep_bridge.py output.py --watch   # again at every save

What it does (docs/plan-external-geometry.md):
1. runs the model file as a module (not as ``__main__``) and takes its ``result`` (the Project);
2. compiles it with Lite-STEP's own compiler to an IFC file in a temporary folder (Lapin never reads that IFC as a
   project: it only lets ifcopenshell mesh each product, in world coordinates, metres);
3. for each product, ``element.createBody`` (or ``element.setBody`` when it is there already), its IFC class as Lite-STEP
   wrote it, on the Lapin storey of the same name when there is one, its GlobalId made from the product's name
   (``lapinbim.global_id(…, "lite-step")``: Lite-STEP's own GlobalIds change at every compile, the name does not), owner
   ``lite-step:<model file>``; all of it in one undo step (``lapin.step``);
4. elements it made before whose product is gone are deleted in the same step.

A sketch, not a product: one body per product (openings already carved by Lite-STEP's compiler), no types, materials or
property sets carried; the starting point for a bridge of Lite-STEP's own.
"""

from __future__ import annotations

import argparse
import os
import runpy
import sys
import tempfile
import time
from pathlib import Path

import lapinbim as lapin


def compile_model(model: Path) -> Path:
    """The model file run as a module and compiled by Lite-STEP: the IFC file written."""
    try:
        from lite_step import compile_main
    except ImportError:
        sys.exit("lite_step is not importable: run this from a Lite-STEP checkout (or install it) — Lapin does not ship it.")
    globals_ = runpy.run_path(str(model), run_name="litestep_model")
    project = globals_.get("result")
    if project is None:
        sys.exit(f"{model} defines no `result` (its Project).")
    out = Path(tempfile.mkdtemp(prefix="lapin-litestep-")) / "model.py"
    argv = sys.argv
    sys.argv = [str(out)]                    # compile_main reads its own flags from the command line
    try:
        path = compile_main(result=project, source_path=str(out), output_name="model.ifc")
    finally:
        sys.argv = argv
    if path is None:
        sys.exit("Lite-STEP did not write the IFC (its messages say why).")
    return Path(path)


def products(ifc_path: Path):
    """Each product with a body: (key, IFC class, name, storey name, vertices N×3 in metres, faces M×3)."""
    import ifcopenshell
    import ifcopenshell.geom
    import ifcopenshell.util.element

    f = ifcopenshell.open(str(ifc_path))
    settings = ifcopenshell.geom.settings()
    settings.set(settings.USE_WORLD_COORDS, True)
    it = ifcopenshell.geom.iterator(settings, f, max(1, (os.cpu_count() or 2) - 1))
    seen: dict[str, int] = {}
    if not it.initialize():
        return
    while True:
        shape = it.get()
        product = f.by_id(shape.id)
        verts = list(shape.geometry.verts)
        faces = list(shape.geometry.faces)
        if faces:
            storey = ifcopenshell.util.element.get_container(product)
            storey_name = storey.Name if storey is not None and storey.is_a("IfcBuildingStorey") else None
            key = f"{storey_name}/{product.is_a()}/{product.Name}"
            seen[key] = seen.get(key, 0) + 1
            if seen[key] > 1:
                key += f"#{seen[key]}"
            yield key, product.is_a(), product.Name or product.is_a()[3:], storey_name, verts, faces
        if not it.next():
            break


def push(model: Path) -> None:
    ifc_path = compile_model(model)
    owner = f"lite-step:{model.name}"
    storeys = {s.name for s in lapin.model.storeys}
    mine = {e.id for e in lapin.model if e.attrs.get("owner") == owner}
    kept: set[str] = set()
    with lapin.step(f"Lite-STEP {model.name}"):
        for key, cls, name, storey, verts, faces in products(ifc_path):
            gid = lapin.global_id(f"{model.name}:{key}", "lite-step")
            body = {"positions": verts, "indices": faces}
            if gid in mine:
                lapin.set_body(gid, body)
            else:
                lapin.create_body(cls, body, name=name, storey=storey if storey in storeys else None, global_id=gid,
                                  owner=owner, attrs={"liteStep": key})
            kept.add(gid)
        gone = sorted(mine - kept)
        if gone:
            lapin.element.delete(ids=gone)
    print(f"{len(kept)} elements from {model.name} in Lapin ({len(gone)} gone).")


def main() -> None:
    p = argparse.ArgumentParser(description="A Lite-STEP model's elements in the open Lapin (an optional sketch).")
    p.add_argument("model", type=Path, help="the Lite-STEP model file (its `result` is the Project)")
    p.add_argument("--watch", action="store_true", help="push it again whenever the file is saved")
    p.add_argument("--port", type=int, help="the engine's port (else LAPIN_PORT, or the open Lapin's)")
    a = p.parse_args()
    if a.port:
        lapin.connect(a.port)
    model = a.model.resolve()
    push(model)
    if not a.watch:
        return
    seen = model.stat().st_mtime
    print(f"Watching {model.name}: save it to push again; Ctrl+C stops.")
    try:
        while True:
            time.sleep(0.5)
            now = model.stat().st_mtime
            if now != seen:
                seen = now
                try:
                    push(model)
                except (lapin.LapinError, SystemExit, Exception) as e:   # a broken save: said, and the next one tried
                    print(f"Not pushed: {e}")
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
