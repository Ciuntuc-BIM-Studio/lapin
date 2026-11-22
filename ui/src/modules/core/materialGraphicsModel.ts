/**
 * The material graphics configurator's pure side (docs/plan-material-graphics.md, stage 0b, decision 678): what a material
 * says of its graphics per kind of view (as material.list gives it), the payloads the configurator sends (one field, one
 * nested group, reset, many materials in one step, copy from another) and the questions it asks of the engine's preview.
 * Opacity is carried in colours as #rrggbbaa by the engine; here it is a percentage beside the colour (the commands take it so).
 */

import { useLang } from '../../i18n';
import { parseDecimal } from '../../shell/parseDecimal';
import { splitAlpha, withOpacity } from '../../viewport/drawing';

export type MaterialKind = 'section' | 'plan' | 'elevation';
export const KINDS: readonly MaterialKind[] = ['section', 'plan', 'elevation'];

/** A line of a band: its colour, weight (paper mm) and line type; unset parts are the Object styles'. */
export type LineSpec = { color?: string; weight?: number; lineType?: string };
/** A second hatch under the first: pattern, scale, angle, colour and weight. */
export type BackgroundHatch = { hatch: string; hatchScale?: number; hatchAngle?: number; hatchColor?: string; hatchWeight?: number };
export type GroupField = 'backgroundHatch' | 'cutLine' | 'separatorLine';

/** What a material stores for a kind of view (material.list's `own`). */
export type OwnGraphics = {
  hatch?: string | null; hatchScale?: number; fill?: string | null; hatchPen?: number; hatchUnits?: 'paper' | 'model'; hatchAngle?: number; hatchAlign?: 'element' | 'view' | 'layer';
  hatchColor?: string; hatchWeight?: number; hatchLineType?: string; hatchOpacity?: number; halftone?: boolean; fillOpacity?: number;
  backgroundHatch?: BackgroundHatch; cutLine?: LineSpec; separatorLine?: LineSpec;
};
/** One kind of view resolved (what is drawn): the flexible keys are there when set. */
export type ResolvedGraphics = OwnGraphics & { hatch: string | null; hatchScale: number; fill: string | null; membrane: boolean; drawJoints: boolean; joins?: boolean; source: 'own' | 'name' };
/** A material as material.list gives it. */
export type MaterialRow = { id: string; name: string; color: string; inUse: boolean; category?: string | null; section: ResolvedGraphics; plan: ResolvedGraphics; elevation: ResolvedGraphics; own: Partial<Record<MaterialKind, OwnGraphics>> };

export type Field = keyof OwnGraphics;

/** The ISO series of line weights, paper millimetres. */
export const ISO_WEIGHTS = [0.13, 0.18, 0.25, 0.35, 0.5, 0.7, 1.0];
export const WEIGHT_RANGE: [number, number] = [0.05, 5];
/** The scales of the preview. */
export const GRAPHICS_SCALES = [20, 50, 100, 200, 500] as const;
export type SampleLayout = 'strip' | 'band';

/** A number as the editors show it: the language's decimal separator (a comma in Romanian), no grouping. */
export function localNumber(v: number): string {
  try { return new Intl.NumberFormat(useLang.getState().lang, { useGrouping: false, maximumFractionDigits: 6 }).format(v); } catch { return String(v); }
}

/** A number as a person types it (a comma too); NaN for anything else. */
export function parseNumber(text: string): number {
  return parseDecimal(text) ?? NaN;
}

/** A weight in 0.05–5 mm, or null when the text is not one. */
export function parseWeight(text: string): number | null {
  const v = parseNumber(text);
  return Number.isFinite(v) && v >= WEIGHT_RANGE[0] && v <= WEIGHT_RANGE[1] ? v : null;
}

/** An opacity in 0–100 per cent (whole), or null. */
export function parseOpacity(text: string): number | null {
  const v = parseNumber(text);
  return Number.isFinite(v) && v >= 0 && v <= 100 ? Math.round(v) : null;
}

