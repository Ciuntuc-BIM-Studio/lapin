/**
 * The unified Catalog (decision 263, stage 4b; docs/plan-user-extensibility.md §4.1): beside the project's types, what can be
 * brought into the project from every source — Lapin's own, this computer's, the firm's folders and the installed
 * extensions — for each kind of library (types, symbols, line types, standards, rules, bar shapes, parameters, hatches, families).
 * One list, fed by per-kind adapters: each turns what a source holds into entries (a name, what it is, where it comes from and
 * under which licence, a picture where the library has one) with the action that adds it to the project.
 */

import { libraryKind } from '../platform/libraries';
import { readTypePack } from '../platform/extensions/packs';
import { parsePack, type SymDef, type SymPreview } from '../modules/core/symbols/symbolModel';

export type CatalogSource = 'builtin' | 'computer' | 'firm' | 'extension';
export const SOURCES: { id: CatalogSource; label: string }[] = [
  { id: 'builtin', label: 'Lapin' }, { id: 'computer', label: 'This computer' }, { id: 'firm', label: 'Firm folders' }, { id: 'extension', label: 'Extensions' },
];

export interface CatalogEntry {
  key: string;
  /** The library kind: the file's extension (`lapinsymbols`, `lin`…). */
  kind: string;
  name: string;
  /** What it is, in a few words (its category, the names in the file). */
  detail?: string;
  source: CatalogSource;
  /** The folder's name, the extension's name. */
  from?: string;
  /** An SPDX expression, for what an extension brought. */
  license?: string;
  preview?: SymPreview | null;
  /** Adds it to the project (or, for hatch patterns, to the project's materials). */
  add?: () => Promise<unknown>;
}

/** The names a file holds, shortly: the first few and how many more. */
export function namesText(names: string[], max = 4): string {
  if (names.length <= max) return names.join(', ');
  return `${names.slice(0, max).join(', ')} +${names.length - max}`;
}

/** One entry for a library file (a rule pack, a .lin, a bar shapes file…), its names read by its kind; a file that is not one is left out with the reason. */
export function fileEntry(f: { kind: string; name: string; text: string; source: CatalogSource; from?: string; license?: string; file?: string }, add?: (text: string, file: string) => Promise<unknown>): CatalogEntry | { problem: string } {
  const k = libraryKind(f.kind);
  if (!k) return { problem: `${f.name}: not a library file this edition reads.` };
  let names: string[];
  try { names = k.names(f.text); } catch (e) { return { problem: `${f.name}: ${e instanceof Error ? e.message : String(e)}` }; }
  const bring = add ?? k.bring;
  const file = f.file ?? `${f.name}.${f.kind}`;
  return {
    key: `${f.source}:${f.kind}:${f.from ?? ''}:${f.name}`, kind: f.kind, name: f.name, detail: namesText(names), source: f.source,
    ...(f.from ? { from: f.from } : {}), ...(f.license ? { license: f.license } : {}), ...(bring ? { add: () => bring(f.text, file) } : {}),
  };
}

/** The symbols of a .lapinsymbols as entries, one each, by category. */
export function symbolEntries(text: string, src: { source: CatalogSource; from?: string; license?: string }, add: (s: { name: string; def: SymDef }) => Promise<unknown>): CatalogEntry[] {
  return (parsePack(text) ?? []).map((s) => ({
    key: `${src.source}:lapinsymbols:${src.from ?? ''}:${s.name}`, kind: 'lapinsymbols', name: s.name, detail: s.def.category, source: src.source,
    ...(src.from ? { from: src.from } : {}), ...(src.license ? { license: src.license } : {}), add: () => add(s),
  }));
}

/** The types of a .lapintypes as entries, one each; adding one brings that type (and what it is made of). */
export function typeEntries(text: string, src: { source: CatalogSource; from?: string; license?: string; pack: string }, add: (ref: string) => Promise<unknown>): CatalogEntry[] {
  let pack;
  try { pack = readTypePack(new TextEncoder().encode(text)); } catch { return []; }
  return pack.types.map((t) => ({
    key: `${src.source}:lapintypes:${src.pack}:${t.ref}`, kind: 'lapintypes', name: t.name, detail: `${t.class.replace(/^(Ifc|Lapin)/, '').replace(/Type$/, '')} · ${src.pack}`, source: src.source,
    ...(src.from ? { from: src.from } : {}), ...(src.license ? { license: src.license } : {}), add: () => add(t.ref),
  }));
}

/** The entries matching a search (name, detail, source's name), a kind and a source, by name. */
export function filterEntries(list: CatalogEntry[], f: { search?: string; kind?: string | null; source?: CatalogSource | null }): CatalogEntry[] {
  const words = (f.search ?? '').toLowerCase().split(/\s+/).filter(Boolean);
  return list
    .filter((e) => (!f.kind || e.kind === f.kind) && (!f.source || e.source === f.source))
    .filter((e) => { const t = `${e.name} ${e.detail ?? ''} ${e.from ?? ''}`.toLowerCase(); return words.every((w) => t.includes(w)); })
    .sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));
}

/** How many entries each kind has. */
export function countByKind(list: CatalogEntry[]): Record<string, number> {
  const out: Record<string, number> = {};
  for (const e of list) out[e.kind] = (out[e.kind] ?? 0) + 1;
  return out;
}
