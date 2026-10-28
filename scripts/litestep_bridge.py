"""
Bridge runner between Lite-STEP (05_log_cabin.py) and Lapin / BubbleBIM.
Exports JSON schema, DAG dependency graph, executes parameterized recompiles,
and supports bidirectional patching of Python source code from IFC/Lapin edits.
"""

import os
import sys
import re
import json
import argparse
from pathlib import Path

def _resolve_litestep_root() -> Path:
    here = Path(__file__).resolve().parent
    for cand in [
        Path(os.environ["LITESTEP_ROOT"]) if "LITESTEP_ROOT" in os.environ else None,
        here.parent.parent / "lite-step",
        here.parent.parent.parent / "lite-step",
        Path(r"C:\Users\arbir\projects\lite-step"),
    ]:
        if cand and cand.exists():
            return cand
    return Path(r"C:\Users\arbir\projects\lite-step")

LITESTEP_ROOT = _resolve_litestep_root()
if str(LITESTEP_ROOT) not in sys.path:
    sys.path.insert(0, str(LITESTEP_ROOT))

LOCAL_SOURCE = Path(__file__).resolve().parent / "05_log_cabin.py"
DEFAULT_SOURCE = LOCAL_SOURCE if LOCAL_SOURCE.exists() else (LITESTEP_ROOT / "examples" / "05_log_cabin.py")

PARAMETER_SPECS = [
    {
        "id": "CABIN_LENGTH",
        "name": "Cabin Length",
        "kind": "length",
        "default": 6000.0,
        "min": 4000.0,
        "max": 15000.0,
        "step": 500.0,
        "unit": "mm",
        "tier": "fast_foundation_slow_logs"
    },
    {
        "id": "CABIN_WIDTH",
        "name": "Cabin Width",
        "kind": "length",
        "default": 4000.0,
        "min": 3000.0,
        "max": 7000.0,
        "step": 500.0,
        "unit": "mm",
        "tier": "fast_foundation_slow_logs"
    },
    {
        "id": "CENTER_X",
        "name": "Center X",
        "kind": "length",
        "default": 0.0,
        "min": -10000.0,
        "max": 10000.0,
        "step": 100.0,
        "unit": "mm",
        "tier": "fast_foundation_slow_logs"
    },
    {
        "id": "CENTER_Y",
        "name": "Center Y",
        "kind": "length",
        "default": 0.0,
        "min": -10000.0,
        "max": 10000.0,
        "step": 100.0,
        "unit": "mm",
        "tier": "fast_foundation_slow_logs"
    },
    {
        "id": "NUM_LOGS_PER_WALL",
        "name": "Log Tiers",
        "kind": "number",
        "default": 7,
        "min": 5,
        "max": 11,
        "step": 1,
        "unit": "tiers",
        "tier": "slow_logs"
    },
    {
        "id": "ROOF_PITCH",
        "name": "Roof Pitch",
        "kind": "angle",
        "default": 35.0,
        "min": 20.0,
        "max": 50.0,
        "step": 5.0,
        "unit": "deg",
        "tier": "fast_roof"
    },
    {
        "id": "RANDOM_SEED",
        "name": "Wood Grain Seed",
        "kind": "number",
        "default": 42,
        "min": 1,
        "max": 99999,
        "step": 1,
        "unit": "seed",
        "tier": "slow_logs"
    }
]