/** Whether the material sets the field itself for the kind (else it is inherited: the section's for a plan, the name's, the category's). */
export function isSet(own: OwnGraphics | undefined, field: Field): boolean {
  return !!own && field in own && own[field] !== undefined;
}

/** One field changed: its value, or back to default (undefined: `reset`, applied first by the engine, so any field returns, hatch and fill too). */
export function fieldPatch(field: Field, value: unknown): Record<string, unknown> {
  return value === undefined ? { reset: [field] } : { [field]: value };
}

/**
 * One part of a group changed (the background hatch, a cut line, a separator line). The engine replaces a group whole, so the
 * whole group goes with the part changed; a group left empty (or a background without its pattern) is reset.
 */
export function groupPatch(own: OwnGraphics | undefined, group: GroupField, key: string, value: unknown): Record<string, unknown> {
  const next: Record<string, unknown> = { ...(own?.[group] ?? {}) };
  if (value === undefined || value === null) delete next[key]; else next[key] = value;
  if (group === 'backgroundHatch' && next.hatch === undefined) return { reset: [group] };
  return Object.keys(next).length ? { [group]: next } : { reset: [group] };
}

/** The command's arguments for a change to one or many materials (many: one undo step, `ids`). */
export function updateArgs(targets: readonly string[], kind: MaterialKind, patch: Record<string, unknown> | null): Record<string, unknown> {
  return { ...(targets.length === 1 ? { id: targets[0] } : { ids: [...targets] }), [kind]: patch };
}

/** A material's own graphics of every kind as the arguments that make another the same (kinds it has none for are cleared). */
export function copyArgs(targets: readonly string[], from: MaterialRow): Record<string, unknown> {
  const args: Record<string, unknown> = { ids: [...targets], replace: true };
  for (const kind of KINDS) {
    const own = from.own[kind];
    args[kind] = own && Object.keys(own).length ? structuredClone(own) : null;
  }
  return args;
}

/** What a field shows when it is not set: the resolved value, as text for an input's placeholder. */
export function inheritedText(r: ResolvedGraphics | undefined, field: Field): string {
  const v = r?.[field];
  return v === undefined || v === null || typeof v === 'object' ? '' : typeof v === 'number' ? localNumber(v) : String(v);
}

/** A line of the band as the resolved graphics have it, with the keys it leaves out. */
export function lineOf(own: OwnGraphics | undefined, group: 'cutLine' | 'separatorLine'): LineSpec { return own?.[group] ?? {}; }

/**
 * What is not saved yet, merged for the preview: the engine merges `graphics` onto the saved material (a field with no
 * value, `undefined`, asks for the default; kept out of the draft when it equals nothing).
 */
export function draftGraphics(draft: Record<string, unknown>): Record<string, unknown> | undefined {
  const keys = Object.keys(draft).filter((k) => draft[k] !== undefined);
  return keys.length ? Object.fromEntries(keys.map((k) => [k, draft[k]])) : undefined;
}

export type PreviewRequest = { id: string; kind: MaterialKind; scale: number; layout: SampleLayout; width: number; height: number; graphics?: Record<string, unknown> };

/** material.preview's arguments. */
export function previewArgs(id: string, kind: MaterialKind, scale: number, layout: SampleLayout, draft: Record<string, unknown>, size: { width?: number; height?: number } = {}): PreviewRequest {
  const graphics = draftGraphics(draft);
  return { id, kind, scale, layout, width: size.width ?? 60, height: size.height ?? (layout === 'band' ? 30 : 15), ...(graphics ? { graphics } : {}) };
}

/** The colour a swatch fills with: the fill (the material's own colour for "material") at its opacity. */
export function swatchFill(r: ResolvedGraphics | undefined, materialColour: string): string | null {
  if (!r?.fill) return null;
  const base = r.fill === 'material' ? materialColour : r.fill;
  return withOpacity(base, r.fillOpacity);
}

/** A swatch's hatch colour with its opacity, as an 8-digit hex the engine's pens carry, for the colours a swatch draws in. */
export function swatchInk(r: ResolvedGraphics | undefined, fallback: string): { color: string; opacity: number } {
  const c = r?.hatchColor ?? fallback;
  const split = splitAlpha(c);
  const own = (r?.hatchOpacity ?? 100) / 100;
  return { color: split.color ?? fallback, opacity: split.opacity * own };
}

