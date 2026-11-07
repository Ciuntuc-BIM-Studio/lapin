/**
 * The direct way of sketching in 3D, SketchUp's (decision 374): the state of
 * Studio's Line, Rectangle and Circle drawn right on a face (or the storey's
 * level), and what a click, a typed value or Enter makes of it. Everything
 * here is on the plane locked by the first click, in its (x, y); the overlay
 * (Sketch3D.tsx) takes the mouse and the keys and stores what is done.
 */

import { create } from 'zustand';
import type { Vec2 } from '../../platform/module';
import type { Vec3 } from '../camera';
import type { WorkFrame } from '../workPlane';
import type { FacePick } from './sketch3d';
import type { Inferred, Lock } from './inference';
import type { Vcb } from './vcb';

export type DirectTool = 'line' | 'rect' | 'circle' | 'arc' | 'offset';

/** The plane drawn on: a face's (its pick and outline) or the storey's level (no pick). */
export interface DirectPlane { frame: WorkFrame; pick?: FacePick; outline?: Vec3[]; storeyId?: string | null; name: string; /** The face's key in the overlay's cache. */ key?: string }

export interface DirectState {
  tool: DirectTool | null;
  /** The plane under the cursor before the first click; locked by it. */
  plane: DirectPlane | null;
  locked: boolean;
  pts: Vec2[];
  inferred: Inferred | null;
  text: string;
  lock: Lock;
  /** The lock was made by holding Shift (let go with it). */
  shiftLock: boolean;
  /** Offset: the loop clicked (the face's outline, or the region of it), on the plane. */
  loop: Vec2[] | null;
}

const idle = { plane: null, locked: false, pts: [], inferred: null, text: '', lock: null, shiftLock: false, loop: null };
export const useDirect = create<DirectState>(() => ({ tool: null, ...idle }));

export const startDirect = (tool: DirectTool) => useDirect.setState({ tool, ...idle });
export const endDirect = () => useDirect.setState({ tool: null, ...idle });
/** The shape in hand dropped, the tool kept (SketchUp's: draw the next one). */
export const resetDirect = () => useDirect.setState({ ...idle });

export type Outcome =
  | { kind: 'more'; pts: Vec2[] }
  | { kind: 'arc'; start: Vec2; end: Vec2; through: Vec2 }
  | { kind: 'closed'; outline: Vec2[]; circle?: { c: Vec2; r: number } }
  | { kind: 'open'; pts: Vec2[] }
  | { kind: 'none' };

const dist = (a: Vec2, b: Vec2) => Math.hypot(a.x - b.x, a.y - b.y);
const EPS = 1e-6;

/** A rectangle from a corner to the opposite one, square to the plane's x and y. */
export const rect = (a: Vec2, b: Vec2): Vec2[] => [a, { x: b.x, y: a.y }, b, { x: a.x, y: b.y }];

/** A circle's outline in chords (the engine keeps it a circle). */
export const circlePts = (c: Vec2, r: number, n = 48): Vec2[] =>
  Array.from({ length: n }, (_, i) => ({ x: c.x + r * Math.cos((2 * Math.PI * i) / n), y: c.y + r * Math.sin((2 * Math.PI * i) / n) }));

/** A click at a point: the next corner, or the shape done (a line closed on its first point, a rectangle, a circle). */
export function click(tool: DirectTool, pts: readonly Vec2[], p: Vec2): Outcome {
  if (!pts.length) return { kind: 'more', pts: [p] };
  const first = pts[0], last = pts[pts.length - 1];
  if (tool === 'rect') {
    if (Math.abs(p.x - first.x) < EPS || Math.abs(p.y - first.y) < EPS) return { kind: 'none' };
    return { kind: 'closed', outline: rect(first, p) };
  }
  if (tool === 'circle') {
    const r = dist(first, p);
    return r < 1e-4 ? { kind: 'none' } : { kind: 'closed', outline: circlePts(first, r), circle: { c: first, r } };
  }
  if (tool === 'arc') {
    if (pts.length === 1) return dist(p, first) < 1e-4 ? { kind: 'none' } : { kind: 'more', pts: [first, p] };
    return Math.abs(bulgeOf(first, pts[1], p)) < 1e-6 ? { kind: 'none' } : { kind: 'arc', start: first, end: pts[1], through: arcMiddle(first, pts[1], bulgeOf(first, pts[1], p)) };
  }
  if (dist(p, last) < EPS) return { kind: 'none' };
  if (pts.length >= 3 && dist(p, first) < EPS) return { kind: 'closed', outline: [...pts] };
  return { kind: 'more', pts: [...pts, p] };
}

