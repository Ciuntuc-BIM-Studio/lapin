/**
 * Openings vs MEP (a tester's request, decision 289): the openings in walls,
 * floors and roofs as solid voids, and the engine's check that every pipe,
 * duct or cable carrier crossing a wall or floor goes through one, wholly
 * inside it with the clearance asked ("x completely inside y"). The engine
 * computes (`coordination.voids`, `coordination.clashes`, `coordination.remarks`);
 * here are the settings, the list's order and filters, and how a row reads.
 */

import { execute, query, useSession } from '../../../engine/session';
import { frontView } from '../../../viewport/frontView';

export type VoidKind = 'wall' | 'door' | 'window' | 'slab' | 'roof';
export type ClashStatus = 'missing' | 'tooSmall' | 'ok' | 'empty';

export const VOID_KINDS: VoidKind[] = ['wall', 'door', 'window', 'slab', 'roof'];
export const KIND_LABELS: Record<VoidKind, string> = { wall: 'Wall openings', door: 'Door openings', window: 'Window openings', slab: 'Floor openings', roof: 'Roof openings' };
export const STATUS_LABELS: Record<ClashStatus, string> = { missing: 'No opening', tooSmall: 'Opening too small', ok: 'Fits', empty: 'Empty void' };
/** The order the list shows them in: what needs an opening first, what fits last. */
export const STATUS_ORDER: ClashStatus[] = ['missing', 'tooSmall', 'empty', 'ok'];

export interface CoordinationSettings { kinds: VoidKind[]; offset: number; clearance: number; empty: boolean }

export interface VoidDto {
  key: string; mark: string; kind: VoidKind; shape: string; hostId: string; host?: string | null; openingId?: string | null; fillId?: string | null; fill?: string | null;
  selectId: string; storeyId?: string | null; globalId: string; width: number; height: number; depth: number; diameter?: number;
  positions?: number[]; indices?: number[];
}

export interface ClashResult {
  key: string; status: ClashStatus; mepId?: string | null; mep?: string | null; mepClass?: string | null; hostId: string; host?: string | null; hostClass?: string | null;
  by: number; centre: [number, number, number]; message: string; mark?: string; voidKind?: VoidKind; openingId?: string | null; selectId?: string;
}

export interface ClashCheck { results: ClashResult[]; counts: Record<ClashStatus, number>; available: boolean }

export const DEFAULT_SETTINGS: CoordinationSettings = { kinds: [...VOID_KINDS], offset: 0, clearance: 0, empty: false };

export const loadSettings = () => query<CoordinationSettings>('coordination.settings').then((s) => (s ? { ...DEFAULT_SETTINGS, ...s } : DEFAULT_SETTINGS));
export const saveSettings = (s: CoordinationSettings) => execute('coordination.setSettings', { kinds: s.kinds, offset: s.offset, clearance: s.clearance, empty: s.empty });
/**
 * View → Opening voids in a plan, section or elevation (decision 290): the view in front draws its openings' voids, or
 * stops, as its own setting over its style's; the engine draws them, so sheets print them. Returns what the view does now.
 */
export async function toggleVoids2d(): Promise<boolean | null> {
  const id = await frontView();
  if (!id || id === '3d') return null;
  const style = await query<{ resolved: { voids?: boolean } }>('view.style', { id });
  if (!style) return null;
  const on = !style.resolved.voids;
  await execute('view.setStyle', { id, voids: on });
  return on;
}

export const loadVoids = (mesh = false) => query<{ voids: VoidDto[]; count: number }>('coordination.voids', { mesh }).then((r) => r?.voids ?? []);
export const runCheck = () => query<ClashCheck>('coordination.clashes', {});
export const clashRemarks = (author: string) => execute('coordination.remarks', { author }) as Promise<{ added: number; updated: number } | null>;

/** Millimetres as typed (a comma for the point too) into metres; null when not a number or out of ±500 mm. */
export function mmToMetres(text: string, min = -500, max = 500): number | null {
  const t = text.trim().replace(',', '.');
  if (!t) return null;
  const v = Number(t);
  if (!Number.isFinite(v) || v < min || v > max) return null;
  return Math.round(v * 10) / 10000;
}

/** Metres as millimetres to show (no trailing zeros). */
export const metresToMm = (m: number) => String(Math.round(m * 10000) / 10);

