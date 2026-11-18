/**
 * A wall's reference line (forum topic 159, GitHub #63, decision 685): the line a wall keeps besides its axis (which stays on its
 * core's centre), a face of it or an offset from the core's centre. Where it lies across the wall is the engine's rule
 * (WallReference.LeftOf), told with the wall as `referenceAxis` {start, end, arc?, left}; here only the geometry that follows from
 * it: the line as an Arc, and an axis from a reference line moved (a grip dragged). Pure.
 */

import type { Vec2 } from '../platform/module';
import { arcLength, isArc, pointAt, riseThrough, tangentAt, type Arc } from './arc';

/** The kinds a wall's reference line may be (the engine's names). */
export type ReferenceKind = 'coreCentre' | 'coreExterior' | 'coreInterior' | 'exteriorFace' | 'interiorFace' | 'layer' | 'offset';

/** A reference line as kept on a wall: {kind, offset? (metres left of the core's centre), layer? (a face, 0 the exterior)}. */
export interface ReferenceLine { kind: ReferenceKind; offset?: number; layer?: number }

const point = (v: unknown): Vec2 | null => Array.isArray(v) && typeof v[0] === 'number' && typeof v[1] === 'number' ? { x: v[0], y: v[1] } : null;

/** The reference line the wall keeps (its props' `referenceLine`); null: none, the core's centre. */
export function keptReference(props: Record<string, unknown>): ReferenceLine | null {
  const r = props.referenceLine as Partial<ReferenceLine> | undefined;
  if (!r || typeof r.kind !== 'string' || r.kind === 'coreCentre') return null;
  return { kind: r.kind, ...(typeof r.offset === 'number' ? { offset: r.offset } : {}), ...(typeof r.layer === 'number' ? { layer: r.layer } : {}) };
}

/** The wall's reference line as the engine worked it out, and how far left of the axis it lies; null when it keeps none. */
export function referenceArc(props: Record<string, unknown>): { arc: Arc; left: number } | null {
  const r = props.referenceAxis as { start?: unknown; end?: unknown; arc?: unknown; left?: unknown } | undefined;
  const a = point(r?.start), b = point(r?.end);
  if (!r || !a || !b) return null;
  return { arc: { a, b, rise: typeof r.arc === 'number' ? r.arc : 0 }, left: typeof r.left === 'number' ? r.left : 0 };
}

/** A path set off `y` to its left: a parallel line, or a concentric arc (the engine's WallReference.Offset). */
export function offsetArc(c: Arc, y: number): Arc {
  if (Math.abs(y) < 1e-12) return c;
  const at = (s: number) => { const p = pointAt(c, s), t = tangentAt(c, s); return { x: p.x - t.y * y, y: p.y + t.x * y }; };
  if (!isArc(c)) return { a: at(0), b: at(arcLength(c)), rise: 0 };
  const l = arcLength(c);
  const a = at(0), b = at(l);
  return { a, b, rise: riseThrough(a, b, at(l / 2)) };
}

/** The axis of a wall whose reference line, `left` of its axis, is `line`. */
export const axisFromReference = (line: Arc, left: number): Arc => offsetArc(line, -left);
