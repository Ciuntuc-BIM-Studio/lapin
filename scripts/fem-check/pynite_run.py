"""Solve the bar problem Lapin dumped (`<model>.lapin.json`, written by SavDumpTests) with PyNite, and write PyNite's
results next to it as JSON: each case's node displacements, the reactions at the held nodes and each bar's end forces
in its local axes, in Lapin's units (m, rad, kN, kN·m).

Run with the Python of a virtual environment that has PyNiteFEA (MIT): `python -I pynite_run.py in.lapin.json out.json`.
The bars are Euler-Bernoulli as PyNite's are; compare with the dump's `eulerBernoulli` solution (Lapin's own solver
on the same problem with no shear areas). Part of Lapin's scripts/fem-check (MIT).
"""

import json
import math
import sys

from Pynite import FEModel3D

# An arm (a bar's end off its node) is a short bar this many times as stiff as its bar's section in every way (its
# largest second moment for bending and torsion alike): being far shorter as well, it is stiffer than the bar by
# millions, and not so stiff that the solve loses digits to it.
RIGID = 1e4


def sub(a, b):
    return [a[0] - b[0], a[1] - b[1], a[2] - b[2]]


def add(a, b):
    return [a[0] + b[0], a[1] + b[1], a[2] + b[2]]


def dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def cross(a, b):
    return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]


def norm(a):
    return math.sqrt(dot(a, a))


def main(src, dst):
    lapin = json.load(open(src))
    p = lapin["problem"]
    nodes, elements, cases = p["nodes"], p["elements"], p["cases"]
    m = FEModel3D()
    used = set()
    for e in elements:
        used.update((e["i"], e["j"]))
    for k, n in enumerate(nodes):
        if k in used:
            m.add_node(f"N{k}", *n["at"])
    for k, n in enumerate(nodes):
        if k not in used:
            continue
        held = n["held"] or [False] * 6
        m.def_support(f"N{k}", *held)
        # Node rotations no bar stiffens (only pinned ends meet there): Lapin holds them by a spring that takes
        # nothing; a feeble spring here does the same.
        for d, h in zip(("RX", "RY", "RZ"), held[3:]):
            if not h:
                m.def_support_spring(f"N{k}", d, 1e-6)
    materials = {}
    arm_ends = {}
    for k, e in enumerate(elements):
        key = (e["E"], e["G"])
        if key not in materials:
            materials[key] = f"M{len(materials)}"
            m.add_material(materials[key], e["E"], e["G"], e["E"] / (2 * e["G"]) - 1, 0)
        ends = []
        for end, off in (("i", e["offI"]), ("j", e["offJ"])):
            node = f"N{e[end]}"
            if norm(off) > 1e-9:
                # One arm for the bars that end at the same point off the same node (PyNite would cut a bar at a second
                # node there); being rigid, it is the same as one each.
                where = (e[end], *(round(x, 9) for x in off))
                if where not in arm_ends:
                    arm_ends[where] = f"E{len(arm_ends)}"
                    m.add_node(arm_ends[where], *add(nodes[e[end]]["at"], off))
                    arm = f"arm{len(arm_ends)}"
                    big = RIGID * max(e["I22"], e["I33"], e["J"])
                    m.add_section(arm, RIGID * e["A"], big, big, big)
                    m.add_member(arm, node, arm_ends[where], materials[key], arm)
                node = arm_ends[where]
            ends.append(node)
        # PyNite's local y as Lapin's local 2, so I22 is its Iy and I33 its Iz.
        m.add_section(f"S{k}", e["A"], e["I22"], e["I33"], e["J"])
        name = f"B{k}"
        m.add_member(name, ends[0], ends[1], materials[key], f"S{k}")
        mem = m.members[name]
        t = mem.T()
        y0, z0 = list(t[1, 0:3]), list(t[2, 0:3])
        ey = e["ey"]
        mem.rotation = math.degrees(math.atan2(dot(ey, z0), dot(ey, y0)))
        if any(e["releaseI"]) or any(e["releaseJ"]):
            m.def_releases(name, *e["releaseI"], *e["releaseJ"])
    for c in cases:
        code = c["code"]
        for x in c["nodal"]:
            for d, v in zip(("FX", "FY", "FZ"), x["f"]):
                if v:
                    m.add_node_load(f"N{x['node']}", d, v, case=code)
        for x in c["moments"]:
            for d, v in zip(("MX", "MY", "MZ"), x["m"]):
                if v:
                    m.add_node_load(f"N{x['node']}", d, v, case=code)
        for s in c["span"]:
            name = f"B{s['element']}"
            for i, d in enumerate(("FX", "FY", "FZ")):
                qa, qb = s["qa"][i], s["qb"][i]
                if qa == 0 and qb == 0:
                    continue
                if s["point"]:
                    m.add_member_pt_load(name, d, qa, s["a"], case=code)
                else:
                    m.add_member_dist_load(name, d, qa, qb, s["a"], s["b"], case=code)
        m.add_load_combo(code, {code: 1.0})
    m.analyze_linear(check_statics=False, check_stability=False)
    out = {"cases": {}}
    for c in cases:
        code = c["code"]
        u, reactions = [], {}
        for k, n in enumerate(nodes):
            if k not in used:
                u.append([0.0] * 6)
                continue
            nd = m.nodes[f"N{k}"]
            u.append([nd.DX[code], nd.DY[code], nd.DZ[code], nd.RX[code], nd.RY[code], nd.RZ[code]])
            if n["held"]:
                reactions[n["id"]] = [nd.RxnFX[code], nd.RxnFY[code], nd.RxnFZ[code], nd.RxnMX[code], nd.RxnMY[code], nd.RxnMZ[code]]
        ends = []
        for k in range(len(elements)):
            subs = list(m.members[f"B{k}"].sub_members.values())
            fi, fj = subs[0].f(code), subs[-1].f(code)
            ends.append([float(v) for v in list(fi[0:6, 0]) + list(fj[6:12, 0])])
        out["cases"][code] = {"u": u, "reactions": reactions, "ends": ends}
    json.dump(out, open(dst, "w"))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
