from pathlib import Path
import sys
import time
import math
import numpy as np

_repo_root = Path(__file__).resolve().parent.parent
if str(_repo_root) not in sys.path:
    sys.path.insert(0, str(_repo_root))

from lite_step import compile_main
from lite_step.models import Box, Door, Slab, Element, Material, Mesh, Point, Point2D, Product, Project, Site, Sweep, Wall, Window, miter
from lite_step.optimization import jit_compile, solve_sequence

I = lambda x: int(round(x))  # noqa: E731

# RANDOM_SEED handled at top

CABIN_LENGTH = 6000
CABIN_WIDTH = 4000
CENTER_X = 0
CENTER_Y = 0
NUM_LOGS_PER_WALL = 7
ROOF_PITCH = 35
RANDOM_SEED = 42
CORNER_OVERHANG = 250.0

LOG_LEN_LONG = CABIN_LENGTH + 2.0 * CORNER_OVERHANG
LOG_LEN_SHORT = CABIN_WIDTH + 2.0 * CORNER_OVERHANG
NUM_X_STATIONS = 31
NUM_THETA_SEGS = 20

LOG_R_BUTT_MIN, LOG_R_BUTT_MAX = 170.0, 195.0
LOG_R_TIP_MIN, LOG_R_TIP_MAX = 155.0, 175.0
AVG_LOG_DIAMETER = 350.0
WALL_Z_RAISE = AVG_LOG_DIAMETER / 2.0

SLAB_EXTEND = 150.0
SLAB_HEIGHT = 300.0
SLAB_X_MIN = CENTER_X - (CABIN_LENGTH / 2.0 + SLAB_EXTEND)
SLAB_X_MAX = CENTER_X + (CABIN_LENGTH / 2.0 + SLAB_EXTEND)
SLAB_Y_MIN = CENTER_Y - (CABIN_WIDTH / 2.0 + SLAB_EXTEND)
SLAB_Y_MAX = CENTER_Y + (CABIN_WIDTH / 2.0 + SLAB_EXTEND)

MOSS_WIDTH = 50.0
MOSS_COLOR = "#4A7C3B"
SLAB_COLOR = "#8D99AE"

LOG_PALETTES = [
    ["#825833", "#94673D", "#6E4725", "#9C7146", "#784E2A", "#8C6038", "#704828"],
    ["#7D532F", "#8E6238", "#694321", "#966C41", "#734A26", "#875C34", "#6B4424"],
    ["#865C37", "#986B41", "#724B29", "#9F7449", "#7C522E", "#90643C", "#744C2C"],
    ["#805632", "#91653B", "#6C4523", "#996F44", "#764D28", "#8A5E36", "#6E4626"],
]

def make_clearing(x_min: float, x_max: float, y_min: float, y_max: float) -> Mesh:
    pad_buf = 2500.0
    pad_x0 = min(-5000.0, x_min - pad_buf)
    pad_x1 = max(5000.0, x_max + pad_buf)
    pad_y0 = min(-4000.0, y_min - pad_buf)
    pad_y1 = max(4000.0, y_max + pad_buf)

    site_x0 = pad_x0 - 9000.0
    site_x1 = pad_x1 + 9000.0
    site_y0 = pad_y0 - 8000.0
    site_y1 = pad_y1 + 8000.0

    nx, ny = 21, 21
    xs = np.linspace(site_x0, site_x1, nx)
    ys = np.linspace(site_y0, site_y1, ny)

    grid = []
    for y in ys:
        row = []
        for x in xs:
            dx = max(0.0, pad_x0 - x, x - pad_x1)
            dy = max(0.0, pad_y0 - y, y - pad_y1)
            d = np.hypot(dx, dy)
            if d <= 0.0:
                h = 0.0
            else:
                t = min(1.0, d / 6500.0)
                h = -2800.0 * (3 * t**2 - 2 * t**3)
            row.append(int(round(h)))
        grid.append(row)

    return Mesh(
        name="clearing",
        heightmap=grid,
        depth=4000,
        corner_min=Point2D(x=int(site_x0), y=int(site_y0)),
        corner_max=Point2D(x=int(site_x1), y=int(site_y1)),
        source_type="synthetic",
        color="#5b6b45",
    )

CLEARING = make_clearing(SLAB_X_MIN, SLAB_X_MAX, SLAB_Y_MIN, SLAB_Y_MAX)


def make_log(log_id: int, length: float, rng: np.random.RandomState) -> dict:
    u = rng.uniform
    pi2 = 2 * math.pi
    log = {
        "log_id": log_id, "length": length,
        "r_butt": u(LOG_R_BUTT_MIN, LOG_R_BUTT_MAX), "r_tip": u(LOG_R_TIP_MIN, LOG_R_TIP_MAX),
        "twist_rate": u(1.2, 2.5) * rng.choice([-1.0, 1.0]),
        "camber_amp_y1": u(22, 42), "camber_amp_y2": u(8, 18), "camber_amp_y3": u(3, 8),
        "camber_phase_y1": u(0, pi2), "camber_phase_y2": u(0, pi2), "camber_phase_y3": u(0, pi2),
        "camber_amp_z1": u(20, 40), "camber_amp_z2": u(8, 16), "camber_amp_z3": u(3, 8),
        "camber_phase_z1": u(0, pi2), "camber_phase_z2": u(0, pi2), "camber_phase_z3": u(0, pi2),
        "ovality": u(0.06, 0.11), "oval_angle": u(0, math.pi),
        "bark_amp3": u(0.018, 0.040), "bark_phase3": u(0, pi2),
        "bark_amp5": u(0.008, 0.020), "bark_phase5": u(0, pi2),
    }
    log["knobs"] = [
        {"u": u(0.18, 0.82), "angle": u(0, pi2), "height": u(45, 75), "sigma_u": u(0.04, 0.09), "sigma_th": u(0.35, 0.65)}
        for _ in range(rng.randint(1, 4))
    ]
    return log


