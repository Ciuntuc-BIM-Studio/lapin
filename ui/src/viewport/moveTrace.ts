/**
 * The move trace (decision 165): while something is moved or copied from a
 * base point (MOVE, COPY, a dragged grip), the base point keeps a marker, a
 * dashed line runs from it to the moved point, and a faded ghost of what is
 * moved stays where it was until the move is taken or cancelled. The line
 * and the marker take the aim's colour: snapped to a point, held by ortho or
 * polar, on a tracking guide, or free. The readout (Δx, Δy, distance, angle)
 * is the live input's, in the project's units.
 */

import { create } from 'zustand';
import type { Vec2 } from '../platform/module';
import type { Shape } from './cad/shapes';
import { formatAngle, formatLength, liveBegin, liveEnd, livePoint, useLive } from './liveInput';

export interface Trace {
  /** Who started it ("cad", "grip"); one at a time. */
  owner: string;
  /** The original position. */
  base: Vec2;
  /** What is moved, where it was. */
  ghost: Shape[];
  /** Also draw the ghost carried to the moved point (a grip drag has no preview of its own). */
  carry: boolean;
}

export const useTrace = create<{ trace: Trace | null }>(() => ({ trace: null }));

/** A move starts from `base`: the marker, the line and the ghost show until traceEnd. */
export function traceBegin(owner: string, base: Vec2, ghost: Shape[], carry = false): void {
  const t = useTrace.getState().trace;
  // The same move asked again (a COPY's next copy): kept as it is.
  if (t && t.owner === owner && Math.hypot(t.base.x - base.x, t.base.y - base.y) < 1e-12 && t.carry === carry && t.ghost.length === ghost.length) return;
  useTrace.setState({ trace: { owner, base, ghost, carry } });
}

/** The move is taken or cancelled (only the owner's own, when named). */
export function traceEnd(owner?: string): void {
  const t = useTrace.getState().trace;
  if (!t || (owner && t.owner !== owner)) return;
  useTrace.setState({ trace: null });
}

/** The offset from the base to the moved point: Δx, Δy, the distance (m) and the angle (degrees from +X, counter-clockwise, 0–360, as the live input measures). */
export function traceOffset(base: Vec2, to: Vec2): { dx: number; dy: number; dist: number; ang: number } {
  const dx = to.x - base.x, dy = to.y - base.y;
  const dist = Math.hypot(dx, dy);
  const ang = dist < 1e-12 ? 0 : ((Math.atan2(dy, dx) * 180) / Math.PI + 360) % 360;
  return { dx, dy, dist, ang: ang >= 359.95 ? 0 : ang };
}

/** The offset as the readout shows it, in the project's units (formatLength) and degrees. */
export function traceReadout(base: Vec2, to: Vec2): { dx: string; dy: string; dist: string; ang: string } {
  const o = traceOffset(base, to);
  return { dx: formatLength(o.dx), dy: formatLength(o.dy), dist: formatLength(o.dist), ang: formatAngle(o.ang) };
}

/** How the moved point is held. */
export type TraceState = 'snap' | 'ortho' | 'polar' | 'tracking' | 'free';

/**
 * The aim's state, from what the aim says held the point (Aimed.lock): an
 * object snap; the ortho line, a polar angle or a tracking guide actually
 * holding it; or free (the grid counts as free). A point that only happens
 * to be square to the base is free: blue means the constraint holds.
 */
export function traceState(aimed: { kind: 'point' | 'grid' | 'free'; mode?: string; lock?: 'ortho' | 'polar' | 'tracking' } | null): TraceState {
  if (!aimed) return 'free';
  if (aimed.mode) return 'snap';
  if (aimed.lock) return aimed.lock;
  return 'free';
}

/** The class the line and the marker take for a state (colours in theme.css: ortho and polar share the locked colour). */
export const traceClass = (s: TraceState): string => `trace-${s === 'ortho' || s === 'polar' ? 'locked' : s}`;

/**
 * Dragging a selection by its body (a plan): from the press point, the same
 * trace as a move grip (the ghost left and carried, the marker, the line,
 * the readout with Δx and Δy); a typed length or angle, then Enter, puts it
 * there (`commit`). Owned by "drag".
 */
export function carryBegin(base: Vec2, ghost: Shape[], commit: (p: Vec2) => void): void {
  traceBegin('drag', base, ghost, true);
  liveBegin('drag', base, commit, { delta: true });
}

/** The drag is let go, cancelled (Esc) or its view goes. */
export function carryEnd(): void {
  traceEnd('drag');
  liveEnd('drag');
}

/** True while a selection is dragged by its body. */
export const carrying = (): boolean => useTrace.getState().trace?.owner === 'drag';

/** Where a body drag lets go: the cursor's point, or the typed length and angle from the base; the offset, or null when nothing moved. */
export function carryBy(base: Vec2, cursor: Vec2): [number, number] | null {
  const l = useLive.getState();
  const to = (l.owner === 'drag' ? livePoint({ ...l, base, cursor }) : null) ?? cursor;
  const dx = to.x - base.x, dy = to.y - base.y;
  return Math.hypot(dx, dy) > 1e-9 ? [dx, dy] : null;
}
