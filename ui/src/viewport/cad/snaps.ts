/**
 * Object snaps (the old Lapin's drafting snaps, same rules): endpoint >
 * intersection > midpoint > center > quadrant > perpendicular > nearest,
 * within a tolerance, over a uniform grid index of the shapes' boxes.
 *
 * Forum topic 151 adds two, never ahead of the drawn lines' ends and crossings: endpoint > intersection > wall axis (its ends and
 * middle) > midpoint > tangent > centre > quadrant > perpendicular > on a line (the wall axis or the nearest drawn line, the
 * nearer of the two). The tangent goes before the centre because a curve offers its centre wherever the cursor is on it. The
 * wall axes (the axis each wall is placed by) come in `wallAxes`; the tangent takes the drawn circles, arcs and ellipses, and the
 * elements' own curves in `curves` (a curved wall's faces and axis, an outline's curved edges), from the previous point `from`.
 */

import type { Vec2 } from '../../platform/module';
import { angleInArc, bounds, dist, ellipseWhole, intersectShapes, keyPoints, nearestOnShape, prims, type Shape } from './shapes';

export type SnapMode = 'endpoint' | 'midpoint' | 'center' | 'quadrant' | 'intersection' | 'perpendicular' | 'nearest' | 'tangent' | 'wallAxis' | 'gridAxis' | 'division' | 'distance';
export const ALL_MODES: ReadonlySet<SnapMode> = new Set<SnapMode>(['endpoint', 'midpoint', 'center', 'quadrant', 'intersection', 'perpendicular', 'nearest', 'tangent', 'wallAxis', 'gridAxis', 'division', 'distance']);

/**
 * A broad phase: each shape's box in the cells of a grid. The cell is twice the shapes' average size, and never finer than
 * a 1024th of what they all span: a point cloud's slice in a plan gives thousands of snap points, shapes of no size, and
 * with little else drawn the average fell to nothing, the cell to a micrometre, and a query a few centimetres wide swept
 * billions of cells on every move of the mouse (forum topic 222, the plan frozen). A query wider than the cells there are
 * walks those cells instead of the grid.
 */
export class SnapIndex {
  private readonly cells = new Map<string, { x: number; y: number; ids: number[] }>();
  private readonly big: number[] = [];
  private readonly cell: number;

  constructor(shapes: readonly Shape[], cell?: number) {
    const boxes = shapes.map(bounds);
    if (cell == null) {
      const sizes = boxes.map((b) => Math.max(b.max.x - b.min.x, b.max.y - b.min.y)).filter(Number.isFinite);
      const avg = sizes.length ? sizes.reduce((s, x) => s + x, 0) / sizes.length : 1;
      let x0 = Infinity, y0 = Infinity, x1 = -Infinity, y1 = -Infinity;
      for (const b of boxes) {
        if (Number.isFinite(b.min.x)) x0 = Math.min(x0, b.min.x);
        if (Number.isFinite(b.min.y)) y0 = Math.min(y0, b.min.y);
        if (Number.isFinite(b.max.x)) x1 = Math.max(x1, b.max.x);
        if (Number.isFinite(b.max.y)) y1 = Math.max(y1, b.max.y);
      }
      const span = x1 >= x0 && y1 >= y0 ? Math.max(x1 - x0, y1 - y0) : 0;
      cell = Math.max(avg * 2, span / 1024, 1e-6);
    }
    this.cell = cell;
    boxes.forEach((b, i) => {
      const [x0, x1, y0, y1] = [this.c(b.min.x), this.c(b.max.x), this.c(b.min.y), this.c(b.max.y)];
      // A shape whose box is not finite is kept apart, offered to every query.
      if (![x0, x1, y0, y1].every(Number.isFinite) || (x1 - x0 + 1) * (y1 - y0 + 1) > 4096) { this.big.push(i); return; }
      for (let x = x0; x <= x1; x++) for (let y = y0; y <= y1; y++) {
        const k = `${x},${y}`;
        const l = this.cells.get(k);
        if (l) l.ids.push(i); else this.cells.set(k, { x, y, ids: [i] });
      }
    });
  }

