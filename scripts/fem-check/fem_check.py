"""Lapin's structural analysis checked against two other programs, on the developer's machine only.

1. The dump: SavDumpTests (engine/tests) exports a set of models with IFC's structural analysis view (<model>.ifc) and
   writes Lapin's own analysis of them (<model>.lapin.json), when LAPIN_SAV_OUT names a folder.
2. ifc_structural_mechanics (GPL-3.0, run as a separate program, never part of Lapin) analyses each .ifc with
   CalculiX, and its `check` compares the members with the building elements.
3. PyNite (MIT) solves the bar problem each dump holds (pynite_run.py), to compare with Lapin's solver on the same
   problem as Euler-Bernoulli bars.

Then a table for each model and load case: the reactions' sum, each support's reaction, the nodes' displacements,
the bars' axes displaced along them and their largest forces, against the tolerance of the model's kind. See README.md.
Python 3.10+, the standard library only. Part of Lapin (MIT).
"""

import argparse
import bisect
import json
import math
import os
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent

# The models SavDumpTests writes and how closely the other program should agree with Lapin. `rect`: solid rectangles
# only, the same model on both sides (the differences are the mesh, the 9.81 m/s² of ifc_structural_mechanics against
# 9.80665, its torsion-corrected shear across the width, its displacements read on the faces); `open`: sections other
# than solid rectangles, which ifc_structural_mechanics analyses as equivalent rectangles (its README); `shell`: a floor
# both analyse as a shell (Lapin from S2b, docs/plan-fem.md §10.10: MITC4 on its own mesh against their shell elements on
# Gmsh's), each beam on its own axis under the slab (the export's profile Position, as Lapin's rigid arms). The two differ in
# how a beam holds the slab: theirs tied to it in translation along the line, a hinge (their W-JNT-001), Lapin's monolithic,
# the slab's edge turning with the beam; so the beams' shear, moments and torsion are shown, not judged (13–90 % on
# 12.11.2026), the sums, supports (5–9 %), nodes and deflected axes (under 5 %) are.
MODELS = {
    "cantilever": "rect",
    "simple-beam": "rect",
    "portal": "rect",
    "space-frame": "open",
    "space-frame-rect": "rect",
    "guide-frame": "shell",
    "steel-hall-upn": "open",
    "steel-hall": "open",
}
TOL = {
    #        ΣR     support  nodes  along  forces  torsion
    "rect": (0.002, 0.02, 0.03, 0.03, 0.03, 0.03),
    # An open section's equivalent rectangle is tens of times too stiff in torsion (its README): torsion is shown, not judged.
    "open": (0.002, 0.03, 0.03, 0.03, 0.05, math.inf),
    "shell": (0.002, 0.10, 0.05, 0.05, math.inf, math.inf),
}
# PyNite against Lapin's Euler-Bernoulli solve, of the largest of each: the same bars, so the only difference is the
# arms', rigid in Lapin and very stiff bars in PyNite (pynite_run.py). Rotations are not judged: at a node only pinned
# ends meet, its turning is held by Lapin's spring and by PyNite's feeble one, each its own way.
PYNITE_TOL = 1e-4

# results.json's section forces against Lapin's: its axis 1 is the profile's depth, Lapin's local 2 (IFC's Axis), its 2 = x × 1
# Lapin's local 3; Vf1 is the shear along 1, Mf1 the moment that puts the +1 side in tension (about 2: Lapin's Mz).
FORCES = [("N", "max_N_N", 1e3), ("Vy", "max_Vf1_N", 1e3), ("Vz", "max_Vf2_N", 1e3), ("T", "max_T_Nm", 1e3), ("My", "max_Mf2_Nm", 1e3), ("Mz", "max_Mf1_Nm", 1e3)]


def env(name, default=None):
    v = os.environ.get(name)
    return v if v else default


def run(cmd, **kw):
    print("  $", " ".join(str(c) for c in cmd), flush=True)
    return subprocess.run([str(c) for c in cmd], **kw)


def key(p, tol=0.002):
    return tuple(round(c / tol) for c in p)


def norm(v):
    return math.sqrt(sum(x * x for x in v))


def rel(diff, scale):
    return diff / scale if scale > 1e-12 else (0.0 if diff < 1e-12 else math.inf)


def dump(out, models):
    env_ = dict(os.environ, LAPIN_SAV_OUT=str(out), LAPIN_SAV_MODELS=",".join(models))
    r = run(["dotnet", "test", REPO / "engine/tests/Lapin.Engine.Tests", "--filter", "FullyQualifiedName~SavDumpTests"], env=env_)
    if r.returncode != 0:
        sys.exit("the dump test failed")