/** Whether two materials' graphics are the same kind-by-kind (the multi-selection shows "mixed" otherwise). */
export function sameGraphics(rows: readonly MaterialRow[]): boolean {
  if (rows.length < 2) return true;
  const key = (m: MaterialRow) => JSON.stringify(KINDS.map((k) => m.own[k] ?? null));
  const first = key(rows[0]);
  return rows.every((r) => key(r) === first);
}

// ---- By scale (docs/plan-material-graphics.md, stage 2) ----------------------------------------------------------------

/** One step of a kind's `byScale`: a range of scale denominators (inclusive; from 100 = 1:100 and smaller scales, to 50 = 1:50 and larger) and the fields it changes. */
export type ScaleStep = OwnGraphics & { from?: number; to?: number };
/** The fields a step may carry (the engine's list; `hatchUnits` is validated like the material's). */
export const STEP_FIELDS: readonly Field[] = ['hatch', 'hatchScale', 'fill', 'hatchPen', 'hatchAngle', 'hatchAlign', 'hatchColor', 'hatchWeight', 'hatchLineType', 'hatchOpacity', 'halftone', 'fillOpacity', 'backgroundHatch', 'cutLine', 'separatorLine', 'hatchUnits'];
export const MAX_STEPS = 20;
export const SCALE_RANGE: [number, number] = [1, 100000];
/** The scales offered as presets for a step's ends (and the preview's). */
export const SCALE_PRESETS = [20, 50, 100, 200, 500] as const;

/** A scale denominator typed ("100", "1:100"): a whole number in 1–100000, or null. */
export function parseScaleDenominator(text: string): number | null {
  const s = text.trim().replace(/^1\s*:\s*/, '');
  if (!/^\d+$/.test(s)) return null;
  const v = Number(s);
  return v >= SCALE_RANGE[0] && v <= SCALE_RANGE[1] ? v : null;
}

/** The fields a step sets (everything but its range). */
export function stepFields(step: ScaleStep): string[] {
  return Object.keys(step).filter((k) => k !== 'from' && k !== 'to' && (step as Record<string, unknown>)[k] !== undefined);
}

/** The steps of a kind, as material.list's raw `own` has them. */
export function stepsOf(own: OwnGraphics | undefined): ScaleStep[] {
  const s = (own as { byScale?: ScaleStep[] } | undefined)?.byScale;
  return Array.isArray(s) ? s : [];
}

/** What is wrong with a step, as text, or null: the engine's rules (a range end, from ≤ to, at least one field, only the fields allowed). */
export function stepProblem(step: ScaleStep): string | null {
  const { from, to } = step;
  if (from === undefined && to === undefined) return 'A step needs a scale: from, to, or both';
  for (const v of [from, to]) if (v !== undefined && (!Number.isInteger(v) || v < SCALE_RANGE[0] || v > SCALE_RANGE[1])) return 'A scale is a whole number from 1 to 100000';
  if (from !== undefined && to !== undefined && from > to) return '"From" must not be larger than "to"';
  const fields = stepFields(step);
  if (fields.length === 0) return 'A step sets at least one field';
  if (fields.some((f) => !STEP_FIELDS.includes(f as Field))) return 'A step cannot set that field';
  return null;
}

/** The first problem of a list of steps (its index and text), or null. */
export function stepsProblem(steps: readonly ScaleStep[]): { index: number; text: string } | null {
  if (steps.length > MAX_STEPS) return { index: MAX_STEPS, text: 'At most 20 steps' };
  for (let i = 0; i < steps.length; i++) { const p = stepProblem(steps[i]); if (p) return { index: i, text: p }; }
  return null;
}

/** The patch that sets a kind's steps (an empty list takes them away). */
export function stepsPatch(steps: readonly ScaleStep[]): Record<string, unknown> {
  return steps.length ? { byScale: steps.map((s) => structuredClone(s)) } : { reset: ['byScale'] };
}

