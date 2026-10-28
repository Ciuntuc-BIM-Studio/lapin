/** What the MEP tools work with: the current system, how new runs are drawn, the run end a Connect is holding. */

import { create } from 'zustand';

export type Plane = 'floor' | 'ceiling' | 'wall';

export interface MepPrefs {
  /** The system new nodes and runs go in (null: the first that fits, made if none). */
  systemId: string | null;
  /** New runs: paired or single (null: the system's default). */
  pair: boolean | null;
  /** New runs: the plane they run in (null: the system's default). */
  plane: Plane | null;
  /** Connect: the run end picked first, waiting for what it joins. */
  pending: { runId: string; end: 'start' | 'end' } | null;
}

export const useMep = create<MepPrefs>(() => ({ systemId: null, pair: null, plane: null, pending: null }));

export const setMep = (patch: Partial<MepPrefs>): void => useMep.setState(patch);