/** The point a length (and maybe an angle, degrees from the plane's x) puts from the last one, the way the cursor points. */
export function along(from: Vec2, toward: Vec2 | null, length: number, deg?: number): Vec2 {
  if (deg != null) { const a = (deg * Math.PI) / 180; return { x: from.x + Math.cos(a) * length, y: from.y + Math.sin(a) * length }; }
  const d = toward ? dist(from, toward) : 0;
  const u = d > 1e-9 ? { x: (toward!.x - from.x) / d, y: (toward!.y - from.y) / d } : { x: 1, y: 0 };
  return { x: from.x + u.x * length, y: from.y + u.y * length };
}

/** A typed value, applied (Enter): the next point at a length, a rectangle's width and height, a circle's radius. */
export function typed(tool: DirectTool, pts: readonly Vec2[], v: Vcb, cursor: Vec2 | null): Outcome {
  if (!pts.length) return { kind: 'none' };
  const first = pts[0], last = pts[pts.length - 1];
  if (tool === 'rect') {
    if (v.kind !== 'pair') return { kind: 'none' };
    const sx = cursor && cursor.x < first.x ? -1 : 1, sy = cursor && cursor.y < first.y ? -1 : 1;
    return click('rect', pts, { x: first.x + sx * Math.abs(v.a), y: first.y + sy * Math.abs(v.b) });
  }
  if (tool === 'circle') {
    const r = v.kind === 'radius' || v.kind === 'length' ? Math.abs(v.value) : null;
    return r ? { kind: 'closed', outline: circlePts(first, r), circle: { c: first, r } } : { kind: 'none' };
  }
  if (tool === 'arc') {
    if (v.kind !== 'length') return { kind: 'none' };
    // Two points: the chord typed along the mouse's way; the chord placed: the bulge, on the mouse's side.
    if (pts.length === 1) return click('arc', pts, along(first, cursor, v.value));
    const side = cursor ? Math.sign(bulgeOf(first, pts[1], cursor)) || 1 : 1;
    return { kind: 'arc', start: first, end: pts[1], through: arcMiddle(first, pts[1], side * Math.abs(v.value)) };
  }
  if (v.kind === 'length') return click('line', pts, along(last, cursor, v.value));
  if (v.kind === 'lengthAngle') return click('line', pts, along(last, cursor, v.value, v.deg));
  return { kind: 'none' };
}

/** Enter with nothing typed, or Esc: a line of two points or more stays an open sketch; anything else is dropped. */
export function finishOpen(tool: DirectTool, pts: readonly Vec2[]): Outcome {
  return tool === 'line' && pts.length >= 2 ? { kind: 'open', pts: [...pts] } : { kind: 'none' };
}

/** How far a point stands off the chord a→b, left of it positive (an arc's bulge, SketchUp's). */
export function bulgeOf(a: Vec2, b: Vec2, p: Vec2): number {
  const d = dist(a, b);
  if (d < 1e-12) return 0;
  return ((b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x)) / d;
}

/** The middle of the arc on the chord a→b with a bulge (left positive). */
export function arcMiddle(a: Vec2, b: Vec2, bulge: number): Vec2 {
  const d = dist(a, b) || 1;
  return { x: (a.x + b.x) / 2 - ((b.y - a.y) / d) * bulge, y: (a.y + b.y) / 2 + ((b.x - a.x) / d) * bulge };
}

