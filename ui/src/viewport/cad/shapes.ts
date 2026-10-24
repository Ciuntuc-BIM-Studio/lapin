/**
 * Drafted shapes in the interface (the engine's CAD shapes, in a view's
 * coordinates, metres): what snapping, picking and previews need. The edits
 * themselves (offset, trim…) run in the engine.
 */

import type { Vec2 } from '../../platform/module';

export type Shape =
  | { type: 'line'; a: Vec2; b: Vec2 }
  | { type: 'polyline'; pts: Vec2[]; closed: boolean }
  | { type: 'circle'; c: Vec2; r: number }
  | { type: 'arc'; c: Vec2; r: number; start: number; end: number };

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
const pt = (v: unknown): Vec2 | null => (Array.isArray(v) && v.length >= 2 && typeof v[0] === 'number' && typeof v[1] === 'number' ? { x: v[0], y: v[1] } : null);

/** A shape from the engine's JSON ({type, a: [x, y]…}). */
export function fromJson(o: Json): Shape | null {
  switch (o.type) {
    case 'line': { const a = pt(o.a), b = pt(o.b); return a && b ? { type: 'line', a, b } : null; }
    case 'polyline': {
      const pts = Array.isArray(o.pts) ? o.pts.map(pt) : [];
      return pts.length >= 2 && pts.every(Boolean) ? { type: 'polyline', pts: pts as Vec2[], closed: !!o.closed } : null;
    }
    case 'circle': { const c = pt(o.c); return c && typeof o.r === 'number' ? { type: 'circle', c, r: o.r } : null; }
    case 'arc': { const c = pt(o.c); return c && typeof o.r === 'number' ? { type: 'arc', c, r: o.r, start: Number(o.start) || 0, end: Number(o.end) || 0 } : null; }
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
  }
  return k;
}

const cross = (a: Vec2, b: Vec2) => a.x * b.y - a.y * b.x;
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
  const den = cross(d1, d2);
  const l1 = Math.hypot(d1.x, d1.y), l2 = Math.hypot(d2.x, d2.y);
  const w = P(s2.a.x - s1.a.x, s2.a.y - s1.a.y);
  if (Math.abs(den) / Math.max(l1 * l2, 1e-18) < 1e-12) {
    if (Math.abs(cross(w, d1)) / Math.max(l1, 1e-18) > 1e-9) return;
    for (const [p, a, b] of [[s1.a, s2.a, s2.b], [s1.b, s2.a, s2.b], [s2.a, s1.a, s1.b], [s2.b, s1.a, s1.b]] as const)
      if (nearestOnSegment(p, a, b).dist < 1e-9) addUnique(o, p);
    return;
  }
  const t = cross(w, d2) / den, u = cross(w, d1) / den;
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
  }
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
