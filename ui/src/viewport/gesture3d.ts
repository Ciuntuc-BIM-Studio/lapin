/**
 * The placing tools' gestures in the 3D view (docs/plan-3d-modelling.md,
 * stage B; testers' report #105): the same clicks as in plan (PlanView.tsx),
 * kept apart from the view so they are tested on their own. A point is the
 * cursor snapped in 3D (snap3d.ts) brought onto the active level, as the tool
 * takes plan points; a value typed goes from the last point the way the cursor
 * points. Place3D.tsx draws them and takes the mouse.
 */

import type { Gesture, Vec2 } from '../platform/module';
import { along, arcPts } from './sketch/direct';
import type { Vcb } from './sketch/vcb';

/** The gestures the 3D host takes as points (pick, face and pick-then-point stay the view's own clicks). */
export const POINT_GESTURES = ['point', 'twoPoints', 'rectangle', 'threePoints', 'polygon', 'polyline'] as const;
export type PointGesture = Extract<Gesture, { kind: typeof POINT_GESTURES[number] }>;
export const isPointGesture = (g: Gesture | undefined): g is PointGesture => !!g && (POINT_GESTURES as readonly string[]).includes(g.kind);

/** What a click does: the points placed now, and the points the tool completes with (null: not yet). */
export interface Step3 { pts: Vec2[]; done: Vec2[] | null }

const near = (a: Vec2, b: Vec2, tol: number) => Math.hypot(a.x - b.x, a.y - b.y) < tol;

/** A click at `p` with `pts` placed (`tol`: how near the first or last point closes or ends, m). */
export function clickStep(g: PointGesture, pts: readonly Vec2[], p: Vec2, tol: number): Step3 {
  switch (g.kind) {
    case 'point': return { pts: [], done: [p] };
    case 'twoPoints':
    case 'rectangle':
      if (!pts.length) return { pts: [p], done: null };
      return { pts: g.kind === 'twoPoints' && g.repeat ? [p] : [], done: [pts[0], p] };
    case 'threePoints':
      if (pts.length < 2) return { pts: [...pts, p], done: null };
      return { pts: [], done: [pts[0], pts[1], p] };
    case 'polygon':
      if (pts.length >= 3 && near(pts[0], p, tol)) return { pts: [], done: [...pts] };
      return { pts: [...pts, p], done: null };
    case 'polyline':
      if (pts.length >= 2 && near(pts[pts.length - 1], p, tol)) return { pts: [], done: [...pts] };
      return { pts: [...pts, p], done: null };
  }
}

/** Enter with nothing typed: a polygon of three points or more closes, a polyline of two or more ends; else nothing. */
export function enterStep(g: PointGesture, pts: readonly Vec2[]): Step3 | null {
  if (g.kind === 'polygon' && pts.length >= 3) return { pts: [], done: [...pts] };
  if (g.kind === 'polyline' && pts.length >= 2) return { pts: [], done: [...pts] };
  return null;
}

/**
 * The point a typed value puts down: a length (and an angle) from the last point the way the cursor points; for a
 * rectangle a width,height from its first corner towards the cursor. Null when the value says nothing here.
 */
export function typedPoint(g: PointGesture, pts: readonly Vec2[], v: Vcb, cursor: Vec2 | null): Vec2 | null {
  if (!pts.length) return null;
  const first = pts[0], last = pts[pts.length - 1];
  if (g.kind === 'rectangle' && v.kind === 'pair') {
    const sx = cursor && cursor.x < first.x ? -1 : 1, sy = cursor && cursor.y < first.y ? -1 : 1;
    return { x: first.x + sx * Math.abs(v.a), y: first.y + sy * Math.abs(v.b) };
  }
  if (v.kind === 'length') return along(last, cursor, v.value);
  if (v.kind === 'lengthAngle') return along(last, cursor, v.value, v.deg);
  return null;
}

/** The tool's own rule for the next point (a copper pipe's 45°), over the snap. */
export function constrained(g: PointGesture, pts: readonly Vec2[], p: Vec2): Vec2 {
  return (g.kind === 'polyline' || g.kind === 'threePoints') && g.constrain && pts.length ? g.constrain([...pts], p) : p;
}

/** What is drawn while the cursor moves: the outline the points and the cursor make (closed for a rectangle and a polygon). */
export function previewOf(g: PointGesture, pts: readonly Vec2[], cursor: Vec2 | null): { pts: Vec2[]; closed: boolean } {
  const all = cursor ? [...pts, cursor] : [...pts];
  if (g.kind === 'rectangle' && pts.length === 1 && cursor) {
    const a = pts[0];
    return { pts: [a, { x: cursor.x, y: a.y }, cursor, { x: a.x, y: cursor.y }], closed: true };
  }
  if (g.kind === 'threePoints' && cursor && pts.length) {
    const own = g.preview?.([...pts], cursor);
    if (own) return { pts: own, closed: false };
    // An arc: from its start to its end through the cursor.
    if (g.arc && pts.length === 2) return { pts: arcPts(pts[0], cursor, pts[1]), closed: false };
  }
  return { pts: all, closed: g.kind === 'polygon' && all.length >= 3 };
}

/**
 * The storey a point in space stands on (a floor's top clicked in 3D): the highest whose level is at or under it (5 cm
 * spare, a finish's thickness); null under every storey.
 */
export function storeyAt(z: number, storeys: readonly { id: string; elevation: number }[]): string | null {
  let best: { id: string; elevation: number } | null = null;
  for (const s of storeys) if (s.elevation <= z + 0.05 && (!best || s.elevation > best.elevation)) best = s;
  return best?.id ?? null;
}
