/**
 * The 3D sketch's inference (docs/plan-studio-sketch.md, decision 374), shared
 * by both ways of drawing on a plane in 3D: SketchUp's (the tools draw right
 * on the face under the cursor) and the command line's (a work plane, the CAD
 * commands in its own x, y). Everything here is on the plane, in its (x, y):
 * the cursor, the edges it may lie on (the face's outline, the lines drawn),
 * the model's axes that lie in the plane (red x, green y, blue z) and a lock.
 * What the cursor infers, in SketchUp's order: an endpoint, a midpoint, a
 * locked direction, a point on an edge, a model axis from the last point,
 * parallel or perpendicular to an edge (magenta), else on the face.
 */

import type { Vec2 } from '../../platform/module';
import type { Vec3 } from '../camera';

export type AxisName = 'red' | 'green' | 'blue';
export type GuideColor = AxisName | 'magenta';
export type InferenceKind =
  | 'Endpoint' | 'Midpoint' | 'On edge' | 'On face' | 'Intersection'
  | 'On red axis' | 'On green axis' | 'On blue axis'
  | 'Parallel to edge' | 'Perpendicular to edge';

export interface Seg2 { a: Vec2; b: Vec2 }
/** A model axis lying in the plane: its name and its unit direction there. */
export interface PlaneAxis { name: AxisName; dir: Vec2 }
/** A held direction from the last point: a model axis, or an edge's way (parallel) or across it (perpendicular). */
export type Lock = { axis: AxisName } | { dir: Vec2; color: GuideColor; kind: InferenceKind } | null;

export interface InferInput {
  raw: Vec2;
  /** The last point placed (lines from it are inferred along axes and edges); null for the first point. */
  base: Vec2 | null;
  /** Edges the cursor may snap to: the face's outline, the lines drawn so far. */
  edges: readonly Seg2[];
  /** Points that are endpoints besides the edges' ends (a shape's first point). */
  points?: readonly Vec2[];
  axes: readonly PlaneAxis[];
  lock?: Lock;
  /** How near counts, on the plane (metres): the snap's reach in pixels over the plane's pixels per metre. */
  tol: number;
  /** How near an angle counts, radians (default 2.5°). */
  angleTol?: number;
}

export interface Inferred {
  point: Vec2;
  kind: InferenceKind;
  /** A guide line drawn while inferred: from the base along the axis or edge way, or the edge snapped to. */
  guide?: { from: Vec2; to: Vec2; color: GuideColor };
  /** The edge the point lies on or runs along (the Down arrow then locks parallel to it). */
  edge?: Seg2;
  /** The direction inferred from the base, if any (Shift holds it). */
  dir?: Vec2;
  color?: GuideColor;
}

const sub = (a: Vec2, b: Vec2): Vec2 => ({ x: a.x - b.x, y: a.y - b.y });
const len = (a: Vec2) => Math.hypot(a.x, a.y);
const dist = (a: Vec2, b: Vec2) => Math.hypot(a.x - b.x, a.y - b.y);
const unit = (a: Vec2): Vec2 => { const l = len(a) || 1; return { x: a.x / l, y: a.y / l }; };
const mid = (s: Seg2): Vec2 => ({ x: (s.a.x + s.b.x) / 2, y: (s.a.y + s.b.y) / 2 });

/** The point of a segment nearest a point. */
export function nearestOnSeg(s: Seg2, p: Vec2): Vec2 {
  const d = sub(s.b, s.a), l2 = d.x * d.x + d.y * d.y;
  if (l2 < 1e-18) return s.a;
  const t = Math.max(0, Math.min(1, ((p.x - s.a.x) * d.x + (p.y - s.a.y) * d.y) / l2));
  return { x: s.a.x + d.x * t, y: s.a.y + d.y * t };
}

/** A point projected on the line through a base along a unit direction. */
export const onRay = (base: Vec2, dir: Vec2, p: Vec2): Vec2 => {
  const t = (p.x - base.x) * dir.x + (p.y - base.y) * dir.y;
  return { x: base.x + dir.x * t, y: base.y + dir.y * t };
};

