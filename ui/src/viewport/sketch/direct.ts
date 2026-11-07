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

export type DirectTool = 'line' | 'rect' | 'circle';

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
}

const idle = { plane: null, locked: false, pts: [], inferred: null, text: '', lock: null, shiftLock: false };
export const useDirect = create<DirectState>(() => ({ tool: null, ...idle }));

export const startDirect = (tool: DirectTool) => useDirect.setState({ tool, ...idle });
export const endDirect = () => useDirect.setState({ tool: null, ...idle });
/** The shape in hand dropped, the tool kept (SketchUp's: draw the next one). */
export const resetDirect = () => useDirect.setState({ ...idle });

export type Outcome =
  | { kind: 'more'; pts: Vec2[] }
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
  if (v.kind === 'length') return click('line', pts, along(last, cursor, v.value));
  if (v.kind === 'lengthAngle') return click('line', pts, along(last, cursor, v.value, v.deg));
  return { kind: 'none' };
}

/** Enter with nothing typed, or Esc: a line of two points or more stays an open sketch; anything else is dropped. */
export function finishOpen(tool: DirectTool, pts: readonly Vec2[]): Outcome {
  return tool === 'line' && pts.length >= 2 ? { kind: 'open', pts: [...pts] } : { kind: 'none' };
}

/** The shape as it stands with the cursor (for the preview), and whether it is closed. */
export function preview(tool: DirectTool, pts: readonly Vec2[], cursor: Vec2 | null): { pts: Vec2[]; closed: boolean } {
  if (!pts.length || !cursor) return { pts: [...pts], closed: false };
  if (tool === 'rect') return { pts: rect(pts[0], cursor), closed: true };
  if (tool === 'circle') { const r = dist(pts[0], cursor); return r > 1e-6 ? { pts: circlePts(pts[0], r), closed: true } : { pts: [], closed: false }; }
  return { pts: [...pts, cursor], closed: false };
}
