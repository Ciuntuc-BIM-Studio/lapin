/**
 * The part's own sketch being edited in place (decision 520): which one, the tool in hand, the shape selected, and the camera to
 * go back to. Kept apart from the overlay so the edit mode's Tab and double click can ask about it without importing it.
 */

import { create } from 'zustand';
import type { CameraPose } from '../../viewport/camera';
import type { SketchTool } from './partSketchModel';

export interface PartSketchState {
  partId: string | null;
  sketchId: string | null;
  tool: SketchTool | null;
  /** The shape selected (Delete removes it). */
  selected: string | null;
  /** The camera as it was before the sketch turned it to the plane; put back on leaving. */
  saved: (CameraPose & { ortho: boolean }) | null;
}

export const usePartSketch = create<PartSketchState>(() => ({ partId: null, sketchId: null, tool: null, selected: null, saved: null }));

/** The sketch being edited: its part's and its own id, or null. */
export function activePartSketch(): { partId: string; sketchId: string } | null {
  const s = usePartSketch.getState();
  return s.partId && s.sketchId ? { partId: s.partId, sketchId: s.sketchId } : null;
}

/** What the sketch editing lends the edit mode (set by PartSketchEdit when it loads): leaving, and opening the part's own sketch for a double click. */
export const sketchHooks: { leave: () => void; openFor: (partId: string) => Promise<boolean> } = { leave: () => undefined, openFor: async () => false };
