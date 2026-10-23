/** Snapping in plan (from BubbleBIM Pro's plan view, adapted). */

import type { Vec2 } from '../platform/module';

export interface SnapResult { point: Vec2; kind: 'point' | 'grid' | 'free' }

/**
 * Snap a plan point: to the nearest model point within `pickRadius` (metres),
 * else to the grid. With `ortho`, the point is first squared to the previous
 * one (horizontal or vertical), which is how walls are usually drawn.
 */
export function snap(p: Vec2, opts: { grid: number; points: Vec2[]; pickRadius: number; from?: Vec2; ortho?: boolean }): SnapResult {
  let q = p;
  if (opts.ortho && opts.from) {
    const dx = Math.abs(p.x - opts.from.x), dy = Math.abs(p.y - opts.from.y);
    q = dx >= dy ? { x: p.x, y: opts.from.y } : { x: opts.from.x, y: p.y };
  }
  let best: Vec2 | null = null;
  let bestD = opts.pickRadius;
  for (const c of opts.points) {
    const d = Math.hypot(c.x - q.x, c.y - q.y);
    if (d <= bestD) { bestD = d; best = c; }
  }
  if (best) return { point: best, kind: 'point' };
  if (opts.grid > 0) {
    const g = opts.grid;
    const snapped = { x: Math.round(q.x / g) * g, y: Math.round(q.y / g) * g };
    if (opts.ortho && opts.from) {
      // Keep the squared axis exact.
      if (q.y === opts.from.y) snapped.y = opts.from.y;
      if (q.x === opts.from.x) snapped.x = opts.from.x;
    }
    return { point: { x: round(snapped.x), y: round(snapped.y) }, kind: 'grid' };
  }
  return { point: q, kind: 'free' };
}

const round = (v: number) => Math.round(v * 1e6) / 1e6;

/** Distance from a point to a segment. */
export function distToSegment(p: Vec2, a: Vec2, b: Vec2): number {
  const dx = b.x - a.x, dy = b.y - a.y;
  const l2 = dx * dx + dy * dy;
  const t = l2 > 0 ? Math.max(0, Math.min(1, ((p.x - a.x) * dx + (p.y - a.y) * dy) / l2)) : 0;
  return Math.hypot(p.x - (a.x + t * dx), p.y - (a.y + t * dy));
}

export function pointInPolygon(p: Vec2, poly: Vec2[]): boolean {
  let inside = false;
  for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
    const a = poly[i], b = poly[j];
    if ((a.y > p.y) !== (b.y > p.y) && p.x < ((b.x - a.x) * (p.y - a.y)) / (b.y - a.y) + a.x) inside = !inside;
  }
  return inside;
}

export const pairs = (flat: ArrayLike<number>): Vec2[] => {
  const o: Vec2[] = [];
  for (let i = 0; i + 1 < flat.length; i += 2) o.push({ x: flat[i], y: flat[i + 1] });
  return o;
};

/** Metres as the plan shows them: millimetres. */
export const toMm = (m: number) => `${Math.round(m * 1000)}`;
