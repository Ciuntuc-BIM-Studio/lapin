"""Solve the plates of Lapin's ShellElementTests with PyNite's quadrilaterals and print what they give, as shares of plate
theory, next to Lapin's (docs/plan-fem.md §10.9): a square 4 × 4 m, t = 0.04 m, E = 30 GPa, ν = 0.3, 10 kPa, simply
supported ("hard": w and the turning along each edge held) or clamped, on regular meshes. PyNite's quadrilateral is
DKMQ (Katili 1993), Lapin's MITC4: two formulations, both converging to Timoshenko and Woinowsky-Krieger's values.

Run with the Python of a virtual environment that has PyNiteFEA (MIT): `python -I pynite_plate.py`. Part of Lapin's
scripts/fem-check (MIT); nothing of PyNite is copied here.
"""

from Pynite import FEModel3D

A, T, E, NU, Q = 4.0, 0.04, 30e6, 0.3, 10.0
D = E * T ** 3 / (12 * (1 - NU * NU))
REF = {False: (0.00406235, 0.0478864), True: (0.00126532, 0.022905)}


def plate(n, clamped):
    m = FEModel3D()
    m.add_material("c", E, E / (2 * (1 + NU)), NU, 0)
    name = lambda i, j: f"N{i}_{j}"
    for j in range(n + 1):
        for i in range(n + 1):
            m.add_node(name(i, j), A * i / n, A * j / n, 0)
            on_x, on_y = i in (0, n), j in (0, n)
            edge = on_x or on_y
            rx = edge if clamped else on_x
            ry = edge if clamped else on_y
            m.def_support(name(i, j), True, True, edge, rx, ry, True)
    for j in range(n):
        for i in range(n):
            q = f"Q{i}_{j}"
            m.add_quad(q, name(i, j), name(i + 1, j), name(i + 1, j + 1), name(i, j + 1), T, "c")
            m.add_quad_surface_pressure(q, Q)
    m.analyze_linear(check_statics=True)
    c = n // 2
    w = abs(m.nodes[name(c, c)].DZ["Combo 1"])
    # The centre's moment: the four quadrilaterals' values at their corner there, averaged.
    corners = [(f"Q{c - 1}_{c - 1}", 1, 1), (f"Q{c}_{c - 1}", -1, 1), (f"Q{c}_{c}", -1, -1), (f"Q{c - 1}_{c}", 1, -1)]
    mx = sum(abs(float(m.quads[q].moment(xi, eta)[0][0])) for q, xi, eta in corners) / 4
    alpha, beta = REF[clamped]
    return w / (alpha * Q * A ** 4 / D), mx / (beta * Q * A * A)


# Lapin's, from ShellElementTests (A_plate_under_a_uniform_load_converges_to_the_plate_theory), 12.11.2026.
LAPIN = {(False, 8): (0.99537, 1.00038), (False, 16): (0.99926, 1.00006), (True, 8): (0.99038, 1.01767), (True, 16): (0.99907, 1.00450)}

if __name__ == "__main__":
    for clamped in (False, True):
        for n in (8, 16):
            w, mx = plate(n, clamped)
            lw, lm = LAPIN[(clamped, n)]
            kind = "clamped" if clamped else "simply supported"
            print(f"{kind:17} {n:2} × {n:<2}  PyNite w {w:.5f}  Mx {mx:.5f}   Lapin w {lw:.5f}  Mx {lm:.5f}")
