/**
 * Openings vs MEP (a tester's request, decision 289): the openings in walls,
 * floors and roofs as solid voids, and the engine's check that every pipe,
 * duct or cable carrier crossing a wall or floor goes through one, wholly
 * inside it with the clearance asked ("x completely inside y"). The engine
 * computes (`coordination.voids`, `coordination.clashes`, `coordination.remarks`);
 * here are the settings, the list's order and filters, and how a row reads.
 */

import { execute, query } from '../../../engine/session';

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
