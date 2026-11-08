/**
 * The 3D view's snapping core (docs/plan-3d-modelling.md): what the cursor's
 * ray infers among the bodies drawn, in SketchUp's order. Shared by the 3D
 * move (stage A), the placing tools in 3D (stage B) and the grips (stage C);
 * the sketch's inference (sketch/inference.ts) is its counterpart on a plane.
 *
 * 1. An endpoint, then a midpoint, of the edges near the cursor on the screen
 *    (what lies behind the face under the cursor is not taken);
 * 2. a point on an edge (the edge's point nearest the ray);
 * 3. from a base point, the model's red, green and blue axes through it; a
 *    locked axis holds the point on its line, and an endpoint hovered then is
 *    brought onto the line ("from point");
 * 4. on the face under the cursor, else on the active level.
 */

import type { Vec3 } from './camera';

export interface Pt { x: number; y: number }
export interface Seg3 { a: Vec3; b: Vec3 }
/** A ray: its origin and unit direction. */
export interface Ray3 { origin: Vec3; dir: Vec3 }
export type Axis3 = 'red' | 'green' | 'blue';
export type Snap3Kind = 'Endpoint' | 'Midpoint' | 'On edge' | 'On face' | 'On level' | 'On red axis' | 'On green axis' | 'On blue axis';
/** A held axis from the base (→ red, ← green, ↑ blue, or Shift on the one inferred). */
export type Lock3 = { axis: Axis3 } | null;

export const AXIS_DIR: Record<Axis3, Vec3> = { red: { x: 1, y: 0, z: 0 }, green: { x: 0, y: 1, z: 0 }, blue: { x: 0, y: 0, z: 1 } };
const AXIS_KIND: Record<Axis3, Snap3Kind> = { red: 'On red axis', green: 'On green axis', blue: 'On blue axis' };

export interface Snap3Input {
  /** The cursor on the screen (px), and its ray into the model. */
  cursor: Pt;
  ray: Ray3;
  /** A model point on the screen (px); null behind the camera. */
  project: (p: Vec3) => Pt | null;
  /** The edges near the cursor (the bodies' sharp edges). */
  segs: readonly Seg3[];
  /** Points to snap to besides the edges' ends. */
  points?: readonly Vec3[];
  /** Where the ray meets the body under the cursor; null over empty space. */
  face: Vec3 | null;
  /** Where the ray meets the active level; null when it runs away from it. */
  level: Vec3 | null;
  /** The point the move or the line goes from: the axes are inferred through it. */
  base?: Vec3 | null;
  lock?: Lock3;
  /** The axes inferred from the base (default all three; a plan tool's point only red and green). */
  axes?: readonly Axis3[];
  /** The reach on the screen, px (default 10). */
  tol?: number;
}

export interface Snapped3 {
  point: Vec3;
  kind: Snap3Kind;
  /** The axis it lies on from the base. */
  color?: Axis3;
  /** The guide drawn from the base along the axis. */
  guide?: { from: Vec3; to: Vec3; color: Axis3 };
  /** A point hovered with an axis held: what the point was brought from onto the axis. */
  from?: Vec3;
  /** The edge it lies on. */
  seg?: Seg3;
}

const sub = (a: Vec3, b: Vec3): Vec3 => ({ x: a.x - b.x, y: a.y - b.y, z: a.z - b.z });
const add = (a: Vec3, b: Vec3, k = 1): Vec3 => ({ x: a.x + b.x * k, y: a.y + b.y * k, z: a.z + b.z * k });
const dot = (a: Vec3, b: Vec3) => a.x * b.x + a.y * b.y + a.z * b.z;
const mid = (s: Seg3): Vec3 => ({ x: (s.a.x + s.b.x) / 2, y: (s.a.y + s.b.y) / 2, z: (s.a.z + s.b.z) / 2 });
const px = (a: Pt, b: Pt) => Math.hypot(a.x - b.x, a.y - b.y);

/** How far along the ray a point lies (its depth from the camera). */
export const depthOf = (ray: Ray3, p: Vec3) => dot(sub(p, ray.origin), ray.dir);

/** The point of the line through `at` along `dir` nearest the ray (its parameter along the line); null when they run together. */
export function lineNearRay(at: Vec3, dir: Vec3, ray: Ray3): number | null {
  const w = sub(at, ray.origin);
  const a = dot(dir, dir), b = dot(dir, ray.dir), c = dot(ray.dir, ray.dir), d = dot(dir, w), e = dot(ray.dir, w);
  const den = a * c - b * b;
  if (Math.abs(den) < 1e-12 * a * c) return null;
  return (b * e - c * d) / den;
}

/** The point of a segment nearest the ray. */
export function segNearRay(s: Seg3, ray: Ray3): Vec3 {
  const d = sub(s.b, s.a);
  if (dot(d, d) < 1e-18) return s.a;
  const t = lineNearRay(s.a, d, ray);
  return t == null ? s.a : add(s.a, d, Math.max(0, Math.min(1, t)));
}

