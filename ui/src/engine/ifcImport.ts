/**
 * An IFC or Fragments import in two steps (docs/fragments.md, Exchange): the file looked at first (project.ifcSurvey or project.fragSurvey: its
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
  /** Its property and quantity sets by name, with how many elements carry each; the technical ones (Navisworks' transform, ids, geometry statistics) marked. */
  sets?: { name: string; count: number; technical: boolean }[];
}

/**
 * The import's rules (docs/spec-ifc.md, Regulile importului), general whatever program wrote the file: a body the
 * file repeats kept once (no loss); flat bodies, elements far from the rest (metres), duplicates in place, and
 * ports or openings written as objects left out; meshes simplified within a tolerance (metres); numbers written as
 * text read as numbers; property sets left out by name.
 */
export interface ImportRules {
  dedupe: boolean; flat: boolean; far: number | null; duplicates: boolean; pseudo: boolean; simplify: number; numbers: boolean; skipSets?: string[];
}

/** The rules a user chose last, kept in this browser as their general rule (the sets aside: they are each file's). */
export interface RememberedRules {
  dedupe: boolean; flat: boolean; farOn: boolean; far: string; duplicates: boolean; pseudo: boolean; simplifyOn: boolean; simplify: string; numbers: boolean; technical: boolean;
}

export const DEFAULT_RULES: RememberedRules = {
  dedupe: true, flat: false, farOn: false, far: '1000', duplicates: false, pseudo: false, simplifyOn: false, simplify: '5', numbers: false, technical: true,
};

const RULES_KEY = 'lapin.importIfc.rules';

export function rememberedRules(): RememberedRules {
  try {
    const raw = localStorage.getItem(RULES_KEY);
    return raw ? { ...DEFAULT_RULES, ...(JSON.parse(raw) as Partial<RememberedRules>) } : DEFAULT_RULES;
  } catch { return DEFAULT_RULES; }
}

export function rememberRules(r: RememberedRules): void {
  try { localStorage.setItem(RULES_KEY, JSON.stringify(r)); } catch { /* kept for this import only */ }
}

const num = (s: string) => { const v = Number(s.replace(',', '.')); return Number.isFinite(v) && v > 0 ? v : null; };

/** The rules as the engine takes them: metres, the sets left out by name. */
export function rulesOf(r: RememberedRules, skipSets: string[]): ImportRules {
  const far = r.farOn ? num(r.far) : null;
  const mm = r.simplifyOn ? num(r.simplify) : null;
  return {
    dedupe: r.dedupe, flat: r.flat, far, duplicates: r.duplicates, pseudo: r.pseudo, simplify: mm ? Math.min(mm, 100) / 1000 : 0, numbers: r.numbers,
    ...(skipSets.length ? { skipSets } : {}),
  };
}

/** How a file comes in: recognised as Lapin elements, the rest references (auto); the rest made Studio solids too (editable); all references; linked. */
export type ImportMode = 'auto' | 'editable' | 'reference' | 'link';

/** The file the user picked: by path (the desktop) or its bytes as base64 (a browser). */
export interface IfcSource { name: string; path?: string; data?: string }

/** A long engine call in progress (a survey, an import): what it does now, and how far along. */
export interface Busy { title: string; stage: string; done: number; total: number; started: number }

/** The exchange format: IFC, or That Open's Fragments. */
export type ExchangeFormat = 'ifc' | 'frag';

interface ImportState {
  source: IfcSource | null;
  survey: Survey | null;
  mode: ImportMode;
  format: ExchangeFormat;
  busy: Busy | null;
}

export const useIfcImport = create<ImportState>(() => ({ source: null, survey: null, mode: 'auto', format: 'ifc', busy: null }));

/** The engine's progress events, while a call is running: the overlay's stage and bar. Listened to from the first long call on. */
export function onProgress(d: unknown): void {
  const busy = useIfcImport.getState().busy;
  if (!busy) return;
  const p = d as { stage?: string; done?: number; total?: number };
  useIfcImport.setState({ busy: { ...busy, stage: p.stage ?? busy.stage, done: p.done ?? 0, total: p.total ?? 0 } });
}

let listening = false;
export const begin = (title: string, stage: string) => {
  if (!listening) { engine.onEvent('progress', onProgress); listening = true; }
  useIfcImport.setState({ busy: { title, stage, done: 0, total: 0, started: Date.now() } });
};
export const end = () => useIfcImport.setState({ busy: null });

const args = (s: IfcSource) => (s.data != null ? { name: s.name, data: s.data } : { path: s.path });

