/**
 * The pure parts of the table import and of the layer maps (docs/plan-user-extensibility.md §7): a column mapping remembered on
 * this computer by the table's heading signature, the `.lapinmapping` file (a table's mapping or a drafting layer map), and the
 * defaults of a converter's options. Mappings are kept by heading text, not by column number, so the same sheet with its
 * columns in another order still finds its mapping.
 */

export interface MapItem { column: number; key: string | null; unit?: string | null }
/** A mapping as kept: by the column's heading. */
export interface MapEntry { header: string; key: string | null; unit?: string | null }

export interface TargetColumn { key: string; label: string; kind: string; required?: boolean; choices?: string[]; help?: string; units?: string[] }
export interface TableTarget { id: string; label: string; module: string; open: boolean; description?: string; columns: TargetColumn[] }

/** A heading compared without case, accents, spaces or punctuation. */
export const normHeader = (s: string): string => s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().replace(/[^\p{L}\p{N}]+/gu, '');

/** The headings of a table, as the signature the mapping is kept under: the target and the headings, in any order. */
export function headerSignature(target: string, headers: string[]): string {
  return `${target}|${headers.map(normHeader).filter(Boolean).sort().join(',')}`;
}

const MEMORY = 'lapin.tableMappings';
const LIMIT = 60;
type Memory = Record<string, { at: number; entries: MapEntry[] }>;
const readMemory = (): Memory => { try { return JSON.parse(localStorage.getItem(MEMORY) ?? '{}') as Memory; } catch { return {}; } };

/** Entries from a mapping over these headings (columns left out are kept too, so they stay left out). */
export const entriesOf = (headers: string[], mapping: MapItem[]): MapEntry[] =>
  headers.map((h, i) => {
    const m = mapping.find((x) => x.column === i);
    return { header: h, key: m?.key || null, ...(m?.unit ? { unit: m.unit } : {}) };
  }).filter((e) => e.header.trim());

/** A mapping over a table's columns from entries kept: each column takes the entry of its own heading; null when none matches. */
export function applyEntries(headers: string[], entries: MapEntry[]): MapItem[] | null {
  const by = new Map(entries.map((e) => [normHeader(e.header), e]));
  const out: MapItem[] = [];
  let hit = 0;
  headers.forEach((h, i) => {
    const e = by.get(normHeader(h));
    if (!e) return;
    hit++;
    if (e.key) out.push({ column: i, key: e.key, ...(e.unit ? { unit: e.unit } : {}) });
  });
  return hit > 0 ? out : null;
}

export function rememberMapping(target: string, headers: string[], mapping: MapItem[]): void {
  try {
    const mem = readMemory();
    mem[headerSignature(target, headers)] = { at: Date.now(), entries: entriesOf(headers, mapping) };
    const keys = Object.keys(mem).sort((a, b) => mem[b].at - mem[a].at);
    for (const k of keys.slice(LIMIT)) delete mem[k];
    localStorage.setItem(MEMORY, JSON.stringify(mem));
  } catch { /* not kept */ }
}

/** The mapping kept for these headings, if any, over this table's columns. */
export function recallMapping(target: string, headers: string[]): MapItem[] | null {
  const got = readMemory()[headerSignature(target, headers)];
  return got ? applyEntries(headers, got.entries) : null;
}

/** One column given a meaning: a target column is used by one column only (it is taken from another), "open" by any. */
export function setColumnKey(mapping: MapItem[], column: number, key: string | null, unit?: string | null): MapItem[] {
  const rest = mapping.filter((m) => m.column !== column && !(key && key !== 'open' && m.key === key));
  return key ? [...rest, { column, key, ...(unit ? { unit } : {}) }].sort((a, b) => a.column - b.column) : rest;
}

export const setColumnUnit = (mapping: MapItem[], column: number, unit: string | null): MapItem[] =>
  mapping.map((m) => (m.column === column ? (unit ? { ...m, unit } : { column: m.column, key: m.key }) : m));

/** The target's required columns no column is mapped to. */
export const missingRequired = (target: TableTarget, mapping: MapItem[], hasDefault: (key: string) => boolean = () => false): TargetColumn[] =>
  target.columns.filter((c) => c.required && !mapping.some((m) => m.key === c.key) && !hasDefault(c.key));