/** The point of the line through `at` along the unit `dir` nearest `p`. */
export const onLine = (at: Vec3, dir: Vec3, p: Vec3): Vec3 => add(at, dir, dot(sub(p, at), dir));

/** What the cursor infers (see the file's comment for the order). */
export function snap3(o: Snap3Input): Snapped3 | null {
  const tol = o.tol ?? 10;
  const faceDepth = o.face ? depthOf(o.ray, o.face) : Infinity;
  // A point farther than the face under the cursor is behind it: hidden.
  const seen = (p: Vec3) => depthOf(o.ray, p) > 0 && depthOf(o.ray, p) <= faceDepth + Math.max(0.02, faceDepth * 0.002);
  const near = (list: Iterable<Vec3>): Vec3 | null => {
    let best: Vec3 | null = null, bd = tol;
    for (const p of list) {
      if (!seen(p)) continue;
      const s = o.project(p);
      if (!s) continue;
      const d = px(s, o.cursor);
      if (d < bd - 1e-9 || (best && Math.abs(d - bd) <= 1e-9 && depthOf(o.ray, p) < depthOf(o.ray, best))) { bd = d; best = p; }
    }
    return best;
  };
  const ends = function* () { for (const p of o.points ?? []) yield p; for (const s of o.segs) { yield s.a; yield s.b; } };
  const base = o.base ?? null;

  // An axis held from the base: the point stays on its line; an endpoint or midpoint hovered is brought onto it.
  const held = base && o.lock ? o.lock.axis : null;
  if (base && held) {
    const dir = AXIS_DIR[held];
    const p = near(ends()) ?? near(o.segs.map(mid));
    const q = p ? onLine(base, dir, p) : (() => { const t = lineNearRay(base, dir, o.ray); return t == null ? base : add(base, dir, t); })();
    return { point: q, kind: AXIS_KIND[held], color: held, guide: { from: base, to: q, color: held }, ...(p ? { from: p } : {}) };
  }

  const end = near(ends());
  if (end) return { point: end, kind: 'Endpoint' };
  const m = near(o.segs.map(mid));
  if (m) return { point: m, kind: 'Midpoint' };

  // On an edge: the edge's point nearest the ray, within the reach.
  let edge: { p: Vec3; s: Seg3; d: number } | null = null;
  for (const s of o.segs) {
    const p = segNearRay(s, o.ray);
    if (!seen(p)) continue;
    const sp = o.project(p);
    if (!sp) continue;
    const d = px(sp, o.cursor);
    if (d <= tol && (!edge || d < edge.d)) edge = { p, s, d };
  }

  // The axes through the base, when the cursor runs along one on the screen.
  let axis: { p: Vec3; name: Axis3; d: number } | null = null;
  if (base) {
    const sb = o.project(base);
    for (const name of o.axes ?? (['red', 'green', 'blue'] as Axis3[])) {
      const t = lineNearRay(base, AXIS_DIR[name], o.ray);
      if (t == null) continue;
      const p = add(base, AXIS_DIR[name], t);
      const sp = o.project(p);
      if (!sp || (sb && px(sb, o.cursor) < tol)) continue;
      const d = px(sp, o.cursor);
      if (d <= tol && (!axis || d < axis.d)) axis = { p, name, d };
    }
  }
  if (axis && (!edge || axis.d < edge.d)) return { point: axis.p, kind: AXIS_KIND[axis.name], color: axis.name, guide: { from: base!, to: axis.p, color: axis.name } };
  if (edge) return { point: edge.p, kind: 'On edge', seg: edge.s };
  if (o.face) return { point: o.face, kind: 'On face' };
  if (o.level) return { point: o.level, kind: 'On level' };
  return null;
}

/** The arrow keys' locks in 3D, as SketchUp's: → red, ← green, ↑ blue; the same key again lets go. Undefined: not a lock key. */
export function arrowLock3(key: string, current: Lock3): Lock3 | undefined {
  const axis: Axis3 | null = key === 'ArrowRight' ? 'red' : key === 'ArrowLeft' ? 'green' : key === 'ArrowUp' ? 'blue' : null;
  if (!axis) return undefined;
  return current?.axis === axis ? null : { axis };
}

/** The inferences a grip dragged in 3D takes (decision 399): the level under the cursor is not one (the grip's own plane is). */
const GRIP_TAKES = new Set<Snap3Kind>(['Endpoint', 'Midpoint', 'On edge', 'On red axis', 'On green axis', 'On face']);

/** Where a grip goes from what the cursor infers: the point's place in plan, or null when it infers nothing a grip takes. */
export function gripTarget(s: Snapped3 | null): Pt | null {
  return s && GRIP_TAKES.has(s.kind) ? { x: s.point.x, y: s.point.y } : null;
}