  private c(v: number) { return Math.floor(v / this.cell); }

  /** The shapes whose box may meet the query box, sorted. */
  query(min: Vec2, max: Vec2): number[] {
    const out = new Set<number>(this.big);
    const [x0, x1, y0, y1] = [this.c(min.x), this.c(max.x), this.c(min.y), this.c(max.y)];
    if (![x0, x1, y0, y1].every(Number.isFinite) || (x1 - x0 + 1) * (y1 - y0 + 1) > this.cells.size) {
      // Wider than the cells there are (or not a box): those cells walked, the ones inside it taken.
      for (const c of this.cells.values()) if (c.x >= x0 && c.x <= x1 && c.y >= y0 && c.y <= y1) for (const i of c.ids) out.add(i);
    } else {
      for (let x = x0; x <= x1; x++) for (let y = y0; y <= y1; y++)
        for (const i of this.cells.get(`${x},${y}`)?.ids ?? []) out.add(i);
    }
    return [...out].sort((a, b) => a - b);
  }
}

export interface SnapOptions {
  tolerance: number; modes: ReadonlySet<SnapMode>; from?: Vec2 | null;
  /** The walls' axes (a line or an arc each): the Wall axis snap's. */
  wallAxes?: readonly Shape[];
  /** Curves the drawn lines only approximate (an element's arcs, chorded): the Tangent snap's, besides the drawn circles, arcs and ellipses. */
  curves?: readonly Shape[];
  /** The shown grid's axes (a line each: IfcGrid's): the Grid axis snap's, and all that "Grid only" takes. */
  gridAxes?: readonly Shape[];
  /** The Division snap's: each edge cut into this many equal parts (3: thirds, 4: quarters), a point at each cut (the middle is the Midpoint's). */
  divisions?: readonly number[];
  /** The Distance snap's: a point this far (m) from each end of an edge, along it (forum topic 226). */
  distance?: number;
}
/**
 * `shape` is the index in the shapes searched (−1: a wall axis or an element curve, then `on` is it). `along`: a point anywhere
 * on the wall axis (a line snap, as nearest is), not one of its ends or its middle.
 */
export interface Snap { point: Vec2; mode: SnapMode; shape: number; on?: Shape; along?: boolean }

/** A snap that slides along a line (the nearest point, a point on a wall axis), not a point of its own: ortho and the guides go first. */
export const isLineSnap = (s: { mode: SnapMode; along?: boolean }) => s.mode === 'nearest' || ((s.mode === 'wallAxis' || s.mode === 'gridAxis') && !!s.along);

type Key = SnapMode | 'axisPoint' | 'gridPoint' | 'gridPerp';
const ORDER: Key[] = ['endpoint', 'intersection', 'axisPoint', 'gridPoint', 'midpoint', 'division', 'distance', 'tangent', 'center', 'quadrant', 'perpendicular', 'gridPerp'];

/** Where a line from `from` touches the curve (a circle, an arc where it runs, an ellipse or an elliptical arc where it runs): none from inside it or on it. */
export function tangentPoints(s: Shape, from: Vec2): Vec2[] {
  if (s.type === 'circle' || s.type === 'arc') {
    const rx = from.x - s.c.x, ry = from.y - s.c.y, dc = Math.hypot(rx, ry);
    if (dc <= s.r * (1 + 1e-9)) return [];
    const a0 = Math.atan2(ry, rx), da = Math.acos(s.r / dc);
    return [a0 + da, a0 - da].filter((a) => s.type === 'circle' || angleInArc(a, s.start, s.end)).map((a) => ({ x: s.c.x + s.r * Math.cos(a), y: s.c.y + s.r * Math.sin(a) }));
  }
  if (s.type === 'ellipse') {
    // P(t) = c + m cos t + n sin t touches the line from B where (P − B) × P′ = 0: K + A cos t + B' sin t = 0, with
    // A = (c − B) × n, B' = −(c − B) × m, K = m × n.
    const n = { x: -s.m.y * s.ratio, y: s.m.x * s.ratio };
    const u = { x: s.c.x - from.x, y: s.c.y - from.y };
    const cr = (p: Vec2, q: Vec2) => p.x * q.y - p.y * q.x;
    const A = cr(u, n), B = -cr(u, s.m), K = cr(s.m, n), R = Math.hypot(A, B);
    if (R < 1e-15 || Math.abs(K) >= R * (1 - 1e-12)) return [];
    const phi = Math.atan2(B, A), w = Math.acos(-K / R);
    const whole = ellipseWhole(s);
    return [phi + w, phi - w].filter((t) => whole || angleInArc(t, s.start, s.end))
      .map((t) => ({ x: s.c.x + s.m.x * Math.cos(t) + n.x * Math.sin(t), y: s.c.y + s.m.y * Math.cos(t) + n.y * Math.sin(t) }));
  }
  return [];
}


