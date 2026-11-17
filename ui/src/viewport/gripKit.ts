/**
 * Grips the modules share: an element on an axis (walls, beams, strip
 * footings: its two ends and a middle that moves it whole) and an outline
 * (slabs, openings, roofs, platforms: its vertices, and its edge middles that
 * move an edge parallel; Ctrl/⌘ inserts or removes a vertex).
 */

import type { Grip, GripStep, RefLine, Vec2 } from '../platform/module';
import { anticlockwise, arcsOf, edgeArc, edgeMiddles, insertVertex, moveVertex, offsetEdge, outlinePoints, removeVertex, setArc, toPairs, type Outline } from './contour';
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

/** What an outline whose edges may be curved and whose corners may be rounded asks the element's module (forum topic 97). */
export interface CurvedOutline {
  /** Corner i rounded by an arc of `radius` (one engine command, one undo step). */
  fillet: (i: number, radius: number) => GripStep | null;
  /** Corner i cut straight across `distance` back along each edge. */
  chamfer: (i: number, distance: number) => GripStep | null;
}

/**
 * A closed outline's grips; `save` turns the edited outline into the element's update command. Each corner takes Ctrl/⌘-click
 * to be removed (never under three), each edge's middle Ctrl/⌘-click to put a corner there; a right-click on the element
 * offers the same (GripLayer's cornerItems), where the corner goes at the point clicked. With `curved` (a floor, a floor
 * opening, a ceiling; forum topic 97, stage 2) an edge may be an arc: a curved edge has a round grip at its arc's middle,
 * dragged to bend it through the point, and its diamond at its chord's middle moves it; a right-click offers "Make arc",
 * "Make straight", "Fillet corner…" and "Chamfer corner…".
 */
export function outlineGrips<E>(o: Outline<E>, save: (next: Outline<E>) => GripStep, curved?: CurvedOutline, prefix = ''): Grip[] {
  const n = o.pts.length;
  const straightAt = (i: number) => Math.abs(o.arcs?.[i] ?? 0) <= 1e-6;
  const grips: Grip[] = o.pts.map((p, i) => ({
    key: `${prefix}v${i}`, kind: 'vertex' as const, at: p,
    title: o.pts.length > 3 ? 'Drag to move the corner · Ctrl/⌘-click: remove this corner' : 'Drag to move the corner (an outline keeps three corners at least)',
    step: (to: Vec2) => save(moveVertex(o, i, to)),
    remove: () => { const next = removeVertex(o, i); return next ? save(next) : null; },
    ...(curved ? { corner: { straight: straightAt(i) && straightAt((i - 1 + n) % n), fillet: (r: number) => curved.fillet(i, r), chamfer: (d: number) => curved.chamfer(i, d) } } : {}),
  }));
  // Outward is to the right of the edges of an anticlockwise outline.
  const out = anticlockwise(o.pts) ? 1 : -1;
  edgeMiddles(o.pts).forEach((m, i) => {
    const a = o.pts[i], b = o.pts[(i + 1) % n];
    const c = edgeArc(o, i);
    const bent = isArc(c);
    const len = Math.hypot(b.x - a.x, b.y - a.y) || 1;
    grips.push({
      key: `${prefix}e${i}`, kind: 'edgeMid', at: m,
      title: 'Drag to move the edge · Shift-drag: offset the whole outline · Ctrl/⌘-click: add a corner',
      step: (to: Vec2) => { const next = offsetEdge(o, i, to); return next ? save(next) : null; },
      offsetAll: { outline: o as Outline<unknown>, normal: { x: (out * (b.y - a.y)) / len, y: (-out * (b.x - a.x)) / len }, save: (next) => save(next as Outline<E>) },
      insert: () => save(insertVertex(o, i)),
      edge: bent ? axisLine(c) : [a, b],
      addCornerAt: (to: Vec2) => save(insertVertex(o, i, bent ? to : nearestOnSegment(a, b, to).at)),
      ...(curved ? bent
        ? { straighten: () => save(setArc(o, i, 0)) }
        // Bulging out of the outline by an eighth of its length.
        : { bend: () => save(setArc(o, i, (anticlockwise(o.pts) ? -1 : 1) * Math.hypot(b.x - a.x, b.y - a.y) / 8)) } : {}),
    });
    if (curved && bent) grips.push({ key: `${prefix}a${i}`, kind: 'radius', at: middle(c), title: 'Drag to bend the edge through the point', step: (to: Vec2) => save(setArc(o, i, riseThrough(a, b, to))) });
  });
  return grips;
}

/**
 * An outline's grips with its elliptical edges (audit S4; one per corner, null where not one) lent to "Offset outline", which
 * then lays the curve at the distance from an ellipse in chords; unchanged when it has none.
 */
export function withEllipses(grips: Grip[], ellipses: unknown, corners: number): Grip[] {
  if (!Array.isArray(ellipses) || ellipses.length !== corners || !ellipses.some(Boolean)) return grips;
  return grips.map((g) => (g.offsetAll ? { ...g, offsetAll: { ...g.offsetAll, outline: { ...g.offsetAll.outline, ellipses } } } : g));
}

