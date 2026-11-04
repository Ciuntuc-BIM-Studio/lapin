/**
 * The schedule generator's data (docs/plan-4d5d.md, stage 3), apart from React so it can be tested: a template's steps
 * and the spec the configurator edits, the engine's preview of it, the norms, and what the configurator does to a draft —
 * a step moved, switched, its kinds toggled, a step made for the elements left without a task, the names of the
 * catalogue's steps put into the interface's language, and where a preview's bars stand.
 */

import { dayOf } from './gantt';

export type Split = 'once' | 'storey';

export interface GenStep {
  key: string; name: string; classes: string[]; part?: string; split: Split; days: number; norm?: string; crew: number;
  wet?: boolean; link: 'FS' | 'SS' | 'FF' | 'SF'; lag: number; on: boolean; kind?: string; cycle?: boolean;
}

export interface GenSpec { template: string; steps: GenStep[]; byStorey: boolean; demolition: boolean; formwork: boolean; scaffolding: boolean }

export interface Template { id: string; label: string; description: string; spec: GenSpec }
export interface OwnStyle { id: string; label: string; spec: GenSpec }

export interface NormRow { code: string; name: string; unit: string; hours: number; crew?: number }
export interface NormsDto { count: number; source: string | null; indicative: boolean; rows: NormRow[] }

export interface GeneratorOptions {
  suggested: string; spec: GenSpec; generated: boolean; start: string; tasks: number; styles: OwnStyle[]; norms: NormsDto;
}

export interface PreviewTask {
  id: string; name: string; level: number; summary: boolean; start: string; finish: string; duration: number; critical: boolean; count: number;
  source?: 'norm' | 'default'; quantity?: number; unit?: string; norm?: string; crew?: number; kept?: boolean;
}

export interface PreviewSummary {
  tasks: number; start: string; finish: string; workingDays: number; total: number; linked: number; none: string[]; partial: string[];
  unlinkedClasses: string[]; fromNorms: number; byDefault: number; noNorm: string[]; indicative: boolean;
  formwork: number; scaffolds: number; demolished: number; kept: number; removed: number;
}

export interface PreviewDto { spec: GenSpec; tasks: PreviewTask[]; problem: string | null; summary: PreviewSummary }

/** The demolition step's key: its elements are those the phases demolish, not kinds. */
export const DEMOLITION = 'demolition';

/** A step moved from one place to another in the sequence (the others keep their order). */
export function moveStep(steps: readonly GenStep[], from: number, to: number): GenStep[] {
  const out = [...steps];
  if (from < 0 || from >= out.length) return out;
  const [s] = out.splice(from, 1);
  out.splice(Math.max(0, Math.min(out.length, to)), 0, s);
  return out;
}

/** A step of the spec changed by its key. */
export function withStep(spec: GenSpec, key: string, patch: Partial<GenStep>): GenSpec {
  return { ...spec, steps: spec.steps.map((s) => (s.key === key ? { ...s, ...patch } : s)) };
}

/** A kind (an IFC class) put into a step's elements or taken out of them. */
export function toggleClass(step: GenStep, cls: string): GenStep {
  const has = step.classes.includes(cls);
  return { ...step, classes: has ? step.classes.filter((c) => c !== cls) : [...step.classes, cls] };
}

/** A key no step of the spec has yet ("other", "other-2"…). */
export function freshKey(spec: GenSpec, base: string): string {
  const keys = new Set(spec.steps.map((s) => s.key));
  if (!keys.has(base)) return base;
  for (let n = 2; ; n++) if (!keys.has(`${base}-${n}`)) return `${base}-${n}`;
}

/**
 * A step for the elements no step takes, by their kinds, a task a storey, before the external works (the last step) or
 * at the end; null when there are none.
 */
export function stepForLeftovers(spec: GenSpec, classes: readonly string[], name: string): GenSpec | null {
  if (!classes.length) return null;
  const step: GenStep = { key: freshKey(spec, 'other'), name, classes: [...classes], split: 'storey', days: 5, crew: 0, link: 'FS', lag: 0, on: true };
  const at = spec.steps.findIndex((s) => s.key === 'external');
  const steps = [...spec.steps];
  steps.splice(at >= 0 ? at : steps.length, 0, step);
  return { ...spec, steps };
}

/** The catalogue's step names (written in English) put into the interface's language, so the tasks are named in it. */
export function translateNames(spec: GenSpec, tr: (s: string) => string): GenSpec {
  return { ...spec, steps: spec.steps.map((s) => ({ ...s, name: tr(s.name) })) };
}

/** A norm as a choice names it: its code, its text and how much it takes ("LAP-MASON · Masonry · 5 h/m3"). */
export function normLabel(n: NormRow): string {
  const h = Number.isInteger(n.hours) ? `${n.hours}` : `${Math.round(n.hours * 1000) / 1000}`;
  return [n.code, n.name, `${h} h/${n.unit}`].filter(Boolean).join(' · ');
}

/** A quantity as the preview shows it: two decimals at most, its unit. */
export function quantityText(q: number | undefined, unit: string | undefined): string {
  if (q == null) return '';
  const v = Math.round(q * 100) / 100;
  return `${v.toLocaleString('en-US', { maximumFractionDigits: 2 }).replace(/,/g, ' ')} ${unit ?? ''}`.trim();
}

/** Where each preview row's bar stands: its left and width as fractions of the schedule's span (a summary's too), or null for a row with no dates. */
export function bars(tasks: readonly PreviewTask[]): { from: number; days: number; rows: { left: number; width: number }[] } {
  if (!tasks.length) return { from: 0, days: 1, rows: [] };
  const from = Math.min(...tasks.map((t) => dayOf(t.start)));
  const to = Math.max(...tasks.map((t) => dayOf(t.finish) + 1));
  const days = Math.max(1, to - from);
  return {
    from, days,
    rows: tasks.map((t) => {
      const a = dayOf(t.start) - from;
      const b = dayOf(t.finish) + 1 - from;
      return { left: a / days, width: Math.max(0.004, (b - a) / days) };
    }),
  };
}

/** The steps whose tasks took their days because no norm timed them (the warning's list), the site set-up left out. */
export const untimed = (s: PreviewSummary | undefined): string[] => s?.noNorm ?? [];
