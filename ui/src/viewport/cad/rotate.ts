/**
 * Rotating about a hinge, by an angle or (AutoCAD's ROTATE → Reference) by
 * three points: the hinge, a point giving the reference direction, a point
 * giving the new one; the turn is the new direction minus the reference one.
 * A typed angle stands for a direction measured from the UCS's X axis (decision 445),
 * so it turns with the UCS; a turn itself is the same in every frame.
 */

import type { Vec2 } from '../../platform/module';
import type { Shape } from './shapes';

/** The direction of a point seen from the hinge, in degrees from the plan's +X. */
export const directionOf = (c: Vec2, p: Vec2): number => (Math.atan2(p.y - c.y, p.x - c.x) * 180) / Math.PI;

/** A direction as a number (typed, degrees from the UCS's X axis) or a point (seen from the hinge): degrees from the plan's +X. */
export function directionFrom(c: Vec2, d: Vec2 | number, ucsDeg = 0): number {
  return typeof d === 'number' ? d + ucsDeg : directionOf(c, d);
}

/** A turn in degrees, taken into (-180, 180]: the short way round. */
export function normalizeTurn(deg: number): number {
  let r = deg % 360;
  if (r > 180) r -= 360;
  if (r <= -180) r += 360;
  return Math.abs(r) < 1e-9 ? 0 : r;
}

/** The turn that takes the reference direction to the new one (each a typed angle or a point), counter-clockwise positive. */
export function rotationFromReference(c: Vec2, reference: Vec2 | number, target: Vec2 | number, ucsDeg = 0): number {
  return normalizeTurn(directionFrom(c, target, ucsDeg) - directionFrom(c, reference, ucsDeg));
}

/** A point turned about the hinge by an angle (degrees). */
export function turned(p: Vec2, c: Vec2, deg: number): Vec2 {
  const a = (deg * Math.PI) / 180, co = Math.cos(a), si = Math.sin(a);
  const dx = p.x - c.x, dy = p.y - c.y;
  return { x: c.x + co * dx - si * dy, y: c.y + si * dx + co * dy };
}

/** A shape turned about the hinge (the preview of the selection while the angle is chosen). */
export function rotatedShape(s: Shape, c: Vec2, deg: number): Shape {
  const a = (deg * Math.PI) / 180;
  switch (s.type) {
    case 'line': return { ...s, a: turned(s.a, c, deg), b: turned(s.b, c, deg) };
    case 'polyline': return { ...s, pts: s.pts.map((p) => turned(p, c, deg)) };
    case 'circle': return { ...s, c: turned(s.c, c, deg) };
    case 'arc': return { ...s, c: turned(s.c, c, deg), start: s.start + a, end: s.end + a };
  }
}
