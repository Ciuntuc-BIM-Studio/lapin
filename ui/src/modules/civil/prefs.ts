/** The terrain brush as the user set it: what it does, how wide, how much; kept for the session. */

import { create } from 'zustand';
import type { NodeDto } from '../../engine/protocol';
import type { PointGripMode } from './pointGrips';

export type BrushKind = 'raise' | 'lower' | 'flatten' | 'smooth';

export interface Brush { kind: BrushKind; radius: number; amount: number; level: number | null; strength: number }

export const useBrush = create<Brush>(() => ({ kind: 'raise', radius: 3, amount: 0.5, level: null, strength: 0.5 }));

export const setBrush = (patch: Partial<Brush>): void => useBrush.setState(patch);

/** An earthwork that is a ditch (a trench along a path), not a platform. */
export const isDitch = (n: NodeDto) => n.type === 'IfcEarthworksCut' && n.props.predefinedType === 'TRENCH';

/** Whether 3D vertical point grips are shown for the selected terrain, and on which points (every one, the control cage's, or every one to choose the controls). */
export const useTerrainEdit = create<{ editPoints: boolean; setEditPoints: (v: boolean) => void; gripMode: PointGripMode; setGripMode: (m: PointGripMode) => void }>((set) => ({
  editPoints: false,
  setEditPoints: (editPoints) => set({ editPoints }),
  gripMode: 'all',
  setGripMode: (gripMode) => set({ gripMode }),
}));