/** Step one: the file looked at, then the dialog to choose what of it comes in. */
export async function surveyIfc(source: IfcSource, mode: ImportMode, format: ExchangeFormat = 'ifc'): Promise<void> {
  begin(`Reading ${source.name}`, format === 'frag' ? 'Reading the file' : 'Indexing the file');
  try {
    const survey = await engine.call<Survey>(format === 'frag' ? 'project.fragSurvey' : 'project.ifcSurvey', args(source), { timeoutMs: LONG_TIMEOUT_MS });
    useIfcImport.setState({ source, survey, mode, format });
    useUi.setState({ dialog: 'importIfc' });
  } catch (e) {
    notify(e instanceof Error ? e.message : String(e));
  } finally {
    end();
  }
}

/** What an import made editable (Studio solids of its references) and why the rest stayed references. */
export interface EditableReport { made: number; kept: Record<string, number> }
export interface ImportResult { elements: number; updated: number; references: number; recognized?: number; recognizedByClass?: Record<string, number>; editable?: EditableReport | null; notes: string[] }
export interface FragResult { elements: number; shared: number; storeys: number; skipped: number; native?: number; leftOut?: number; editable?: EditableReport | null; recognition?: { recognized: number; byClass: Record<string, number> } | null; notes: string[] }

/** The editable part of an import's outcome: how many were made Studio solids, and why the others stayed references. */
export function editableText(e: EditableReport | null | undefined): string {
  if (!e) return '';
  const kept = Object.entries(e.kept);
  const n = kept.reduce((a, [, v]) => a + v, 0);
  return ` ${e.made} made editable (Studio solids)${n ? `; ${n} kept as references: ${kept.map(([why, v]) => `${why} (${v})`).join(', ')}` : ''}.`;
}

/** What a Fragments import brought, said plainly: a file of this very project brings nothing new. */
export function fragOutcome(name: string, r: FragResult): string {
  if (!r.elements && r.native) return `Nothing new from ${name}: its ${r.native} element${r.native === 1 ? ' is' : 's are'} already in this project (the file was exported from it). Import it into another project, or a new one.`;
  const rec = r.recognition?.recognized ?? 0;
  const parts = [`${r.elements} element${r.elements === 1 ? '' : 's'}`];
  if (rec) parts.push(`${rec} recognised as Lapin elements (${Object.entries(r.recognition!.byClass).map(([cls, n]) => `${n} ${cls.replace(/^Ifc/, '').toLowerCase()}`).join(', ')})`);
  if (r.shared) parts.push(`${r.shared} shared bod${r.shared === 1 ? 'y' : 'ies'}`);
  if (r.storeys) parts.push(`${r.storeys} storey${r.storeys === 1 ? '' : 's'} made`);
  if (r.native) parts.push(`${r.native} already in this project`);
  if (r.skipped) parts.push(`${r.skipped} without a body Lapin reads`);
  return `Imported ${name}: ${parts.join(', ')}.${editableText(r.editable)}`;
}

/** Step two: what was chosen (null: everything) brought in, its stages shown; the outcome said. */
export async function runIfcImport(choice: { kinds: string[] | null; storeys: string[] | null; mode: ImportMode; plan2d?: boolean; only2d?: boolean; rules?: ImportRules; group?: boolean; editable?: boolean }): Promise<void> {
  const { source, format } = useIfcImport.getState();
  if (!source) return;
  useUi.setState({ dialog: null });
  useIfcImport.setState({ survey: null });
  const frag = format === 'frag';
  begin(`${choice.mode === 'link' ? 'Linking' : 'Importing'} ${source.name}`, 'Reading the file');
  try {
    const params = {
      ...args(source), group: choice.group ?? true,
      mode: choice.mode,
      ...(choice.kinds ? { kinds: choice.kinds } : {}),
      ...(choice.storeys ? { storeys: choice.storeys } : {}),
      ...(choice.plan2d === false ? { plan2d: false } : {}),
      ...(choice.only2d === false ? { only2d: false } : {}),
      ...(choice.rules ? { rules: choice.rules } : {}),
    };
    if (frag) notify(fragOutcome(source.name, await engine.call<FragResult>('project.importFragments', params, { timeoutMs: LONG_TIMEOUT_MS })));
    else notify(outcome(source.name, choice.mode, await engine.call<ImportResult>('project.importIfc', params, { timeoutMs: LONG_TIMEOUT_MS })));
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
  return `${mode === 'link' ? 'Linked' : 'Imported'} ${name}: ${parts.join(', ') || 'nothing to bring in'}.${editableText(r.editable)}${mode === 'link' ? ' Read-only: Manage → Links.' : ''}${r.notes?.length ? ` ${r.notes.join(' ')}` : ''}`;
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
