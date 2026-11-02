/**
 * The hatch swatches' previews, asked of the engine once per pattern and
 * scale: forgotten when patterns are imported (one may be redefined under its
 * name) and when another project opens (its patterns are its own); the
 * swatches on screen then ask again (their key holds the epoch).
 */

import { create } from 'zustand';
import { useSession } from '../../engine/session';

export type HatchPreview = { solid: boolean; segments: number[] };

export const hatchPreviews = new Map<string, HatchPreview>();

/** Bumped each time the previews are forgotten. */
export const useHatchEpoch = create<{ epoch: number }>(() => ({ epoch: 0 }));

/** Forget every preview (after `hatch.import`). */
export function forgetHatchPreviews(): void {
  hatchPreviews.clear();
  useHatchEpoch.setState((s) => ({ epoch: s.epoch + 1 }));
}

// Another project (a file opened, a new project: its document changes, its version starts again).
useSession.subscribe((s, prev) => {
  if (s.doc?.path !== prev.doc?.path || s.version < prev.version) forgetHatchPreviews();
});
