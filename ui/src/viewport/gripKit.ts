/**
 * Grips the modules share: an element on an axis (walls, beams, strip
 * footings: its two ends and a middle that moves it whole) and an outline
 * (slabs, openings, roofs, platforms: its vertices, and its edge middles that
 * move an edge parallel; Ctrl/⌘ inserts or removes a vertex).
 */

import type { Grip, GripStep, RefLine, Vec2 } from '../platform/module';
import { edgeMiddles, insertVertex, moveVertex, offsetEdge, removeVertex, toPairs, type Outline } from './contour';
import { arcLength, axisLine, isArc, middle, pointAt, riseThrough, tangentAt, type Arc } from './arc';
export { pt } from '../platform/vec';


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

/**
 * A path's grips, straight or curved: its ends (an arc keeps its rise), a
 * middle that moves it whole (on a curved one, on its chord) and, on a curved
 * one, its arc's middle, dragged to bend it through the point.
 */
export function pathGrips(c: Arc, step: (start: Vec2, end: Vec2, rise: number) => GripStep): Grip[] {
  if (!isArc(c)) return axisGrips(c.a, c.b, (s, e) => step(s, e, 0));
  const chordMid = { x: (c.a.x + c.b.x) / 2, y: (c.a.y + c.b.y) / 2 };
  return [
    { key: 'start', kind: 'end', at: c.a, step: (to) => step(to, c.b, c.rise) },
    { key: 'end', kind: 'end', at: c.b, step: (to) => step(c.a, to, c.rise) },
    { key: 'move', kind: 'move', at: chordMid, step: (to) => { const d = { x: to.x - chordMid.x, y: to.y - chordMid.y }; return step({ x: c.a.x + d.x, y: c.a.y + d.y }, { x: c.b.x + d.x, y: c.b.y + d.y }, c.rise); } },
    { key: 'arc', kind: 'radius', at: middle(c), title: 'Drag to bend the arc through the point', step: (to) => step(c.a, c.b, riseThrough(c.a, c.b, to)) },
  ];
}

/** A path's axis line (an arc drawn in short steps). */
export const pathRef = (c: Arc): RefLine => ({ points: axisLine(c), closed: false, kind: 'axis' });

/** The point s along a path and its left normal there. */
export function frameAt(c: Arc, s: number): { at: Vec2; along: Vec2; left: Vec2 } {
  const at = pointAt(c, s), along = tangentAt(c, s);
  return { at, along, left: { x: -along.y, y: along.x } };
}

/** A flip arrow at a path's middle, across it, pointing to the side `side` (+1 left, −1 right). */
export function flipAcross(c: Arc, side: number, title: string, click: () => GripStep): Grip {
  const f = frameAt(c, arcLength(c) / 2);
  return { key: 'flip', kind: 'flip', at: f.at, dir: { x: f.left.x * side, y: f.left.y * side }, title, click };
}

/** The point of the segment a→b nearest to p (clamped to the segment), and how far p is from it. */
export function nearestOnSegment(a: Vec2, b: Vec2, p: Vec2): { at: Vec2; dist: number; t: number } {
  const dx = b.x - a.x, dy = b.y - a.y, len2 = dx * dx + dy * dy;
  const t = len2 < 1e-18 ? 0 : Math.max(0, Math.min(1, ((p.x - a.x) * dx + (p.y - a.y) * dy) / len2));
  const at = { x: a.x + dx * t, y: a.y + dy * t };
  return { at, dist: Math.hypot(p.x - at.x, p.y - at.y), t };
}

/**
 * A closed outline's grips; `save` turns the edited outline into the element's update command. Each corner takes Ctrl/⌘-click
 * to be removed (never under three), each edge's middle Ctrl/⌘-click to put a corner there; a right-click on the element
 * offers the same (GripLayer's cornerItems), where the corner goes at the point clicked.
 */
export function outlineGrips<E>(o: Outline<E>, save: (next: Outline<E>) => GripStep): Grip[] {
  const grips: Grip[] = o.pts.map((p, i) => ({
    key: `v${i}`, kind: 'vertex' as const, at: p,
    title: o.pts.length > 3 ? 'Drag to move the corner · Ctrl/⌘-click: remove this corner' : 'Drag to move the corner (an outline keeps three corners at least)',
    step: (to: Vec2) => save(moveVertex(o, i, to)),
    remove: () => { const next = removeVertex(o, i); return next ? save(next) : null; },
  }));
  edgeMiddles(o.pts).forEach((m, i) => {
    const a = o.pts[i], b = o.pts[(i + 1) % o.pts.length];
    grips.push({
      key: `e${i}`, kind: 'edgeMid', at: m,
      title: 'Drag to move the edge · Ctrl/⌘-click: add a corner',
      step: (to: Vec2) => { const next = offsetEdge(o, i, to); return next ? save(next) : null; },
      insert: () => save(insertVertex(o, i)),
      edge: [a, b],
      addCornerAt: (to: Vec2) => save(insertVertex(o, i, nearestOnSegment(a, b, to).at)),
    });
  });
  return grips;
}

export const outlineRef = (pts: Vec2[]): RefLine => ({ points: pts, closed: true, kind: 'outline' });
export { toPairs };
