/**
 * The cash flow (docs/plan-4d5d.md, cash flow) as the interface holds it: the engine's answer to cost.cashFlow — the
 * periods (a month or a week) with what is certified, held back, recovered, paid in and out, the running total; the
 * certificates; the retention's releases; the peak funding need — and the chart's arithmetic apart from React so it can be
 * tested. The engine works the payments out on the planned schedule from the terms on the estimate (cost.setCashFlow).
 */

import { useEffect } from 'react';
import { create } from 'zustand';
import { focusPane, notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { safeFileName, saveFile } from '../../engine/files';
import { axisOf, niceStep, openCostTime, type Plot } from './costTime';

export interface CashSettingsDto {
  basis: 'net' | 'direct' | 'contract'; contractValue: number | null; costBasis: 'direct' | 'withOverheads'; costSide: boolean; supplierDays: number;
  advance: number; advanceDate: string | null; advanceTask: string | null;
  retention: number; retentionAtTakeover: number; defectsMonths: number; takeoverDate: string | null; takeoverTask: string | null;
  cycle: 'monthly' | 'weekly'; certificateDay: number; paymentDays: number; vat: boolean; indicative: boolean;
}
export interface CashPeriodDto { start: string; cost: number; certified: number; retention: number; recovered: number; in: number; out: number; net: number; cumulative: number; retentionHeld: number; advanceLeft: number }
export interface CashCertificateDto { no: number; date: string; certified: number; retention: number; recovered: number; net: number; vat: number; due: string; actual: boolean }
export interface CashFlowDto {
  estimate: string; currency: string; period: 'month' | 'week'; settings: CashSettingsDto;
  contractValue: number; costTotal: number; advance: number; advanceDate: string; takeover: string; defectsEnd: string; actual: boolean;
  certified: number; retention: number; cashIn: number; cashOut: number; peak: number; peakPeriod: string | null;
  periods: CashPeriodDto[]; certificates: CashCertificateDto[]; releases: { due: string; value: number; what: 'takeover' | 'defects' }[];
}
export interface NoCashDto { why: string }

export const isCash = (d: CashFlowDto | NoCashDto | null): d is CashFlowDto => !!d && 'periods' in d;

export const useCashFlow = create<{ data: CashFlowDto | NoCashDto | null; key: string; period: 'month' | 'week' }>(() => ({ data: null, key: '', period: 'month' }));

let asking: string | null = null;

async function refresh(key: string, period: 'month' | 'week'): Promise<void> {
  if (useCashFlow.getState().key === key || asking === key) return;
  asking = key;
  const d = await query<CashFlowDto | NoCashDto>('cost.cashFlow', { period });
  if (asking !== key) return;
  asking = null;
  useCashFlow.setState({ data: d ?? null, key });
}

/** The cash flow, kept in step with the model and the period asked. */
export function useCash(): CashFlowDto | NoCashDto | null {
  const version = useSession((s) => s.version);
  const period = useCashFlow((s) => s.period);
  const data = useCashFlow((s) => s.data);
  useEffect(() => { void refresh(`${version}:${period}`, period); }, [version, period]);
  return data;
}

// ── The chart's arithmetic ──────────────────────────────────────────────────

/** A scale from `min` (≤ 0) to `max` (≥ 0) in steps of 1, 2 or 5 × 10ⁿ: its ends, its ticks. */
export function signedAxis(min: number, max: number, ticks = 6): { lo: number; hi: number; values: number[] } {
  const lo0 = Math.min(0, min), hi0 = Math.max(0, max);
  if (lo0 === 0) { const a = axisOf(hi0, ticks); return { lo: 0, hi: a.top, values: a.values }; }
  const step = niceStep(hi0 - lo0, ticks);
  const lo = Math.floor(lo0 / step + 1e-9) * step;
  const hi = Math.max(lo + step, Math.ceil(hi0 / step - 1e-9) * step);
  const values: number[] = [];
  for (let v = lo; v <= hi + step * 1e-6; v += step) values.push(Math.round(v * 1e6) / 1e6);
  return { lo, hi, values };
}

/**
 * The chart in a plot box: a bar a period for its net cash flow (up from nought when in, down when out), the cumulative as
 * a line through the periods' ends, a marker at the peak funding need; one scale for both.
 */
export function cashChartOf(d: CashFlowDto, box: Plot) {
  const n = Math.max(1, d.periods.length);
  const values = d.periods.flatMap((p) => [p.net, p.cumulative]);
  const axis = signedAxis(Math.min(0, ...values), Math.max(0, ...values));
  const y = (v: number) => box.y1 - (v - axis.lo) / (axis.hi - axis.lo) * (box.y1 - box.y0);
  const slot = (box.x1 - box.x0) / n;
  const zero = y(0);
  const bars = d.periods.map((p, i) => {
    const w = slot * 0.6;
    const x = box.x0 + i * slot + (slot - w) / 2;
    const top = Math.min(zero, y(p.net));
    return { start: p.start, x, w, y: top, h: Math.abs(y(p.net) - zero), out: p.net < 0, mid: box.x0 + (i + 0.5) * slot };
  });
  const line = d.periods.length === 0 ? '' : d.periods.map((p, i) => `${i ? 'L' : 'M'}${(box.x0 + (i + 1) * slot).toFixed(1)},${y(p.cumulative).toFixed(1)}`).join('');
  const at = d.peakPeriod ? d.periods.findIndex((p) => p.start === d.peakPeriod) : -1;
  const peak = at >= 0 ? { x: box.x0 + (at + 1) * slot, y: y(d.periods[at].cumulative) } : null;
  return { axis, y, zero, bars, line, peak, slot };
}

/** A period's label: MM.yy for a month, dd.MM for a week's Monday. */
export const periodLabel = (start: string, period: 'month' | 'week') =>
  period === 'week' ? `${start.slice(8, 10)}.${start.slice(5, 7)}` : `${start.slice(5, 7)}.${start.slice(2, 4)}`;

// ── What the view does with it ──────────────────────────────────────────────

/** Changes the terms (one undo step); null puts a field back to its default. */
export async function setCashTerms(patch: Partial<Record<keyof CashSettingsDto, unknown>> | { reset: true }): Promise<void> {
  await execute('cost.setCashFlow', patch as Record<string, unknown>);
}

const bytesOf = (b64: string) => Uint8Array.from(atob(b64), (c) => c.charCodeAt(0));

export async function exportCashXlsx(name: string): Promise<void> {
  const b64 = await query<string>('cost.cashFlowXlsx', { period: useCashFlow.getState().period });
  if (!b64) return;
  const saved = await saveFile(bytesOf(b64), `${safeFileName(name, 'Cost')} cash flow.xlsx`, 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');
  if (saved) notify(`Exported ${saved}.`);
}

export async function exportCashCsv(name: string): Promise<void> {
  const csv = await query<string>('cost.cashFlowCsv', { period: useCashFlow.getState().period });
  if (csv == null) return;
  const saved = await saveFile(new TextEncoder().encode(csv), `${safeFileName(name, 'Cost')} cash flow.csv`, 'text/csv');
  if (saved) notify(`Exported ${saved}.`);
}

/** The cash flow's view: in its pane, else in the S-curve's (which it takes the place of), else in a pane under the view. */
export function openCashFlow(): void {
  const s = useUi.getState();
  const own = s.panes.findIndex((p) => p.view === 'cashFlow');
  if (own >= 0) { focusPane(own); return; }
  const curve = s.panes.findIndex((p) => p.view === 'costTime');
  if (curve >= 0) { focusPane(curve); useUi.setState({ view: 'cashFlow' }); return; }
  if (s.panes.length < 2) {
    const first = s.panes[0];
    useUi.setState({ panes: [first, { ...first, view: 'cashFlow' }], split: 'column', pane: 1 });
    useUi.setState({ view: 'cashFlow' });
    return;
  }
  focusPane(1);
  useUi.setState({ view: 'cashFlow' });
}

/** Back to the S-curve in the cash flow's pane. */
export function showCurve(): void {
  const at = useUi.getState().panes.findIndex((p) => p.view === 'cashFlow');
  if (at >= 0) { focusPane(at); useUi.setState({ view: 'costTime' }); return; }
  openCostTime();
}
