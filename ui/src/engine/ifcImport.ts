/**
 * An IFC import in two steps: the file looked at first (project.ifcSurvey: its
 * storeys and its elements by kind and discipline), for the user to choose what
 * to bring in the Import IFC dialog; then the import of what was chosen, its
 * stages shown as it goes (the engine's progress events). The engine keeps the
 * file it surveyed, so the import does not read it again.
 */

import { create } from 'zustand';
import { engine } from './session';
import { LONG_TIMEOUT_MS } from './client';
import { notify, useUi } from '../app/store';

export interface SurveyKind { key: string; class: string; category?: string | null; label: string; count: number }
export interface SurveyGroup { discipline: string; label: string; count: number; kinds: SurveyKind[] }
export interface SurveyStorey { id: string; name: string; elevation: number; count: number }
export interface Survey {
  name: string; size: number; schema: string; application?: string | null; products: number;
  storeys: SurveyStorey[]; noStorey: number; groups: SurveyGroup[];
  /** The 2D its elements carry: how many have their own plan 2D besides a body, how many are nothing but 2D. */
  twoD?: { elements: number; only: number };
}

export type ImportMode = 'auto' | 'reference' | 'link';

/** The file the user picked: by path (the desktop) or its bytes as base64 (a browser). */
export interface IfcSource { name: string; path?: string; data?: string }

/** A long engine call in progress (a survey, an import): what it does now, and how far along. */
export interface Busy { title: string; stage: string; done: number; total: number; started: number }

interface ImportState {
  source: IfcSource | null;
  survey: Survey | null;
  mode: ImportMode;
  busy: Busy | null;
}

export const useIfcImport = create<ImportState>(() => ({ source: null, survey: null, mode: 'auto', busy: null }));

/** The engine's progress events, while a call is running: the overlay's stage and bar. Listened to from the first long call on. */
export function onProgress(d: unknown): void {
  const busy = useIfcImport.getState().busy;
  if (!busy) return;
  const p = d as { stage?: string; done?: number; total?: number };
  useIfcImport.setState({ busy: { ...busy, stage: p.stage ?? busy.stage, done: p.done ?? 0, total: p.total ?? 0 } });
}

let listening = false;
const begin = (title: string, stage: string) => {
  if (!listening) { engine.onEvent('progress', onProgress); listening = true; }
  useIfcImport.setState({ busy: { title, stage, done: 0, total: 0, started: Date.now() } });
};
const end = () => useIfcImport.setState({ busy: null });

const args = (s: IfcSource) => (s.data != null ? { name: s.name, data: s.data } : { path: s.path });

/** Step one: the file looked at, then the dialog to choose what of it comes in. */
export async function surveyIfc(source: IfcSource, mode: ImportMode): Promise<void> {
  begin(`Reading ${source.name}`, 'Indexing the file');
  try {
    const survey = await engine.call<Survey>('project.ifcSurvey', args(source), { timeoutMs: LONG_TIMEOUT_MS });
    useIfcImport.setState({ source, survey, mode });
    useUi.setState({ dialog: 'importIfc' });
  } catch (e) {
    notify(e instanceof Error ? e.message : String(e));
  } finally {
    end();
  }
}

export interface ImportResult { elements: number; updated: number; references: number; recognized?: number; recognizedByClass?: Record<string, number>; notes: string[] }

/** Step two: what was chosen (null: everything) brought in, its stages shown; the outcome said. */
export async function runIfcImport(choice: { kinds: string[] | null; storeys: string[] | null; mode: ImportMode; plan2d?: boolean; only2d?: boolean }): Promise<void> {
  const { source } = useIfcImport.getState();
  if (!source) return;
  useUi.setState({ dialog: null });
  useIfcImport.setState({ survey: null });
  begin(`${choice.mode === 'link' ? 'Linking' : 'Importing'} ${source.name}`, 'Reading the file');
  try {
    const r = await engine.call<ImportResult>('project.importIfc', {
      ...args(source), mode: choice.mode,
      ...(choice.kinds ? { kinds: choice.kinds } : {}),
      ...(choice.storeys ? { storeys: choice.storeys } : {}),
      ...(choice.plan2d === false ? { plan2d: false } : {}),
      ...(choice.only2d === false ? { only2d: false } : {}),
    }, { timeoutMs: LONG_TIMEOUT_MS });
    notify(outcome(source.name, choice.mode, r));
  } catch (e) {
    notify(e instanceof Error ? e.message : String(e));
  } finally {
    end();
    useIfcImport.setState({ source: null });
  }
}

/** What came in natively (Lapin's own files, and foreign elements recognised into native ones), and what stays a reference. */
export function outcome(name: string, mode: ImportMode, r: ImportResult): string {
  const recognised = r.recognizedByClass && Object.keys(r.recognizedByClass).length
    ? ` (${Object.entries(r.recognizedByClass).map(([cls, n]) => `${n} ${cls.replace(/^Ifc/, '').replace(/OpeningElement/, 'opening').toLowerCase()}${n === 1 ? '' : 's'}`).join(', ')})`
    : '';
  const parts = [
    r.elements ? `${r.elements} native` : '',
    r.recognized ? `${r.recognized} recognised${recognised}` : '',
    r.updated ? `${r.updated} updated` : '',
    r.references ? `${r.references} as reference` : '',
  ].filter(Boolean);
  return `${mode === 'link' ? 'Linked' : 'Imported'} ${name}: ${parts.join(', ') || 'nothing to bring in'}.${mode === 'link' ? ' Read-only: Manage → Links.' : ''}${r.notes?.length ? ` ${r.notes.join(' ')}` : ''}`;
}

/** The choice as the engine takes it: null where everything is chosen (no filter at all). */
export function choiceOf(survey: Survey, offKinds: ReadonlySet<string>, offStoreys: ReadonlySet<string>): { kinds: string[] | null; storeys: string[] | null } {
  const allKinds = survey.groups.flatMap((g) => g.kinds.map((k) => k.key));
  const allStoreys = [...survey.storeys.map((s) => s.id), ...(survey.noStorey ? [''] : [])];
  return {
    kinds: offKinds.size ? allKinds.filter((k) => !offKinds.has(k)) : null,
    storeys: offStoreys.size ? allStoreys.filter((s) => !offStoreys.has(s)) : null,
  };
}

/** How many elements the choice brings, about: the kinds' share times the storeys' share (elements are counted by one or the other, not both). */
export function chosenCount(survey: Survey, offKinds: ReadonlySet<string>, offStoreys: ReadonlySet<string>): number {
  const total = survey.products || 1;
  const kinds = survey.groups.flatMap((g) => g.kinds).filter((k) => !offKinds.has(k.key)).reduce((a, k) => a + k.count, 0);
  const storeys = survey.storeys.filter((s) => !offStoreys.has(s.id)).reduce((a, s) => a + s.count, 0) + (offStoreys.has('') ? 0 : survey.noStorey);
  return Math.round((kinds / total) * (storeys / total) * total);
}