def ism_run(ism, ccx, ifc, out, mesh):
    env_ = dict(os.environ)
    if ccx:
        env_["PATH"] = f"{ccx}{os.pathsep}{env_.get('PATH', '')}"
    out.mkdir(parents=True, exist_ok=True)
    with open(out / "analyze.log", "w") as log:
        a = run([ism, "analyze", ifc, "--output", out, "--mesh-size", mesh], env=env_, stdout=log, stderr=subprocess.STDOUT)
    c = run([ism, "check", ifc, "--json-output"], env=env_, capture_output=True, text=True)
    (out / "check.json").write_text(c.stdout)
    return a.returncode, c


def check_findings(c):
    try:
        doc = json.loads(c.stdout)
    except json.JSONDecodeError:
        return [c.stdout.strip()[-300:] or c.stderr.strip()[-300:]]
    items = doc.get("findings") or doc.get("warnings") or doc.get("messages") or []
    return [f"{x.get('code')}: {x.get('message', '')}" if isinstance(x, dict) else str(x) for x in items]


def interp(xs, ys, x):
    i = bisect.bisect_left(xs, x)
    if i <= 0:
        return ys[0]
    if i >= len(xs):
        return ys[-1]
    t = (x - xs[i - 1]) / (xs[i] - xs[i - 1]) if xs[i] > xs[i - 1] else 0
    return [a + (b - a) * t for a, b in zip(ys[i - 1], ys[i])]


def compare_ism(lapin, res, kind):
    """One row a load case: Lapin against results.json of ifc_structural_mechanics."""
    tol = TOL[kind]
    rows = []
    run_cases = {c["code"]: c for c in lapin["run"]["cases"]}
    elements = lapin["problem"]["elements"]
    members = {m["ifc_guid"]: m for m in res.get("members", [])}
    supports = res.get("supports", [])
    connections = res.get("connections", [])
    axes_off = []
    for e in elements:
        m = members.get(e["ifcGuid"])
        if m and "section_axes" in m:
            d = abs(sum(a * b for a, b in zip(m["section_axes"]["1"], e["ey"])))
            if d < 0.99:
                axes_off.append((e["name"], d))
    for code, lr in lapin["results"].items():
        rc = run_cases.get(code)
        if not rc or norm(rc["sumLoads"]) < 1e-9:
            continue
        g = res["global_reactions"]["by_load_case"].get(code)
        if g is None:
            rows.append({"case": code, "missing": True})
            continue
        load = norm(rc["sumLoads"])
        sl = rc["sumReactions"]
        si = [g["fx_N"] / 1e3, g["fy_N"] / 1e3, g["fz_N"] / 1e3]
        r_sum = rel(norm([a - b for a, b in zip(sl, si)]), load)
        # Each support by its place.
        theirs = {key(s["position_m"]): s["by_load_case"].get(code) for s in supports}
        fmax = max([norm(r["f"][:3]) for r in lr["reactions"]] + [1e-12])
        mmax = max([norm(r["f"][3:]) for r in lr["reactions"]] + [1e-12])
        df = dm = 0.0
        unmatched = 0
        for r in lr["reactions"]:
            t = theirs.get(key(r["at"]))
            if t is None:
                unmatched += 1
                continue
            df = max(df, norm([r["f"][i] - t[n] / 1e3 for i, n in enumerate(("fx_N", "fy_N", "fz_N"))]))
            if "mx_Nm" in t:
                dm = max(dm, norm([r["f"][3 + i] - t[n] / 1e3 for i, n in enumerate(("mx_Nm", "my_Nm", "mz_Nm"))]))
        # The nodes, by their place.
        ub = {key(c["position_m"]): c["by_load_case"].get(code) for c in connections}
        umax = max([norm(n["d"][:3]) for n in lr["nodes"]] + [1e-15])
        du = 0.0
        for n in lr["nodes"]:
            t = ub.get(key(n["at"]))
            if t:
                du = max(du, norm([a - b for a, b in zip(n["d"][:3], t["translation_m"])]))
        # Along the bars: Lapin's 21 points against their deflected shape.
        along = lapin["along"][code]
        amax_l = amax_i = da = 0.0
        for k, e in enumerate(elements):
            pts = along[k]
            amax_l = max(amax_l, max(norm(p[1:]) for p in pts))
            m = members.get(e["ifcGuid"])
            shape = m and m.get("deflected_shapes", {}).get(code)
            if not shape:
                continue
            xs = [p[0] for p in pts]
            ys = [p[1:] for p in pts]
            for x, ux, uy, uz in zip(shape["distance_m"], shape["ux_m"], shape["uy_m"], shape["uz_m"]):
                amax_i = max(amax_i, norm([ux, uy, uz]))
                da = max(da, norm([a - b for a, b in zip(interp(xs, ys, x), [ux, uy, uz])]))
        # The bars' largest forces, each against the model's largest of its kind.
        frames = {f["id"]: f for f in lr["frames"]}
        worst = {}
        for name, theirs_key, scale in FORCES:
            top = max([max(abs(f["extremes"][name]["max"]), abs(f["extremes"][name]["min"])) for f in lr["frames"]] + [1e-12])
            w = 0.0
            for e in elements:
                m = members.get(e["ifcGuid"])
                sf = m and m.get("by_load_case", {}).get(code, {}).get("section_forces")
                f = frames.get(e["id"])
                if not sf or not f or theirs_key not in sf:
                    continue
                mine = max(abs(f["extremes"][name]["max"]), abs(f["extremes"][name]["min"]))
                w = max(w, abs(mine - abs(sf[theirs_key]) / scale))
            worst[name] = rel(w, top) if top > 1e-6 else 0.0
        r_sup = max(rel(df, fmax), rel(dm, mmax) if mmax > 1e-6 else 0)
        r_nodes = rel(du, max(umax, amax_l))
        r_along = rel(da, max(amax_l, 1e-15))
        r_forces = max(v for k, v in worst.items() if k != "T")
        ok = r_sum <= tol[0] and r_sup <= tol[1] and r_nodes <= tol[2] and r_along <= tol[3] and r_forces <= tol[4] and worst["T"] <= tol[5] and unmatched == 0
        rows.append({
            "case": code, "load": load, "sumL": sl, "sumI": si, "r_sum": r_sum, "r_sup": r_sup, "unmatched": unmatched,
            "umax_l": umax, "r_nodes": r_nodes, "amax_l": amax_l, "amax_i": amax_i, "r_along": r_along, "forces": worst,
            "ok": ok,
        })
    return rows, axes_off