/** Whether a step covers a scale denominator (from 100 = 1:100 and smaller scales; to 50 = 1:50 and larger). */
export function stepCovers(step: ScaleStep, scale: number): boolean {
  return (step.from === undefined || scale >= step.from) && (step.to === undefined || scale <= step.to);
}

/** The first step that covers the scale (first match wins), or -1. */
export function firstStepAt(steps: readonly ScaleStep[], scale: number): number {
  return steps.findIndex((s) => stepCovers(s, scale));
}

/** A scale that lies in the step, to preview it at. */
export function sampleScale(step: ScaleStep): number {
  return step.from ?? step.to ?? 50;
}

/** The range as text: "1:100 and smaller", "1:50 and larger", "1:100 to 1:199" (without the numbers' own words: for a tooltip). */
export function rangeLabel(step: ScaleStep): string {
  if (step.from !== undefined && step.to !== undefined) return step.from === step.to ? `1:${step.from}` : `1:${step.from} – 1:${step.to}`;
  return step.from !== undefined ? `≥ 1:${step.from}` : `≤ 1:${step.to}`;
}

export function addStep(steps: readonly ScaleStep[], step: ScaleStep): ScaleStep[] { return [...steps, step]; }
export function removeStep(steps: readonly ScaleStep[], i: number): ScaleStep[] { return steps.filter((_, k) => k !== i); }
/** A step moved by `by` places (first match wins, so the order matters); out of range leaves the list as it is. */
export function moveStep(steps: readonly ScaleStep[], i: number, by: number): ScaleStep[] {
  const j = i + by;
  if (i < 0 || i >= steps.length || j < 0 || j >= steps.length) return [...steps];
  const next = [...steps];
  [next[i], next[j]] = [next[j], next[i]];
  return next;
}
/** A copy of a step right after it. */
export function duplicateStep(steps: readonly ScaleStep[], i: number): ScaleStep[] {
  if (i < 0 || i >= steps.length) return [...steps];
  const next = [...steps];
  next.splice(i + 1, 0, structuredClone(steps[i]));
  return next;
}
/** A step's range changed (an end undefined: open). */
export function setStepRange(steps: readonly ScaleStep[], i: number, end: 'from' | 'to', value: number | undefined): ScaleStep[] {
  return steps.map((s, k) => {
    if (k !== i) return s;
    const { [end]: _gone, ...rest } = s;
    return value === undefined ? rest : { ...rest, [end]: value };
  });
}
/** A step's field set (value) or taken back to inherited (undefined). `hatch` and `fill` may be null: none. */
export function setStepField(steps: readonly ScaleStep[], i: number, field: Field, value: unknown): ScaleStep[] {
  return steps.map((s, k) => {
    if (k !== i) return s;
    const next: Record<string, unknown> = { ...s };
    if (value === undefined) delete next[field]; else next[field] = value;
    return next as ScaleStep;
  });
}
/** A group of a step (background hatch, a line) with one part changed; a group left empty is taken away. */
export function setStepGroup(steps: readonly ScaleStep[], i: number, group: GroupField, key: string, value: unknown): ScaleStep[] {
  const cur = { ...((steps[i]?.[group] as Record<string, unknown> | undefined) ?? {}) };
  if (value === undefined || value === null) delete cur[key]; else cur[key] = value;
  const empty = Object.keys(cur).length === 0 || (group === 'backgroundHatch' && cur.hatch === undefined);
  return setStepField(steps, i, group, empty ? undefined : cur);
}

/** A new step: a range and one field that changes nothing (the engine wants at least one), for the person to edit. */
export function newStep(r: ResolvedGraphics | undefined, from = 100): ScaleStep {
  return { from, hatchScale: r?.hatchScale ?? 1 };
}

/** "Simplify at small scales": from 1:100 a plain grey fill with no hatch, from 1:200 solid (editable after). */
export function simplifySteps(): ScaleStep[] {
  return [{ from: 100, to: 199, hatch: null, fill: '#b3b3b3' }, { from: 200, hatch: 'SOLID' }];
}