def log_camber(log: dict, u: float) -> tuple[float, float]:
    dy = sum(log[f"camber_amp_y{i}"] * math.sin(i * math.pi * u + log[f"camber_phase_y{i}"]) for i in (1, 2, 3))
    dz = sum(log[f"camber_amp_z{i}"] * math.sin(i * math.pi * u + log[f"camber_phase_z{i}"]) for i in (1, 2, 3))
    return dy, dz


def log_raw_radius(log: dict, u: float, theta: float) -> float:
    r_nominal = log["r_butt"] * (1.0 - u) + log["r_tip"] * u
    theta_grain = theta + log["twist_rate"] * u
    ovality_mod = 1.0 + log["ovality"] * math.cos(2 * (theta_grain - log["oval_angle"]))
    bark_mod = (
        1.0
        + log["bark_amp3"] * math.cos(3 * theta_grain + log["bark_phase3"])
        + log["bark_amp5"] * math.cos(5 * theta_grain + log["bark_phase5"])
    )
    r = r_nominal * ovality_mod * bark_mod
    for knob in log["knobs"]:
        du = (u - knob["u"]) / knob["sigma_u"]
        dth = (theta - knob["angle"] + math.pi) % (2 * math.pi) - math.pi
        dth_norm = dth / knob["sigma_th"]
        r += knob["height"] * math.exp(-0.5 * (du * du + dth_norm * dth_norm))
    return max(r, 80.0)


def log_profiles(log: dict, flip: bool, rot_angle: float):
    n = int(NUM_X_STATIONS)
    x_vals = np.linspace(-log["length"] / 2.0, log["length"] / 2.0, n)
    z_top = np.zeros(n, dtype=np.float64)
    z_bot = np.zeros(n, dtype=np.float64)

    knobs = log["knobs"]
    _jit_eval_profile(
        float(log["r_butt"]), float(log["r_tip"]), float(log["twist_rate"]),
        float(log["camber_amp_y1"]), float(log["camber_amp_y2"]), float(log["camber_amp_y3"]),
        float(log["camber_phase_y1"]), float(log["camber_phase_y2"]), float(log["camber_phase_y3"]),
        float(log["camber_amp_z1"]), float(log["camber_amp_z2"]), float(log["camber_amp_z3"]),
        float(log["camber_phase_z1"]), float(log["camber_phase_z2"]), float(log["camber_phase_z3"]),
        float(log["ovality"]), float(log["oval_angle"]),
        float(log["bark_amp3"]), float(log["bark_phase3"]),
        float(log["bark_amp5"]), float(log["bark_phase5"]),
        np.array([k["u"] for k in knobs], dtype=np.float64),
        np.array([k["angle"] for k in knobs], dtype=np.float64),
        np.array([k["height"] for k in knobs], dtype=np.float64),
        np.array([k["sigma_u"] for k in knobs], dtype=np.float64),
        np.array([k["sigma_th"] for k in knobs], dtype=np.float64),
        x_vals, z_top, z_bot, flip, rot_angle,
    )
    return x_vals, z_top, z_bot


@jit_compile(fastmath=True, nogil=True)
def _jit_eval_profile(
    r_butt, r_tip, twist_rate,
    c_y1, c_y2, c_y3, p_y1, p_y2, p_y3,
    c_z1, c_z2, c_z3, p_z1, p_z2, p_z3,
    ovality, oval_angle,
    b_a3, b_p3, b_a5, b_p5,
    knobs_u, knobs_ang, knobs_h, knobs_su, knobs_sth,
    x_vals, z_top, z_bot,
    flip, rot_angle,
):
    num_stations = len(x_vals)
    length = x_vals[num_stations - 1] - x_vals[0]
    cos_rot, sin_rot = np.cos(rot_angle), np.sin(rot_angle)
    d_th = 2.0 * np.pi / 120.0

    for i in range(num_stations):
        x = x_vals[i]
        u = (x + length / 2.0) / length
        u_log = (1.0 - u) if flip else u

        dy = (c_y1 * np.sin(np.pi * u_log + p_y1)
            + c_y2 * np.sin(2.0 * np.pi * u_log + p_y2)
            + c_y3 * np.sin(3.0 * np.pi * u_log + p_y3))
        dz = (c_z1 * np.sin(np.pi * u_log + p_z1)
            + c_z2 * np.sin(2.0 * np.pi * u_log + p_z2)
            + c_z3 * np.sin(3.0 * np.pi * u_log + p_z3))
        dz_c = dy * sin_rot + dz * cos_rot

        r_nom = r_butt * (1.0 - u_log) + r_tip * u_log

        max_z, min_z = -1e9, 1e9
        for j in range(120):
            th = j * d_th
            theta_grain = (th - rot_angle) + twist_rate * u_log
            ovality_mod = 1.0 + ovality * np.cos(2.0 * (theta_grain - oval_angle))
            bark_mod = 1.0 + b_a3 * np.cos(3.0 * theta_grain + b_p3) + b_a5 * np.cos(5.0 * theta_grain + b_p5)
            r = r_nom * ovality_mod * bark_mod

            for k_idx in range(len(knobs_u)):
                du = (u_log - knobs_u[k_idx]) / knobs_su[k_idx]
                dth = (th - rot_angle - knobs_ang[k_idx] + np.pi) % (2.0 * np.pi) - np.pi
                r += knobs_h[k_idx] * np.exp(-0.5 * (du * du + (dth / knobs_sth[k_idx]) ** 2))

            if r < 80.0:
                r = 80.0
            z_sample = dz_c + r * np.sin(th)
            if z_sample > max_z:
                max_z = z_sample
            if z_sample < min_z:
                min_z = z_sample

        z_top[i] = max_z
        z_bot[i] = min_z


