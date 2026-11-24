/**
 * The project's IDS files (buildingSMART IDS 1.0, docs/ids.md): the calls (added, read, run over the model, the results, remarks
 * and the report, all the engine's) and what the panel and Manage → Issue… work out from a run: each specification's line, the
 * failing elements to show, and whether a submission may go (a failing specification stops it unless the user writes why).
 */

import { notify, useUi } from '../../../app/store';
import { engine, execute, query, useSession } from '../../../engine/session';
import { LONG_TIMEOUT_MS } from '../../../engine/client';
import { useCamera } from '../../../viewport/camera';

export interface IdsFile { id: string; name: string; file: string; title?: string; version?: string; specifications: number; size: number; sha256?: string; addedAt?: string; addedBy?: string; xml?: string }
export interface IdsFiles { files: IdsFile[]; blocks: boolean; onSubmit: boolean }

export interface IdsFailure { globalId: string; class: string; name?: string; elementId?: string; facet: number; requirement: string; actual: string; expected: string; instructions?: string }
export type SpecStatus = 'pass' | 'fail' | 'skipped';
export interface IdsSpec {
  index: number; name: string; identifier?: string; description?: string; instructions?: string; usage: 'required' | 'optional' | 'prohibited';
  status: SpecStatus; applicable: number; passed: number; failed: number; message?: string; failures: IdsFailure[]; more: number; elements: string[]; requirements: string[];
}
export interface IdsFileRun { id: string; name: string; file: string; title: string; schema: string; objects: number; ms: number; failing: number; passing: number; skipped: number; specifications: IdsSpec[] }
export interface IdsRun { source: string; exportMs: number; ms: number; stale: boolean; failing: number; blocks: boolean; onSubmit: boolean; blocking: boolean; files: IdsFileRun[]; none?: boolean }

export const idsFiles = () => query<IdsFiles>('ids.files');

/** An .ids file the user picked, kept in the project (one undo step); the engine refuses one that is not IDS 1.0 and says why. */
export async function addIdsFile(file: File): Promise<IdsFile> {
  const text = await file.text();
  return engine.call<IdsFile>('ids.add', { text, name: file.name, author: me() });
}

/** Several IDS files' texts (a CDE server's, W6d) kept in the project in one undo step. */
export const addIdsFiles = (files: { name: string; text: string }[]) => engine.call<unknown>('ids.add', { files, author: me() });

export const removeIdsFile = (id: string) => execute('ids.remove', { id });
export const setIdsRules = (rules: { blocks?: boolean; onSubmit?: boolean }) => execute('ids.setRules', rules);

/** The project's IDS files (or those given) over the model as its IFC export writes it: it can take a while on a large model. */
export const runIds = (ids?: string[]) => engine.call<IdsRun>('ids.run', ids ? { ids } : {}, { timeoutMs: LONG_TIMEOUT_MS });
export const idsResults = () => engine.call<IdsRun>('ids.results', {});
export const moreFailures = (file: string, spec: number, offset: number, limit = 100) =>
  engine.call<IdsSpec & { total: number; offset: number }>('ids.results', { file, spec, offset, limit });

/** The remarks of the last run (one per failing specification, its elements selected), in one undo step. */
export const makeIdsRemarks = (specs?: string[]) => engine.call<{ made: number; updated: number; ids: string[] }>('ids.makeRemarks', { author: me(), ...(specs ? { specs } : {}) });

export const idsReport = (path?: string) => engine.call<{ data?: string; name?: string; path?: string }>('ids.report', path ? { path } : {});

const AUTHOR_KEY = 'lapin.remarks.author';
/** Who makes the remarks: the name the remarks dialog keeps. */
function me(): string {
  try { return localStorage.getItem(AUTHOR_KEY) || 'Lapin'; } catch { return 'Lapin'; }
}

// ------------------------------------------------------------ what a run says

