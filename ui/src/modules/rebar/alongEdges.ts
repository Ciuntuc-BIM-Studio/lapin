/**
 * Bars along the edges of an element's cut (rebar.alongOutline, docs/plan-user-extensibility.md §6.2): the cut's outline read from
 * the view's drawing, and the side a click lands on. The sides are numbered as the engine numbers them: the outline's biggest loop,
 * counter-clockwise, side i from vertex i to vertex i + 1.
 */

import type { Vec2 } from '../../platform/module';
import { distToSegment, pairs, pointInPolygon } from '../../viewport/snap';

const area = (poly: Vec2[]): number => {
  let a = 0;
  for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) a += poly[j].x * poly[i].y - poly[i].x * poly[j].y;
  return a / 2;
};

/** A loop without its closing vertex repeated. */
const open = (loop: Vec2[]): Vec2[] => {
  const n = loop.length;
  return n > 1 && loop[0].x === loop[n - 1].x && loop[0].y === loop[n - 1].y ? loop.slice(0, -1) : loop;
};

/** The outline (outer loop, counter-clockwise) of the cut piece holding `p`, from the loops (x y x y …) of the view's cut marks; null when no cut holds it. */
export function outlineAt(marks: { loops: number[][] }[], p: Vec2): Vec2[] | null {
  for (const m of marks) {
    const loops = m.loops.map((l) => open(pairs(l))).filter((l) => l.length >= 3);
    if (!loops.some((l) => pointInPolygon(p, l))) continue;
    // A point in a hole belongs to no cut: it must lie in the outer loop and in no other.
    const outer = loops.reduce((a, b) => (Math.abs(area(b)) > Math.abs(area(a)) ? b : a));
    if (!pointInPolygon(p, outer) || loops.some((l) => l !== outer && pointInPolygon(p, l))) continue;
    return area(outer) < 0 ? [...outer].reverse() : outer;
  }
  return null;
}

/** The side of the outline nearest to `p` and its distance (m). */
export function nearestSide(loop: Vec2[], p: Vec2): { index: number; distance: number } {
  let best = { index: 0, distance: Infinity };
  for (let i = 0; i < loop.length; i++) {
    const d = distToSegment(p, loop[i], loop[(i + 1) % loop.length]);
    if (d < best.distance) best = { index: i, distance: d };
  }
  return best;
}

/** The sides chosen by clicks near them: each click toggles the side nearest to it, sorted. */
export function toggleSides(loop: Vec2[], clicks: Vec2[]): number[] {
  const set = new Set<number>();
  for (const c of clicks) {
    const { index } = nearestSide(loop, c);
    if (set.has(index)) set.delete(index); else set.add(index);
  }
  return [...set].sort((a, b) => a - b);
}
