/**
 * Guide lines (object snap tracking): points the cursor rested on, and the
 * points of the gesture, are acquired; the cursor then holds on the
 * horizontal and vertical lines through them (and where two such lines
 * cross), and on the polar angles from the base point. Each hold shows its
 * guide as a dashed line from the acquired point.
 */

import type { Vec2 } from '../../platform/module';

export interface Guide { from: Vec2; to: Vec2; kind: 'h' | 'v' | 'polar' }
export interface Tracked { point: Vec2; guides: Guide[] }

/**
 * The cursor held on the guides within `tol`: an orthogonal line through an
 * acquired point, two of them crossing, or a polar angle (every `polarStep`
 * degrees) from the base. Null when no guide is near.
 */
export function track(p: Vec2, acquired: readonly Vec2[], tol: number, o: { base?: Vec2 | null; polarStep?: number | null } = {}): Tracked | null {
  let h: { y: number; from: Vec2; d: number } | null = null;
  let v: { x: number; from: Vec2; d: number } | null = null;
  for (const a of acquired) {
    const dy = Math.abs(p.y - a.y), dx = Math.abs(p.x - a.x);
    if (dy <= tol && (!h || dy < h.d)) h = { y: a.y, from: a, d: dy };
    if (dx <= tol && (!v || dx < v.d)) v = { x: a.x, from: a, d: dx };
  }
  if (h && v) {
    const q = { x: v.x, y: h.y };
    return { point: q, guides: [{ from: h.from, to: q, kind: 'h' }, { from: v.from, to: q, kind: 'v' }] };
  }
  if (h) { const q = { x: p.x, y: h.y }; return { point: q, guides: [{ from: h.from, to: q, kind: 'h' }] }; }
  if (v) { const q = { x: v.x, y: p.y }; return { point: q, guides: [{ from: v.from, to: q, kind: 'v' }] }; }
  const b = o.base;
  if (b && o.polarStep) {
    const d = Math.hypot(p.x - b.x, p.y - b.y);
    if (d > tol) {
      const step = (o.polarStep * Math.PI) / 180;
      const a = Math.round(Math.atan2(p.y - b.y, p.x - b.x) / step) * step;
      const q = { x: b.x + d * Math.cos(a), y: b.y + d * Math.sin(a) };
      if (Math.hypot(q.x - p.x, q.y - p.y) <= tol) return { point: q, guides: [{ from: b, to: q, kind: 'polar' }] };
    }
  }
  return null;
}

/** On an ortho line from the base (horizontal or vertical), where an acquired point's crossing guide meets it. */
export function trackOnOrtho(q: Vec2, base: Vec2, acquired: readonly Vec2[], tol: number): Tracked | null {
  const horizontal = Math.abs(q.y - base.y) < 1e-12;
  let best: { point: Vec2; from: Vec2; d: number } | null = null;
  for (const a of acquired) {
    if (a === base) continue;
    const d = horizontal ? Math.abs(q.x - a.x) : Math.abs(q.y - a.y);
    if (d <= tol && (!best || d < best.d)) best = { point: horizontal ? { x: a.x, y: base.y } : { x: base.x, y: a.y }, from: a, d };
  }
  return best ? { point: best.point, guides: [{ from: best.from, to: best.point, kind: horizontal ? 'v' : 'h' }] } : null;
}
