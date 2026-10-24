/**
 * A selection box's rule, the same in plans and in drawing views: dragged to
 * the right it is a window and takes what lies wholly inside it; to the left
 * it is crossing and takes what touches it.
 */

import type { Vec2 } from '../platform/module';

export type Seg = [Vec2, Vec2];

/** The test for a box from world corner `p` (where the drag began) to `q`; `windowMode` when dragged to the right on screen. */
export function boxTest(p: Vec2, q: Vec2, windowMode: boolean): (segs: Seg[]) => boolean {
  const [x0, x1, y0, y1] = [Math.min(p.x, q.x), Math.max(p.x, q.x), Math.min(p.y, q.y), Math.max(p.y, q.y)];
  const inside = (v: Vec2) => v.x >= x0 && v.x <= x1 && v.y >= y0 && v.y <= y1;
  const edges: Seg[] = [[{ x: x0, y: y0 }, { x: x1, y: y0 }], [{ x: x1, y: y0 }, { x: x1, y: y1 }], [{ x: x1, y: y1 }, { x: x0, y: y1 }], [{ x: x0, y: y1 }, { x: x0, y: y0 }]];
  const crosses = (u: Vec2, v: Vec2) => inside(u) || inside(v) || edges.some(([r, s2]) => {
    const d1 = { x: v.x - u.x, y: v.y - u.y }, d2 = { x: s2.x - r.x, y: s2.y - r.y };
    const den = d1.x * d2.y - d1.y * d2.x;
    if (Math.abs(den) < 1e-15) return false;
    const t = ((r.x - u.x) * d2.y - (r.y - u.y) * d2.x) / den, w = ((r.x - u.x) * d1.y - (r.y - u.y) * d1.x) / den;
    return t >= 0 && t <= 1 && w >= 0 && w <= 1;
  });
  return (segs) => segs.length > 0 && (windowMode ? segs.every(([u, v]) => inside(u) && inside(v)) : segs.some(([u, v]) => crosses(u, v)));
}

/** A flat segment list (x1, y1, x2, y2, …) as segments. */
export function segsOf(flat: ArrayLike<number>): Seg[] {
  const out: Seg[] = [];
  for (let k = 0; k + 3 < flat.length; k += 4) out.push([{ x: flat[k], y: flat[k + 1] }, { x: flat[k + 2], y: flat[k + 3] }]);
  return out;
}
