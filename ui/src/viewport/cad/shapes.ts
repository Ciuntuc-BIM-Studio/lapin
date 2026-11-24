/**
 * Drafted shapes in the interface (the engine's CAD shapes, in a view's
 * coordinates, metres): what snapping, picking and previews need. The edits
 * themselves (offset, trim…) run in the engine.
 */

import type { Vec2 } from '../../platform/module';
import { cross2, ptStrict } from '../../platform/vec';

export type Shape =
  | { type: 'line'; a: Vec2; b: Vec2 }
  | { type: 'polyline'; pts: Vec2[]; closed: boolean }
  | { type: 'circle'; c: Vec2; r: number }
  | { type: 'arc'; c: Vec2; r: number; start: number; end: number }
  /**
   * An ellipse (audit S4): its centre, its major semi-axis `m` (a vector), its minor one `ratio` of it a quarter turn anticlockwise;
   * an elliptical arc runs anticlockwise between the parameters `start` and `end` (equal: the whole ellipse). P(t) = c + m cos t + n sin t.
   */
  | { type: 'ellipse'; c: Vec2; m: Vec2; ratio: number; start: number; end: number };

/** A point of an ellipse at parameter t. */
export function ellipseAt(s: { c: Vec2; m: Vec2; ratio: number }, t: number): Vec2 {
  const n = { x: -s.m.y * s.ratio, y: s.m.x * s.ratio };
  return { x: s.c.x + s.m.x * Math.cos(t) + n.x * Math.sin(t), y: s.c.y + s.m.y * Math.cos(t) + n.y * Math.sin(t) };
}
/** Whether an ellipse is whole (no arc of it). */
export const ellipseWhole = (s: { start: number; end: number }) => Math.abs(normAngle(s.end - s.start)) < 1e-12;
const ellipseSweep = (s: { start: number; end: number }) => (ellipseWhole(s) ? Math.PI * 2 : sweepOf(s.start, s.end));
/** An ellipse's points, every 1/64 of a turn of its parameter (the screen's; the engine lays it by the chord rule). */
export function ellipsePoints(s: { c: Vec2; m: Vec2; ratio: number; start: number; end: number }): Vec2[] {
  const sw = ellipseSweep(s), n = Math.max(8, Math.ceil((64 * sw) / (Math.PI * 2)));
  const t0 = ellipseWhole(s) ? 0 : s.start;
  return Array.from({ length: n + 1 }, (_, i) => ellipseAt(s, t0 + (sw * i) / n));
}

const TWO_PI = Math.PI * 2;
export const P = (x: number, y: number): Vec2 => ({ x, y });
export const dist = (a: Vec2, b: Vec2) => Math.hypot(a.x - b.x, a.y - b.y);
export const normAngle = (a: number) => { let r = a % TWO_PI; if (r < 0) r += TWO_PI; return r >= TWO_PI - 1e-15 ? 0 : r; };
export const sweepOf = (start: number, end: number) => { const s = normAngle(end - start); return s < 1e-12 ? TWO_PI : s; };
export const angleInArc = (a: number, start: number, end: number) => {
  const d = normAngle(a - start);
  return d <= normAngle(end - start) + 1e-9 || d >= TWO_PI - 1e-9;
};
const at = (c: Vec2, r: number, a: number): Vec2 => ({ x: c.x + Math.cos(a) * r, y: c.y + Math.sin(a) * r });

type Json = Record<string, unknown>;

/** A shape from the engine's JSON ({type, a: [x, y]…}). */
export function fromJson(o: Json): Shape | null {
  switch (o.type) {
    case 'line': { const a = ptStrict(o.a), b = ptStrict(o.b); return a && b ? { type: 'line', a, b } : null; }
    case 'polyline': {
      const pts = Array.isArray(o.pts) ? o.pts.map(ptStrict) : [];
      return pts.length >= 2 && pts.every(Boolean) ? { type: 'polyline', pts: pts as Vec2[], closed: !!o.closed } : null;
    }
    case 'circle': { const c = ptStrict(o.c); return c && typeof o.r === 'number' ? { type: 'circle', c, r: o.r } : null; }
    case 'arc': { const c = ptStrict(o.c); return c && typeof o.r === 'number' ? { type: 'arc', c, r: o.r, start: Number(o.start) || 0, end: Number(o.end) || 0 } : null; }
    case 'ellipse': {
      const c = ptStrict(o.c), m = ptStrict(o.m);
      return c && m && typeof o.ratio === 'number' && o.ratio > 0 ? { type: 'ellipse', c, m, ratio: o.ratio, start: Number(o.start) || 0, end: Number(o.end) || 0 } : null;
    }
  }
  return null;
}

