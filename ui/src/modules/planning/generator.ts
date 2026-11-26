/**
 * The schedule generator's data (docs/plan-4d5d.md, stage 3), apart from React so it can be tested: a template's steps
 * and the spec the configurator edits, the engine's preview of it, the norms, and what the configurator does to a draft —
 * a step moved, switched, its kinds toggled, a step made for the elements left without a task, the names of the
 * catalogue's steps put into the interface's language, and where a preview's bars stand.
 */

import type { Condition } from '../../shell/fieldSchedule';
import { dayOf } from './gantt';

export type Split = 'once' | 'storey';

export interface GenStep {
  key: string; name: string; classes: string[]; part?: string; split: Split; days: number; norm?: string; crew: number;
  wet?: boolean; link: 'FS' | 'SS' | 'FF' | 'SF'; lag: number; on: boolean; kind?: string; cycle?: boolean;
  /** By any field: the elements the step takes, the fields it splits into tasks by, and where the duration and quantity come from. */
  conditions?: Condition[]; splitBy?: string[]; durationField?: string; quantityField?: string;
}

export interface GenSpec { template: string; steps: GenStep[]; byStorey: boolean; demolition: boolean; formwork: boolean; scaffolding: boolean; procurement?: Procurement }

/** A step of a procurement chain: its days by default, IFC's kind of task (LOGISTIC for the transport). */
export interface ProcStep { key: string; name: string; days: number; kind?: string }
/** A package bought for the works: the step whose first task it precedes, its own days for some chain steps, the lag before the installation. */
export interface ProcPackage { key: string; name: string; before: string; on: boolean; days?: Record<string, number>; lag?: number }
/** The generator's procurement: the user's chain (Lapin's by default, indicative) and the packages, after the drawings' milestone. */
export interface Procurement { on: boolean; chain: ProcStep[]; packages: ProcPackage[]; milestone?: string; title?: string }

export interface Template { id: string; label: string; description: string; spec: GenSpec }
export interface OwnStyle { id: string; label: string; spec: GenSpec }

export interface NormRow { code: string; name: string; unit: string; hours: number; crew?: number }
export interface NormsDto { count: number; source: string | null; indicative: boolean; rows: NormRow[] }

export interface GeneratorOptions {
  suggested: string; spec: GenSpec; generated: boolean; start: string; tasks: number; styles: OwnStyle[]; norms: NormsDto;
  /** Lapin's chain, what Procurement switches on. */
  procurementDefault?: Procurement;
}

export interface PreviewTask {
  id: string; name: string; level: number; summary: boolean; start: string; finish: string; duration: number; critical: boolean; count: number;
  source?: 'norm' | 'default' | 'procurement'; quantity?: number; unit?: string; norm?: string; crew?: number; kept?: boolean; phase?: string;
}

export interface PreviewSummary {
  tasks: number; start: string; finish: string; workingDays: number; total: number; linked: number; none: string[]; partial: string[];
  unlinkedClasses: string[]; fromNorms: number; byDefault: number; noNorm: string[]; indicative: boolean;
  formwork: number; scaffolds: number; demolished: number; kept: number; removed: number;
  procurement?: number; procurementIndicative?: boolean;
}

export interface PreviewDto { spec: GenSpec; tasks: PreviewTask[]; problem: string | null; summary: PreviewSummary; notes?: string[] }

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

/** The spec as the engine takes it: no empty field in a step's split, no empty condition or field name. */
export function cleanSpec(spec: GenSpec): GenSpec {
  return { ...spec, steps: spec.steps.map((s) => {
    const { conditions, splitBy, durationField, quantityField, ...rest } = s;
    const out: GenStep = { ...rest };
    const c = (conditions ?? []).filter((x) => x.field.trim());
    if (c.length) out.conditions = c;
    const by = (splitBy ?? []).map((x) => x.trim()).filter(Boolean).slice(0, 4);
    if (by.length) out.splitBy = by;
    if (durationField?.trim()) out.durationField = durationField.trim();
    if (quantityField?.trim()) out.quantityField = quantityField.trim();
    return out;
  }), ...(spec.procurement ? { procurement: cleanProcurement(spec.procurement) } : {}) };
}

/** Procurement as the engine takes it: chain steps with a name, packages before a step. */
function cleanProcurement(p: Procurement): Procurement {
  return {
    ...p,
    chain: p.chain.filter((s) => s.name.trim()).map((s) => ({ ...s, name: s.name.trim() })),
    packages: p.packages.filter((x) => x.before).map((x) => ({ ...x, name: x.name.trim() })),
  };
}

