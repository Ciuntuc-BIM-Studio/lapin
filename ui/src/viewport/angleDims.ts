/**
 * Angular temporary dimensions in plan (decision 738; users: "angular temporary dimensions we can fix"). Pure: what
 * AngleTempDims.tsx draws, what the live input's angle field types, and what a value typed turns.
 *
 * - While a wall, a beam, a line or a polyline is drawn: the angle the segment makes with a reference at its start, the
 *   previous segment (measured inside the corner, from the ray back along it: 90° square, 180° straight on), else the wall or
 *   beam the start lies on (the smaller angle to its line), else the plan's X axis (counter-clockwise, as the ∠ field reads it).
 *   Typed (the live box's third field), it sets the direction, on the side of the reference the cursor is.
 * - A straight wall or beam selected: at each end where it meets a wall or beam that is not parallel (joined there, or its end
 *   on it), the smaller angle between their axes at the point where they cross. Typed, the element turns about that point.
 */

import type { Vec2 } from '../platform/module';
import type { DimLineInfo } from './lineDims';

/** What an angle is measured from: a ray (`ray`: only that way, a previous segment) or a line (either way, the nearer). */
export interface AngleRef { dir: Vec2; ray: boolean; what: 'previous' | 'element' | 'x'; name?: string; id?: string }

const sub = (p: Vec2, q: Vec2): Vec2 => ({ x: p.x - q.x, y: p.y - q.y });
const dot = (p: Vec2, q: Vec2) => p.x * q.x + p.y * q.y;
const cross = (p: Vec2, q: Vec2) => p.x * q.y - p.y * q.x;
const unit = (p: Vec2): Vec2 => { const l = Math.hypot(p.x, p.y) || 1; return { x: p.x / l, y: p.y / l }; };
const neg = (p: Vec2): Vec2 => ({ x: -p.x, y: -p.y });
export const toDeg = (r: number) => (r * 180) / Math.PI;
export const toRad = (d: number) => (d * Math.PI) / 180;
/** Lines this near parallel (the sine of the angle between them) make no angle. */
const PARALLEL = 0.02;

/** The X axis, when nothing else is at the start. */
export const X_REF: AngleRef = { dir: { x: 1, y: 0 }, ray: true, what: 'x' };

/** The previous segment as the reference at its end `base`: the ray from the corner back along it. */
export function previousRef(base: Vec2, prev: Vec2 | null | undefined): AngleRef | null {
  if (!prev || Math.hypot(prev.x - base.x, prev.y - base.y) < 1e-9) return null;
  return { dir: unit(sub(prev, base)), ray: true, what: 'previous' };
}

/** The angle direction `d` makes with the reference: the ray measured from, the angle (0–180°) and the side (+1 counter-clockwise). */
export function angleFrom(ref: AngleRef, d: Vec2): { ray: Vec2; deg: number; side: 1 | -1 } | null {
  if (Math.hypot(d.x, d.y) < 1e-12) return null;
  const u = unit(d);
  const ray = ref.ray || dot(ref.dir, u) >= 0 ? unit(ref.dir) : neg(unit(ref.dir));
  const c = cross(ray, u);
  return { ray, deg: Math.abs(toDeg(Math.atan2(c, dot(ray, u)))), side: c >= 0 ? 1 : -1 };
}

/** The direction `deg` degrees from `ray`, turned toward `side`. */
export function directionAt(ray: Vec2, side: 1 | -1, deg: number): Vec2 {
  const a = Math.atan2(ray.y, ray.x) + side * toRad(deg);
  return { x: Math.cos(a), y: Math.sin(a) };
}

/** The arc drawn for an angle: from `from` radians counter-clockwise or not to `to`, its value in degrees. */
export interface AngleArc { from: number; to: number; value: number }