const arr = (p: Vec2) => [p.x, p.y];
export function toJson(s: Shape): Json {
  switch (s.type) {
    case 'line': return { type: 'line', a: arr(s.a), b: arr(s.b) };
    case 'polyline': return { type: 'polyline', pts: s.pts.map(arr), closed: s.closed };
    case 'circle': return { type: 'circle', c: arr(s.c), r: s.r };
    case 'arc': return { type: 'arc', c: arr(s.c), r: s.r, start: s.start, end: s.end };
    case 'ellipse': return { type: 'ellipse', c: arr(s.c), m: arr(s.m), ratio: s.ratio, ...(ellipseWhole(s) ? {} : { start: s.start, end: s.end }) };
  }
}

export type Prim = { kind: 'seg'; a: Vec2; b: Vec2 } | { kind: 'arc'; c: Vec2; r: number; start: number; end: number; full: boolean };

export function prims(s: Shape): Prim[] {
  switch (s.type) {
    case 'line': return [{ kind: 'seg', a: s.a, b: s.b }];
    case 'polyline': {
      const n = s.closed ? s.pts.length : s.pts.length - 1;
      return Array.from({ length: Math.max(0, n) }, (_, i) => ({ kind: 'seg' as const, a: s.pts[i], b: s.pts[(i + 1) % s.pts.length] }));
    }
    case 'circle': return [{ kind: 'arc', c: s.c, r: s.r, start: 0, end: 0, full: true }];
    case 'arc': return [{ kind: 'arc', c: s.c, r: s.r, start: s.start, end: s.end, full: false }];
    case 'ellipse': { const pts = ellipsePoints(s); return pts.slice(1).map((b, i) => ({ kind: 'seg' as const, a: pts[i], b })); }
  }
}

export function bounds(s: Shape): { min: Vec2; max: Vec2 } {
  let pts: Vec2[];
  switch (s.type) {
    case 'line': pts = [s.a, s.b]; break;
    case 'polyline': pts = s.pts; break;
    case 'circle': pts = [P(s.c.x - s.r, s.c.y - s.r), P(s.c.x + s.r, s.c.y + s.r)]; break;
    case 'arc':
      pts = [at(s.c, s.r, s.start), at(s.c, s.r, s.end)];
      for (let k = 0; k < 4; k++) if (angleInArc((k * Math.PI) / 2, s.start, s.end)) pts.push(at(s.c, s.r, (k * Math.PI) / 2));
      break;
    case 'ellipse': pts = ellipsePoints(s); break;
  }
  return {
    min: P(Math.min(...pts.map((p) => p.x)), Math.min(...pts.map((p) => p.y))),
    max: P(Math.max(...pts.map((p) => p.x)), Math.max(...pts.map((p) => p.y))),
  };
}

export function nearestOnSegment(p: Vec2, a: Vec2, b: Vec2): { point: Vec2; dist: number } {
  const dx = b.x - a.x, dy = b.y - a.y;
  const t = Math.max(0, Math.min(1, ((p.x - a.x) * dx + (p.y - a.y) * dy) / Math.max(dx * dx + dy * dy, 1e-18)));
  const q = P(a.x + dx * t, a.y + dy * t);
  return { point: q, dist: dist(p, q) };
}

export function nearestOnPrim(pr: Prim, p: Vec2): { point: Vec2; dist: number } {
  if (pr.kind === 'seg') return nearestOnSegment(p, pr.a, pr.b);
  const ang = Math.atan2(p.y - pr.c.y, p.x - pr.c.x);
  if (pr.full || angleInArc(ang, pr.start, pr.end)) { const q = at(pr.c, pr.r, ang); return { point: q, dist: dist(p, q) }; }
  const e1 = at(pr.c, pr.r, pr.start), e2 = at(pr.c, pr.r, pr.end);
  return dist(p, e1) <= dist(p, e2) ? { point: e1, dist: dist(p, e1) } : { point: e2, dist: dist(p, e2) };
}

