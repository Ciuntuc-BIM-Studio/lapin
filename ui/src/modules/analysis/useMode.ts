/** The mode shape shown in the 3D view, oscillating (docs/plan-fem.md §11.5, §11.10): which mode, the shape the engine gave for it (analysis.mode), the card's scale and whether the model at rest stays drawn (see-through) or goes. Derived, never saved: a change to the model asks for it again. */

import { create } from 'zustand';
import { query } from '../../engine/session';
import { hideDiagrams, showDiagrams, useResults } from './useResults';
import type { ModeShapeDto } from './seismic';

export interface ModeState {
  /** The mode animated (1-based), null: none. */
  mode: number | null;
  /** A buckling mode of this combination is shown (S5), not a natural mode. */
  buckling: string | null;
  shape: ModeShapeDto | null;
  /** The swing's size as a multiple of the automatic one (its largest displacement a share of the model's size). */
  mult: number;
  /** The model at rest stays drawn, see-through, behind the shape; off: it is hidden. */
  undeformed: boolean;
}

export const useMode = create<ModeState>(() => ({ mode: null, buckling: null, shape: null, mult: 1, undeformed: true }));

let ticket = 0;
/** Whether the diagrams' card was up when the animation started: Stop brings it back (null: no animation). */
let resume: boolean | null = null;

/** Asks for mode n's shape and animates it; the diagrams come off the view meanwhile. The old shape stays until the new one is here (a refresh after an edit). */
export async function animateMode(n: number): Promise<void> {
  const mine = ++ticket;
  if (resume === null) resume = useResults.getState().show;
  useMode.setState({ mode: n, buckling: null });
  const shape = await query<ModeShapeDto>('analysis.mode', { mode: n });
  if (mine !== ticket) return;
  if (!shape) { stopMode(); return; }
  useMode.setState({ shape });
  hideDiagrams();
}

/** The engine's buckling mode (analysis.bucklingMode): the natural mode's shape format, with its αcr in place of the period. */
interface BucklingShapeDto extends Omit<ModeShapeDto, 'T' | 'f' | 'ratio' | 'buckling'> { combination: string; name: string; alpha: number; class: string }
export const bucklingShape = (d: BucklingShapeDto): ModeShapeDto => ({
  mode: d.mode, T: 0, f: 0, ratio: [0, 0], scale: d.scale, max: d.max, polylines: d.polylines, shells: d.shells,
  buckling: { combination: d.combination, name: d.name, alpha: d.alpha, class: d.class },
});

/** Asks for a combination's buckling mode n and animates it like a natural mode. */
export async function animateBucklingMode(combination: string, n: number): Promise<void> {
  const mine = ++ticket;
  if (resume === null) resume = useResults.getState().show;
  useMode.setState({ mode: n, buckling: combination });
  const dto = await query<BucklingShapeDto>('analysis.bucklingMode', { combination, mode: n });
  if (mine !== ticket) return;
  if (!dto) { stopMode(); return; }
  useMode.setState({ shape: bucklingShape(dto) });
  hideDiagrams();
}

/** Ends the animation and puts back the diagrams' card it took off. */
export function stopMode(): void {
  ticket++;
  useMode.setState({ mode: null, buckling: null, shape: null });
  if (resume) showDiagrams();
  resume = null;
}

/** The model changed: the mode shown is worked out again (it ends if the mode is gone). */
export function refreshMode(): void {
  const { mode: n, buckling } = useMode.getState();
  if (n === null) return;
  void (buckling ? animateBucklingMode(buckling, n) : animateMode(n));
}

export const setModeMult = (mult: number): void => useMode.setState({ mult });
export const setShowUndeformed = (undeformed: boolean): void => useMode.setState({ undeformed });