/** The drawn segment's angle from its reference (the X axis: counter-clockwise from +X, as the ∠ field reads it). */
export function segmentArc(ref: AngleRef | null, base: Vec2, end: Vec2): AngleArc | null {
  const d = sub(end, base);
  if (Math.hypot(d.x, d.y) < 1e-9) return null;
  if (!ref || ref.what === 'x') {
    const a = Math.atan2(d.y, d.x), ccw = ((a % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI);
    return { from: 0, to: ccw, value: toDeg(ccw) };
  }
  const m = angleFrom(ref, d);
  if (!m) return null;
  const from = Math.atan2(m.ray.y, m.ray.x);
  return { from, to: from + m.side * toRad(m.deg), value: m.deg };
}

/** Where along and across a line (from its start, left of its axis) a point lies. */
function inFrame(l: { a: Vec2; b: Vec2 }, p: Vec2) {
  const v = sub(l.b, l.a), len = Math.hypot(v.x, v.y), d = unit(v);
  const q = sub(p, l.a);
  return { s: dot(q, d), t: cross(d, q), len, d };
}

const half = (l: DimLineInfo) => Math.max(...l.faces.map(Math.abs));
const straightKinds = (l: DimLineInfo) => (l.kind ?? 'wall') === 'wall' || l.kind === 'beam';

/** The wall or beam a point lies on (within its thickness, along its length), nearest its axis: the reference a segment drawn from it starts at. */
export function elementAt(p: Vec2, lines: DimLineInfo[], tol = 1e-3): AngleRef | null {
  let best: { l: DimLineInfo; t: number; d: Vec2 } | null = null;
  for (const l of lines) {
    if (!straightKinds(l)) continue;
    const f = inFrame(l, p);
    if (!(f.len > 1e-6)) continue;
    const w = half(l) + tol;
    if (Math.abs(f.t) > w || f.s < -w || f.s > f.len + w) continue;
    if (!best || Math.abs(f.t) < Math.abs(best.t)) best = { l, t: f.t, d: f.d };
  }
  return best ? { dir: best.d, ray: false, what: 'element', name: best.l.name, id: best.l.id } : null;
}

/** One angle where the host meets another: the point their axes cross, the rays from it, the angle and the way it opens. */
export interface Meeting {
  key: string;
  end: 'start' | 'end';
  other: { id: string; name: string; kind: string; a: Vec2; b: Vec2 };
  at: Vec2;
  /** The other's ray (measured from) and the host's (along it, toward its far end). */
  otherRay: Vec2;
  hostRay: Vec2;
  /** 0–90° (the smaller angle); `side` +1 when the host's ray is counter-clockwise of the other's. */
  deg: number;
  side: 1 | -1;
}

/** Where two lines cross; null when parallel. */
function crossing(a0: Vec2, u: Vec2, b0: Vec2, v: Vec2): Vec2 | null {
  const c = cross(u, v);
  if (Math.abs(c) < 1e-12) return null;
  const t = cross(sub(b0, a0), v) / c;
  return { x: a0.x + u.x * t, y: a0.y + u.y * t };
}

/**
 * The angles a straight wall or beam (`host`, as view.dimLines gives it) makes at its ends with the walls and beams it meets
 * there: joined at that end, or its end on one (within their thicknesses). One per end, the joined one first, else the nearest.
 */
export function meetingAngles(host: DimLineInfo, lines: DimLineInfo[]): Meeting[] {
  const v = sub(host.b, host.a), len = Math.hypot(v.x, v.y);
  if (!(len > 1e-6)) return [];
  const d = unit(v);
  const out: Meeting[] = [];
  for (const end of ['start', 'end'] as const) {
    const p = end === 'start' ? host.a : host.b;
    const hostRay = end === 'start' ? d : neg(d);
    const joined = host.joins?.[end] ?? [];
    let best: { m: Meeting; rank: number } | null = null;
    for (const l of lines) {
      if (l.id === host.id || !straightKinds(l)) continue;
      const f = inFrame(l, p);
      if (!(f.len > 1e-6) || Math.abs(cross(d, f.d)) <= PARALLEL) continue;
      const reach = half(l) + half(host) + 1e-3;
      const isJoined = joined.includes(l.id);
      if (!isJoined && (Math.abs(f.t) > half(l) + 1e-3 || f.s < -reach || f.s > f.len + reach)) continue;
      const at = crossing(host.a, d, l.a, f.d);
      if (!at || Math.hypot(at.x - p.x, at.y - p.y) > reach + 0.05) continue;
      // The other's ray making the smaller angle with the host's; square, the one toward its body.
      const c = dot(f.d, hostRay);
      const toward = Math.abs(c) > 1e-9 ? c > 0 : inFrame(l, at).s < f.len / 2;
      const otherRay = toward ? f.d : neg(f.d);
      const cr = cross(otherRay, hostRay);
      const m: Meeting = {
        key: `angle:${end}`, end, other: { id: l.id, name: l.name, kind: l.kind ?? 'wall', a: l.a, b: l.b }, at, otherRay, hostRay,
        deg: Math.abs(toDeg(Math.atan2(cr, dot(otherRay, hostRay)))), side: cr >= 0 ? 1 : -1,
      };
      const rank = (isJoined ? 0 : 1) + Math.abs(f.t) / 1000;
      if (!best || rank < best.rank) best = { m, rank };
    }
    if (best) out.push(best.m);
  }
  return out;
}

/** How far (degrees, counter-clockwise) the host turns about the meeting point for the angle to read `value`; null when it already does or cannot. */
export function rotationFor(m: Pick<Meeting, 'otherRay' | 'hostRay' | 'side'>, value: number): number | null {
  if (!(value > 0 && value < 180)) return null;
  const want = directionAt(m.otherRay, m.side, value);
  const turn = toDeg(Math.atan2(cross(m.hostRay, want), dot(m.hostRay, want)));
  return Math.abs(turn) < 1e-9 ? null : turn;
}

/** A point turned about `c` by `deg` degrees counter-clockwise (what element.transform's rotate does to the host). */
export function rotated(p: Vec2, c: Vec2, deg: number): Vec2 {
  const a = toRad(deg), q = sub(p, c);
  return { x: c.x + q.x * Math.cos(a) - q.y * Math.sin(a), y: c.y + q.x * Math.sin(a) + q.y * Math.cos(a) };
}

/** How each ray runs along its element's axis (start → end): +1 the same way, −1 against it; what a permanent angle holds on. */
export const wayAlong = (ray: Vec2, a: Vec2, b: Vec2): 1 | -1 => (dot(ray, sub(b, a)) >= 0 ? 1 : -1);
