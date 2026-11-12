/** The analysis as the interface holds it: the last run, what is shown of it (the case, the quantity, the scale) and the answers the engine gave for that. Results are derived, never saved: a change to the model makes them stale. */

import { create } from 'zustand';
import { engine, useSession } from '../../engine/session';
import { useUi } from '../../app/store';
import { pickArgs, pickFrom, validPick, type DiagramDto, type Pick, type Quantity, type ResultsDto, type RunDto } from './results';

export interface ResultsState {
  run: RunDto | null;
  /** The engine's refusal of the run (a bar with no material, say), exactly as it said it. */
  error: string | null;
  running: boolean;
  /** The model the run was made on; another one means the results are stale. */
  at: unknown;
  stale: boolean;
  /** The diagrams are drawn in the 3D view. */
  show: boolean;
  pick: Pick | null;
  quantity: Quantity;
  /** The diagram's scale as a multiple of the engine's default. */
  mult: number;
  reactions: boolean;
  results: ResultsDto | null;
  diagram: DiagramDto | null;
}

export const useResults = create<ResultsState>(() => ({
  run: null, error: null, running: false, at: null, stale: false, show: false, pick: null, quantity: 'Mz', mult: 1, reactions: true, results: null, diagram: null,
}));

const set = useResults.setState;
let ticket = 0;
let baseScale: { key: string; scale: number } | null = null;

const ask = <T,>(name: string, args: Record<string, unknown>) => engine.call<T>('doc.query', { name, args });

/** The results and the diagram of what is picked, asked again (the newest answer only counts). */
export async function refresh(): Promise<void> {
  const s = useResults.getState();
  if (!s.run || !s.run.ok || !s.pick) { set({ results: null, diagram: null }); return; }
  const mine = ++ticket;
  const key = `${s.pick.kind}:${s.pick.key}:${s.quantity}`;
  const scale = baseScale?.key === key && s.mult !== 1 ? baseScale.scale * s.mult : undefined;
  try {
    const [results, diagram] = await Promise.all([
      ask<ResultsDto>('analysis.results', pickArgs(s.pick)),
      ask<DiagramDto>('analysis.diagram', { ...pickArgs(s.pick), quantity: s.quantity, ...(scale ? { scale } : {}) }),
    ]);
    if (mine !== ticket || useResults.getState().stale) return;
    if (scale === undefined) baseScale = { key, scale: diagram.scale };
    set({ results, diagram, error: null });
  } catch (e) {
    if (mine === ticket) set({ results: null, diagram: null, error: e instanceof Error ? e.message : String(e) });
  }
}

/** Runs the analysis on the model as it is; `open` shows the Results dialog. */
export async function runAnalysis(open = true): Promise<void> {
  if (useResults.getState().running) return;
  set({ running: true, error: null });
  const at = useSession.getState().nodes;
  try {
    const run = await ask<RunDto>('analysis.run', {});
    const pick = validPick(run, useResults.getState().pick);
    baseScale = null;
    set({ run, running: false, at, stale: false, pick, mult: 1, show: run.ok, results: null, diagram: null });
    if (open) useUi.setState({ dialog: 'analysisResults' });
    await refresh();
  } catch (e) {
    set({ run: null, running: false, error: e instanceof Error ? e.message : String(e), results: null, diagram: null, show: false });
    if (open) useUi.setState({ dialog: 'analysisResults' });
  }
}

export const setPick = (p: Pick | string | null): void => {
  const pick = typeof p === 'string' ? pickFrom(p) : p;
  if (!pick) return;
  baseScale = null;
  set({ pick, mult: 1 });
  void refresh();
};
export const setQuantity = (quantity: Quantity): void => { baseScale = null; set({ quantity, mult: 1 }); void refresh(); };
let slide: ReturnType<typeof setTimeout> | undefined;
export const setMult = (mult: number): void => {
  set({ mult });
  clearTimeout(slide);
  slide = setTimeout(() => void refresh(), 120);
};
export const setReactions = (reactions: boolean): void => set({ reactions });
/** Takes the diagrams off the view (the run is kept). */
export const hideDiagrams = (): void => set({ show: false });
export const showDiagrams = (): void => set({ show: true });
export const clearResults = (): void => { ticket++; baseScale = null; set({ run: null, results: null, diagram: null, show: false, stale: false, error: null, at: null }); };

/** A change to the model makes what was worked out stale: the drawing goes, the card offers Run again. */
export function markStale(nodes: unknown): void {
  const s = useResults.getState();
  if (!s.run || s.stale || s.at === nodes) return;
  ticket++;
  set({ stale: true, results: null, diagram: null });
}