/** The point of a line (a polyline: an arc in steps) nearest to p, how far p is from it and how far along it is (0 to 1). */
export function nearestOnLine(pts: Vec2[], p: Vec2): { at: Vec2; dist: number; t: number } {
  if (pts.length === 2) return nearestOnSegment(pts[0], pts[1], p);
  const lens = pts.slice(1).map((q, i) => Math.hypot(q.x - pts[i].x, q.y - pts[i].y));
  const total = lens.reduce((x, y) => x + y, 0) || 1;
  let best = { at: pts[0], dist: Infinity, t: 0 }, before = 0;
  for (let i = 0; i + 1 < pts.length; i++) {
    const h = nearestOnSegment(pts[i], pts[i + 1], p);
    if (h.dist < best.dist) best = { at: h.at, dist: h.dist, t: (before + h.t * lens[i]) / total };
    before += lens[i];
  }
  return best;
}

/**
 * An element's outline whose edges may be arcs (decision 589 and audit S4: a floor's, a drawn room's, a drawn balcony's, a
 * platform's, a parcel's): its grips, each edit `save`d with the corners and every edge's rise; a corner rounded or cut by the
 * module's `roundCorner` / `chamferCorner` (one engine command, one undo step).
 */
export function curvedOutlineGrips(id: string, pts: Vec2[], rawArcs: unknown, save: (pts: Vec2[], arcs: number[]) => GripStep, module = 'architecture', rawEllipses?: unknown): Grip[] {
  if (pts.length < 3) return [];
  return withEllipses(outlineGrips({ pts, arcs: arcsOf(rawArcs, pts.length) }, (o) => save(o.pts, o.arcs ?? o.pts.map(() => 0)), {
    fillet: (index, radius) => ({ command: `${module}.roundCorner`, args: { id, index, radius } }),
    chamfer: (index, distance) => ({ command: `${module}.chamferCorner`, args: { id, index, distance } }),
  }), rawEllipses, pts.length);
}

/** An element's own holes (audit S4): each a loop of corners, and its edges' rises (none when straight). */
export function holesOf(raw: unknown, rawArcs: unknown): { pts: Vec2[]; arcs?: number[] }[] {
  if (!Array.isArray(raw)) return [];
  return raw.map((loop, k) => {
    const pts = Array.isArray(loop) ? loop.filter((p): p is [number, number] => Array.isArray(p) && typeof p[0] === 'number' && typeof p[1] === 'number').map(([x, y]) => ({ x, y })) : [];
    return { pts, arcs: arcsOf(Array.isArray(rawArcs) ? rawArcs[k] : undefined, pts.length) };
  }).filter((h) => h.pts.length >= 3);
}

/**
 * An element's own holes' grips (audit S4): each hole's corners and edges as an outline's (keys `h<k>v…`), every edit `save`d
 * with all the holes; its corners rounded or cut by the module's `roundCorner` / `chamferCorner` with `hole`; a grip in its middle
 * drags the hole and offers "Remove hole".
 */
export function holeGrips(id: string, raw: unknown, rawArcs: unknown, save: (holes: number[][][], arcs: number[][]) => GripStep, module = 'architecture', rawEllipses?: unknown): Grip[] {
  const holes = holesOf(raw, rawArcs);
  // A hole's elliptical edges go to "Offset outline" (the curve at the distance from an ellipse, in chords).
  const ellipsesOf = (k: number) => (Array.isArray(rawEllipses) ? rawEllipses[k] : undefined);
  const write = (k: number, next: { pts: Vec2[]; arcs?: number[] }) => {
    const all = holes.map((h, i) => (i === k ? next : h));
    return save(all.map((h) => toPairs(h.pts)), all.map((h) => h.arcs ?? h.pts.map(() => 0)));
  };
  return holes.flatMap((h, k) => {
    const c = h.pts.reduce((s, p) => ({ x: s.x + p.x / h.pts.length, y: s.y + p.y / h.pts.length }), { x: 0, y: 0 });
    const grips = withEllipses(outlineGrips({ pts: h.pts, arcs: h.arcs }, (o) => write(k, { pts: o.pts, arcs: o.arcs }), {
      fillet: (index, radius) => ({ command: `${module}.roundCorner`, args: { id, hole: k, index, radius } }),
      chamfer: (index, distance) => ({ command: `${module}.chamferCorner`, args: { id, hole: k, index, distance } }),
    }, `h${k}`), ellipsesOf(k), h.pts.length);
    grips.push({
      key: `h${k}c`, kind: 'move', at: c, title: 'Drag to move the hole',
      step: (to: Vec2) => write(k, { pts: h.pts.map((p) => ({ x: p.x + to.x - c.x, y: p.y + to.y - c.y })), arcs: h.arcs }),
      remove: () => ({ command: `${module}.removeOutlineHole`, args: { id, hole: k } }), removeLabel: 'Remove hole',
    });
    return grips;
  });
}

export const holeRefs = (raw: unknown, rawArcs: unknown): RefLine[] => holesOf(raw, rawArcs).map((h) => outlineRef(h.pts, h.arcs));

export const outlineRef = (pts: Vec2[], arcs?: number[]): RefLine => ({ points: arcs ? outlinePoints({ pts, arcs }) : pts, closed: true, kind: 'outline' });
export { toPairs };