/** Where two segments cross (inside both), or null. */
export function crossing(s: Seg2, t: Seg2): Vec2 | null {
  const r = sub(s.b, s.a), q = sub(t.b, t.a);
  const den = r.x * q.y - r.y * q.x;
  if (Math.abs(den) < 1e-12) return null;
  const w = sub(t.a, s.a);
  const u = (w.x * q.y - w.y * q.x) / den, v = (w.x * r.y - w.y * r.x) / den;
  return u >= -1e-9 && u <= 1 + 1e-9 && v >= -1e-9 && v <= 1 + 1e-9 ? { x: s.a.x + r.x * u, y: s.a.y + r.y * u } : null;
}

/**
 * The model's axes that lie in a plane (x red, y green, z blue), as unit directions in its (x, y): a level face has red
 * and green, a wall's face the one along it and blue, a sloping roof face only the one along its eaves.
 */
export function axesInPlane(right: Vec3, up: Vec3): PlaneAxis[] {
  const o: PlaneAxis[] = [];
  const world: [AxisName, Vec3][] = [['red', { x: 1, y: 0, z: 0 }], ['green', { x: 0, y: 1, z: 0 }], ['blue', { x: 0, y: 0, z: 1 }]];
  for (const [name, w] of world) {
    const d = { x: w.x * right.x + w.y * right.y + w.z * right.z, y: w.x * up.x + w.y * up.y + w.z * up.z };
    if (len(d) > 0.999) o.push({ name, dir: unit(d) });
  }
  return o;
}

const AXIS_KIND: Record<AxisName, InferenceKind> = { red: 'On red axis', green: 'On green axis', blue: 'On blue axis' };

/** What the cursor infers on the plane (see the file's comment for the order). */
export function infer(o: InferInput): Inferred {
  const { raw, base, edges, tol } = o;
  const angleTol = o.angleTol ?? (2.5 * Math.PI) / 180;
  // A locked direction holds the point on its line from the base; endpoints and midpoints on that line still snap.
  const lock = o.lock && base ? lockDir(o.lock, o.axes) : null;
  const constrain = (p: Vec2) => (lock ? onRay(base!, lock.dir, p) : p);
  const at = constrain(raw);

  // Endpoints (the edges' ends and the points given), then midpoints.
  const ends: Vec2[] = [...(o.points ?? []), ...edges.flatMap((s) => [s.a, s.b])];
  const near = (list: Vec2[]) => {
    let best: Vec2 | null = null, bd = tol;
    for (const p of list) {
      if (lock && dist(onRay(base!, lock.dir, p), p) > 1e-6) continue;
      const d = dist(p, raw);
      if (d <= bd) { bd = d; best = p; }
    }
    return best;
  };
  const end = near(ends);
  if (end) return { point: end, kind: 'Endpoint' };
  const m = near(edges.map(mid));
  if (m) return { point: m, kind: 'Midpoint' };
  if (lock) return { point: at, kind: lock.kind, color: lock.color, dir: lock.dir, guide: { from: base!, to: at, color: lock.color } };

  // Where two edges cross, then on an edge.
  for (let i = 0; i < edges.length; i++) for (let j = i + 1; j < edges.length; j++) {
    const x = crossing(edges[i], edges[j]);
    if (x && dist(x, raw) <= tol) return { point: x, kind: 'Intersection' };
  }
  let onEdge: { p: Vec2; s: Seg2; d: number } | null = null;
  for (const s of edges) { const p = nearestOnSeg(s, raw); const d = dist(p, raw); if (d <= tol && (!onEdge || d < onEdge.d)) onEdge = { p, s, d }; }

  if (base && dist(raw, base) > tol) {
    const way = unit(sub(raw, base));
    // A model axis from the base.
    for (const a of o.axes) {
      const c = way.x * a.dir.x + way.y * a.dir.y;
      if (Math.abs(c) >= Math.cos(angleTol)) {
        const p = onRay(base, a.dir, raw);
        // On an edge too: where the axis line meets it.
        const hit = onEdge ? lineMeetsSeg(base, a.dir, onEdge.s) : null;
        const q = hit && dist(hit, raw) <= tol ? hit : p;
        return { point: q, kind: AXIS_KIND[a.name], color: a.name, dir: a.dir, guide: { from: base, to: q, color: a.name } };
      }
    }
    if (onEdge) return { point: onEdge.p, kind: 'On edge', edge: onEdge.s };
    // Parallel or perpendicular to an edge (the nearest such, magenta).
    for (const s of edges) {
      const e = unit(sub(s.b, s.a));
      if (len(sub(s.b, s.a)) < 1e-9) continue;
      const c = way.x * e.x + way.y * e.y;
      if (Math.abs(c) >= Math.cos(angleTol)) {
        const p = onRay(base, e, raw);
        return { point: p, kind: 'Parallel to edge', color: 'magenta', dir: e, edge: s, guide: { from: base, to: p, color: 'magenta' } };
      }
      if (Math.abs(c) <= Math.sin(angleTol)) {
        const n = { x: -e.y, y: e.x };
        const p = onRay(base, n, raw);
        return { point: p, kind: 'Perpendicular to edge', color: 'magenta', dir: n, edge: s, guide: { from: base, to: p, color: 'magenta' } };
      }
    }
  } else if (onEdge) return { point: onEdge.p, kind: 'On edge', edge: onEdge.s };
  return { point: raw, kind: 'On face' };
}