export function nearestOnShape(s: Shape, p: Vec2): { point: Vec2; dist: number } {
  let best = { point: p, dist: Infinity };
  for (const pr of prims(s)) { const n = nearestOnPrim(pr, p); if (n.dist < best.dist) best = n; }
  return best;
}

export interface KeyPoints { endpoints: Vec2[]; midpoints: Vec2[]; centers: Vec2[]; quadrants: Vec2[] }

export function keyPoints(s: Shape): KeyPoints {
  const k: KeyPoints = { endpoints: [], midpoints: [], centers: [], quadrants: [] };
  switch (s.type) {
    case 'line': k.endpoints.push(s.a, s.b); k.midpoints.push(P((s.a.x + s.b.x) / 2, (s.a.y + s.b.y) / 2)); break;
    case 'polyline':
      k.endpoints.push(...s.pts);
      for (const pr of prims(s)) if (pr.kind === 'seg') k.midpoints.push(P((pr.a.x + pr.b.x) / 2, (pr.a.y + pr.b.y) / 2));
      break;
    case 'circle':
      k.centers.push(s.c);
      for (let i = 0; i < 4; i++) k.quadrants.push(at(s.c, s.r, (i * Math.PI) / 2));
      break;
    case 'arc': {
      k.endpoints.push(at(s.c, s.r, s.start), at(s.c, s.r, s.end));
      k.midpoints.push(at(s.c, s.r, s.start + sweepOf(s.start, s.end) / 2));
      k.centers.push(s.c);
      for (let i = 0; i < 4; i++) if (angleInArc((i * Math.PI) / 2, s.start, s.end)) k.quadrants.push(at(s.c, s.r, (i * Math.PI) / 2));
      break;
    }
    case 'ellipse': {
      // Its centre and its axes' ends (those on an arc of it), an arc's ends and middle.
      const whole = ellipseWhole(s);
      k.centers.push(s.c);
      for (let i = 0; i < 4; i++) if (whole || angleInArc((i * Math.PI) / 2, s.start, s.end)) k.quadrants.push(ellipseAt(s, (i * Math.PI) / 2));
      if (!whole) { k.endpoints.push(ellipseAt(s, s.start), ellipseAt(s, s.end)); k.midpoints.push(ellipseAt(s, s.start + ellipseSweep(s) / 2)); }
      break;
    }
  }
  return k;
}

function addUnique(o: Vec2[], p: Vec2) { if (!o.some((q) => dist(p, q) < 1e-9)) o.push(p); }

export function intersectPrims(p: Prim, q: Prim): Vec2[] {
  const o: Vec2[] = [];
  if (p.kind === 'seg' && q.kind === 'seg') segSeg(p, q, o);
  else if (p.kind === 'seg' && q.kind === 'arc') segArc(p, q, o);
  else if (p.kind === 'arc' && q.kind === 'seg') segArc(q, p, o);
  else if (p.kind === 'arc' && q.kind === 'arc') arcArc(p, q, o);
  return o;
}

function segSeg(s1: { a: Vec2; b: Vec2 }, s2: { a: Vec2; b: Vec2 }, o: Vec2[]) {
  const d1 = P(s1.b.x - s1.a.x, s1.b.y - s1.a.y), d2 = P(s2.b.x - s2.a.x, s2.b.y - s2.a.y);
  const den = cross2(d1, d2);
  const l1 = Math.hypot(d1.x, d1.y), l2 = Math.hypot(d2.x, d2.y);
  const w = P(s2.a.x - s1.a.x, s2.a.y - s1.a.y);
  if (Math.abs(den) / Math.max(l1 * l2, 1e-18) < 1e-12) {
    if (Math.abs(cross2(w, d1)) / Math.max(l1, 1e-18) > 1e-9) return;
    for (const [p, a, b] of [[s1.a, s2.a, s2.b], [s1.b, s2.a, s2.b], [s2.a, s1.a, s1.b], [s2.b, s1.a, s1.b]] as const)
      if (nearestOnSegment(p, a, b).dist < 1e-9) addUnique(o, p);
    return;
  }
  const t = cross2(w, d2) / den, u = cross2(w, d1) / den;
  if (t >= -1e-9 && t <= 1 + 1e-9 && u >= -1e-9 && u <= 1 + 1e-9) addUnique(o, P(s1.a.x + d1.x * t, s1.a.y + d1.y * t));
}