@jit_compile(fastmath=True, nogil=True)
def upper_convex_hull(x_vals: np.ndarray, h_vals: np.ndarray) -> np.ndarray:
    n = len(x_vals)
    hull = np.empty(n, dtype=np.int64)
    hull_len = 0
    for i in range(n):
        x, y = x_vals[i], h_vals[i]
        while hull_len >= 2:
            idx1, idx2 = hull[hull_len - 2], hull[hull_len - 1]
            x1, y1 = x_vals[idx1], h_vals[idx1]
            x2, y2 = x_vals[idx2], h_vals[idx2]
            if (x2 - x1) * (y - y1) - (y2 - y1) * (x - x1) >= 0:
                hull_len -= 1
            else:
                break
        hull[hull_len] = i
        hull_len += 1
    return hull[:hull_len]


@jit_compile(fastmath=True, nogil=True)
def compute_resting_offset_and_gap(
    lower_top: np.ndarray,
    upper_bot: np.ndarray,
    dx: float,
    x_vals: np.ndarray,
    min_span: float,
) -> tuple[float, float, float, float]:
    n = len(lower_top)
    h = np.empty(n, dtype=np.float64)
    for k in range(n):
        h[k] = lower_top[k] - upper_bot[k]

    hull = upper_convex_hull(x_vals, h)
    m = len(hull)
    best_gap, best_z0, best_s, best_dist = 1e18, 0.0, 0.0, 0.0

    for u1 in range(m):
        i = hull[u1]
        for u2 in range(u1 + 1, m):
            j = hull[u2]
            dist = x_vals[j] - x_vals[i]
            if dist < min_span:
                continue

            s_cand = (h[j] - h[i]) / dist
            z0_cand = h[i] - s_cand * x_vals[i]

            valid = True
            for uk in range(u1 + 1, u2):
                k = hull[uk]
                if z0_cand + s_cand * x_vals[k] < h[k] - 1e-4:
                    valid = False
                    break

            if valid:
                gap_sum = 0.0
                for k in range(n):
                    gap_sum += (z0_cand + s_cand * x_vals[k]) - h[k]
                gap_area = gap_sum * dx
                if gap_area < best_gap:
                    best_gap = gap_area
                    best_z0 = z0_cand
                    best_s = s_cand
                    best_dist = dist

    if best_gap >= 1e17:
        max_idx = 0
        max_h = h[0]
        for k in range(1, n):
            if h[k] > max_h:
                max_h, max_idx = h[k], k
        xi, hi = x_vals[max_idx], max_h
        for uk in range(m):
            k = hull[uk]
            dist = np.abs(x_vals[k] - xi)
            if dist < min_span:
                continue
            s_cand = (h[k] - hi) / (x_vals[k] - xi)
            z0_cand = -1e18
            for p in hull:
                val = h[p] - s_cand * x_vals[p]
                if val > z0_cand:
                    z0_cand = val

            gap_sum = 0.0
            for p in range(n):
                gap_sum += (z0_cand + s_cand * x_vals[p]) - h[p]
            gap_area = gap_sum * dx + 5000.0
            if gap_area < best_gap:
                best_gap, best_z0, best_s, best_dist = gap_area, z0_cand, s_cand, dist

    return best_z0, best_s, best_gap, best_dist


def optimize_log_wall(
    logs: list,
    base_height: float,
    num_rotations: int = 24,
) -> tuple[list[int], list[bool], list[float], list[tuple[float, float]], list[float], float, float]:
    num_logs = len(logs)
    length = logs[0]["length"]
    min_span = 0.30 * length
    x_vals = np.linspace(-length / 2.0, length / 2.0, NUM_X_STATIONS)
    dx = length / (NUM_X_STATIONS - 1)
    rotation_candidates = list(np.linspace(0, 2 * math.pi, num_rotations, endpoint=False))

    orientations = {}
    num_oris = 2 * num_rotations
    for idx, log in enumerate(logs):
        orientations[idx] = []
        for flip in [False, True]:
            for rot in rotation_candidates:
                _, z_top, z_bot = log_profiles(log, flip, rot)
                orientations[idx].append({"z_top": z_top, "z_bot": z_bot, "flip": flip, "rot": rot})

    foundation_top = np.full(NUM_X_STATIONS, float(base_height), dtype=np.float64)
    base_seating = {
        idx: [compute_resting_offset_and_gap(foundation_top, orientations[idx][o]["z_bot"], dx, x_vals, min_span)
              for o in range(num_oris)]
        for idx in range(num_logs)
    }

    pairwise_best = {i: {o: {} for o in range(num_oris)} for i in range(num_logs)}
    for la in range(num_logs):
        for oa in range(num_oris):
            zta = orientations[la][oa]["z_top"]
            for lb in range(num_logs):
                if lb == la:
                    continue
                best_g, best_e = 1e18, None
                for ob in range(num_oris):
                    z0, s, gap, dist = compute_resting_offset_and_gap(zta, orientations[lb][ob]["z_bot"], dx, x_vals, min_span)
                    if gap < best_g:
                        best_g, best_e = gap, (ob, z0, s, gap, dist)
                pairwise_best[la][oa][lb] = best_e

    cost_matrix = np.full((num_logs, num_logs), 1e8)
    for i in range(num_logs):
        for j in range(num_logs):
            if i != j:
                cost_matrix[i, j] = min(pairwise_best[i][o][j][3] for o in range(num_oris))

    base_costs = np.array([min(base_seating[i][o][2] for o in range(num_oris)) for i in range(num_logs)])
    best_perm = solve_sequence(num_logs, cost_matrix, base_costs=base_costs)

    curr_o = min(range(num_oris), key=lambda o: base_seating[best_perm[0]][o][2])
    best_ori_indices = [curr_o]
    best_total_gap = 0.0
    for layer in range(num_logs - 1):
        la, lb = best_perm[layer], best_perm[layer + 1]
        next_o, _, _, gap, _ = pairwise_best[la][curr_o][lb]
        best_total_gap += gap
        curr_o = next_o
        best_ori_indices.append(curr_o)

    best_flips, best_rots, best_placements, best_dists = [], [], [], []
    base_log = best_perm[0]
    z0_world, s_world, _, dist = base_seating[base_log][best_ori_indices[0]]
    best_flips.append(orientations[base_log][best_ori_indices[0]]["flip"])
    best_rots.append(orientations[base_log][best_ori_indices[0]]["rot"])
    best_placements.append((z0_world, s_world))
    best_dists.append(dist)

    for layer in range(num_logs - 1):
        la, lb = best_perm[layer], best_perm[layer + 1]
        oa, ob = best_ori_indices[layer], best_ori_indices[layer + 1]
        _, z0_rel, s_rel, _, dist = pairwise_best[la][oa][lb]
        z0_world += z0_rel
        s_world += s_rel
        best_flips.append(orientations[lb][ob]["flip"])
        best_rots.append(orientations[lb][ob]["rot"])
        best_placements.append((z0_world, s_world))
        best_dists.append(dist)

    return best_perm, best_flips, best_rots, best_placements, best_dists, 0.0, best_total_gap


