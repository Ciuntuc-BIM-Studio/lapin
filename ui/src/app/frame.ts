/** A frame in the plan (a UCS): its origin and its X axis's angle, and the moves in and out of it. Pure: no store. */

import type { Vec2 } from '../platform/module';

/** A frame in the plan: its origin and its X axis's angle (degrees). */
export interface Frame { origin: Vec2; angle: number }

export const isIdentity = (f: Frame | null): boolean => !f || (Math.abs(f.origin.x) < 1e-12 && Math.abs(f.origin.y) < 1e-12 && Math.abs(f.angle) < 1e-9);

/** A plan point in the frame's own coordinates. */
export function toFrame(f: Frame, p: Vec2): Vec2 {
  const a = (f.angle * Math.PI) / 180, c = Math.cos(a), s = Math.sin(a);
  const dx = p.x - f.origin.x, dy = p.y - f.origin.y;
  return { x: c * dx + s * dy, y: -s * dx + c * dy };
}

/** A point given in the frame's coordinates, in the plan. */
export function fromFrame(f: Frame, q: Vec2): Vec2 {
  const a = (f.angle * Math.PI) / 180, c = Math.cos(a), s = Math.sin(a);
  return { x: f.origin.x + c * q.x - s * q.y, y: f.origin.y + s * q.x + c * q.y };
}

/** A direction given in the frame, in the plan (no origin). */
export function turnFromFrame(f: Frame, d: Vec2): Vec2 {
  const a = (f.angle * Math.PI) / 180, c = Math.cos(a), s = Math.sin(a);
  return { x: c * d.x - s * d.y, y: s * d.x + c * d.y };
}

