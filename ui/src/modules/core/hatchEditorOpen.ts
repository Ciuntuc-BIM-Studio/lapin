/** How Manage → Hatch patterns is opened (on a pattern, on a new one, or from a material's editor it returns to). Kept apart so callers do not load the dialog. */

import { create } from 'zustand';
import { useUi } from '../../app/store';

/** What the dialog opens on, and where it goes back to (the material editor it was opened from). */
export const useHatchEditor = create<{ select: string | null; fresh: boolean; backTo: string | null }>(() => ({ select: null, fresh: false, backTo: null }));

/** Opens Manage → Hatch patterns on a pattern (or a new one); `fromMaterial`: the material whose editor it returns to on close. */
export function openHatchPatterns(opts: { select?: string | null; fresh?: boolean; fromMaterial?: string | null } = {}): void {
  useHatchEditor.setState({ select: opts.select ?? null, fresh: !!opts.fresh, backTo: opts.fromMaterial ?? null });
  useUi.setState({ dialog: 'hatchPatterns' });
}

/** The back target and the start state are read once when the dialog opens. */
export function leaveHatchPatterns(): void {
  const back = useHatchEditor.getState().backTo;
  useHatchEditor.setState({ select: null, fresh: false, backTo: null });
  useUi.setState(back ? { materialFocus: { name: back, tab: 'graphics' }, dialog: 'materials' } : { dialog: null });
}