/** How much too small the opening is, in whole millimetres rounded up (a fraction of a millimetre counts as one); empty when it fits. */
export function shortBy(r: Pick<ClashResult, 'status' | 'by'>): string {
  if (r.status !== 'tooSmall' || !(r.by > 0)) return '';
  return String(Math.max(1, Math.ceil(r.by * 1000 - 0.05)));
}

/** A void's size as the list reads it: width × height (mm), or Ø for a round one; then its depth. */
export function sizeOf(v: Pick<VoidDto, 'width' | 'height' | 'depth' | 'diameter'>): string {
  const mm = (x: number) => Math.round(x * 1000);
  return v.diameter != null ? `Ø${mm(v.diameter)}` : `${mm(v.width)} × ${mm(v.height)}`;
}

/** The results the list shows, in its order (by status, then as the engine gave them); the ones that fit only when asked. */
export function shown(results: ClashResult[], withOk: boolean): ClashResult[] {
  return results
    .map((r, i) => ({ r, i }))
    .filter(({ r }) => withOk || r.status !== 'ok')
    .sort((a, b) => STATUS_ORDER.indexOf(a.r.status) - STATUS_ORDER.indexOf(b.r.status) || a.i - b.i)
    .map(({ r }) => r);
}

/** What a row selects: the MEP element and its host, and the void's opening when there is one. */
export function pairOf(r: ClashResult): string[] {
  const ids = [r.mepId, r.hostId, r.status !== 'missing' ? r.openingId : null].filter((x): x is string => !!x);
  return [...new Set(ids)];
}

/** The clashes among the results (what becomes a remark). */
export const clashCount = (c: ClashCheck | null) => (c ? c.counts.missing + c.counts.tooSmall : 0);

/** The kinds as a set, toggled. */
export function toggleKind(kinds: VoidKind[], kind: VoidKind): VoidKind[] {
  const on = new Set(kinds);
  if (on.has(kind)) on.delete(kind); else on.add(kind);
  return VOID_KINDS.filter((k) => on.has(k));
}

/** The voids of one kind or all, as the Voids list filters them. */
export const voidsOfKinds = (voids: VoidDto[], kinds: VoidKind[]) => voids.filter((v) => kinds.includes(v.kind));

// ─── The clash report (decision 293): run on demand, kept in the project, fixed from it ───────────────

export type ItemState = 'open' | 'fixed' | 'ignored';
/** An item's group: no opening, no opening because the cut order held it (load-bearing, ranked over the run), too small, empty. */
export type ItemKind = 'missing' | 'held' | 'tooSmall' | 'empty';

export interface ClashItem {
  n: number; key: string; kind: ClashStatus; held?: 'structural' | 'order'; mepId?: string | null; hostId: string; mark?: string | null; openingId?: string | null; voidKey?: string | null;
  by: number; centre: [number, number, number]; message: string; state: ItemState; comment?: string; fixedBy?: string;
  /** When it was first found resolved (decision 296): its message then says how the opening and the run are at the latest run. */
  resolvedAt?: string;
  mep?: string | null; mepClass?: string | null; host?: string | null; hostClass?: string | null;
}

export interface ClashReport { id: string; date: string; hash: string; stale: boolean; clearance: number; counts: Record<string, number>; items: ClashItem[] }

export const ITEM_KINDS: ItemKind[] = ['missing', 'held', 'tooSmall', 'empty'];
export const ITEM_KIND_LABELS: Record<ItemKind, string> = { missing: 'No opening', held: 'Not cut: load-bearing or cut order', tooSmall: 'Opening too small', empty: 'Empty void' };
export const STATE_LABELS: Record<ItemState, string> = { open: 'To fix', fixed: 'Resolved', ignored: 'Ignored' };

export const itemKind = (i: Pick<ClashItem, 'kind' | 'held'>): ItemKind => (i.kind === 'missing' && i.held ? 'held' : i.kind === 'ok' ? 'missing' : i.kind);

/** The items the report shows, grouped by kind in its order, each group by number; fixed and ignored ones only when asked. */
export function groupItems(items: ClashItem[], show: { fixed: boolean; ignored: boolean }): { kind: ItemKind; items: ClashItem[] }[] {
  const shownItems = items.filter((i) => i.state === 'open' || (i.state === 'fixed' && show.fixed) || (i.state === 'ignored' && show.ignored));
  return ITEM_KINDS.map((kind) => ({ kind, items: shownItems.filter((i) => itemKind(i) === kind).sort((a, b) => a.n - b.n) })).filter((g) => g.items.length > 0);
}

