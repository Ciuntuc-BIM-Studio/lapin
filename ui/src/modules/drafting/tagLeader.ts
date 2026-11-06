/**
 * A tag's elbow leader (decision 331), as ArchiCAD's labels: an oblique segment from the element to a knee, then a
 * shoulder under the text. The engine draws it; here is what its grips need: the knee kept to 30°, 45°, 60° or 90°
 * from the leader's end (Shift frees it), and the shape a tag type gives its tags.
 */

import type { Vec2 } from '../../platform/module';

export type LeaderShape = 'straight' | 'elbow';

/** The angles a knee keeps from its leader's end, every way round (degrees from +x). */
export const KNEE_ANGLES = [30, 45, 60, 90].flatMap((a) => [a, 180 - a, 180 + a, 360 - a]).filter((a, i, all) => all.indexOf(a) === i);

/**
 * A knee dragged to `to`: on the nearest line from `end` at one of {@link KNEE_ANGLES}, as far along it as `to` is;
 * `free` (Shift held): where it is dragged.
 */
export function snapKnee(end: Vec2, to: Vec2, free: boolean): Vec2 {
  const dx = to.x - end.x, dy = to.y - end.y;
  if (free || Math.hypot(dx, dy) < 1e-9) return to;
  let best = to, gap = Infinity;
  for (const deg of KNEE_ANGLES) {
    const r = (deg * Math.PI) / 180;
    const ux = Math.cos(r), uy = Math.sin(r);
    const t = dx * ux + dy * uy;
    if (t <= 0) continue;
    const p = { x: end.x + ux * t, y: end.y + uy * t };
    const d = Math.hypot(to.x - p.x, to.y - p.y);
    if (d < gap) { gap = d; best = p; }
  }
  return best;
}

/** The shape a tag type gives its leaders: an elbow unless it says straight; a tag of no type is an elbow too. */
export function leaderShapeOf(type?: { leaderShape?: unknown } | null): LeaderShape {
  return type?.leaderShape === 'straight' ? 'straight' : 'elbow';
}

/** A tag's own shape over its type's (none: its type's), and straight for one bent by hand at a free elbow. */
export function tagLeaderShape(props: { leaderShape?: unknown; elbow?: unknown }, type?: { leaderShape?: unknown } | null): LeaderShape {
  if (props.leaderShape === 'straight' || props.leaderShape === 'elbow') return props.leaderShape;
  if (Array.isArray(props.elbow)) return 'straight';
  return leaderShapeOf(type);
}
