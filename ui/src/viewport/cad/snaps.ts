/**
 * Object snaps (the old Lapin's drafting snaps, same rules): endpoint >
 * intersection > midpoint > center > quadrant > perpendicular > nearest,
 * within a tolerance, over a uniform grid index of the shapes' boxes.
 */

import type { Vec2 } from '../../platform/module';
import { bounds, dist, intersectShapes, keyPoints, nearestOnShape, prims, type Shape } from './shapes';

export type SnapMode = 'endpoint' | 'midpoint' | 'center' | 'quadrant' | 'intersection' | 'perpendicular' | 'nearest';
export const ALL_MODES: ReadonlySet<SnapMode> = new Set<SnapMode>(['endpoint', 'midpoint', 'center', 'quadrant', 'intersection', 'perpendicular', 'nearest']);

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

export interface SnapOptions { tolerance: number; modes: ReadonlySet<SnapMode>; from?: Vec2 | null }
export interface Snap { point: Vec2; mode: SnapMode; shape: number }

const ORDER: SnapMode[] = ['endpoint', 'intersection', 'midpoint', 'center', 'quadrant', 'perpendicular', 'nearest'];

export function findSnap(p: Vec2, shapes: readonly Shape[], index: SnapIndex | null, o: SnapOptions): Snap | null {
  const tol = o.tolerance;
  const ids = index ? index.query({ x: p.x - tol, y: p.y - tol }, { x: p.x + tol, y: p.y + tol }) : shapes.map((_, i) => i);
  const near = ids.filter((i) => nearestOnShape(shapes[i], p).dist <= tol || keyPoints(shapes[i]).centers.some((c) => dist(c, p) <= tol));
  const best = new Map<SnapMode, Snap>();
  const offer = (mode: SnapMode, point: Vec2, shape: number) => {
    if (!o.modes.has(mode) || dist(point, p) > tol) return;
    const cur = best.get(mode);
    if (!cur || dist(point, p) < dist(cur.point, p)) best.set(mode, { point, mode, shape });
  };
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
    offer('nearest', nearestOnShape(s, p).point, i);
  }
  if (o.modes.has('intersection'))
    for (let a = 0; a < near.length; a++) for (let b = a + 1; b < near.length; b++)
      for (const q of intersectShapes(shapes[near[a]], shapes[near[b]])) offer('intersection', q, near[a]);
  for (const m of ORDER) { const s = best.get(m); if (s) return s; }
  return null;
}