/** The next (or previous) item after the one shown, in the list's order, round the end; the first when none is shown. */
export function step(groups: { items: ClashItem[] }[], current: number | null, dir: 1 | -1): number | null {
  const order = groups.flatMap((g) => g.items.map((i) => i.n));
  if (order.length === 0) return null;
  const at = current == null ? -1 : order.indexOf(current);
  if (at < 0) return dir === 1 ? order[0] : order[order.length - 1];
  return order[(at + dir + order.length) % order.length];
}

/** What fixes an item: an opening made where there is none, the opening enlarged where it is too small; nothing for an empty void. */
export function fixOf(i: Pick<ClashItem, 'kind' | 'openingId' | 'mepId'>): 'make' | 'enlarge' | null {
  if (!i.mepId || i.kind === 'empty' || i.kind === 'ok') return null;
  return i.kind === 'tooSmall' && i.openingId ? 'enlarge' : 'make';
}

/** What an item selects: its MEP element, its host, and its opening when there is one. */
export const itemPair = (i: ClashItem): string[] => [...new Set([i.mepId, i.hostId, i.kind !== 'missing' ? i.openingId : null].filter((x): x is string => !!x))];

/** When a report ran, as the interface writes a date: dd.mm.yyyy hh:mm in local time; the text as given when it is no date. */
export function reportDate(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  const p = (n: number) => String(n).padStart(2, '0');
  return `${p(d.getDate())}.${p(d.getMonth() + 1)}.${d.getFullYear()} ${p(d.getHours())}:${p(d.getMinutes())}`;
}

/** How much too small an item's opening is, as the list writes it (mm, rounded up). */
export const itemShortBy = (i: Pick<ClashItem, 'kind' | 'by' | 'state'>) => (i.state === 'open' ? shortBy({ status: i.kind, by: i.by }) : '');

export const loadReport = () => query<ClashReport | null>('coordination.report');
export const runClashCheck = () => execute('coordination.runCheck', {}) as Promise<ClashReport | null>;
export const setItems = (numbers: number[], patch: { state?: 'open' | 'ignored'; comment?: string }) => execute('coordination.setItems', { numbers, ...patch });
export const fixItems = (numbers: number[], action: 'auto' | 'make' | 'enlarge' = 'auto') =>
  execute('coordination.fix', { numbers, action }) as Promise<{ fixed: number; failed: { n: number; message: string }[] } | null>;
/** The report's open clashes as remarks (numbered), for BCF: their ids. */
export const reportRemarks = (author: string) => execute('coordination.remarks', { author }) as Promise<{ added: number; updated: number; ids: string[] } | null>;
export const reportFile = (format: 'csv' | 'pdf') => query<{ name: string; data: string }>('coordination.reportFile', { format });

/**
 * View → Clash marks: the view in front (a plan, section or elevation) marks the report's open clashes with red clouds
 * and their numbers, or stops; on the screen only. Returns what the view does now, null with no 2D view in front.
 */
export async function toggleClashMarks(on?: boolean): Promise<boolean | null> {
  const id = await frontView();
  if (!id || id === '3d') return null;
  const now = useSession.getState().nodes.get(id)?.props.clashMarks === true;
  const next = on ?? !now;
  const r = await execute('coordination.markView', { id, on: next }) as { on: boolean } | null;
  return r?.on ?? null;
}

// ─── MEP in the cut order (decision 293) ───────────────────────────────────────────────────────────

export interface MepCut { cut: boolean; clearance: number; step: number; minSize: number; structural: boolean }
export const DEFAULT_MEP_CUT: MepCut = { cut: false, clearance: 0.02, step: 0.01, minSize: 0.05, structural: false };
export const loadMepCut = () => query<MepCut>('cutOrder.mep').then((r) => ({ ...DEFAULT_MEP_CUT, ...(r ?? {}) }));
export const saveMepCut = (patch: Partial<MepCut>) => execute('cutOrder.setMep', patch);

/** The opening a run cuts round itself: its outside grown by the clearance on both sides, rounded up to the step, never under the smallest. */
export function provisionSize(outside: number, rule: Pick<MepCut, 'clearance' | 'step' | 'minSize'>): number {
  let s = outside + 2 * rule.clearance;
  if (rule.step > 1e-6) s = Math.ceil(s / rule.step - 1e-6) * rule.step;
  return Math.round(Math.max(s, rule.minSize) * 1e6) / 1e6;
}