def build_log_mesh(
    log: dict, flip_x: bool, rotation_rad: float, z0: float, slope_s: float,
    color: str, name: str, axis: str = "X", origin_x: float = 0.0, origin_y: float = 0.0, origin_z: float = 0.0,
) -> Mesh:
    verts: list[Point] = []
    faces: list[tuple[int, int, int]] = []

    x_vals = np.linspace(-log["length"] / 2.0, log["length"] / 2.0, NUM_X_STATIONS)
    theta_vals = np.linspace(0, 2 * math.pi, NUM_THETA_SEGS, endpoint=False)
    cos_rot, sin_rot = math.cos(rotation_rad), math.sin(rotation_rad)

    for i, x in enumerate(x_vals):
        u = (x + log["length"] / 2.0) / log["length"]
        u_log = (1.0 - u) if flip_x else u

        raw_dy, raw_dz = log_camber(log, u_log)
        dy_center = raw_dy * cos_rot - raw_dz * sin_rot
        dz_center = raw_dy * sin_rot + raw_dz * cos_rot
        z_offset_x = z0 + slope_s * x

        for th in theta_vals:
            r = log_raw_radius(log, u_log, th - rotation_rad)
            py = dy_center + r * math.cos(th)
            pz = dz_center + r * math.sin(th) + z_offset_x
            wx = origin_x + (x if axis == "X" else py)
            wy = origin_y + (py if axis == "X" else x)
            verts.append(Point(x=I(wx), y=I(wy), z=I(origin_z + pz)))

    for i in range(NUM_X_STATIONS - 1):
        ring0, ring1 = i * NUM_THETA_SEGS, (i + 1) * NUM_THETA_SEGS
        for t in range(NUM_THETA_SEGS):
            tn = (t + 1) % NUM_THETA_SEGS
            v00, v01, v10, v11 = ring0 + t, ring0 + tn, ring1 + t, ring1 + tn
            if axis == "X":
                faces.extend([(v00, v01, v11), (v00, v11, v10)])
            else:
                faces.extend([(v00, v11, v01), (v00, v10, v11)])

    for sign, u_cap in ((-1.0, 1.0 if flip_x else 0.0), (1.0, 0.0 if flip_x else 1.0)):
        dy_c, dz_c = log_camber(log, u_cap)
        dy_rot = dy_c * cos_rot - dz_c * sin_rot
        dz_rot = dy_c * sin_rot + dz_c * cos_rot
        zc = z0 + slope_s * (sign * log["length"] / 2.0)
        c_idx = len(verts)

        wx = origin_x + (sign * log["length"] / 2.0 if axis == "X" else dy_rot)
        wy = origin_y + (dy_rot if axis == "X" else sign * log["length"] / 2.0)
        verts.append(Point(x=I(wx), y=I(wy), z=I(origin_z + dz_rot + zc)))

        ring_base = 0 if sign < 0 else (NUM_X_STATIONS - 1) * NUM_THETA_SEGS
        for t in range(NUM_THETA_SEGS):
            tn = (t + 1) % NUM_THETA_SEGS
            if (axis == "X" and sign < 0) or (axis != "X" and sign > 0):
                faces.append((c_idx, ring_base + tn, ring_base + t))
            else:
                faces.append((c_idx, ring_base + t, ring_base + tn))

    return Mesh(name=name, vertices=verts, faces=faces, is_watertight=True, color=color)


