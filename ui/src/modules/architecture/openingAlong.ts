/**
 * Where an opening sits along its wall as the Properties panel shows it (a tester's report, forum topic 47): a distance from a
 * reference to a point of the opening. The reference is the wall's start, the nearest face of a wall meeting the host, or that
 * wall's core face; the point is the opening's start, centre or end (towards the wall's end). Derived from
 * architecture.openingReferences on every ask, never stored; written as the `along` architecture.moveOpening takes (the
 * opening's centre, metres from the wall's start).
 */

import { chooseSide, faceScale, targetsOn, type OpeningRefs, type Way } from './openingDims';

export type AlongFrom = 'start' | 'face' | 'core';
export type AlongPoint = 'start' | 'centre' | 'end';
export const ALONG_FROMS: readonly AlongFrom[] = ['start', 'face', 'core'];
export const ALONG_POINTS: readonly AlongPoint[] = ['start', 'centre', 'end'];

/** The point's offset from the opening's centre along the wall, in widths. */
const OFFSET: Record<AlongPoint, number> = { start: -0.5, centre: 0, end: 0.5 };

/** A reference: where it lies along the wall's axis, and which way the opening is from it (+1: towards the wall's end). */
export interface AlongRef { s: number; away: 1 | -1; name: string | null }

/**
 * The reference for `from` of an opening at `along` (centre), `width` wide, measuring to `point`. The wall's start is 0; a wall's face
 * or core face is the nearest one to the point on the side of the host that more walls meet (as the temporary dimensions choose it),
 * falling back to the wall's start where no wall meets the host or the references are not known yet.
 */
export function referenceFor(refs: OpeningRefs | null, from: AlongFrom, along: number, width: number, point: AlongPoint): AlongRef {
  const start: AlongRef = { s: 0, away: 1, name: null };
  if (from === 'start' || !refs?.walls?.length) return start;
  const side = chooseSide(refs);
  const p = along + OFFSET[point] * width;
  let best: (AlongRef & { d: number }) | null = null;
  for (const way of [-1, 1] as Way[]) {
    for (const t of targetsOn(refs, side, way, along + (way * width) / 2)) {
      if (t.kind !== 'wall') continue;
      const opt = from === 'core' ? t.options.find((o) => o.what === 'nearCore') ?? t.options[0] : t.options[0];
      const d = Math.abs(opt.s - p);
      if (!best || d < best.d) best = { s: opt.s, away: way < 0 ? 1 : -1, name: t.name, d };
    }
  }
  return best ? { s: best.s, away: best.away, name: best.name } : start;
}

/** The distance shown: from the reference to the opening's point, positive away from the reference (a wall beyond the opening reads the same way). */
export function alongShown(ref: AlongRef, along: number, width: number, point: AlongPoint, scale = 1): number {
  return ref.away * (along + OFFSET[point] * width - ref.s) * scale;
}

/** The `along` (the opening's centre) that makes the distance read `value`, the reference staying put. */
export function alongWritten(ref: AlongRef, value: number, width: number, point: AlongPoint, scale = 1): number {
  return ref.s + (ref.away * value) / (scale || 1) - OFFSET[point] * width;
}

/** The scale of the face the references are measured on (1 on a straight wall). */
export const alongScale = (refs: OpeningRefs | null): number => (refs ? faceScale(refs, chooseSide(refs)) : 1);
