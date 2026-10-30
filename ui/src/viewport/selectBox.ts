/**
 * A selection region's rule, the same in plans and in drawing views: dragged to
 * the right it is a window and takes what lies wholly inside it; to the left
 * it is crossing and takes what touches it. The region is a box, or a lasso
 * drawn freehand (Space during the drag), as in AutoCAD.
 */

import type { Vec2 } from '../platform/module';

export type Seg = [Vec2, Vec2];
export type RegionTest = (segs: Seg[]) => boolean;

/** Where segments u–v and r–s meet, as their parameters (0..1 on each), or null when parallel. */
function meet(u: Vec2, v: Vec2, r: Vec2, s: Vec2): { t: number; w: number } | null {
  const d1 = { x: v.x - u.x, y: v.y - u.y }, d2 = { x: s.x - r.x, y: s.y - r.y };
  const den = d1.x * d2.y - d1.y * d2.x;
  if (Math.abs(den) < 1e-15) return null;
  return { t: ((r.x - u.x) * d2.y - (r.y - u.y) * d2.x) / den, w: ((r.x - u.x) * d1.y - (r.y - u.y) * d1.x) / den };
}

/** The test for a box from world corner `p` (where the drag began) to `q`; `windowMode` when dragged to the right on screen. */
export function boxTest(p: Vec2, q: Vec2, windowMode: boolean): RegionTest {
  const [x0, x1, y0, y1] = [Math.min(p.x, q.x), Math.max(p.x, q.x), Math.min(p.y, q.y), Math.max(p.y, q.y)];
  const inside = (v: Vec2) => v.x >= x0 && v.x <= x1 && v.y >= y0 && v.y <= y1;
  const edges: Seg[] = [[{ x: x0, y: y0 }, { x: x1, y: y0 }], [{ x: x1, y: y0 }, { x: x1, y: y1 }], [{ x: x1, y: y1 }, { x: x0, y: y1 }], [{ x: x0, y: y1 }, { x: x0, y: y0 }]];
  const crosses = (u: Vec2, v: Vec2) => inside(u) || inside(v) || edges.some(([r, s]) => { const m = meet(u, v, r, s); return !!m && m.t >= 0 && m.t <= 1 && m.w >= 0 && m.w <= 1; });
  return (segs) => segs.length > 0 && (windowMode ? segs.every(([u, v]) => inside(u) && inside(v)) : segs.some(([u, v]) => crosses(u, v)));
}

/**
 * The test for a lasso, a closed polygon of world points. A window takes what
 * lies wholly inside it: both ends of every segment inside and no edge of the
 * lasso crossed, since a concave lasso can hold both ends of a wall that pokes
 * out of it. Crossing takes what touches it.
 */
export function lassoTest(poly: Vec2[], windowMode: boolean): RegionTest {
  if (poly.length < 3) return () => false;
  const edges: Seg[] = poly.map((p, i) => [p, poly[(i + 1) % poly.length]]);
  const inside = (p: Vec2) => {
    let c = false;
    for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
      const a = poly[i], b = poly[j];
      if ((a.y > p.y) !== (b.y > p.y) && p.x < ((b.x - a.x) * (p.y - a.y)) / (b.y - a.y) + a.x) c = !c;
    }
    return c;
  };
  // Strictly within both segments: an end resting on the lasso's edge is not a crossing.
  const crossesEdge = (u: Vec2, v: Vec2) => edges.some(([r, s]) => { const m = meet(u, v, r, s); return !!m && m.t > 1e-9 && m.t < 1 - 1e-9 && m.w > 1e-9 && m.w < 1 - 1e-9; });
  return (segs) => segs.length > 0 && (windowMode
    ? segs.every(([u, v]) => inside(u) && inside(v) && !crossesEdge(u, v))
    : segs.some(([u, v]) => inside(u) || inside(v) || crossesEdge(u, v)));
}

/** A flat segment list (x1, y1, x2, y2, …) as segments. */
export function segsOf(flat: ArrayLike<number>): Seg[] {
  const out: Seg[] = [];
  for (let k = 0; k + 3 < flat.length; k += 4) out.push([{ x: flat[k], y: flat[k + 1] }, { x: flat[k + 2], y: flat[k + 3] }]);
  return out;
}
