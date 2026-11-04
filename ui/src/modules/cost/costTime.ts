/**
 * Cost over time (docs/plan-4d5d.md, stage 7) as the interface holds it: the engine's answer to cost.overTime — the
 * S-curve a day (planned, the baseline's, the work done's to the status date), the months planned and done, by chapter —
 * and the chart's arithmetic apart from React so it can be tested: the scales' steps, the curves' paths, the months'
 * bars. The engine lays the estimate on the schedule; the interface only draws it.
 */

import { useEffect } from 'react';
import { create } from 'zustand';
import { focusPane, notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { safeFileName, saveFile } from '../../engine/files';

export interface MonthDto { month: string; planned: number; done: number; plannedCum: number; doneCum: number; baselineCum?: number }
export interface ChapterTimeDto { name: string; months: number[]; done: number[]; notScheduled: number; total: number }
export interface OverTimeDto {
  currency: string; budget: number; scheduled: number; unscheduled: number; toDate: number; net: number; total: number;
  schedule: string; start: string; finish: string; dataDate: string | null; doneThrough: string | null; from: string; to: string;
  /** Cumulative at the end of each day from `from`: planned to `to`, the baseline's (when saved), done to the status date. */
  planned: number[]; baseline: number[] | null; done: number[];
  months: MonthDto[]; chapters: ChapterTimeDto[]; tasks: { id: string; cost: number; done: number }[];
}
/** No curve: why (no estimate, no schedule). */
export interface NoCurveDto { why: string }

export const isCurve = (d: OverTimeDto | NoCurveDto | null): d is OverTimeDto => !!d && 'planned' in d;

export const useCostTime = create<{ data: OverTimeDto | NoCurveDto | null; version: number }>(() => ({ data: null, version: -2 }));

let asking: number | null = null;

async function refresh(version: number): Promise<void> {
  if (useCostTime.getState().version === version || asking === version) return;
  asking = version;
  const d = await query<OverTimeDto | NoCurveDto>('cost.overTime');
  if (asking !== version) return;
  asking = null;
  useCostTime.setState({ data: d ?? null, version });
}

/** The curve, kept in step with the model. */
export function useOverTime(): OverTimeDto | NoCurveDto | null {
  const version = useSession((s) => s.version);
  const data = useCostTime((s) => s.data);
  useEffect(() => { void refresh(version); }, [version]);
  return data;
}

// ── The chart's arithmetic ──────────────────────────────────────────────────

/** An axis step for values up to `max`: 1, 2 or 5 times a power of ten, about `ticks` of them (the engine's sheet uses the same). */
export function niceStep(max: number, ticks = 5): number {
  if (!(max > 0)) return 1;
  const raw = max / ticks;
  const p = 10 ** Math.floor(Math.log10(raw));
  const f = raw / p;
  return (f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10) * p;
}

/** An axis' top: the step and the first multiple of it at or over `max`. */
export function axisOf(max: number, ticks = 5): { step: number; top: number; values: number[] } {
  const step = niceStep(max, ticks);
  const top = Math.max(step, Math.ceil(max / step - 1e-9) * step);
  const values: number[] = [];
  for (let v = 0; v <= top + step * 1e-6; v += step) values.push(Math.round(v * 1e6) / 1e6);
  return { step, top, values };
}

/** A sum on an axis: thousands as k, millions as M. */
export function shortMoney(v: number): string {
  const a = Math.abs(v);
  const r = (x: number) => String(Math.round(x * 100) / 100);
  return a >= 1e6 ? `${r(v / 1e6)} M` : a >= 1e3 ? `${r(v / 1e3)} k` : r(v);
}

const DAY = 86_400_000;
const dayNo = (iso: string) => { const [y, m, d] = iso.slice(0, 10).split('-').map(Number); return Math.round(Date.UTC(y, m - 1, d) / DAY); };

export interface Plot { x0: number; x1: number; y0: number; y1: number }

/** The chart laid in a plot box: x by day (the day's end), y by the cumulative scale; the months' bars by their own scale. */
export function chartOf(d: OverTimeDto, box: Plot) {
  const from = dayNo(d.from);
  const days = Math.max(1, dayNo(d.to) - from + 1);
  const x = (day: number) => box.x0 + (day - from) * (box.x1 - box.x0) / days;
  const curves = [d.planned, d.baseline ?? [], d.done];
  const cum = axisOf(Math.max(d.budget, ...curves.flat(), 0));
  const y = (v: number) => box.y1 - v / cum.top * (box.y1 - box.y0);
  const month = axisOf(Math.max(0, ...d.months.map((m) => Math.max(m.planned, m.done))));
  const ym = (v: number) => box.y1 - v / month.top * (box.y1 - box.y0);
  /** A cumulative series as an SVG path: from nothing at the first day's start, a point at each day's end. */
  const path = (series: number[]) => series.length === 0 ? '' :
    `M${x(from).toFixed(1)},${y(0).toFixed(1)}` + series.map((v, i) => `L${x(from + i + 1).toFixed(1)},${y(v).toFixed(1)}`).join('');
  const bars = d.months.map((m) => {
    const [yy, mm] = m.month.split('-').map(Number);
    const a = Math.max(from, Math.round(Date.UTC(yy, mm - 1, 1) / DAY));
    const b = Math.min(from + days, Math.round(Date.UTC(yy, mm, 1) / DAY));
    const w = (x(b) - x(a)) * 0.36;
    const mid = (x(a) + x(b)) / 2;
    return { month: m.month, mid, start: x(a), planned: { x: mid - w, w, y: ym(m.planned), h: box.y1 - ym(m.planned) }, done: { x: mid, w, y: ym(m.done), h: box.y1 - ym(m.done) } };
  });
  const status = d.dataDate ? x(dayNo(d.dataDate) + 1) : null;
  return { x, y, ym, cum, month, path, bars, status, planned: path(d.planned), baseline: d.baseline ? path(d.baseline) : '', done: path(d.done) };
}

/** The value of a cumulative series at the end of a day (its last value after it; nothing before it). */
export function valueAt(d: OverTimeDto, series: number[], iso: string): number {
  const i = dayNo(iso) - dayNo(d.from);
  if (i < 0 || series.length === 0) return 0;
  return series[Math.min(i, series.length - 1)];
}

// ── What the Cost tab does with it ──────────────────────────────────────────

/** Cost over time in a pane under the view (as the estimate). */
export function openCostTime(): void {
  const s = useUi.getState();
  const at = s.panes.findIndex((p) => p.view === 'costTime');
  if (at >= 0) { focusPane(at); return; }
  if (s.panes.length < 2) {
    const first = s.panes[0];
    useUi.setState({ panes: [first, { ...first, view: 'costTime' }], split: 'column', pane: 1 });
    useUi.setState({ view: 'costTime' });
    return;
  }
  focusPane(1);
  useUi.setState({ view: 'costTime' });
}

const bytesOf = (b64: string) => Uint8Array.from(atob(b64), (c) => c.charCodeAt(0));

/** The curve, the months and the tasks' costs as a workbook. */
export async function exportCostTimeXlsx(name: string): Promise<void> {
  const b64 = await query<string>('cost.overTimeXlsx');
  if (!b64) return;
  const saved = await saveFile(bytesOf(b64), `${safeFileName(name, 'Cost')} S-curve.xlsx`, 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');
  if (saved) notify(`Exported ${saved}.`);
}

/** The curve and the month table as a PDF. */
export async function exportCostTimePdf(name: string): Promise<void> {
  const b64 = await query<string>('cost.overTimePdf');
  if (!b64) return;
  const saved = await saveFile(bytesOf(b64), `${safeFileName(name, 'Cost')} S-curve.pdf`, 'application/pdf');
  if (saved) notify(`Exported ${saved}.`);
}

/** The S-curve on a new sheet (A3), the sheet opened. */
export async function curveOnSheet(): Promise<void> {
  const r = await execute('cost.curveSheet', {}) as { sheetId?: string } | null;
  if (r?.sheetId) useUi.setState({ view: 'sheet', sheetId: r.sheetId });
}