/** A specification's line: its counts in words. */
export function specLine(s: Pick<IdsSpec, 'status' | 'applicable' | 'passed' | 'failed' | 'usage'>): string {
  if (s.status === 'skipped') return 'Not for this IFC version';
  if (s.usage === 'prohibited') return s.applicable === 0 ? 'None, as asked' : `${s.applicable} found, none allowed`;
  if (s.applicable === 0) return s.usage === 'required' ? 'Nothing it applies to' : 'Nothing it applies to (optional)';
  return `${s.passed} of ${s.applicable} pass`;
}

/** The run's specifications that fail, each with its file, in order. */
export function failing(run: IdsRun | null): { file: IdsFileRun; spec: IdsSpec }[] {
  return (run?.files ?? []).flatMap((file) => file.specifications.filter((spec) => spec.status === 'fail').map((spec) => ({ file, spec })));
}

/** The totals of a run: specifications passing, failing and not for this version. */
export function totals(run: IdsRun | null): { pass: number; fail: number; skipped: number } {
  const all = (run?.files ?? []).flatMap((f) => f.specifications);
  return { pass: all.filter((s) => s.status === 'pass').length, fail: all.filter((s) => s.status === 'fail').length, skipped: all.filter((s) => s.status === 'skipped').length };
}

/**
 * Whether Submit for approval may go after the IDS run (Manage → Issue…, On the CDE server): nothing to run, or the run passes, or the
 * project lets failures through; else only with "Submit despite the IDS failures" ticked and a note saying why.
 */
export function submitGate(run: IdsRun | null, state: { files: number; onSubmit: boolean; blocks: boolean; running: boolean; failed: boolean }, override: { on: boolean; note: string }):
  { blocked: boolean; why: string | null; idsOverride?: string; idsFailing?: number } {
  if (state.files === 0 || !state.onSubmit) return { blocked: false, why: null };
  if (state.running) return { blocked: true, why: 'Checking the IDS files…' };
  if (state.failed || !run) return { blocked: true, why: 'The IDS check could not run: check again.' };
  const n = totals(run).fail;
  if (n === 0) return { blocked: false, why: null };
  if (!state.blocks) return { blocked: false, why: null, idsFailing: n };
  if (override.on && override.note.trim()) return { blocked: false, why: null, idsOverride: override.note.trim(), idsFailing: n };
  return { blocked: true, why: override.on ? 'Say why it goes despite the IDS failures.' : 'IDS specifications fail: fix them, or submit despite them with a note.' };
}

/** A failure's sentence: the element, what it has and what is needed. */
export function failureText(f: IdsFailure): string {
  return `${f.name ? `${f.name} ` : ''}(${f.class}): ${f.actual} — ${f.expected}`;
}

// ------------------------------------------------------------ the elements

/** The elements of the model these ids name (a failure's element, a specification's), in order. */
export function present(ids: string[]): string[] {
  const nodes = useSession.getState().nodes;
  return ids.filter((id) => nodes.has(id));
}

/**
 * The elements selected and the 3D view brought round them: the view in front, the camera aimed at their box from where it looks
 * now, far enough to see them all. False when the model has none of them now.
 */
export async function showElements(ids: string[]): Promise<boolean> {
  const here = present(ids);
  if (here.length === 0) { notify('This element is not in the project as it is now.'); return false; }
  useUi.setState({ view: '3d', dialog: null, selected: here, selection: here[here.length - 1] });
  for (let i = 0; i < 40; i++) {
    const api = useCamera.getState().api;
    const box = api?.boxOf?.(here);
    if (api && box) {
      const target = { x: (box.min.x + box.max.x) / 2, y: (box.min.y + box.max.y) / 2, z: (box.min.z + box.max.z) / 2 };
      const size = Math.hypot(box.max.x - box.min.x, box.max.y - box.min.y, box.max.z - box.min.z);
      api.orbitTo({ target, distance: Math.max(3, size * 1.4) });
      return true;
    }
    await new Promise((r) => setTimeout(r, 50));
  }
  return true;
}