def raytrace_log_z_surfaces(
    log: dict, flip: bool, rot: float, z0: float, s: float, x: float, y: float
) -> tuple[float, float]:
    u = (x + log["length"] / 2.0) / log["length"]
    u_log = (1.0 - u) if flip else u

    raw_dy, raw_dz = log_camber(log, u_log)
    cos_rot, sin_rot = math.cos(rot), math.sin(rot)
    yc = raw_dy * cos_rot - raw_dz * sin_rot
    zc = raw_dy * sin_rot + raw_dz * cos_rot + z0 + s * x

    theta_vals = np.linspace(0, 2 * math.pi, 180, endpoint=False)
    pts = [
        (yc + log_raw_radius(log, u_log, th - rot) * math.cos(th),
         zc + log_raw_radius(log, u_log, th - rot) * math.sin(th))
        for th in theta_vals
    ]

    z_hits = []
    num_pts = len(pts)
    for i in range(num_pts):
        y1, z1 = pts[i]
        y2, z2 = pts[(i + 1) % num_pts]
        if (y1 <= y <= y2) or (y2 <= y <= y1):
            if abs(y2 - y1) > 1e-6:
                z_hits.append(z1 + (y - y1) / (y2 - y1) * (z2 - z1))
            else:
                z_hits.append(z1)

    if len(z_hits) >= 2:
        return max(z_hits), min(z_hits)
    if len(z_hits) == 1:
        return z_hits[0], z_hits[0]
    cpz = min(pts, key=lambda p: abs(p[0] - y))[1]
    return cpz, cpz


def build_moss_meshes(
    foundation_top: float, logs: list, best_perm: list[int], best_flips: list[bool],
    best_rots: list[float], best_placements: list[tuple[float, float]], prefix: str, axis: str,
    origin_x: float, origin_y: float, origin_z: float, x_min: float, x_max: float,
) -> list[Mesh]:
    x_vals = np.linspace(x_min, x_max, NUM_X_STATIONS)
    y_front, y_back = -MOSS_WIDTH / 2.0, MOSS_WIDTH / 2.0
    EMBED_MM = 3.0

    moss_meshes = []
    for cav_idx in range(len(logs)):
        name = f"moss_{prefix}_foundation_joint" if cav_idx == 0 else f"moss_{prefix}_cavity_tier_{cav_idx}_to_{cav_idx + 1}"

        if cav_idx > 0:
            low_log = logs[best_perm[cav_idx - 1]]
            low_flip = best_flips[cav_idx - 1]
            low_rot = best_rots[cav_idx - 1]
            low_z0, low_s = best_placements[cav_idx - 1]

        high_log = logs[best_perm[cav_idx]]
        high_flip = best_flips[cav_idx]
        high_rot = best_rots[cav_idx]
        high_z0, high_s = best_placements[cav_idx]

        verts: list[Point] = []
        faces: list[tuple[int, int, int]] = []

        for x in x_vals:
            if cav_idx == 0:
                z_low_f = z_low_b = foundation_top
            else:
                z_low_f, _ = raytrace_log_z_surfaces(low_log, low_flip, low_rot, low_z0, low_s, x, y_front)
                z_low_b, _ = raytrace_log_z_surfaces(low_log, low_flip, low_rot, low_z0, low_s, x, y_back)

            _, z_high_f = raytrace_log_z_surfaces(high_log, high_flip, high_rot, high_z0, high_s, x, y_front)
            _, z_high_b = raytrace_log_z_surfaces(high_log, high_flip, high_rot, high_z0, high_s, x, y_back)

            z_lf = z_low_f - (0.0 if cav_idx == 0 else EMBED_MM)
            z_lb = z_low_b - (0.0 if cav_idx == 0 else EMBED_MM)
            z_hf = max(z_lf + 1.0, z_high_f + EMBED_MM)
            z_hb = max(z_lb + 1.0, z_high_b + EMBED_MM)

            for py, pz in ((y_front, z_lf), (y_front, z_hf), (y_back, z_hb), (y_back, z_lb)):
                wx = origin_x + (x if axis == "X" else py)
                wy = origin_y + (py if axis == "X" else x)
                verts.append(Point(x=I(wx), y=I(wy), z=I(origin_z + pz)))

        for i in range(NUM_X_STATIONS - 1):
            s0, s1 = i * 4, (i + 1) * 4
            for j in range(4):
                jn = (j + 1) % 4
                if axis == "X":
                    faces.extend([(s0 + j, s1 + j, s1 + jn), (s0 + j, s1 + jn, s0 + jn)])
                else:
                    faces.extend([(s0 + j, s1 + jn, s1 + j), (s0 + j, s0 + jn, s1 + jn)])

        last = (NUM_X_STATIONS - 1) * 4
        if axis == "X":
            faces.extend([(0, 1, 2), (0, 2, 3), (last, last + 3, last + 2), (last, last + 2, last + 1)])
        else:
            faces.extend([(0, 2, 1), (0, 3, 2), (last, last + 2, last + 3), (last, last + 1, last + 2)])

        moss_meshes.append(Mesh(name=name, vertices=verts, faces=faces, is_watertight=True, color=MOSS_COLOR))

    return moss_meshes


def build_foundation_slab_mesh() -> Mesh:
    x0, x1 = SLAB_X_MIN, SLAB_X_MAX
    y0, y1 = SLAB_Y_MIN, SLAB_Y_MAX
    z0, z1 = 0.0, SLAB_HEIGHT

    verts = [
        Point(x=I(x0), y=I(y0), z=I(z0)), Point(x=I(x1), y=I(y0), z=I(z0)),
        Point(x=I(x1), y=I(y1), z=I(z0)), Point(x=I(x0), y=I(y1), z=I(z0)),
        Point(x=I(x0), y=I(y0), z=I(z1)), Point(x=I(x1), y=I(y0), z=I(z1)),
        Point(x=I(x1), y=I(y1), z=I(z1)), Point(x=I(x0), y=I(y1), z=I(z1)),
    ]
    faces = [
        (0, 2, 1), (0, 3, 2), (4, 5, 6), (4, 6, 7),
        (0, 1, 5), (0, 5, 4), (1, 2, 6), (1, 6, 5),
        (2, 3, 7), (2, 7, 6), (3, 0, 4), (3, 4, 7),
    ]
    return Mesh(name="foundation_slab_body", vertices=verts, faces=faces, is_watertight=True, color=SLAB_COLOR)


