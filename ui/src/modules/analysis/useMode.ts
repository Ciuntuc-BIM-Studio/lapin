/** The mode shape shown in the 3D view, oscillating (docs/plan-fem.md §11.5): which mode, and the shape the engine gave for it (analysis.mode). Derived, never saved: a change to the model asks for it again. */

import { create } from 'zustand';
import { query } from '../../engine/session';
import { hideDiagrams } from './useResults';
import type { ModeShapeDto } from './seismic';

export interface ModeState {
  /** The mode animated (1-based), null: none. */
  mode: number | null;
  shape: ModeShapeDto | null;
}

export const useMode = create<ModeState>(() => ({ mode: null, shape: null }));

let ticket = 0;

/** Asks for mode n's shape and animates it; the diagrams come off the view meanwhile. The old shape stays until the new one is here (a refresh after an edit). */
export async function animateMode(n: number): Promise<void> {
  const mine = ++ticket;
  useMode.setState({ mode: n });
  const shape = await query<ModeShapeDto>('analysis.mode', { mode: n });
  if (mine !== ticket) return;
  if (!shape) { useMode.setState({ mode: null, shape: null }); return; }
  useMode.setState({ shape });
  hideDiagrams();
}

export function stopMode(): void {
  ticket++;
  useMode.setState({ mode: null, shape: null });
}

/** The model changed: the mode shown is worked out again (it ends if the mode is gone). */
export function refreshMode(): void {
  const n = useMode.getState().mode;
  if (n !== null) void animateMode(n);
}