DAG_GRAPH = {
    "nodes": [
        {"id": "param:CABIN_LENGTH", "label": "CABIN_LENGTH", "kind": "param", "tier": 0},
        {"id": "param:CABIN_WIDTH", "label": "CABIN_WIDTH", "kind": "param", "tier": 0},
        {"id": "param:NUM_LOGS_PER_WALL", "label": "NUM_LOGS_PER_WALL", "kind": "param", "tier": 0},
        {"id": "param:ROOF_PITCH", "label": "ROOF_PITCH", "kind": "param", "tier": 0},
        {"id": "param:RANDOM_SEED", "label": "RANDOM_SEED", "kind": "param", "tier": 0},
        
        {"id": "elem:foundation_slab", "label": "Foundation Slab (IfcSlab)", "kind": "native_lapin", "tier": 0, "latency": "~0.8ms"},
        {"id": "elem:log_walls", "label": "Procedural Organic Logs & Moss", "kind": "litestep_mesh", "tier": 1, "latency": "~350ms"},
        {"id": "elem:wall_top_z", "label": "Wall Top Elevation Z", "kind": "derived_metric", "tier": 1, "latency": "instant once logs settle"},
        {"id": "elem:openings", "label": "Door & Window Cutters", "kind": "shared_opening", "tier": 1, "latency": "~15ms"},
        {"id": "elem:cabin_roof", "label": "Gable Roof (IfcRoof)", "kind": "native_lapin", "tier": 2, "latency": "~2.5ms (blocked by wall_top_z)"}
    ],
    "edges": [
        {"from": "param:CABIN_LENGTH", "to": "elem:foundation_slab", "speed": "instant"},
        {"from": "param:CABIN_WIDTH", "to": "elem:foundation_slab", "speed": "instant"},
        
        {"from": "param:CABIN_LENGTH", "to": "elem:log_walls", "speed": "async"},
        {"from": "param:CABIN_WIDTH", "to": "elem:log_walls", "speed": "async"},
        {"from": "param:NUM_LOGS_PER_WALL", "to": "elem:log_walls", "speed": "async"},
        {"from": "param:RANDOM_SEED", "to": "elem:log_walls", "speed": "async"},
        
        {"from": "elem:log_walls", "to": "elem:wall_top_z", "speed": "instant"},
        {"from": "elem:log_walls", "to": "elem:openings", "speed": "instant"},
        
        {"from": "param:CABIN_LENGTH", "to": "elem:cabin_roof", "speed": "instant_span"},
        {"from": "param:CABIN_WIDTH", "to": "elem:cabin_roof", "speed": "instant_span"},
        {"from": "param:ROOF_PITCH", "to": "elem:cabin_roof", "speed": "instant"},
        {"from": "elem:wall_top_z", "to": "elem:cabin_roof", "speed": "gate"}
    ]
}


def read_parameters(file_path: Path):
    with open(file_path, "r", encoding="utf-8") as f:
        content = f.read()
    values = {}
    for spec in PARAMETER_SPECS:
        pid = spec["id"]
        m = re.search(rf"^{pid}\s*=\s*([0-9.-]+)", content, re.M)
        if m:
            val = float(m.group(1)) if "." in m.group(1) else int(m.group(1))
            values[pid] = val
        else:
            values[pid] = spec["default"]
    return values


def patch_parameters(file_path: Path, new_params: dict):
    with open(file_path, "r", encoding="utf-8") as f:
        content = f.read()

    for k, v in new_params.items():
        val_str = f"{float(v):.1f}" if isinstance(v, float) else str(v)
        pattern = rf"^({k}\s*=\s*)([0-9.-]+)"
        if re.search(pattern, content, re.M):
            content = re.sub(pattern, rf"\g<1>{val_str}", content, flags=re.M)
        else:
            # Append after parameter definitions
            content = re.sub(r'(CORNER_OVERHANG\s*=\s*[0-9.-]+)', rf'{k} = {val_str}\n\g<1>', content, flags=re.M)

    with open(file_path, "w", encoding="utf-8") as f:
        f.write(content)
    return content


def post_process_ifc(ifc_path: Path):
    try:
        import ifcopenshell
        f = ifcopenshell.open(str(ifc_path))

        storeys = f.by_type("IfcBuildingStorey")
        storey = storeys[0] if storeys else None

        # 1. Attach body representation directly to IfcSlab so Lapin recognizes it as a native editable floor
        for slab in list(f.by_type("IfcSlab")):
            for rel in list(f.by_type("IfcRelAggregates")):
                if rel.RelatingObject == slab:
                    for child in list(rel.RelatedObjects):
                        if child.Representation:
                            slab.Representation = child.Representation
                        if child.ObjectPlacement and slab.ObjectPlacement:
                            slab.ObjectPlacement.RelativePlacement = child.ObjectPlacement.RelativePlacement
                        for rc in list(f.by_type("IfcRelContainedInSpatialStructure")):
                            if child in rc.RelatedElements:
                                try:
                                    elems = list(rc.RelatedElements)
                                    elems.remove(child)
                                    rc.RelatedElements = elems
                                except Exception:
                                    pass
                        try:
                            f.remove(child)
                        except Exception:
                            pass
                    try:
                        f.remove(rel)
                    except Exception:
                        pass

        # 2. Promote wall children directly to the storey so Lapin doesn't synthesize ghost interior walls
        spatial_rels = [r for r in f.by_type("IfcRelContainedInSpatialStructure") if storey and r.RelatingStructure == storey]
        spatial_rel = spatial_rels[0] if spatial_rels else None

        walls = list(f.by_type("IfcWall"))
        for w in walls:
            children = []
            for r in list(f.by_type("IfcRelAggregates")):
                if r.RelatingObject == w:
                    children.extend(r.RelatedObjects)
                    try:
                        f.remove(r)
                    except Exception:
                        pass
            if spatial_rel and children:
                cur = list(spatial_rel.RelatedElements)
                for ch in children:
                    if ch not in cur:
                        cur.append(ch)
                spatial_rel.RelatedElements = cur

            # Remove the empty wall container
            for r in list(f.by_type("IfcRelContainedInSpatialStructure")):
                if w in r.RelatedElements:
                    try:
                        cur = list(r.RelatedElements)
                        cur.remove(w)
                        r.RelatedElements = cur
                    except Exception:
                        pass
            try:
                f.remove(w)
            except Exception:
                pass

        # 3. Ensure stable, deterministic GlobalIds for all elements based on their canonical name
        import uuid
        for root in f.by_type('IfcRoot'):
            if root.Name:
                root.GlobalId = ifcopenshell.guid.compress(uuid.uuid5(uuid.NAMESPACE_DNS, f'litestep:{root.Name}').hex)

        # 4. Strip redundant void rels on proxies since they are already pre-carved cleanly
        proxies = set(p.id() for p in f.by_type("IfcBuildingElementProxy"))
        for v in list(f.by_type("IfcRelVoidsElement")):
            if v.RelatingBuildingElement.id() in proxies:
                try:
                    f.remove(v)
                except Exception:
                    pass

        f.write(str(ifc_path))
    except Exception as ex:
        print(f"Warning: post_process_ifc failed: {ex}", file=sys.stderr)


