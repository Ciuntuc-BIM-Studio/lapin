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

export type SnapMode = 'endpoint' | 'midpoint' | 'center' | 'quadrant' | 'intersection' | 'perpendicular' | 'nearest' | 'tangent' | 'wallAxis';
export const ALL_MODES: ReadonlySet<SnapMode> = new Set<SnapMode>(['endpoint', 'midpoint', 'center', 'quadrant', 'intersection', 'perpendicular', 'nearest', 'tangent', 'wallAxis']);

/** A broad phase: each shape's box in the cells of a grid. */
export class SnapIndex {
  private readonly cells = new Map<string, number[]>();
  private readonly big: number[] = [];
  private readonly cell: number;

  constructor(shapes: readonly Shape[], cell?: number) {
    const boxes = shapes.map(bounds);
    if (cell == null) {
      const sizes = boxes.map((b) => Math.max(b.max.x - b.min.x, b.max.y - b.min.y));
      const avg = sizes.length ? sizes.reduce((s, x) => s + x, 0) / sizes.length : 1;
      cell = Math.max(avg * 2, 1e-6);
    }
    this.cell = cell;
    boxes.forEach((b, i) => {
      const [x0, x1, y0, y1] = [this.c(b.min.x), this.c(b.max.x), this.c(b.min.y), this.c(b.max.y)];
      if ((x1 - x0 + 1) * (y1 - y0 + 1) > 4096) { this.big.push(i); return; }
      for (let x = x0; x <= x1; x++) for (let y = y0; y <= y1; y++) {
        const k = `${x},${y}`;
        const l = this.cells.get(k);
        if (l) l.push(i); else this.cells.set(k, [i]);
      }
    });
  }

  private c(v: number) { return Math.floor(v / this.cell); }

  /** The shapes whose box may meet the query box, sorted. */
  query(min: Vec2, max: Vec2): number[] {
    const out = new Set<number>(this.big);
    for (let x = this.c(min.x); x <= this.c(max.x); x++) for (let y = this.c(min.y); y <= this.c(max.y); y++)
      for (const i of this.cells.get(`${x},${y}`) ?? []) out.add(i);
    return [...out].sort((a, b) => a - b);
  }
}

export interface SnapOptions {
  tolerance: number; modes: ReadonlySet<SnapMode>; from?: Vec2 | null;
  /** The walls' axes (a line or an arc each): the Wall axis snap's. */
  wallAxes?: readonly Shape[];
  /** Curves the drawn lines only approximate (an element's arcs, chorded): the Tangent snap's, besides the drawn circles, arcs and ellipses. */
  curves?: readonly Shape[];
}
/**
 * `shape` is the index in the shapes searched (−1: a wall axis or an element curve, then `on` is it). `along`: a point anywhere
 * on the wall axis (a line snap, as nearest is), not one of its ends or its middle.
 */
export interface Snap { point: Vec2; mode: SnapMode; shape: number; on?: Shape; along?: boolean }

/** A snap that slides along a line (the nearest point, a point on a wall axis), not a point of its own: ortho and the guides go first. */
export const isLineSnap = (s: { mode: SnapMode; along?: boolean }) => s.mode === 'nearest' || (s.mode === 'wallAxis' && !!s.along);

type Key = SnapMode | 'axisPoint';
const ORDER: Key[] = ['endpoint', 'intersection', 'axisPoint', 'midpoint', 'tangent', 'center', 'quadrant', 'perpendicular'];

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
  for (const m of ORDER) { const s = best.get(m); if (s) return s; }
  // On a line: the nearer of the wall axis and the drawn lines (the axis on a tie).
  const ax = best.get('wallAxis'), ne = best.get('nearest');
  if (ax && ne) return dist(ax.point, p) <= dist(ne.point, p) + 1e-12 ? ax : ne;
  return ax ?? ne ?? null;
}
