/**
 * A plan's orientation (decision 526: AutoCAD's PLAN, Revit's rotated crop, ArchiCAD's Set Orientation): the model
 * direction it shows horizontal, to the right, kept on the view (`orientation`: degrees, or "ucs" to follow the UCS's
 * X axis). The plan turns on screen by minus that angle, and the engine prints its sheets, PDF and DXF the same way.
 * Texts stay readable as drafting has them: upright within ±90°, read from the bottom or the right.
 */

import type { Vec2 } from '../platform/module';
import { execute } from '../engine/session';
import { turn, type Frame } from '../app/location';

/** The value of `orientation` that follows the UCS. */
export const FOLLOW_UCS = 'ucs';

/** An angle (degrees) in (−180°, 180°]. */
export function normalizeDeg(a: number): number {
  let r = a % 360;
  if (r <= -180) r += 360;
  if (r > 180) r -= 360;
  return r;
}

/** A plan's orientation (degrees): its own angle, the UCS's when it follows it, 0 when it is not turned. */
export function orientationOf(value: unknown, ucs: Frame | null): number {
  if (value === FOLLOW_UCS) return ucs?.angle ?? 0;
  const n = typeof value === 'number' ? value : typeof value === 'string' ? Number(value) : NaN;
  return Number.isFinite(n) ? normalizeDeg(n) : 0;
}

/** The screen turn (radians, anticlockwise) a plan of that orientation takes: minus it; 0 when north is up. */
export const turnOf = (orientationDeg: number): number => (Math.abs(normalizeDeg(orientationDeg)) < 1e-9 ? 0 : (-orientationDeg * Math.PI) / 180);

/** The direction from a to b (degrees): the orientation that shows the edge a → b horizontal. */
export const edgeAngle = (a: Vec2, b: Vec2): number => normalizeDeg((Math.atan2(b.y - a.y, b.x - a.x) * 180) / Math.PI);

/**
 * The edge a → b brought to the nearer reading of it: an edge drawn right to left shows the same line as left to right,
 * so the orientation that turns the plan least (within ±90°) is taken.
 */
export function alignAngle(a: Vec2, b: Vec2): number {
  const d = edgeAngle(a, b);
  return d > 90 ? d - 180 : d <= -90 ? d + 180 : d;
}

/** Whether a text at an angle (degrees, anticlockwise) reads upright: from the bottom, or from the right when vertical. */
export function readable(deg: number): boolean {
  const a = normalizeDeg(deg);
  return a > -90 + 1e-6 && a <= 90 + 1e-6;
}

export interface TurnedText { x: number; y: number; angle: number; anchor: string }

/**
 * A drawing's text as a plan turned by `turnDeg` shows it, still in the drawing's coordinates (the caller's map turns its
 * point): its angle the turned one; one that would read upside down turned over on its own footprint (start ↔ end, its
 * point moved across by its height, `mPerMm` drawing metres per paper mm), as the engine prints it (Turning.Upright).
 */
export function uprightText(t: { x: number; y: number; angle?: number; anchor?: string; baseline?: string; height: number }, turnDeg: number, mPerMm: number): TurnedText {
  const angle = t.angle ?? 0, anchor = t.anchor ?? 'middle';
  if (!turnDeg) return { x: t.x, y: t.y, angle, anchor };
  const a = angle + turnDeg;
  if (readable(a)) return { x: t.x, y: t.y, angle: normalizeDeg(a), anchor };
  const r = (angle * Math.PI) / 180;
  const h = t.baseline === 'middle' ? 0 : t.height * mPerMm;
  return { x: t.x - Math.sin(r) * h, y: t.y + Math.cos(r) * h, angle: normalizeDeg(a + 180), anchor: anchor === 'start' ? 'end' : anchor === 'end' ? 'start' : anchor };
}

/** The screen's corners in the plan, in order round it (the key plan's box of a turned plan). */
export function screenCorners(toWorld: (x: number, y: number) => Vec2, w: number, h: number): Vec2[] {
  return [toWorld(0, h), toWorld(w, h), toWorld(w, 0), toWorld(0, 0)];
}

/** The axes ortho, polar tracking and the grid follow in a turned plan with no UCS: the screen's, through the plan's origin. */
export const screenAxes = (orientationDeg: number): Frame | null => (Math.abs(normalizeDeg(orientationDeg)) < 1e-9 ? null : { origin: { x: 0, y: 0 }, angle: normalizeDeg(orientationDeg) });

/** A plan point on screen through a turn about the view's centre (what PlanView's toScreen does), for tests and helpers. */
export function planToScreen(p: Vec2, view: { cx: number; cy: number; scale: number }, size: { w: number; h: number }, rot: number): Vec2 {
  const d = turn({ x: p.x - view.cx, y: p.y - view.cy }, rot);
  return { x: d.x * view.scale + size.w / 2, y: size.h / 2 - d.y * view.scale };
}

/** The plan point under a screen point (PlanView's toWorld): the inverse of planToScreen. */
export function screenToPlan(x: number, y: number, view: { cx: number; cy: number; scale: number }, size: { w: number; h: number }, rot: number): Vec2 {
  const d = turn({ x: (x - size.w / 2) / view.scale, y: (size.h / 2 - y) / view.scale }, -rot);
  return { x: d.x + view.cx, y: d.y + view.cy };
}

/**
 * Sets a plan's orientation, one undo step: an angle (degrees; 0 or null: north up) or the UCS. A level's default plan
 * (`plan:<storey>`) becomes its plan view with it; the view's id is returned.
 */
export async function setOrientation(viewId: string, value: number | typeof FOLLOW_UCS | null): Promise<string | null> {
  const args = value === FOLLOW_UCS ? { id: viewId, ucs: true } : { id: viewId, angle: value };
  const r = (await execute('view.setOrientation', args)) as { id?: string } | null;
  return r?.id ?? null;
}
