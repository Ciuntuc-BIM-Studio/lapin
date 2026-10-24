/**
 * Grips the modules share: an element on an axis (walls, beams, strip
 * footings: its two ends and a middle that moves it whole) and an outline
 * (slabs, openings, roofs, platforms: its vertices, and its edge middles that
 * move an edge parallel; Ctrl/⌘ inserts or removes a vertex).
 */

import type { Grip, GripStep, RefLine, Vec2 } from '../platform/module';
import { edgeMiddles, insertVertex, moveVertex, offsetEdge, removeVertex, toPairs, type Outline } from './contour';

export const pt = (raw: unknown): Vec2 | null => (Array.isArray(raw) && raw.length >= 2 ? { x: Number(raw[0]), y: Number(raw[1]) } : null);

/**
 * An axis from a to b. `step(start, end)` writes both ends; a dragged end
 * moves alone (its neighbours stay: the join holds while they still meet).
 */
export function axisGrips(a: Vec2, b: Vec2, step: (start: Vec2, end: Vec2) => GripStep): Grip[] {
  const mid = { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
  return [
    { key: 'start', kind: 'end', at: a, step: (to) => step(to, b) },
    { key: 'end', kind: 'end', at: b, step: (to) => step(a, to) },
    { key: 'move', kind: 'move', at: mid, step: (to) => { const d = { x: to.x - mid.x, y: to.y - mid.y }; return step({ x: a.x + d.x, y: a.y + d.y }, { x: b.x + d.x, y: b.y + d.y }); } },
  ];
}

export const axisRef = (a: Vec2, b: Vec2): RefLine => ({ points: [a, b], closed: false, kind: 'axis' });

/** A closed outline's grips; `save` turns the edited outline into the element's update command. */
export function outlineGrips<E>(o: Outline<E>, save: (next: Outline<E>) => GripStep): Grip[] {
  const grips: Grip[] = o.pts.map((p, i) => ({
    key: `v${i}`, kind: 'vertex' as const, at: p,
    step: (to: Vec2) => save(moveVertex(o, i, to)),
    remove: () => { const next = removeVertex(o, i); return next ? save(next) : null; },
  }));
  edgeMiddles(o.pts).forEach((m, i) => grips.push({
    key: `e${i}`, kind: 'edgeMid', at: m,
    step: (to: Vec2) => { const next = offsetEdge(o, i, to); return next ? save(next) : null; },
    insert: () => save(insertVertex(o, i)),
  }));
  return grips;
}

export const outlineRef = (pts: Vec2[]): RefLine => ({ points: pts, closed: true, kind: 'outline' });
export { toPairs };
