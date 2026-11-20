/** The mode shape shown in the 3D view, oscillating (docs/plan-fem.md §11.5, §11.10): which mode, the shape the engine gave for it (analysis.mode), the card's scale and whether the model at rest stays drawn (see-through) or goes. Derived, never saved: a change to the model asks for it again. */

import { create } from 'zustand';
import { query } from '../../engine/session';
import { hideDiagrams, showDiagrams, useResults } from './useResults';
import type { ModeShapeDto } from './seismic';

export interface ModeState {
  /** The mode animated (1-based), null: none. */
  mode: number | null;
  shape: ModeShapeDto | null;
  /** The swing's size as a multiple of the automatic one (its largest displacement a share of the model's size). */
  mult: number;
  /** The model at rest stays drawn, see-through, behind the shape; off: it is hidden. */
  undeformed: boolean;
}

export const useMode = create<ModeState>(() => ({ mode: null, shape: null, mult: 1, undeformed: true }));

let ticket = 0;
/** Whether the diagrams' card was up when the animation started: Stop brings it back (null: no animation). */
let resume: boolean | null = null;

/** Asks for mode n's shape and animates it; the diagrams come off the view meanwhile. The old shape stays until the new one is here (a refresh after an edit). */
export async function animateMode(n: number): Promise<void> {
  const mine = ++ticket;
  if (resume === null) resume = useResults.getState().show;
  useMode.setState({ mode: n });
  const shape = await query<ModeShapeDto>('analysis.mode', { mode: n });
  if (mine !== ticket) return;
  if (!shape) { stopMode(); return; }
  useMode.setState({ shape });
  hideDiagrams();
}

/** Ends the animation and puts back the diagrams' card it took off. */
export function stopMode(): void {
  ticket++;
  useMode.setState({ mode: null, shape: null });
  if (resume) showDiagrams();
  resume = null;
}

/** The model changed: the mode shown is worked out again (it ends if the mode is gone). */
export function refreshMode(): void {
  const n = useMode.getState().mode;
  if (n !== null) void animateMode(n);
}

export const setModeMult = (mult: number): void => useMode.setState({ mult });
export const setShowUndeformed = (undeformed: boolean): void => useMode.setState({ undeformed });
