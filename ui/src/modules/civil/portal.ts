/**
 * The tunnel-portal and bridge-abutment configurator's state (docs/plan-wing-walls.md, S3): the engine's catalogue of parameters
 * and presets (`civil.portal`), a spec as the configurator holds it (each number a value or a formula, `{= embankment + 0.5}`),
 * which parameters apply to it, and how a field's text is read back.
 */

import { create } from 'zustand';

export interface PortalParam {
  key: string; label: string; kind: 'length' | 'number' | 'angle' | 'choice' | 'bool' | 'text'; group: string;
  default: unknown; formula: boolean; min?: number; max?: number; choices?: string[]; showIf?: Record<string, string[]>;
}

export interface PortalPreset { key: string; set: Record<string, unknown> }

export interface PortalCatalog { params: PortalParam[]; presets: PortalPreset[] }

export type PortalSpec = Record<string, unknown>;

export const PRESET_LABELS: Record<string, string> = {
  straight: 'Straight wings', u: 'U wings', flared: 'Flared wings', warped: 'Warped wings', abutment: 'Abutment with wings',
};

export const PRESET_HINTS: Record<string, string> = {
  straight: 'Tunnel portal, its wings in line with the face wall',
  u: 'Tunnel portal, its wings along the road',
  flared: 'Tunnel portal, its wings at an angle to the road',
  warped: 'Tunnel portal, its wings turning their face to the embankment\'s slope',
  abutment: 'Bridge abutment, the embankment held between its wings',
};

/** The configurator open: on a new portal, or on a wall it made (its id). */
export const usePortalDialog = create<{ id: string | null; spec: PortalSpec }>(() => ({ id: null, spec: { preset: 'flared' } }));

/** The spec whole: the defaults, under its preset's, under what it gives (as the engine's Portals.Full). */
export function fullSpec(catalog: PortalCatalog, spec: PortalSpec): PortalSpec {
  const o: PortalSpec = {};
  for (const p of catalog.params) o[p.key] = p.default;
  const preset = typeof spec.preset === 'string' && catalog.presets.some((x) => x.key === spec.preset) ? spec.preset : 'flared';
  Object.assign(o, catalog.presets.find((x) => x.key === preset)?.set ?? {});
  o.preset = preset;
  for (const [k, v] of Object.entries(spec)) if (k !== 'preset') o[k] = v;
  return o;
}

/** A spec with another preset: its numbers and formulas kept, what the preset sets taken over. */
export function withPreset(catalog: PortalCatalog, spec: PortalSpec, preset: string): PortalSpec {
  const set = catalog.presets.find((x) => x.key === preset)?.set ?? {};
  const o: PortalSpec = { ...spec, preset };
  for (const k of Object.keys(set)) delete o[k];
  return o;
}

/** A value as a condition reads it ('true', '30', 'battered'). */
const asText = (v: unknown) => (v == null ? '' : String(v));

/** Whether a parameter applies to the spec (every condition holds). */
export function applies(p: PortalParam, full: PortalSpec): boolean {
  if (!p.showIf) return true;
  return Object.entries(p.showIf).every(([k, values]) => values.includes(asText(full[k])));
}

/** Whether a value is a formula: `{= …}`, or a text starting with `=`. */
export function isFormula(v: unknown): v is string {
  if (typeof v !== 'string') return false;
  const t = v.trim();
  return (t.startsWith('{=') && t.endsWith('}')) || t.startsWith('=');
}

/** A field's text as it is shown: a number in the units given (metres, degrees), a formula as written. */
export function fieldText(v: unknown, decimals = 3): string {
  if (typeof v === 'number') return String(Number(v.toFixed(decimals)));
  return v == null ? '' : String(v);
}

/**
 * A field's text read back: empty is the default (null), a formula as written (`=…` becomes `{= …}`), a number (a comma read
 * as a decimal point); anything else is not read (undefined).
 */
export function readField(text: string): number | string | null | undefined {
  const t = text.trim();
  if (!t) return null;
  if (t.startsWith('{=') && t.endsWith('}')) return t;
  if (t.startsWith('=')) return `{= ${t.slice(1).trim()}}`;
  const n = Number(t.replace(',', '.'));
  return Number.isFinite(n) ? n : undefined;
}

/** The spec as it is sent: what the user set (the defaults left to the engine), the placement and the drawn path kept. */
export function specOut(spec: PortalSpec): PortalSpec {
  const o: PortalSpec = {};
  for (const [k, v] of Object.entries(spec)) if (v !== undefined && v !== null && v !== '') o[k] = v;
  return o;
}

/** The parameters by group, in the catalogue's order, those that apply. */
export function groupsOf(catalog: PortalCatalog, full: PortalSpec): { group: string; params: PortalParam[] }[] {
  const out: { group: string; params: PortalParam[] }[] = [];
  for (const p of catalog.params) {
    if (!applies(p, full)) continue;
    let g = out.find((x) => x.group === p.group);
    if (!g) out.push(g = { group: p.group, params: [] });
    g.params.push(p);
  }
  return out;
}
