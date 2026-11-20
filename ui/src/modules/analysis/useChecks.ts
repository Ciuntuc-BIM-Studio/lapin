/** The design checks as the interface holds them (docs/plan-fem.md §12.7): what analysis.checks and analysis.walls said for the last run, and whether the model is coloured by utilisation. Derived, never saved: a new run asks again. */

import { create } from 'zustand';
import { query } from '../../engine/session';
import { useResults } from './useResults';
import type { RunDto } from './results';
import type { ContoursDto } from './shells';
import type { ChecksDto, WallsDto } from './checks';

export interface ChecksState {
  /** The model is coloured by utilisation (3D and plans). */
  colour: boolean;
  /** The run these answers are for. */
  forRun: RunDto | null;
  loading: boolean;
  checks: ChecksDto | null;
  walls: WallsDto | null;
  /** The slabs' and walls' meshes (from analysis.contours), to colour them. */
  surfaces: ContoursDto | null;
  /** The engine's refusal (the analysis did not run), as it said it. */
  error: string | null;
}

export const useChecks = create<ChecksState>(() => ({ colour: false, forRun: null, loading: false, checks: null, walls: null, surfaces: null, error: null }));

let ticket = 0;

/** Asks for the checks of the run now held, once for each run (the newest answer only counts). */
export async function ensureChecks(): Promise<void> {
  const { run, stale } = useResults.getState();
  const s = useChecks.getState();
  if (!run || !run.ok || stale) return;
  if (s.forRun === run) return;
  const mine = ++ticket;
  useChecks.setState({ forRun: run, loading: true, error: null });
  const [checks, walls, surfaces] = await Promise.all([
    query<ChecksDto>('analysis.checks'),
    query<WallsDto>('analysis.walls'),
    run.shells ? query<ContoursDto>('analysis.contours', { envelope: 'uls', quantity: 'w' }) : Promise.resolve(null),
  ]);
  if (mine !== ticket) return;
  useChecks.setState({ loading: false, checks, walls, surfaces, error: checks ? null : 'The checks could not be made: run the analysis first.' });
}

export const setColour = (colour: boolean): void => { useChecks.setState({ colour }); if (colour) void ensureChecks(); };

/** A new run, or none: the answers are for the old one. */
export function forgetChecks(): void { ticket++; useChecks.setState({ forRun: null, loading: false, checks: null, walls: null, surfaces: null, error: null }); }