// ------------------------------------------------------------ the .lapinmapping file

export interface LayerEntry { layer: string; converter: string | null; options: Record<string, unknown> }
export type MappingFile =
  | { app: 'lapin'; kind: 'table'; version: 1; target: string; mapping: MapEntry[] }
  | { app: 'lapin'; kind: 'layers'; version: 1; layers: LayerEntry[] };

export const tableMappingText = (target: string, headers: string[], mapping: MapItem[]): string =>
  JSON.stringify({ app: 'lapin', kind: 'table', version: 1, target, mapping: entriesOf(headers, mapping) } satisfies MappingFile, null, 2);

export const layerMappingText = (layers: LayerEntry[]): string =>
  JSON.stringify({ app: 'lapin', kind: 'layers', version: 1, layers } satisfies MappingFile, null, 2);

/** A .lapinmapping read; throws a sentence the user can read when it is not one. */
export function parseMappingFile(text: string): MappingFile {
  let j: unknown;
  try { j = JSON.parse(text.replace(/^﻿/, '')); } catch { throw new Error('This is not a Lapin mapping file.'); }
  const o = j as Record<string, unknown> | null;
  if (!o || o.app !== 'lapin' || (o.kind !== 'table' && o.kind !== 'layers')) throw new Error('This is not a Lapin mapping file.');
  if (typeof o.version === 'number' && o.version > 1) throw new Error('This mapping file is from a newer Lapin.');
  if (o.kind === 'table') {
    if (typeof o.target !== 'string' || !Array.isArray(o.mapping)) throw new Error('This is not a Lapin mapping file.');
    const mapping = (o.mapping as unknown[]).flatMap((e): MapEntry[] => {
      const r = e as Record<string, unknown>;
      return r && typeof r.header === 'string' ? [{ header: r.header, key: typeof r.key === 'string' && r.key ? r.key : null, ...(typeof r.unit === 'string' && r.unit ? { unit: r.unit } : {}) }] : [];
    });
    return { app: 'lapin', kind: 'table', version: 1, target: o.target, mapping };
  }
  if (!Array.isArray(o.layers)) throw new Error('This is not a Lapin mapping file.');
  const layers = (o.layers as unknown[]).flatMap((e): LayerEntry[] => {
    const r = e as Record<string, unknown>;
    return r && typeof r.layer === 'string'
      ? [{ layer: r.layer, converter: typeof r.converter === 'string' && r.converter ? r.converter : null, options: r.options && typeof r.options === 'object' ? { ...(r.options as Record<string, unknown>) } : {} }]
      : [];
  });
  return { app: 'lapin', kind: 'layers', version: 1, layers };
}

// ------------------------------------------------------------ drafting layers → converters

export interface ConvOption { key: string; label: string; kind: string; default?: unknown; choices?: string[]; typeClass?: string }

/** The options of a converter at their defaults (a type option left empty: the engine picks the project's first). */
export function optionDefaults(options: ConvOption[]): Record<string, unknown> {
  const out: Record<string, unknown> = {};
  for (const o of options) if (o.default !== undefined && o.default !== null) out[o.key] = o.default;
  return out;
}

/** Options from `given`, kept where the converter has them and the value fits, the rest at their defaults. */
export function mergeOptions(options: ConvOption[], given: Record<string, unknown> | undefined): Record<string, unknown> {
  const out = optionDefaults(options);
  for (const o of options) {
    const v = given?.[o.key];
    if (v === undefined) continue;
    if (o.kind === 'choice' && o.choices && !o.choices.includes(String(v))) continue;
    out[o.key] = v;
  }
  return out;
}

const LAYER_MEMORY = 'lapin.layerMap';
const layerMemory = (): Record<string, { converter: string | null; options: Record<string, unknown> }> => { try { return JSON.parse(localStorage.getItem(LAYER_MEMORY) ?? '{}'); } catch { return {}; } };
export const rememberLayers = (layers: LayerEntry[]): void => {
  try {
    const mem = layerMemory();
    for (const l of layers) mem[l.layer] = { converter: l.converter, options: l.options };
    localStorage.setItem(LAYER_MEMORY, JSON.stringify(mem));
  } catch { /* not kept */ }
};
export const recallLayer = (layer: string): { converter: string | null; options: Record<string, unknown> } | null => layerMemory()[layer] ?? null;
