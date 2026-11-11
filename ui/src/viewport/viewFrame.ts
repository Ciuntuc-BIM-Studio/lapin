/**
 * Where a section's or an elevation's points lie in the model (decision 453): a drawing point (x, y) is
 * origin + right·x + up·y. element.transform takes this frame with the view's points, so Move, Copy and Mirror there
 * carry the model's elements along the view and up (their offset from the level), and drafted shapes in the view.
 */

import { create } from 'zustand';
import type { Vec2 } from '../platform/module';
import type { DrawingDto } from './drawing';

export interface ViewFrame { origin: [number, number, number]; right: [number, number, number]; up: [number, number, number] }

const v3 = (a: number[] | undefined): [number, number, number] => [a?.[0] ?? 0, a?.[1] ?? 0, a?.[2] ?? 0];

/**
 * The frame the view's point `at` lies in; a developed section's piece whose stretch of x holds it (its origin moved
 * back by the stretch's start, so the piece's x reads as the drawing's). Null in a plan, or with no frame.
 */
export function viewFrameAt(drawing: Pick<DrawingDto, 'kind' | 'frame' | 'frames'> | null | undefined, at?: Vec2 | null): ViewFrame | null {
  if (!drawing || drawing.kind === 'plan') return null;
  const piece = at && drawing.frames?.length ? drawing.frames.find((f) => at.x >= f.x0 - 1e-9 && at.x <= f.x1 + 1e-9) ?? null : null;
  if (piece) {
    const [o, r] = [v3(piece.origin), v3(piece.right)];
    return { origin: [o[0] - r[0] * piece.x0, o[1] - r[1] * piece.x0, o[2] - r[2] * piece.x0], right: r, up: v3(piece.up) };
  }
  const f = drawing.frame;
  return f ? { origin: v3(f.origin), right: v3(f.right), up: v3(f.up) } : null;
}

/** A move in the view (dx along it, dy up) as the model has it: [x, y, z]. */
export const inModel = (f: ViewFrame, dx: number, dy: number): [number, number, number] =>
  [0, 1, 2].map((k) => f.right[k] * dx + f.up[k] * dy) as [number, number, number];

/**
 * The selection dragged up or down in a section or an elevation (decision 454): what it rises by so far, which the
 * Properties panel adds to an element's offset from its level while the drag lasts; nothing when none is dragged.
 */
export const useLiveRise = create<{ ids: readonly string[]; dz: number }>(() => ({ ids: [], dz: 0 }));
export const setLiveRise = (ids: readonly string[], dz: number) => useLiveRise.setState({ ids, dz });
export const clearLiveRise = () => { if (useLiveRise.getState().ids.length) useLiveRise.setState({ ids: [], dz: 0 }); };
