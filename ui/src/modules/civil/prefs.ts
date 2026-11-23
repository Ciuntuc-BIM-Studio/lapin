/** The terrain brush as the user set it: what it does, how wide, how much; kept for the session. */

import { create } from 'zustand';
import type { NodeDto } from '../../engine/protocol';
import { useUi } from '../../app/store';
import type { PointGripMode } from './pointGrips';

export type BrushKind = 'raise' | 'lower' | 'flatten' | 'smooth';

export interface Brush { kind: BrushKind; radius: number; amount: number; level: number | null; strength: number }

export const useBrush = create<Brush>(() => ({ kind: 'raise', radius: 3, amount: 0.5, level: null, strength: 0.5 }));

export const setBrush = (patch: Partial<Brush>): void => useBrush.setState(patch);

/** An earthwork that is a ditch (a trench along a path), not a platform. */
export const isDitch = (n: NodeDto) => n.type === 'IfcEarthworksCut' && n.props.predefinedType === 'TRENCH';

/** Whether 3D vertical point grips are shown for the selected terrain, and on which points (every one, the control cage's, or every one to choose the controls). */
export const useTerrainEdit = create<{
  editPoints: boolean; setEditPoints: (v: boolean) => void; gripMode: PointGripMode; setGripMode: (m: PointGripMode) => void;
  /** The points picked to type their elevation (T1, docs/plan-site-grading.md): one terrain's, by index; none: null. */
  picked: { id: string; indices: number[] } | null;
  /** Grips on every vertex along the outline's arcs too (a terrain whose outline is its points); off: on its corners (and the vertices given a height of their own). */
  allOutline: boolean;
}>((set) => ({
  allOutline: false,
  editPoints: false,
  setEditPoints: (editPoints) => set({ editPoints }),
  gripMode: 'all',
  setGripMode: (gripMode) => set({ gripMode }),
  picked: null,
}));

/** The points of terrain `id` picked now (an empty list when another terrain's are, or none). */
export const pickedOf = (id: string): number[] => { const p = useTerrainEdit.getState().picked; return p?.id === id ? p.indices : []; };

/** A point picked, or unpicked when it was (another terrain's picked points are let go); the grips are drawn again. */
export function togglePicked(id: string, index: number): void {
  const now = pickedOf(id);
  const indices = now.includes(index) ? now.filter((i) => i !== index) : [...now, index].sort((a, b) => a - b);
  setPicked(id, indices);
}

/** The points picked set at once (none: let go); the grips are drawn again. */
export function setPicked(id: string, indices: number[]): void {
  useTerrainEdit.setState({ picked: indices.length ? { id, indices } : null });
  redrawGrips();
}

/** The views draw their grips when the selection changes: the same selection again draws them now. */
export const redrawGrips = (): void => useUi.setState({ selected: [...useUi.getState().selected] });