def build_site_block() -> Site:
    site = Site(name="clearing")
    terrain = Element(ifc_class="IfcGeographicElement", predefined_type="TERRAIN", name="terrain")
    terrain.add(CLEARING)
    site.add(terrain)
    return site


def assert_the_slab_sits_on_the_ground(tol=25.0):
    global CLEARING
    for tag, x, y in [
        ("SW", SLAB_X_MIN, SLAB_Y_MIN), ("SE", SLAB_X_MAX, SLAB_Y_MIN),
        ("NE", SLAB_X_MAX, SLAB_Y_MAX), ("NW", SLAB_X_MIN, SLAB_Y_MAX), ("centre", CENTER_X, CENTER_Y),
    ]:
        ground = CLEARING.height_at(x=x, y=y)
        if abs(0.0 - ground) > tol:
            CLEARING = make_clearing(SLAB_X_MIN, SLAB_X_MAX, SLAB_Y_MIN, SLAB_Y_MAX)
            ground = CLEARING.height_at(x=x, y=y)
        assert abs(0.0 - ground) <= tol, f"Slab {tag} corner hangs {0.0 - ground:.0f}mm over clearing"


def carve_mesh_with_boxes(mesh: Mesh, boxes: list[tuple[float, float, float, float, float, float]]) -> Mesh:
    if not boxes or not mesh.vertices or not mesh.faces:
        return mesh
    import manifold3d
    v_np = np.array([[p.x, p.y, p.z] for p in mesh.vertices], dtype=np.float32)
    f_np = np.array(mesh.faces, dtype=np.int32)
    try:
        m = manifold3d.Manifold(manifold3d.Mesh(vert_properties=v_np, tri_verts=f_np))
        modified = False
        for (x0, x1, y0, y1, z0, z1) in boxes:
            bb = m.bounding_box()
            if x0 > bb[3] or x1 < bb[0] or y0 > bb[4] or y1 < bb[1] or z0 > bb[5] or z1 < bb[2]:
                continue
            box_m = manifold3d.Manifold.cube([x1 - x0, y1 - y0, z1 - z0]).translate([x0, y0, z0])
            inter = m ^ box_m
            if inter.volume() > 1e-4:
                m = m - box_m
                modified = True
        if not modified:
            return mesh
        res = m.to_mesh()
        new_verts = [Point(x=I(p[0]), y=I(p[1]), z=I(p[2])) for p in res.vert_properties]
        new_faces = [tuple(int(idx) for idx in tri) for tri in res.tri_verts]
        return Mesh(name=mesh.name, vertices=new_verts, faces=new_faces, is_watertight=True, color=mesh.color)
    except Exception as ex:
        return mesh


def make_box_verts_faces(x0: float, x1: float, y0: float, y1: float, z0: float, z1: float, v_offset: int = 0):
    verts = [
        Point(x=I(x0), y=I(y0), z=I(z0)), Point(x=I(x1), y=I(y0), z=I(z0)),
        Point(x=I(x1), y=I(y1), z=I(z0)), Point(x=I(x0), y=I(y1), z=I(z0)),
        Point(x=I(x0), y=I(y0), z=I(z1)), Point(x=I(x1), y=I(y0), z=I(z1)),
        Point(x=I(x1), y=I(y1), z=I(z1)), Point(x=I(x0), y=I(y1), z=I(z1)),
    ]
    raw_faces = [
        (0, 2, 1), (0, 3, 2), (4, 5, 6), (4, 6, 7),
        (0, 1, 5), (0, 5, 4), (1, 2, 6), (1, 6, 5),
        (2, 3, 7), (2, 7, 6), (3, 0, 4), (3, 4, 7),
    ]
    faces = [(a + v_offset, b + v_offset, c + v_offset) for a, b, c in raw_faces]
    return verts, faces


def build_window_meshes(name: str, axis: str, ox: float, oy: float, base_z: float, width: float = 1000.0, height: float = 1200.0, depth: float = 120.0, frame_thk: float = 55.0) -> list[Mesh]:
    """Generates a single pine outer casing frame and a real sky-blue glass pane, oriented along axis ('X' or 'Y')."""
    half_d = depth / 2.0
    f_verts, f_faces = [], []

    if axis == "X":
        x0, x1 = ox, ox + width
        y0, y1 = oy - half_d, oy + half_d
        z0, z1 = base_z, base_z + height
        boxes = [
            (x0, x1, y0, y1, z0, z0 + frame_thk),
            (x0, x1, y0, y1, z1 - frame_thk, z1),
            (x0, x0 + frame_thk, y0, y1, z0 + frame_thk, z1 - frame_thk),
            (x1 - frame_thk, x1, y0, y1, z0 + frame_thk, z1 - frame_thk),
        ]
        for bx0, bx1, by0, by1, bz0, bz1 in boxes:
            v, f = make_box_verts_faces(bx0, bx1, by0, by1, bz0, bz1, len(f_verts))
            f_verts.extend(v)
            f_faces.extend(f)

        frame_mesh = Mesh(name=f"{name}_frame", vertices=f_verts, faces=f_faces, is_watertight=True, color="#C28D58")

        gv, gf = make_box_verts_faces(x0 + frame_thk, x1 - frame_thk, oy - 8.0, oy + 8.0, z0 + frame_thk, z1 - frame_thk)
        glass_mesh = Mesh(name=f"{name}_glass", vertices=gv, faces=gf, is_watertight=True, color="#9cc8e8")
    else:
        x0, x1 = ox - half_d, ox + half_d
        y0, y1 = oy, oy + width
        z0, z1 = base_z, base_z + height
        boxes = [
            (x0, x1, y0, y1, z0, z0 + frame_thk),
            (x0, x1, y0, y1, z1 - frame_thk, z1),
            (x0, x1, y0, y0 + frame_thk, z0 + frame_thk, z1 - frame_thk),
            (x0, x1, y1 - frame_thk, y1, z0 + frame_thk, z1 - frame_thk),
        ]
        for bx0, bx1, by0, by1, bz0, bz1 in boxes:
            v, f = make_box_verts_faces(bx0, bx1, by0, by1, bz0, bz1, len(f_verts))
            f_verts.extend(v)
            f_faces.extend(f)

        frame_mesh = Mesh(name=f"{name}_frame", vertices=f_verts, faces=f_faces, is_watertight=True, color="#C28D58")

        gv, gf = make_box_verts_faces(ox - 8.0, ox + 8.0, y0 + frame_thk, y1 - frame_thk, z0 + frame_thk, z1 - frame_thk)
        glass_mesh = Mesh(name=f"{name}_glass", vertices=gv, faces=gf, is_watertight=True, color="#9cc8e8")

    return [frame_mesh, glass_mesh]