def compile_to_ifc(source_path: Path, output_path: Path, overrides: dict = None):
    # Temporarily patch parameters or write to scratch
    params = read_parameters(source_path)
    if overrides:
        params.update(overrides)

    temp_py = source_path.parent / "_temp_compile.py"
    with open(source_path, "r", encoding="utf-8") as f:
        code = f.read()

    for k, v in params.items():
        val_str = f"{float(v):.1f}" if isinstance(v, float) else str(v)
        code = re.sub(rf"^({k}\s*=\s*)([0-9.-]+)", rf"\g<1>{val_str}", code, flags=re.M)

    # Ensure output.ifc path can be caught
    generated_ifc = source_path.parent / "output.ifc"
    if generated_ifc.exists():
        try:
            generated_ifc.unlink()
        except Exception:
            pass

    with open(temp_py, "w", encoding="utf-8") as f:
        f.write(code)

    import subprocess
    env = dict(os.environ)
    env["PYTHONPATH"] = str(LITESTEP_ROOT) + (os.pathsep + env["PYTHONPATH"] if "PYTHONPATH" in env else "")
    proc = subprocess.run([sys.executable, str(temp_py)], capture_output=True, text=True, cwd=str(source_path.parent), env=env)
    if temp_py.exists():
        temp_py.unlink()

    generated_ifc = source_path.parent / "output.ifc"
    if generated_ifc.exists():
        post_process_ifc(generated_ifc)
        if generated_ifc != output_path:
            import shutil
            shutil.copyfile(generated_ifc, output_path)
        return True, str(output_path), params
    return False, proc.stderr, params


def main():
    parser = argparse.ArgumentParser(description="Lite-STEP Lapin Bridge")
    parser.add_argument("--schema", action="store_true", help="Print parameter schema and DAG")
    parser.add_argument("--get-params", action="store_true", help="Get current parameter values")
    parser.add_argument("--set-params", type=str, help="JSON dictionary of parameters to patch")
    parser.add_argument("--set-params-file", type=str, help="Path to JSON file of parameters to patch")
    parser.add_argument("--compile", action="store_true", help="Compile cabin to IFC")
    parser.add_argument("--out", type=str, default="", help="Output IFC path")
    args = parser.parse_args()

    if args.schema:
        print(json.dumps({
            "parameters": PARAMETER_SPECS,
            "dag": DAG_GRAPH
        }, indent=2))
        return

    if args.get_params:
        params = read_parameters(DEFAULT_SOURCE)
        print(json.dumps(params, indent=2))
        return

    new_params = None
    if args.set_params_file:
        with open(args.set_params_file, "r", encoding="utf-8") as f:
            new_params = json.load(f)
        patch_parameters(DEFAULT_SOURCE, new_params)
    elif args.set_params:
        new_params = json.loads(args.set_params)
        patch_parameters(DEFAULT_SOURCE, new_params)

    if not args.compile:
        if new_params is not None:
            print(json.dumps({"status": "ok", "patched": new_params}))
        return

    if args.compile:
        out_path = Path(args.out) if args.out else LITESTEP_ROOT / "examples" / "output.ifc"
        success, res, params = compile_to_ifc(DEFAULT_SOURCE, out_path, overrides=new_params)
        if success:
            print(json.dumps({"status": "ok", "ifc": str(out_path), "params": params}))
        else:
            print(json.dumps({"status": "error", "message": res}), file=sys.stderr)
            sys.exit(1)


if __name__ == "__main__":
    main()