/**
 * The points the Division and Distance snaps offer on a shape (forum topic 226, Archicad's intelligent snap points): each
 * straight edge cut into `divisions` equal parts (thirds, quarters…), each arc into equal angles, and a point `distance`
 * (m) in from each end of every edge. The ends and the middle belong to the Endpoint and Midpoint snaps; none sits on an end.
 */
export function edgePoints(s: Shape, divisions: readonly number[], distance: number): { division: Vec2[]; distance: Vec2[] } {
  const out = { division: [] as Vec2[], distance: [] as Vec2[] };
  if (s.type === 'ellipse') return out;
  const add = (list: Vec2[], q: Vec2) => { if (!list.some((r) => dist(r, q) < 1e-9)) list.push(q); };
  const ns = [...new Set(divisions.filter((n) => Number.isInteger(n) && n >= 2 && n <= 64))];
  for (const pr of prims(s)) {
    if (pr.kind === 'seg') {
      const len = dist(pr.a, pr.b);
      if (len < 1e-9) continue;
      const at = (t: number): Vec2 => ({ x: pr.a.x + (pr.b.x - pr.a.x) * t, y: pr.a.y + (pr.b.y - pr.a.y) * t });
      for (const n of ns) for (let k = 1; k < n; k++) if (Math.abs(k / n - 0.5) > 1e-9) add(out.division, at(k / n));
      if (distance > 1e-9 && distance < len - 1e-9) { add(out.distance, at(distance / len)); add(out.distance, at(1 - distance / len)); }
    } else if (!pr.full) {
      const sweep = (((pr.end - pr.start) % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI) || 2 * Math.PI;
      const P = (a: number): Vec2 => ({ x: pr.c.x + pr.r * Math.cos(a), y: pr.c.y + pr.r * Math.sin(a) });
      for (const n of ns) for (let k = 1; k < n; k++) if (Math.abs(k / n - 0.5) > 1e-9) add(out.division, P(pr.start + (sweep * k) / n));
      const da = distance / pr.r, arcLen = sweep * pr.r;
      if (distance > 1e-9 && distance < arcLen - 1e-9) { add(out.distance, P(pr.start + da)); add(out.distance, P(pr.start + sweep - da)); }
    }
  }
  return out;
}

export function findSnap(p: Vec2, shapes: readonly Shape[], index: SnapIndex | null, o: SnapOptions): Snap | null {
  const tol = o.tolerance;
  const ids = index ? index.query({ x: p.x - tol, y: p.y - tol }, { x: p.x + tol, y: p.y + tol }) : shapes.map((_, i) => i);
  const near = ids.filter((i) => nearestOnShape(shapes[i], p).dist <= tol || keyPoints(shapes[i]).centers.some((c) => dist(c, p) <= tol));
  const best = new Map<Key, Snap>();
  const offerAs = (key: Key, s: Snap) => {
    if (!o.modes.has(s.mode) || dist(s.point, p) > tol) return;
    const cur = best.get(key);
    if (!cur || dist(s.point, p) < dist(cur.point, p)) best.set(key, s);
  };
  const offer = (mode: SnapMode, point: Vec2, shape: number) => offerAs(mode, { point, mode, shape });
  for (const i of near) {
    const s = shapes[i];
    const k = keyPoints(s);
    for (const q of k.endpoints) offer('endpoint', q, i);
    for (const q of k.midpoints) offer('midpoint', q, i);
    for (const q of k.quadrants) offer('quadrant', q, i);
    if (o.modes.has('division') || o.modes.has('distance')) {
      const e = edgePoints(s, o.divisions ?? [], o.distance ?? 0);
      for (const q of e.division) offer('division', q, i);
      for (const q of e.distance) offer('distance', q, i);
    }
    // A centre from its curve, or from near the centre itself.
    for (const c of k.centers)
      if (o.modes.has('center') && (nearestOnShape(s, p).dist <= tol || dist(c, p) <= tol)) {
        const cur = best.get('center');
        if (!cur) best.set('center', { point: c, mode: 'center', shape: i });
      }
    if (o.from) for (const pr of prims(s)) if (pr.kind === 'seg') {
      const dx = pr.b.x - pr.a.x, dy = pr.b.y - pr.a.y, l2 = dx * dx + dy * dy;
      if (l2 < 1e-18) continue;
      const t = ((o.from.x - pr.a.x) * dx + (o.from.y - pr.a.y) * dy) / l2;
      if (t >= 0 && t <= 1) offer('perpendicular', { x: pr.a.x + dx * t, y: pr.a.y + dy * t }, i);
    }
    if (o.from && (s.type === 'circle' || s.type === 'arc' || s.type === 'ellipse')) for (const q of tangentPoints(s, o.from)) offer('tangent', q, i);
    offer('nearest', nearestOnShape(s, p).point, i);
  }
  if (o.modes.has('intersection'))
    for (let a = 0; a < near.length; a++) for (let b = a + 1; b < near.length; b++)
      for (const q of intersectShapes(shapes[near[a]], shapes[near[b]])) offer('intersection', q, near[a]);
  if (o.from && o.modes.has('tangent'))
    for (const c of o.curves ?? []) for (const q of tangentPoints(c, o.from)) offerAs('tangent', { point: q, mode: 'tangent', shape: -1, on: c });
  if (o.modes.has('wallAxis'))
    for (const ax of o.wallAxes ?? []) {
      const k = keyPoints(ax);
      for (const q of [...k.endpoints, ...k.midpoints]) offerAs('axisPoint', { point: q, mode: 'wallAxis', shape: -1, on: ax });

      offerAs('wallAxis', { point: nearestOnShape(ax, p).point, mode: 'wallAxis', shape: -1, on: ax, along: true });
    }
  // A wall's axis is an edge too: its thirds, quarters and a distance from its ends (the same Division and Distance snaps).
  if (o.modes.has('division') || o.modes.has('distance'))
    for (const ax of o.wallAxes ?? []) {
      const e = edgePoints(ax, o.divisions ?? [], o.distance ?? 0);
      for (const q of e.division) offerAs('division', { point: q, mode: 'division', shape: -1, on: ax });
      for (const q of e.distance) offerAs('distance', { point: q, mode: 'distance', shape: -1, on: ax });
    }
  if (o.modes.has('gridAxis')) {
    const axes = o.gridAxes ?? [];
    for (const ax of axes) {
      for (const q of keyPoints(ax).endpoints) offerAs('gridPoint', { point: q, mode: 'gridAxis', shape: -1, on: ax });
      if (o.from) for (const pr of prims(ax)) if (pr.kind === 'seg') {
        const dx = pr.b.x - pr.a.x, dy = pr.b.y - pr.a.y, l2 = dx * dx + dy * dy;
        const t = l2 < 1e-18 ? -1 : ((o.from.x - pr.a.x) * dx + (o.from.y - pr.a.y) * dy) / l2;
        if (t >= 0 && t <= 1) offerAs('gridPerp', { point: { x: pr.a.x + dx * t, y: pr.a.y + dy * t }, mode: 'gridAxis', shape: -1, on: ax });
      }
      offerAs('gridAxis', { point: nearestOnShape(ax, p).point, mode: 'gridAxis', shape: -1, on: ax, along: true });
    }
    for (const q of gridCrossings(axes, p, tol)) offerAs('gridPoint', { point: q, mode: 'gridAxis', shape: -1 });
  }
  for (const m of ORDER) { const s = best.get(m); if (s) return s; }
  // On a line: the nearer of the wall axis, the grid axis and the drawn lines (an axis on a tie).
  const ax = best.get('wallAxis'), gx = best.get('gridAxis'), ne = best.get('nearest');
  const axis = ax && gx ? (dist(ax.point, p) <= dist(gx.point, p) + 1e-12 ? ax : gx) : ax ?? gx;
  if (axis && ne) return dist(axis.point, p) <= dist(ne.point, p) + 1e-12 ? axis : ne;
  return axis ?? ne ?? null;
}

/** How far past its ends an axis still counts as crossing another (m): a grid's lines rarely stop exactly at each other. */
const AXIS_REACH = 0.5;

/** Where the grid's axes cross, near `p` (within `tol`): two lines' crossing, each line taken a little past its ends. */
export function gridCrossings(axes: readonly Shape[], p: Vec2, tol: number): Vec2[] {
  const segs: { a: Vec2; b: Vec2 }[] = [];
  for (const ax of axes) if (ax.type === 'line' && dist(ax.a, ax.b) > 1e-9) segs.push(ax);
  const out: Vec2[] = [];
  for (let i = 0; i < segs.length; i++) for (let j = i + 1; j < segs.length; j++) {
    const { a, b } = segs[i], { a: c, b: d } = segs[j];
    const r = { x: b.x - a.x, y: b.y - a.y }, s = { x: d.x - c.x, y: d.y - c.y };
    const den = r.x * s.y - r.y * s.x;
    if (Math.abs(den) < 1e-12 * Math.hypot(r.x, r.y) * Math.hypot(s.x, s.y)) continue;
    const t = ((c.x - a.x) * s.y - (c.y - a.y) * s.x) / den, u = ((c.x - a.x) * r.y - (c.y - a.y) * r.x) / den;
    const lr = Math.hypot(r.x, r.y), ls = Math.hypot(s.x, s.y);
    if (t < -AXIS_REACH / lr || t > 1 + AXIS_REACH / lr || u < -AXIS_REACH / ls || u > 1 + AXIS_REACH / ls) continue;
    const q = { x: a.x + t * r.x, y: a.y + t * r.y };
    if (dist(q, p) <= tol) out.push(q);
  }
  return out;
}

/** Object Snap Override (forum topic 154): the one snap, or 'none', the next point is limited to. */
export type SnapOverride = SnapMode | 'none';

/** AutoCAD's short codes, typed at the command line while a point is asked. */
export const SNAP_CODES: Readonly<Record<string, SnapOverride>> = {
  END: 'endpoint', MID: 'midpoint', INT: 'intersection', CEN: 'center', QUA: 'quadrant', PER: 'perpendicular', TAN: 'tangent', NEA: 'nearest', AXI: 'wallAxis', GRID: 'gridAxis', DIV: 'division', DIS: 'distance', NON: 'none',
};

/** The snaps the next point may use: the override alone, else the running ones. */
export function modesFor(override: SnapOverride | null, running: readonly SnapMode[], gridOnly = false): Set<SnapMode> {
  if (override === 'none') return new Set();
  if (override) return new Set([override]);
  // "Grid only": every other snap is ignored, whatever the SNAP menu has on.
  return gridOnly ? new Set<SnapMode>(['gridAxis']) : new Set(running);
}

/** Whether object snapping applies to the next point: an armed kind snaps even with the SNAP switch off (as in AutoCAD), 'none' never does; "Grid only" snaps too. */
export const snapsOn = (override: SnapOverride | null, osnap: boolean, gridOnly = false): boolean => (override ? override !== 'none' : osnap || gridOnly);