def compare_pynite(lapin, pyn):
    eb = lapin["eulerBernoulli"]["cases"]
    rows = []
    for code, c in eb.items():
        p = pyn["cases"].get(code)
        if p is None:
            continue
        umax = max([norm(u[:3]) for u in c["u"]] + [1e-30])
        if umax < 1e-15:
            continue
        du = max(max(abs(a - b) for a, b in zip(u[:3], q[:3])) for u, q in zip(c["u"], p["u"]))
        rmax = max([abs(x) for r in c["reactions"].values() for x in r] + [1e-30])
        dr = max([abs(a - b) for k, r in c["reactions"].items() for a, b in zip(r, p["reactions"][k])] + [0])
        emax = max([abs(x) for e in c["ends"] for x in e] + [1e-30])
        de = max([abs(a - b) for e, q in zip(c["ends"], p["ends"]) for a, b in zip(e, q)] + [0])
        rows.append({"case": code, "umax": umax, "r_u": du / umax, "rmax": rmax, "r_r": dr / rmax, "r_e": de / emax,
                     "ok": max(du / umax, dr / rmax, de / emax) <= PYNITE_TOL})
    return rows


def pct(x):
    return "inf" if x == math.inf else f"{100 * x:.3f}%"


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", default=env("FEM_CHECK_OUT", str(REPO / "build/fem-check")), help="working folder (default build/fem-check)")
    ap.add_argument("--models", default=",".join(MODELS), help="comma-separated, of: " + ", ".join(MODELS))
    ap.add_argument("--no-dump", action="store_true", help="use the files already in --out/sav")
    ap.add_argument("--ism", default=env("FEM_ISM"), help="the ifc-analysis program of ifc_structural_mechanics (env FEM_ISM); none: skipped")
    ap.add_argument("--ccx-dir", default=env("FEM_CCX_DIR"), help="a folder holding CalculiX's ccx, put first on PATH (env FEM_CCX_DIR)")
    ap.add_argument("--pynite", default=env("FEM_PYNITE_PYTHON"), help="a Python with PyNiteFEA (env FEM_PYNITE_PYTHON); none: skipped")
    ap.add_argument("--mesh-size", default=env("FEM_MESH_SIZE", "0.25"), help="ifc_structural_mechanics' mesh size, m (default 0.25)")
    a = ap.parse_args()
    models = [m.strip() for m in a.models.split(",") if m.strip()]
    for m in models:
        if m not in MODELS:
            sys.exit(f"no model {m}")
    out = Path(a.out).resolve()
    sav = out / "sav"
    sav.mkdir(parents=True, exist_ok=True)
    if not a.no_dump:
        dump(sav, models)
    failed = []
    for model in models:
        kind = MODELS[model]
        lapin = json.load(open(sav / f"{model}.lapin.json"))
        print(f"\n== {model} ({kind}): {lapin['run']['elements']} bars, {lapin['run']['dofs']} degrees of freedom")
        if a.ism:
            code, c = ism_run(a.ism, a.ccx_dir, sav / f"{model}.ifc", out / "ism" / model, a.mesh_size)
            findings = check_findings(c)
            print(f"  ifc-analysis check: {len(findings)} finding(s)")
            for f in findings:
                print(f"    {f}")
            res_path = out / "ism" / model / "results.json"
            if code != 0 or not res_path.exists():
                print(f"  ifc-analysis analyze failed (exit {code}); see {out / 'ism' / model / 'analyze.log'}")
                failed.append(f"{model}: ifc_structural_mechanics did not analyse it")
            else:
                res = json.load(open(res_path))
                for w in res.get("warnings", []):
                    print(f"    [{w.get('code')}] {w.get('message', '')[:160]}")
                rows, axes_off = compare_ism(lapin, res, kind)
                for name, d in axes_off:
                    print(f"  ! {name}: its section axis 1 is not Lapin's local 2 (|cos| {d:.3f})")
                tol = TOL[kind]
                print(f"  ifc_structural_mechanics (tolerances: ΣR {pct(tol[0])}, supports {pct(tol[1])}, nodes {pct(tol[2])}, along {pct(tol[3])}, forces {pct(tol[4])}, torsion {pct(tol[5])})")
                print(f"  {'case':6} {'ΣR Lapin kN':>26} {'ΣR ism kN':>26} {'ΣR':>8} {'supports':>9} {'|u| max mm':>10} {'nodes':>8} {'along L/ism mm':>16} {'along':>8} {'N':>7} {'V':>7} {'T':>7} {'M':>7}  ")
                for r in rows:
                    if r.get("missing"):
                        print(f"  {r['case']:6} not in results.json")
                        failed.append(f"{model} {r['case']}: not analysed by ifc_structural_mechanics")
                        continue
                    f = r["forces"]
                    v3 = lambda v: "(" + ", ".join(f"{x:7.2f}" for x in v) + ")"
                    print(f"  {r['case']:6} {v3(r['sumL']):>26} {v3(r['sumI']):>26} {pct(r['r_sum']):>8} {pct(r['r_sup']):>9} {1e3 * r['umax_l']:10.4f} {pct(r['r_nodes']):>8} "
                          f"{1e3 * r['amax_l']:7.4f}/{1e3 * r['amax_i']:<8.4f} {pct(r['r_along']):>8} {pct(f['N']):>7} {pct(max(f['Vy'], f['Vz'])):>7} {pct(f['T']):>7} {pct(max(f['My'], f['Mz'])):>7}  {'ok' if r['ok'] else 'OFF'}")
                    if r["unmatched"]:
                        print(f"    {r['unmatched']} support(s) of Lapin's not found in results.json")
                    if not r["ok"]:
                        failed.append(f"{model} {r['case']}: ifc_structural_mechanics beyond the tolerance")
        if a.pynite and lapin["problem"].get("shells"):
            print(f"  PyNite: skipped ({lapin['problem']['shells']} shell elements; pynite_run.py takes bars only)")
        elif a.pynite:
            dst = out / "pynite" / f"{model}.json"
            dst.parent.mkdir(parents=True, exist_ok=True)
            p = run([a.pynite, "-I", HERE / "pynite_run.py", sav / f"{model}.lapin.json", dst])
            if p.returncode != 0:
                failed.append(f"{model}: PyNite failed")
                continue
            rows = compare_pynite(lapin, json.load(open(dst)))
            print(f"  PyNite against Lapin's Euler-Bernoulli solve (tolerance {PYNITE_TOL:g} of the largest)")
            print(f"  {'case':6} {'|u| max mm':>11} {'Δu':>10} {'R max':>10} {'ΔR':>10} {'Δ end forces':>13}")
            for r in rows:
                print(f"  {r['case']:6} {1e3 * r['umax']:11.5f} {r['r_u']:10.2e} {r['rmax']:10.3f} {r['r_r']:10.2e} {r['r_e']:13.2e}  {'ok' if r['ok'] else 'OFF'}")
                if not r["ok"]:
                    failed.append(f"{model} {r['case']}: PyNite beyond the tolerance")
    print()
    if failed:
        print("Beyond the tolerances or not run:")
        for f in failed:
            print(f"  {f}")
        sys.exit(1)
    print("Every comparison within its tolerance.")


if __name__ == "__main__":
    main()