def build_door_meshes(name: str, axis: str, ox: float, oy: float, base_z: float, width: float = 1000.0, height: float = 2100.0, depth: float = 140.0, frame_thk: float = 55.0) -> list[Mesh]:
    """Generates a timber door casing frame and a solid wood door leaf, oriented along axis ('X' or 'Y')."""
    half_d = depth / 2.0
    f_verts, f_faces = [], []

    if axis == "X":
        x0, x1 = ox, ox + width
        y0, y1 = oy - half_d, oy + half_d
        z0, z1 = base_z, base_z + height
        boxes = [
            (x0, x1, y0, y1, z0, z0 + 20.0),
            (x0, x1, y0, y1, z1 - frame_thk, z1),
            (x0, x0 + frame_thk, y0, y1, z0 + 20.0, z1 - frame_thk),
            (x1 - frame_thk, x1, y0, y1, z0 + 20.0, z1 - frame_thk),
        ]
        for bx0, bx1, by0, by1, bz0, bz1 in boxes:
            v, f = make_box_verts_faces(bx0, bx1, by0, by1, bz0, bz1, len(f_verts))
            f_verts.extend(v)
            f_faces.extend(f)

        frame_mesh = Mesh(name=f"{name}_frame", vertices=f_verts, faces=f_faces, is_watertight=True, color="#C28D58")

        lv, lf = make_box_verts_faces(x0 + frame_thk + 5.0, x1 - frame_thk - 5.0, oy - 20.0, oy + 20.0, z0 + 20.0, z1 - frame_thk - 5.0)
        leaf_mesh = Mesh(name=f"{name}_leaf", vertices=lv, faces=lf, is_watertight=True, color="#8B5A2B")
    else:
        x0, x1 = ox - half_d, ox + half_d
        y0, y1 = oy, oy + width
        z0, z1 = base_z, base_z + height
        boxes = [
            (x0, x1, y0, y1, z0, z0 + 20.0),
            (x0, x1, y0, y1, z1 - frame_thk, z1),
            (x0, x1, y0, y0 + frame_thk, z0 + 20.0, z1 - frame_thk),
            (x0, x1, y1 - frame_thk, y1, z0 + 20.0, z1 - frame_thk),
        ]
        for bx0, bx1, by0, by1, bz0, bz1 in boxes:
            v, f = make_box_verts_faces(bx0, bx1, by0, by1, bz0, bz1, len(f_verts))
            f_verts.extend(v)
            f_faces.extend(f)

        frame_mesh = Mesh(name=f"{name}_frame", vertices=f_verts, faces=f_faces, is_watertight=True, color="#C28D58")

        lv, lf = make_box_verts_faces(ox - 20.0, ox + 20.0, y0 + frame_thk + 5.0, y1 - frame_thk - 5.0, z0 + 20.0, z1 - frame_thk - 5.0)
        leaf_mesh = Mesh(name=f"{name}_leaf", vertices=lv, faces=lf, is_watertight=True, color="#8B5A2B")

    return [frame_mesh, leaf_mesh]