/** An arc through three points as the preview draws it (its chords), or the straight line when they lie on one. */
export function arcPts(a: Vec2, m: Vec2, b: Vec2, n = 32): Vec2[] {
  const d = 2 * (a.x * (m.y - b.y) + m.x * (b.y - a.y) + b.x * (a.y - m.y));
  if (Math.abs(d) < 1e-12) return [a, b];
  const s = (q: Vec2) => q.x * q.x + q.y * q.y;
  const c = { x: (s(a) * (m.y - b.y) + s(m) * (b.y - a.y) + s(b) * (a.y - m.y)) / d, y: (s(a) * (b.x - m.x) + s(m) * (a.x - b.x) + s(b) * (m.x - a.x)) / d };
  const r = dist(c, a);
  const ang = (q: Vec2) => Math.atan2(q.y - c.y, q.x - c.x);
  const a0 = ang(a), am = ang(m), a1 = ang(b);
  const norm = (x: number) => ((x % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI);
  // The way round from a to b that passes m.
  let sweep = norm(a1 - a0);
  if (norm(am - a0) > sweep) sweep -= 2 * Math.PI;
  return Array.from({ length: n + 1 }, (_, i) => { const t = a0 + (sweep * i) / n; return { x: c.x + r * Math.cos(t), y: c.y + r * Math.sin(t) }; });
}

/** A loop's signed area (counter-clockwise positive). */
export const areaOf = (pts: readonly Vec2[]) => pts.reduce((s, p, i) => { const q = pts[(i + 1) % pts.length]; return s + p.x * q.y - q.x * p.y; }, 0) / 2;

/**
 * A closed loop offset by a distance, inward when positive (SketchUp's Offset of a face), corners mitred; null when it
 * folds (an inward offset past the loop's narrowest place).
 */
export function offsetLoop(loop: readonly Vec2[], d: number): Vec2[] | null {
  if (loop.length < 3) return null;
  const pts = areaOf(loop) >= 0 ? [...loop] : [...loop].reverse();
  const n = pts.length;
  const out: Vec2[] = [];
  for (let i = 0; i < n; i++) {
    const p = pts[(i - 1 + n) % n], q = pts[i], r = pts[(i + 1) % n];
    const e1 = norm2(q.x - p.x, q.y - p.y), e2 = norm2(r.x - q.x, r.y - q.y);
    // Inward is the left of a counter-clockwise loop.
    const n1 = { x: -e1.y, y: e1.x }, n2 = { x: -e2.y, y: e2.x };
    const b = norm2(n1.x + n2.x, n1.y + n2.y);
    const c = b.x * n1.x + b.y * n1.y;
    if (Math.abs(c) < 1e-6) return null;
    out.push({ x: q.x + (b.x * d) / c, y: q.y + (b.y * d) / c });
  }
  const a0 = areaOf(pts), a1 = areaOf(out);
  return a1 > 1e-12 && (d <= 0 || a1 < a0) ? out : null;
}
const norm2 = (x: number, y: number) => { const l = Math.hypot(x, y) || 1; return { x: x / l, y: y / l }; };

/** How far a point lies inside a loop's outline (negative outside): the distance an offset through it takes. */
export function insideDistance(loop: readonly Vec2[], p: Vec2): number {
  let best = Infinity;
  loop.forEach((a, i) => {
    const b = loop[(i + 1) % loop.length];
    const dx = b.x - a.x, dy = b.y - a.y, l2 = dx * dx + dy * dy;
    const t = l2 > 0 ? Math.max(0, Math.min(1, ((p.x - a.x) * dx + (p.y - a.y) * dy) / l2)) : 0;
    best = Math.min(best, Math.hypot(a.x + dx * t - p.x, a.y + dy * t - p.y));
  });
  let inside = false;
  for (let i = 0, j = loop.length - 1; i < loop.length; j = i++) {
    const a = loop[i], b = loop[j];
    if ((a.y > p.y) !== (b.y > p.y) && p.x < ((b.x - a.x) * (p.y - a.y)) / (b.y - a.y) + a.x) inside = !inside;
  }
  return inside ? best : -best;
}

/** Offset: the loop's offset through the cursor or by the typed distance (on the cursor's side). */
export function offsetOutcome(loop: readonly Vec2[], cursor: Vec2 | null, typedD: number | null): Outcome {
  const at = cursor ? insideDistance(loop, cursor) : 0;
  const d = typedD != null ? (at < 0 ? -Math.abs(typedD) : Math.abs(typedD)) : at;
  if (Math.abs(d) < 1e-4) return { kind: 'none' };
  const o = offsetLoop(loop, d);
  return o ? { kind: 'closed', outline: o } : { kind: 'none' };
}

/** The shape as it stands with the cursor (for the preview), and whether it is closed. */
export function preview(tool: DirectTool, pts: readonly Vec2[], cursor: Vec2 | null): { pts: Vec2[]; closed: boolean } {
  if (!pts.length || !cursor) return { pts: [...pts], closed: false };
  if (tool === 'arc') return pts.length === 1 ? { pts: [pts[0], cursor], closed: false } : { pts: arcPts(pts[0], arcMiddle(pts[0], pts[1], bulgeOf(pts[0], pts[1], cursor)), pts[1]), closed: false };
  if (tool === 'rect') return { pts: rect(pts[0], cursor), closed: true };
  if (tool === 'circle') { const r = dist(pts[0], cursor); return r > 1e-6 ? { pts: circlePts(pts[0], r), closed: true } : { pts: [], closed: false }; }
  return { pts: [...pts, cursor], closed: false };
}
