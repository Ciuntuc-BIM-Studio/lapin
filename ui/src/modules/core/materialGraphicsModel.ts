/**
 * The material graphics configurator's pure side (docs/plan-material-graphics.md, stage 0b, decision 670): what a material
 * says of its graphics per kind of view (as material.list gives it), the payloads the configurator sends (one field, one
 * nested group, reset, many materials in one step, copy from another) and the questions it asks of the engine's preview.
 * Opacity is carried in colours as #rrggbbaa by the engine; here it is a percentage beside the colour (the commands take it so).
 */

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
  hatch?: string | null; hatchScale?: number; fill?: string | null; hatchPen?: number; hatchUnits?: 'paper' | 'model'; hatchAngle?: number; hatchAlign?: 'view' | 'layer';
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
export const GRAPHICS_SCALES = [20, 50, 100] as const;
export type SampleLayout = 'strip' | 'band';

/** A number as a person types it (a comma too); NaN for anything else. */
export function parseNumber(text: string): number {
  const s = text.trim().replace(',', '.');
  return s === '' ? NaN : Number(s);
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
  return v === undefined || v === null || typeof v === 'object' ? '' : String(v);
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