def generate_project() -> Project:
    global CLEARING
    CLEARING = make_clearing(SLAB_X_MIN, SLAB_X_MAX, SLAB_Y_MIN, SLAB_Y_MAX)
    proj = Project(name="Laftehus — Procedural Log Cabin (4x6 m Single Room)")

    site = build_site_block()
    assert_the_slab_sits_on_the_ground()
    proj.add(site)

    rng = np.random.RandomState(RANDOM_SEED)

    logs_s = [make_log(i, LOG_LEN_LONG, rng) for i in range(NUM_LOGS_PER_WALL)]
    logs_n = [make_log(i + NUM_LOGS_PER_WALL, LOG_LEN_LONG, rng) for i in range(NUM_LOGS_PER_WALL)]
    logs_w = [make_log(i + 2 * NUM_LOGS_PER_WALL, LOG_LEN_SHORT, rng) for i in range(NUM_LOGS_PER_WALL)]
    logs_e = [make_log(i + 3 * NUM_LOGS_PER_WALL, LOG_LEN_SHORT, rng) for i in range(NUM_LOGS_PER_WALL)]

    opt_s = optimize_log_wall(logs_s, base_height=SLAB_HEIGHT)
    opt_n = optimize_log_wall(logs_n, base_height=SLAB_HEIGHT)
    opt_w = optimize_log_wall(logs_w, base_height=SLAB_HEIGHT + WALL_Z_RAISE)
    opt_e = optimize_log_wall(logs_e, base_height=SLAB_HEIGHT + WALL_Z_RAISE)

    slab_elem = Slab(name="foundation_slab")
    slab_elem.add(Box(name="foundation_slab_body", start=Point(x=I(SLAB_X_MIN), y=I(SLAB_Y_MIN), z=0), end=Point(x=I(SLAB_X_MAX), y=I(SLAB_Y_MAX), z=I(SLAB_HEIGHT)), color=SLAB_COLOR))
    proj.add(slab_elem)

    half_lx = CABIN_LENGTH / 2.0
    half_ly = CABIN_WIDTH / 2.0

    wall_configs = [
        ("south", logs_s, opt_s, "X", CENTER_X, CENTER_Y - half_ly, -half_lx, half_lx, LOG_PALETTES[0]),
        ("north", logs_n, opt_n, "X", CENTER_X, CENTER_Y + half_ly, -half_lx, half_lx, LOG_PALETTES[1]),
        ("west", logs_w, opt_w, "Y", CENTER_X - half_lx, CENTER_Y, -half_ly, half_ly, LOG_PALETTES[2]),
        ("east", logs_e, opt_e, "Y", CENTER_X + half_lx, CENTER_Y, -half_ly, half_ly, LOG_PALETTES[3]),
    ]

    def get_wall_opening_boxes(w_prefix: str) -> list[tuple[float, float, float, float, float, float]]:
        boxes = []
        T_CUT = 1200.0
        if w_prefix == "south":
            oy = CENTER_Y - half_ly
            # Door at along=1200 (measured from x_start)
            dx0 = CENTER_X - half_lx - CORNER_OVERHANG + 1200.0
            boxes.append((dx0, dx0 + 1000.0, oy - T_CUT/2, oy + T_CUT/2, SLAB_HEIGHT, SLAB_HEIGHT + 2100.0))
            # Window on right side of south wall
            wx0 = CENTER_X + half_lx + CORNER_OVERHANG - 2200.0
            boxes.append((wx0, wx0 + 1000.0, oy - T_CUT/2, oy + T_CUT/2, SLAB_HEIGHT + 800.0, SLAB_HEIGHT + 800.0 + 1200.0))
        elif w_prefix == "north":
            oy = CENTER_Y + half_ly
            # Center of north wall
            wx0 = CENTER_X - 500.0
            boxes.append((wx0, wx0 + 1000.0, oy - T_CUT/2, oy + T_CUT/2, SLAB_HEIGHT + 800.0, SLAB_HEIGHT + 800.0 + 1200.0))
        elif w_prefix == "west":
            ox = CENTER_X - half_lx
            # Center of west wall
            wy0 = CENTER_Y - 500.0
            boxes.append((ox - T_CUT/2, ox + T_CUT/2, wy0, wy0 + 1000.0, SLAB_HEIGHT + 800.0, SLAB_HEIGHT + 800.0 + 1200.0))
        elif w_prefix == "east":
            ox = CENTER_X + half_lx
            # Center of east wall
            wy0 = CENTER_Y - 500.0
            boxes.append((ox - T_CUT/2, ox + T_CUT/2, wy0, wy0 + 1000.0, SLAB_HEIGHT + 800.0, SLAB_HEIGHT + 800.0 + 1200.0))
        return boxes

    for prefix, w_logs, opt, axis, ox, oy, xmin, xmax, pal in wall_configs:
        wall = Wall(name=f"log_wall_{prefix}")
        w_boxes = get_wall_opening_boxes(prefix)
        for rank in range(NUM_LOGS_PER_WALL):
            lid = opt[0][rank]
            log_m = build_log_mesh(
                w_logs[lid], opt[1][rank], opt[2][rank], opt[3][rank][0], opt[3][rank][1],
                pal[rank % len(pal)], f"{prefix}_tier_{rank + 1}", axis, ox, oy, 0.0,
            )
            wall.add(carve_mesh_with_boxes(log_m, w_boxes))

        for m in build_moss_meshes(
            SLAB_HEIGHT, w_logs, opt[0], opt[1], opt[2], opt[3], prefix, axis, ox, oy, 0.0, xmin, xmax
        ):
            wall.add(carve_mesh_with_boxes(m, w_boxes))

        if prefix == "south":
            dx0 = CENTER_X - half_lx - CORNER_OVERHANG + 1200.0
            wx0 = CENTER_X + half_lx + CORNER_OVERHANG - 2200.0
            for m in build_door_meshes("door_south", "X", dx0, oy, SLAB_HEIGHT, 1000.0, 2100.0):
                wall.add(m)
            for m in build_window_meshes("win_south", "X", wx0, oy, SLAB_HEIGHT + 800.0, 1000.0, 1200.0):
                wall.add(m)
        elif prefix == "north":
            wx0 = CENTER_X - 500.0
            for m in build_window_meshes("win_north", "X", wx0, oy, SLAB_HEIGHT + 800.0, 1000.0, 1200.0):
                wall.add(m)
        elif prefix == "west":
            wy0 = CENTER_Y - 500.0
            for m in build_window_meshes("win_west", "Y", ox, wy0, SLAB_HEIGHT + 800.0, 1000.0, 1200.0):
                wall.add(m)
        elif prefix == "east":
            wy0 = CENTER_Y - 500.0
            for m in build_window_meshes("win_east", "Y", ox, wy0, SLAB_HEIGHT + 800.0, 1000.0, 1200.0):
                wall.add(m)

        proj.add(wall)

    return proj


result = generate_project()

if __name__ == "__main__":
    compile_main()
