/**
 * The estimate as the interface holds it (docs/plan-4d5d.md, stage 6): the engine's answer to cost.estimate, asked again
 * after every change to the model; what the Cost tab does with it (open it, export its forms); the price lists kept on
 * this computer (the app's library, used in any project) and the articles chosen before, remembered for the next
 * projects as Map imported types remembers its choices.
 */

import { useEffect } from 'react';
import { create } from 'zustand';
import { focusPane, notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { safeFileName, saveFile } from '../../engine/files';

export interface LineDto {
  key: string; code: string; name: string; unit: string; qty: number; basis: string;
  m: number; l: number; e: number; t: number; unitPrice: number; value: number; other: number; hours?: number | null;
  manual?: string | null; priced: boolean; elements: string[];
  /** The tasks a line of the user's is done in (stage 7). */
  tasks?: string[] | null;
  /** "× duration": the time its quantity is multiplied by (months, workingDays, days, hours), the duration used, and what went wrong. */
  time?: string | null; duration?: string | null; notes?: string[] | null;
}
export interface ChapterDto { name: string; m: number; l: number; e: number; t: number; direct: number; lines: LineDto[] }
export interface TotalsDto { m: number; l: number; e: number; t: number; direct: number; labourTax: number; directTotal: number; overheads: number; profit: number; net: number; vat: number; total: number; other: number }
export interface SettingsDto { currency: 'RON' | 'EUR'; other: 'RON' | 'EUR'; eurRate: number; labourTax: number; overheads: number; profit: number; vat: number; grouping: 'chapter' | 'storey'; equivalent: boolean; derive: boolean; priceLists: string[] }
export interface UnpricedDto { type: string | null; class: string; name: string; count: number; elements: string[] }
export interface EstimateDto {
  id: string; name: string; predefined: string; settings: SettingsDto; chapters: ChapterDto[]; totals: TotalsDto;
  unpriced: UnpricedDto[]; missing: string[]; lists: { id: string; name: string }[];
}
export interface FormDto { id: string; title: string; subtitle: string; columns: string[]; numeric: number[]; rows: { cells: string[]; key: string | null; style: string }[] }

/** The forms of the estimate, as the engine names them. */
export const FORMS: { id: string; label: string }[] = [
  { id: 'estimate', label: 'Estimate' },
  { id: 'f3', label: 'Bill of quantities (F3)' },
  { id: 'object', label: 'Object estimate (F2)' },
  { id: 'c6', label: 'Labour (C6)' },
  { id: 'c7', label: 'Materials (C7)' },
  { id: 'c8', label: 'Equipment (C8)' },
  { id: 'c9', label: 'Transport (C9)' },
  { id: 'monthly', label: 'Cost per month' },
];

export const useEstimate = create<{ data: EstimateDto | null; version: number; form: string }>(() => ({ data: null, version: -2, form: 'estimate' }));

let asking: number | null = null;

async function refresh(version: number): Promise<void> {
  if (useEstimate.getState().version === version || asking === version) return;
  asking = version;
  const d = await query<EstimateDto | null>('cost.estimate');
  if (asking !== version) return;
  asking = null;
  useEstimate.setState({ data: d ?? null, version });
}

/** The estimate, kept in step with the model. */
export function useEstimateData(): EstimateDto | null {
  const version = useSession((s) => s.version);
  const data = useEstimate((s) => s.data);
  useEffect(() => { void refresh(version); }, [version]);
  return data;
}

/** A sum of money as the estimate shows it: two decimals, thin spaces between the thousands. */
export function money(v: number): string {
  const [whole, dec] = Math.abs(v).toFixed(2).split('.');
  // A value that rounds to nothing has no sign ("−0.00" would read as a loss).
  const sign = v < 0 && (Number(whole) !== 0 || Number(dec) !== 0) ? '−' : '';
  return `${sign}${whole.replace(/\B(?=(\d{3})+(?!\d))/g, '\u2009')}.${dec}`;
}

/** Today's date as yyyy-mm-dd in the user's own time zone (toISOString is UTC: the day before between 00:00 and the offset). */
export function localDate(now = new Date()): string {
  const p = (n: number) => String(n).padStart(2, '0');
  return `${now.getFullYear()}-${p(now.getMonth() + 1)}-${p(now.getDate())}`;
}

/** The value in the other currency, as the engine works it out (`Costing.InOther`), rounded to cents. */
export function inOther(v: number, s: { currency: string; eurRate: number }): number {
  return Math.round((s.currency === 'RON' ? v / s.eurRate : v * s.eurRate) * 100) / 100;
}

export const qty = (v: number): string => (Math.round(v * 1000) / 1000).toFixed(3);

/** The lines that hold any of the elements selected (a selected element shows its lines). */
export function linesHolding(data: EstimateDto | null, selected: readonly string[]): Set<string> {
  const o = new Set<string>();
  if (!data || selected.length === 0) return o;
  const sel = new Set(selected);
  for (const c of data.chapters) for (const l of c.lines) if (l.elements.some((e) => sel.has(e))) o.add(l.key);
  return o;
}

/** How many elements no article prices, and of how many types. */
export function unpricedCount(data: EstimateDto | null): { types: number; elements: number } {
  const u = data?.unpriced ?? [];
  return { types: u.length, elements: u.reduce((s, x) => s + x.count, 0) };
}

/** What a rate may be: `min` is allowed (`above`: only what is greater than it), `max` too. */
export interface RateBounds { min?: number; max?: number; above?: boolean }
/** A percentage: 0 to 100. */
export const PERCENT: RateBounds = { min: 0, max: 100 };
/** An exchange rate: more than nothing (a rate of 0 would divide by it). */
export const EXCHANGE: RateBounds = { min: 0, above: true };

/** A typed rate (a percentage, the RON for one EUR): a decimal comma taken, null when it is not a number or is out of its bounds. */
export function parseRate(text: string, bounds: RateBounds = {}): number | null {
  const n = Number(text.trim().replace(/\s/g, '').replace(',', '.'));
  if (text.trim() === '' || !Number.isFinite(n)) return null;
  if (bounds.min !== undefined && (bounds.above ? n <= bounds.min : n < bounds.min)) return null;
  if (bounds.max !== undefined && n > bounds.max) return null;
  return n;
}

/** The project's estimate, opened in a pane under the view (made when there is none). */
export async function openEstimate(): Promise<void> {
  let id = useEstimate.getState().data?.id ?? [...useSession.getState().nodes.values()].find((n) => n.type === 'IfcCostSchedule')?.id;
  if (!id) {
    const r = await execute('cost.createEstimate', {}) as { id?: string } | null;
    id = r?.id;
    if (!id) return;
    notify('Estimate made: it prices the model by the articles on its types and the rules on its materials.');
  }
  const s = useUi.getState();
  const at = s.panes.findIndex((p) => p.view === 'estimate');
  if (at >= 0) { focusPane(at); return; }
  if (s.panes.length < 2) {
    const first = s.panes[0];
    useUi.setState({ panes: [first, { ...first, view: 'estimate' }], split: 'column', pane: 1 });
    useUi.setState({ view: 'estimate' });
    return;
  }
  focusPane(1);
  useUi.setState({ view: 'estimate' });
}

const bytesOf = (b64: string) => Uint8Array.from(atob(b64), (c) => c.charCodeAt(0));
const nameOf = () => safeFileName(useEstimate.getState().data?.name ?? 'Estimate', 'Estimate');

/** The estimate's forms as a workbook (all of them, a sheet each) or one form. */
export async function exportXlsx(form?: string): Promise<void> {
  const b64 = await query<string>('cost.xlsx', form ? { form } : {});
  if (!b64) return;
  const saved = await saveFile(bytesOf(b64), `${nameOf()}${form ? ` ${form.toUpperCase()}` : ''}.xlsx`, 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');
  if (saved) notify(`Exported ${saved}.`);
}

/** The estimate's forms as a PDF (all of them, or one). */
export async function exportPdf(form?: string): Promise<void> {
  const b64 = await query<string>('cost.pdf', form ? { form } : {});
  if (!b64) return;
  const saved = await saveFile(bytesOf(b64), `${nameOf()}${form ? ` ${form.toUpperCase()}` : ''}.pdf`, 'application/pdf');
  if (saved) notify(`Exported ${saved}.`);
}

/** A form as a schedule, placed on a new sheet (A3), the sheet opened. */
export async function formOnSheet(form: string): Promise<void> {
  const kind = form === 'estimate' ? 'estimate' : `estimate-${form}`;
  const label = FORMS.find((f) => f.id === form)?.label ?? (form === 'cashflow' ? 'Cash flow' : 'Estimate');
  const r = await execute('architecture.createSchedule', { kind, name: label }) as { id?: string } | null;
  if (!r?.id) return;
  // The new sheet takes the schedule in its own command (`viewId`). The schedule's id is only known once it is made, so
  // these are two undo steps until the engine can make a schedule and its sheet in one (architecture.createSchedule {onSheet}).
  const sheet = await execute('sheet.create', { name: label, paper: 'A3', viewId: r.id }) as { id?: string } | null;
  if (sheet?.id) useUi.setState({ view: 'sheet', sheetId: sheet.id });
}

// ── This computer's library ─────────────────────────────────────────────────

const LIBRARY = 'lapin.priceLists';
const REMEMBERED = 'lapin.articleChoices';

export interface PriceRowDto { c: string; n: string; u: string; m?: number; l?: number; e?: number; t?: number; h?: number; tr?: string; a?: string; k?: string; q?: number }
export interface LibraryList { name: string; currency: string; rows: PriceRowDto[]; added: string }

/** The price lists kept on this computer (the app's or the browser's storage), to bring into any project. */
export function library(): LibraryList[] {
  try { return JSON.parse(localStorage.getItem(LIBRARY) ?? '[]') as LibraryList[]; } catch { return []; }
}

/** A list kept in the library (one of the same name replaced); false when the storage is full or blocked. */
export function keepInLibrary(list: LibraryList): boolean {
  const all = library().filter((l) => l.name !== list.name);
  all.push(list);
  try { localStorage.setItem(LIBRARY, JSON.stringify(all)); return true; } catch { return false; }
}

export function forgetFromLibrary(name: string): void {
  try { localStorage.setItem(LIBRARY, JSON.stringify(library().filter((l) => l.name !== name))); } catch { /* nothing kept */ }
}

/** What an article choice is remembered by: a type by its class and name, a material by its name (folded). */
export const choiceKey = (kind: 'type' | 'material', name: string, cls = ''): string =>
  `${kind}|${cls}|${name.trim().toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '')}`;

export interface Choice { code: string; part?: string | null; basis?: string; time?: string }

/**
 * What an article's quantity may be multiplied by ("× duration", R7): the time its element is on site, from the tasks
 * that take it in the schedule. Auto takes the price's unit's (m × month, pcs/day, h), else none.
 */
export const TIMES: { id: string; label: string }[] = [
  { id: 'auto', label: "By the price's unit" }, { id: 'none', label: 'No duration' }, { id: 'months', label: 'Months on site' },
  { id: 'workingDays', label: 'Working days on site' }, { id: 'days', label: 'Calendar days on site' }, { id: 'hours', label: 'Working hours on site' },
];

/** An article as cost.setArticles takes it: the time only when one is chosen (auto is not stored, so an old article stays as it was). */
export function articleArg(r: { code: string; part?: string | null; basis?: string; time?: string }): { code: string; part?: string | null; basis?: string; time?: string } {
  const o: { code: string; part?: string | null; basis?: string; time?: string } = { code: r.code.trim(), part: r.part ?? null, basis: r.basis ?? 'auto' };
  if (r.time && r.time !== 'auto') o.time = r.time;
  return o;
}

/** A line's duration as the estimate shows it after the quantity ("× 3 months"); empty when its quantity is not multiplied. */
export function durationText(l: Pick<LineDto, 'time' | 'duration'>): string {
  return l.time && l.duration ? `× ${l.duration}` : '';
}

export function remembered(): Record<string, Choice> {
  try { return JSON.parse(localStorage.getItem(REMEMBERED) ?? '{}') as Record<string, Choice>; } catch { return {}; }
}

export function remember(choices: Record<string, Choice>): void {
  try { localStorage.setItem(REMEMBERED, JSON.stringify({ ...remembered(), ...choices })); } catch { /* the project keeps them anyway */ }
}

/**
 * The article a row of the Map articles dialog starts with: what it has now, else what was chosen for a type or
 * material of that name before (if a list here has the code), else the engine's suggestion.
 */
export function startingChoice(now: Choice | null, past: Choice | null, suggested: Choice | null, hasCode: (code: string) => boolean): { choice: Choice | null; from: 'now' | 'remembered' | 'suggested' | 'none' } {
  if (now) return { choice: now, from: 'now' };
  if (past && hasCode(past.code)) return { choice: past, from: 'remembered' };
  if (suggested) return { choice: suggested, from: 'suggested' };
  return { choice: null, from: 'none' };
}