/** Where the line through a base along a direction meets a segment, or null. */
function lineMeetsSeg(base: Vec2, dir: Vec2, s: Seg2): Vec2 | null {
  const far = 1e6;
  return crossing({ a: { x: base.x - dir.x * far, y: base.y - dir.y * far }, b: { x: base.x + dir.x * far, y: base.y + dir.y * far } }, s);
}

function lockDir(l: NonNullable<Lock>, axes: readonly PlaneAxis[]): { dir: Vec2; color: GuideColor; kind: InferenceKind } | null {
  if ('axis' in l) { const a = axes.find((x) => x.name === l.axis); return a ? { dir: a.dir, color: a.name, kind: AXIS_KIND[a.name] } : null; }
  return { dir: unit(l.dir), color: l.color, kind: l.kind };
}

/**
 * The arrow keys' locks, as SketchUp's: → red, ← green, ↑ blue (each only when the axis lies in the plane), ↓ parallel
 * to the edge last inferred; the same key again lets go. Null when the key locks nothing here.
 */
export function arrowLock(key: string, current: Lock, axes: readonly PlaneAxis[], lastEdge: Seg2 | null): Lock | undefined {
  const axis: AxisName | null = key === 'ArrowRight' ? 'red' : key === 'ArrowLeft' ? 'green' : key === 'ArrowUp' ? 'blue' : null;
  if (axis) {
    if (current && 'axis' in current && current.axis === axis) return null;
    return axes.some((a) => a.name === axis) ? { axis } : undefined;
  }
  if (key === 'ArrowDown') {
    if (current && 'dir' in current && current.color === 'magenta') return null;
    return lastEdge ? { dir: unit(sub(lastEdge.b, lastEdge.a)), color: 'magenta', kind: 'Parallel to edge' } : undefined;
  }
  return undefined;
}

/** Shift held: the direction inferred now is kept (an axis or an edge's way), as SketchUp's Shift lock. */
export function holdLock(i: Inferred): Lock {
  if (!i.dir || !i.color) return null;
  if (i.color !== 'magenta') return { axis: i.color };
  return { dir: i.dir, color: 'magenta', kind: i.kind };
}

/** Segments of a closed or open outline. */
export const segsOf = (pts: readonly Vec2[], closed: boolean): Seg2[] =>
  pts.slice(0, closed ? pts.length : pts.length - 1).map((a, i) => ({ a, b: pts[(i + 1) % pts.length] }));