function segArc(s: { a: Vec2; b: Vec2 }, c: Extract<Prim, { kind: 'arc' }>, o: Vec2[]) {
  const d = P(s.b.x - s.a.x, s.b.y - s.a.y), f = P(s.a.x - c.c.x, s.a.y - c.c.y);
  const A = d.x * d.x + d.y * d.y;
  if (A < 1e-18) return;
  const B = 2 * (f.x * d.x + f.y * d.y), C = f.x * f.x + f.y * f.y - c.r * c.r;
  const disc = B * B - 4 * A * C;
  const tol = 1e-9 * Math.max(1, B * B);
  if (disc < -tol) return;
  const ts = Math.abs(disc) <= tol ? [-B / (2 * A)] : [(-B - Math.sqrt(disc)) / (2 * A), (-B + Math.sqrt(disc)) / (2 * A)];
  for (const t of ts) {
    if (t < -1e-9 || t > 1 + 1e-9) continue;
    const p = P(s.a.x + d.x * t, s.a.y + d.y * t);
    if (c.full || angleInArc(Math.atan2(p.y - c.c.y, p.x - c.c.x), c.start, c.end)) addUnique(o, p);
  }
}

function arcArc(a: Extract<Prim, { kind: 'arc' }>, b: Extract<Prim, { kind: 'arc' }>, o: Vec2[]) {
  const dx = b.c.x - a.c.x, dy = b.c.y - a.c.y, d = Math.hypot(dx, dy);
  if (d < 1e-12 || d > a.r + b.r + 1e-9 || d < Math.abs(a.r - b.r) - 1e-9) return;
  const x = (d * d + a.r * a.r - b.r * b.r) / (2 * d), h2 = a.r * a.r - x * x;
  const u = P(dx / d, dy / d), mid = P(a.c.x + u.x * x, a.c.y + u.y * x);
  const pts = h2 <= 1e-12 ? [mid] : [P(mid.x - u.y * Math.sqrt(h2), mid.y + u.x * Math.sqrt(h2)), P(mid.x + u.y * Math.sqrt(h2), mid.y - u.x * Math.sqrt(h2))];
  for (const p of pts) {
    if (!a.full && !angleInArc(Math.atan2(p.y - a.c.y, p.x - a.c.x), a.start, a.end)) continue;
    if (!b.full && !angleInArc(Math.atan2(p.y - b.c.y, p.x - b.c.x), b.start, b.end)) continue;
    addUnique(o, p);
  }
}

export function intersectShapes(a: Shape, b: Shape): Vec2[] {
  const o: Vec2[] = [];
  for (const p of prims(a)) for (const q of prims(b)) for (const x of intersectPrims(p, q)) addUnique(o, x);
  return o;
}

/** A shape as polylines to draw (arcs every few degrees). */
export function polylines(s: Shape): Vec2[][] {
  switch (s.type) {
    case 'line': return [[s.a, s.b]];
    case 'polyline': return [s.closed ? [...s.pts, s.pts[0]] : s.pts];
    case 'circle': return [Array.from({ length: 65 }, (_, i) => at(s.c, s.r, (i / 64) * TWO_PI))];
    case 'arc': {
      const sw = sweepOf(s.start, s.end), n = Math.max(4, Math.ceil(sw / (Math.PI / 36)));
      return [Array.from({ length: n + 1 }, (_, i) => at(s.c, s.r, s.start + (sw * i) / n))];
    }
    case 'ellipse': return [ellipsePoints(s)];
  }
}

/**
 * The ellipse with centre `c`, its first semi-axis to `a` and its other semi-axis `b` long (ELLIPSE's three clicks): the longer
 * one is kept as the major axis, as DXF does.
 */
export function ellipseOf(c: Vec2, a: Vec2, b: number): Shape | null {
  const m = { x: a.x - c.x, y: a.y - c.y }, la = Math.hypot(m.x, m.y);
  if (!(la > 1e-6) || !(b > 1e-6)) return null;
  if (b <= la) return { type: 'ellipse', c, m, ratio: b / la, start: 0, end: 0 };
  // The other axis is the major one: a quarter turn anticlockwise from the first.
  return { type: 'ellipse', c, m: { x: (-m.y * b) / la, y: (m.x * b) / la }, ratio: la / b, start: 0, end: 0 };
}

