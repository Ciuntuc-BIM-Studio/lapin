/**
 * Where a stair goes from the clicks that place it. A stair is stored by its
 * foot (the centre of its first riser) and the way it climbs; entrance steps
 * are placed the other way round, by their top (the middle of the porch's
 * edge, which a snap finds) and the way down, and the foot is worked out
 * from the run the engine gave.
 */

import type { Vec2 } from '../../platform/module';

/** A level as drawings write it: ±0.00, +0.45, −0.45 (metres, two decimals). */
export const levelText = (v: number): string => (Math.abs(v) < 0.005 ? '±0.00' : `${v > 0 ? '+' : '−'}${Math.abs(v).toFixed(2)}`);

/** The unit vector from a to c; +X when they coincide. */
export function dirOf(a: Vec2, c: Vec2): Vec2 {
  const d = Math.hypot(c.x - a.x, c.y - a.y);
  return d < 1e-9 ? { x: 1, y: 0 } : { x: (c.x - a.x) / d, y: (c.y - a.y) / d };
}

/** Entrance steps placed by their top: the foot one run down from it, and the way up (the opposite of the way down). */
export function footFromTop(top: Vec2, toward: Vec2, run: number): { foot: Vec2; up: Vec2 } {
  const down = dirOf(top, toward);
  return { foot: { x: top.x + down.x * run, y: top.y + down.y * run }, up: { x: -down.x, y: -down.y } };
}
