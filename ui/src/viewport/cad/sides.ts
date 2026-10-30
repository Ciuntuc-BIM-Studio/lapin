/**
 * A polyline driven by its sides' lengths (docs/plan-studio-editor.md): a side
 * typed a new length keeps its start and its direction; its end moves along
 * it, and with it the vertices after it, up to the next side that runs the
 * same way (or back), which takes up the change. So a rectangle stays a
 * rectangle and an L stays an L; an open polyline with no such side moves on
 * whole, a closed one never moves the side's own start.
 */

import type { Vec2 } from '../../platform/module';

export function sideLengths(pts: Vec2[], closed: boolean): number[] {
  const n = closed ? pts.length : pts.length - 1;
  return Array.from({ length: Math.max(0, n) }, (_, i) => { const a = pts[i], b = pts[(i + 1) % pts.length]; return Math.hypot(b.x - a.x, b.y - a.y); });
}

export function withSideLength(pts: Vec2[], closed: boolean, i: number, length: number): Vec2[] | null {
  const n = pts.length;
  const sides = closed ? n : n - 1;
  if (i < 0 || i >= sides || !(length > 0)) return null;
  const a = pts[i], b = pts[(i + 1) % n];
  const now = Math.hypot(b.x - a.x, b.y - a.y);
  if (now < 1e-12) return null;
  const u = { x: (b.x - a.x) / now, y: (b.y - a.y) / now };
  const d = { x: u.x * (length - now), y: u.y * (length - now) };
  const out = pts.map((p) => ({ ...p }));
  const move = (k: number) => { out[k] = { x: pts[k].x + d.x, y: pts[k].y + d.y }; };
  move((i + 1) % n);
  for (let s = 1; s < n; s++) {
    const j = (i + 1 + s) % n;
    if (!closed && i + 1 + s >= n) break;
    if (j === i) break;
    const p = pts[(j - 1 + n) % n], q = pts[j];
    const len = Math.hypot(q.x - p.x, q.y - p.y);
    // The side into j runs the way the side typed does (or back): it stretches, and what is after it stays.
    if (len > 1e-12 && Math.abs((q.x - p.x) * u.y - (q.y - p.y) * u.x) / len < 1e-9) break;
    move(j);
  }
  return out;
}