/**
 * The arc that leaves `start` along `dir` (tangent there) and ends at `end`: its centre, radius and angles (always counter-clockwise
 * start to end; `cw` says the travel from `start` to `end` is clockwise, so `start` is the arc's `end`). Null when `end` lies on the
 * tangent line, or on `start`.
 */
export function arcTangent(start: Vec2, dir: Vec2, end: Vec2): { c: Vec2; r: number; start: number; end: number; cw: boolean } | null {
  const len = Math.hypot(dir.x, dir.y);
  if (len < 1e-12) return null;
  const n = P(-dir.y / len, dir.x / len);
  const dx = end.x - start.x, dy = end.y - start.y;
  const along = n.x * dx + n.y * dy;
  if (Math.abs(along) < 1e-9 * Math.max(1, Math.hypot(dx, dy))) return null;
  const t = (dx * dx + dy * dy) / (2 * along);
  const c = P(start.x + n.x * t, start.y + n.y * t);
  const a1 = Math.atan2(start.y - c.y, start.x - c.x), a2 = Math.atan2(end.y - c.y, end.x - c.x);
  return t > 0 ? { c, r: t, start: normAngle(a1), end: normAngle(a2), cw: false } : { c, r: -t, start: normAngle(a2), end: normAngle(a1), cw: true };
}

/** The arc through three points, from the first to the third by the second; null when they lie on a line. */
export function arcThrough(p1: Vec2, p2: Vec2, p3: Vec2): Shape | null {
  const d = 2 * (p1.x * (p2.y - p3.y) + p2.x * (p3.y - p1.y) + p3.x * (p1.y - p2.y));
  if (Math.abs(d) < 1e-12) return null;
  const s1 = p1.x * p1.x + p1.y * p1.y, s2 = p2.x * p2.x + p2.y * p2.y, s3 = p3.x * p3.x + p3.y * p3.y;
  const c = P((s1 * (p2.y - p3.y) + s2 * (p3.y - p1.y) + s3 * (p1.y - p2.y)) / d, (s1 * (p3.x - p2.x) + s2 * (p1.x - p3.x) + s3 * (p2.x - p1.x)) / d);
  const r = dist(c, p1);
  const a1 = Math.atan2(p1.y - c.y, p1.x - c.x), a2 = Math.atan2(p2.y - c.y, p2.x - c.x), a3 = Math.atan2(p3.y - c.y, p3.x - c.x);
  // Counter-clockwise from p1 to p3 when p2 lies on that way round, else from p3 to p1.
  return angleInArc(a2, a1, a3) ? { type: 'arc', c, r, start: normAngle(a1), end: normAngle(a3) } : { type: 'arc', c, r, start: normAngle(a3), end: normAngle(a1) };
}

/** A straight line by its two points. */
export type Line = [Vec2, Vec2];

/** A shape's straight pieces: a line, a polyline's segments, an ellipse's chords; a circle or an arc has none. */
export function segmentsOf(s: Shape): Line[] {
  if (s.type === 'line') return [[s.a, s.b]];
  if (s.type === 'ellipse') return segmentsOf({ type: 'polyline', pts: ellipsePoints(s), closed: false });
  if (s.type === 'polyline') {
    const n = s.pts.length, out: Line[] = [];
    for (let i = 0; i < (s.closed ? n : n - 1); i++) out.push([s.pts[i], s.pts[(i + 1) % n]]);
    return out;
  }
  return [];
}

const distToSegment = (p: Vec2, [a, b]: Line): number => {
  const dx = b.x - a.x, dy = b.y - a.y, l2 = dx * dx + dy * dy;
  const t = l2 > 0 ? Math.max(0, Math.min(1, ((p.x - a.x) * dx + (p.y - a.y) * dy) / l2)) : 0;
  return Math.hypot(a.x + dx * t - p.x, a.y + dy * t - p.y);
};

/** The straight line among these shapes nearest the point (none when they have no straight piece). */
export function nearestLine(shapes: readonly Shape[], at: Vec2): Line | null {
  let best: Line | null = null, least = Infinity;
  for (const s of shapes) for (const l of segmentsOf(s)) {
    if (Math.hypot(l[1].x - l[0].x, l[1].y - l[0].y) < 1e-9) continue;
    const d = distToSegment(at, l);
    if (d < least) { least = d; best = l; }
  }
  return best;
}