// ------------------------------------------------------------------ procurement (docs/plan-4d5d.md)

/** Lapin's chain as the engine gives it, when it gives none: four steps of 5 working days, the transport LOGISTIC. */
export const DEFAULT_CHAIN: ProcStep[] = [
  { key: 'design', name: 'Design and shop drawings', days: 5 },
  { key: 'approval', name: 'Client approval', days: 5 },
  { key: 'manufacturing', name: 'Manufacturing', days: 5 },
  { key: 'transport', name: 'Transport to site', days: 5, kind: 'LOGISTIC' },
];

/** The spec's procurement, or Lapin's (off) when it has none. */
export function procurementOf(spec: GenSpec, fallback?: Procurement): Procurement {
  return spec.procurement ?? { ...(fallback ?? { chain: DEFAULT_CHAIN, packages: [] }), on: false };
}

/** The spec with its procurement changed. */
export function withProcurement(spec: GenSpec, patch: Partial<Procurement>, fallback?: Procurement): GenSpec {
  return { ...spec, procurement: { ...procurementOf(spec, fallback), ...patch } };
}

/** Whether a step has its own package (a package keyed as the step, before it). */
export const procures = (p: Procurement, step: string): boolean => p.packages.some((x) => x.key === step && x.before === step && x.on);

/** A step's package switched on or off (made the first time, named after the step by the engine). */
export function procureStep(p: Procurement, step: string, on: boolean): Procurement {
  const has = p.packages.find((x) => x.key === step && x.before === step);
  if (has) return { ...p, packages: p.packages.map((x) => (x === has ? { ...x, on } : x)) };
  return on ? { ...p, packages: [...p.packages, { key: step, name: '', before: step, on: true }] } : p;
}

/** A key no package (or chain step, with `taken`) has yet. */
export function freshPackageKey(taken: readonly string[], base: string): string {
  const keys = new Set(taken);
  const b = base.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'package';
  if (!keys.has(b)) return b;
  for (let n = 2; ; n++) if (!keys.has(`${b}-${n}`)) return `${b}-${n}`;
}

/** A package the user names, before a step. */
export function addPackage(p: Procurement, name: string, before: string): Procurement {
  return { ...p, packages: [...p.packages, { key: freshPackageKey(p.packages.map((x) => x.key), name), name: name.trim(), before, on: true }] };
}

/** A package changed by its key. */
export function withPackage(p: Procurement, key: string, patch: Partial<ProcPackage>): Procurement {
  return { ...p, packages: p.packages.map((x) => (x.key === key ? { ...x, ...patch } : x)) };
}

/** A package's days for a chain step: its own, else the chain's. */
export const packageDays = (pkg: ProcPackage, s: ProcStep): number => pkg.days?.[s.key] ?? s.days;

/** A package's own days for a chain step set; the chain's value (or none) takes the override away. */
export function setPackageDays(pkg: ProcPackage, s: ProcStep, days: number | null): ProcPackage {
  const next = { ...(pkg.days ?? {}) };
  if (days == null || days === s.days) delete next[s.key]; else next[s.key] = Math.max(0, Math.round(days));
  const { days: _old, ...rest } = pkg;
  return Object.keys(next).length ? { ...rest, days: next } : rest;
}

/** The working days of a package's whole chain. */
export const chainDays = (p: Procurement, pkg: ProcPackage): number => p.chain.reduce((sum, s) => sum + packageDays(pkg, s), 0);

/** A chain step changed, added or taken away. */
export function withChainStep(p: Procurement, key: string, patch: Partial<ProcStep>): Procurement {
  return { ...p, chain: p.chain.map((s) => (s.key === key ? { ...s, ...patch } : s)) };
}
export function addChainStep(p: Procurement, name: string): Procurement {
  return { ...p, chain: [...p.chain, { key: freshPackageKey(p.chain.map((s) => s.key), name), name, days: 5 }] };
}
export function removeChainStep(p: Procurement, key: string): Procurement {
  return { ...p, chain: p.chain.filter((s) => s.key !== key) };
}

/** Whether the chain is Lapin's own (its days indicative). */
export const indicativeChain = (p: Procurement): boolean =>
  p.chain.length === DEFAULT_CHAIN.length && p.chain.every((s, i) => s.key === DEFAULT_CHAIN[i].key && s.days === DEFAULT_CHAIN[i].days && s.name === DEFAULT_CHAIN[i].name && (s.kind ?? '') === (DEFAULT_CHAIN[i].kind ?? ''));
