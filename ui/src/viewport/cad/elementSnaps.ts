/**
 * What a plan's elements lend the Wall axis and Tangent snaps (forum topic 151), exact, from what the engine keeps: a wall's axis
 * is its `start`, `end` and `arc` (the axis it is placed by, IFC's Axis representation); an outline's curved edges are its
 * `contour` with `arcs`. A curved wall's faces are the circles round its axis' centre that the points of its cut lie on: the
 * engine lays them by the chord rule (Lapin.Geometry/Chords), every point on the true arc, so the arcs found are the faces
 * themselves, not copies. Pure.
 */

import type { Vec2 } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { arcOf, arcShape, isArc, middle, type Arc } from '../arc';
import { referenceArc } from '../wallReference';
import { arcsOf, edgeArc, fromPairs } from '../contour';
import { axesOf } from '../../shell/gridAxes';
import { normAngle, type Shape } from './shapes';

/** The classes whose axis is a wall's. */
export const WALL_CLASSES: ReadonlySet<string> = new Set(['IfcWall', 'IfcCurtainWall']);

/** A wall's axis, and the reference line it keeps when it keeps one (decision 685): the lines its Wall axis snap takes. */
function wallLines(n: NodeDto): Arc[] {
  const c = arcOf(n.props);
  if (!c) return [];
  const ref = referenceArc(n.props);
  return ref ? [c, ref.arc] : [c];
}

/** The shown walls' axes, a line or an arc each, and the reference lines they keep. */
export function wallAxes(items: readonly { nodeId: string }[], nodes: ReadonlyMap<string, NodeDto>): Shape[] {
  const out: Shape[] = [];
  for (const r of items) {
    const n = nodes.get(r.nodeId);
    if (!n || !WALL_CLASSES.has(n.type)) continue;
    for (const c of wallLines(n)) if (Math.hypot(c.b.x - c.a.x, c.b.y - c.a.y) > 1e-9) out.push(arcShape(c));
  }
  return out;
}

/** The ends of the shown walls' axes, as the engine lists them among an element's snap points (a curved one's middle too); a kept reference line's too. */
export function wallAxisPoints(items: readonly { nodeId: string }[], nodes: ReadonlyMap<string, NodeDto>): Vec2[] {
  const out: Vec2[] = [];
  for (const r of items) {
    const n = nodes.get(r.nodeId);
    if (!n || !WALL_CLASSES.has(n.type)) continue;
    for (const c of wallLines(n)) out.push(c.a, c.b, ...(isArc(c) ? [middle(c)] : []));
  }
  return out;
}

interface PlanLike { nodeId: string; plan: readonly { kind: string; closed: boolean; points: ArrayLike<number> }[] }

/** The shown elements' own curves: a curved wall's axis and faces, an outline's curved edges. */
export function elementCurves(items: readonly PlanLike[], nodes: ReadonlyMap<string, NodeDto>): Shape[] {
  const out: Shape[] = [];
  for (const r of items) {
    const n = nodes.get(r.nodeId);
    if (!n) continue;
    // A path element bent into an arc (a curved wall, a beam): its axis and, for a wall, its faces.
    const c = arcOf(n.props);
    if (c && isArc(c)) {
      const axis = arcShape(c);
      if (axis.type === 'arc') {
        out.push(axis);
        if (WALL_CLASSES.has(n.type)) out.push(...facesAround(axis, r.plan));
      }
    }
    // An outline with curved edges (a floor, a ceiling, an opening, a roof: forum topic 97).
    const pts = fromPairs(n.props.contour);
    const arcs = pts.length >= 3 ? arcsOf(n.props.arcs, pts.length) : undefined;
    if (arcs) for (let i = 0; i < pts.length; i++) if (Math.abs(arcs[i]) > 1e-6) {
      const s = arcShape(edgeArc({ pts, arcs }, i));
      if (s.type === 'arc') out.push(s);
    }
    // An open polyline with curved segments (railing, feature line, channel, kerb: forum topic 293).
    const pPts = fromPairs(n.props.points);
    const pArcs = pPts.length >= 2 ? arcsOf(n.props.arcs, pPts.length - 1) : undefined;
    if (pArcs) for (let i = 0; i + 1 < pPts.length; i++) if (Math.abs(pArcs[i]) > 1e-6) {
      const s = arcShape({ a: pPts[i], b: pPts[i + 1], rise: pArcs[i] });
      if (s.type === 'arc') out.push(s);
    }
  }
  return out;
}

/** Unique centerpoints of all curved elements currently shown (forum topic 293). */
export function elementCenters(items: readonly PlanLike[], nodes: ReadonlyMap<string, NodeDto>): Vec2[] {
  const out: Vec2[] = [];
  const seen = new Set<string>();
  for (const s of elementCurves(items, nodes)) {
    if (s.type !== 'arc' && s.type !== 'circle') continue;
    const key = `${Math.round(s.c.x * 1e4)},${Math.round(s.c.y * 1e4)}`;
    if (seen.has(key)) continue;
    seen.add(key);
    out.push(s.c);
  }
  return out;
}

/**
 * The arcs round an axis' centre that three points or more of the element's plan lie on (its faces, its layers' seams), each
 * over the angles those points span. The axis' own radius is left out (it is the axis).
 */
function facesAround(axis: Extract<Shape, { type: 'arc' }>, plan: PlanLike['plan']): Shape[] {
  const byRadius = new Map<number, { r: number; offs: number[] }>();
  const sweep = normAngle(axis.end - axis.start) || Math.PI * 2;
  for (const it of plan) {
    if (it.kind === 'label' || it.kind === 'hit') continue;
    for (let k = 0; k + 1 < it.points.length; k += 2) {
      const x = it.points[k] - axis.c.x, y = it.points[k + 1] - axis.c.y;
      const r = Math.hypot(x, y);
      if (r < 1e-6) continue;
      const key = Math.round(r * 1e5);
      // Where the point lies along the axis' sweep: past its end by less than half the rest of the turn counts as after it.
      let off = normAngle(Math.atan2(y, x) - axis.start);
      if (off > sweep + (Math.PI * 2 - sweep) / 2) off -= Math.PI * 2;
      const g = byRadius.get(key) ?? byRadius.get(key - 1) ?? byRadius.get(key + 1);
      if (g) g.offs.push(off); else byRadius.set(key, { r, offs: [off] });
    }
  }
  const out: Shape[] = [];
  for (const g of byRadius.values()) {
    if (g.offs.length < 3 || Math.abs(g.r - axis.r) < 1e-6) continue;
    const lo = Math.min(...g.offs), hi = Math.max(...g.offs);
    if (hi - lo < 1e-9) continue;
    out.push({ type: 'arc', c: axis.c, r: g.r, start: normAngle(axis.start + lo), end: normAngle(axis.start + hi) });
  }
  return out;
}

/** A grid's axes (its `axes` property: tag, a, b) as lines, the Grid axis snap's: none of zero length. */
export function gridAxisShapes(raw: unknown): Shape[] {
  return axesOf(raw).filter((x) => Math.hypot(x.b.x - x.a.x, x.b.y - x.a.y) > 1e-9).map((x): Shape => ({ type: 'line', a: x.a, b: x.b }));
}
