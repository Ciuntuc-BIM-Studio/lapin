/**
 * A content pack's file read and checked before it goes into its library (docs/plan-extensions.md): each kind's own file
 * format, as Lapin writes it (Export in My furniture, the profile libraries, the title blocks; a .pat as AutoCAD writes it;
 * a price list as the Price lists library keeps it). A file that is not what its kind takes is refused with a sentence and
 * nothing is kept. The catalogue's CI checks the same (lapin-extensions, tools/validate.mjs).
 */

const utf8 = (bytes: Uint8Array): string => {
  try { return new TextDecoder('utf-8', { fatal: true }).decode(bytes).replace(/^﻿/, ''); } catch { throw new Error('The file is not UTF-8 text.'); }
};

function json(bytes: Uint8Array, what: string): Record<string, unknown> {
  let v: unknown;
  try { v = JSON.parse(utf8(bytes)); } catch (e) { throw new Error(e instanceof Error && e.message.startsWith('The file') ? e.message : `The file is not ${what}: it is not JSON.`); }
  if (!v || typeof v !== 'object' || Array.isArray(v)) throw new Error(`The file is not ${what}.`);
  return v as Record<string, unknown>;
}

/** A .lapinlib (my furniture's file, decision 476): its items' names; the engine checks each item as it takes it in. */
export function readFurnitureLibrary(bytes: Uint8Array): { names: string[] } {
  const o = json(bytes, 'a Lapin furniture library');
  if (o.format !== 'lapin-furniture-library') throw new Error('The file is not a Lapin furniture library.');
  if (typeof o.version !== 'number' || o.version > 1) throw new Error('This furniture library was written by a newer Lapin: update Lapin to read it.');
  const items = o.items;
  if (!Array.isArray(items) || items.length === 0) throw new Error('The furniture library has no item.');
  for (const it of items as Record<string, unknown>[]) {
    const type = it?.type as Record<string, unknown> | undefined;
    if (typeof it?.name !== 'string' || !type || typeof type.class !== 'string' || !type.spec || typeof type.spec !== 'object') throw new Error('An item of the furniture library has no name or no type.');
  }
  return { names: (items as { name: string }[]).map((i) => i.name) };
}

export interface ProfileLibraryFile { format: string; version: number; name: string; description?: string | null; material?: string; standard?: string | null; profiles: { name: string; kind: string }[] }

/** A .lapinprofiles (a profile library, docs/plan-profile-libraries.md). */
export function readProfileLibrary(bytes: Uint8Array): ProfileLibraryFile {
  const o = json(bytes, 'a Lapin profile library');
  if (o.format !== 'lapin-profiles') throw new Error('The file is not a Lapin profile library.');
  if (typeof o.version !== 'number' || o.version > 1) throw new Error('This profile library was written by a newer Lapin: update Lapin to read it.');
  if (typeof o.name !== 'string' || !o.name.trim()) throw new Error('The profile library has no name.');
  if (!Array.isArray(o.profiles) || o.profiles.length === 0) throw new Error('The profile library has no profile.');
  if ((o.profiles as Record<string, unknown>[]).some((p) => typeof p?.name !== 'string' || typeof p?.kind !== 'string')) throw new Error('A profile of the library has no name or no kind.');
  return o as unknown as ProfileLibraryFile;
}

/** A .pat file's patterns (its *NAME lines), each with at least one line of its own; the engine reads the rest. */
export function readPatterns(bytes: Uint8Array): { text: string; names: string[] } {
  const text = utf8(bytes);
  const names: string[] = [];
  let lines = 0;
  for (const raw of text.split(/\r?\n/)) {
    const line = raw.replace(/;.*$/, '').trim();
    if (!line) continue;
    if (line.startsWith('*')) {
      if (names.length && lines === 0) throw new Error(`The pattern ${names[names.length - 1]} has no line.`);
      const name = line.slice(1).split(',')[0].trim();
      if (!name) throw new Error('A pattern of the file has no name.');
      names.push(name.toUpperCase());
      lines = 0;
    } else {
      if (!names.length) throw new Error('The file is not a hatch pattern file (.pat): it does not start with a *NAME line.');
      if (!/^-?[\d.]+(\s*,\s*-?[\d.eE+-]+)+$/.test(line)) throw new Error(`A line of the pattern ${names[names.length - 1]} is not numbers: „${line}”.`);
      lines++;
    }
  }
  if (!names.length) throw new Error('The file has no hatch pattern.');
  if (lines === 0) throw new Error(`The pattern ${names[names.length - 1]} has no line.`);
  return { text, names };
}

/** A title block file (Title blocks → Export: `lapin.titleBlock`). */
export function readTitleBlock(bytes: Uint8Array): { format: string; version: number; name: string; template: { items: unknown[] } & Record<string, unknown> } {
  const o = json(bytes, 'a Lapin title block');
  if (o.format !== undefined && o.format !== 'lapin.titleBlock') throw new Error('The file is not a Lapin title block.');
  const t = o.template as Record<string, unknown> | undefined;
  if (typeof o.name !== 'string' || !t || typeof t !== 'object' || !Array.isArray(t.items)) throw new Error('The file is not a Lapin title block.');
  return { format: 'lapin.titleBlock', version: 1, name: o.name, template: t as { items: unknown[] } };
}

export interface PriceRow { c: string; n: string; u: string; m?: number; l?: number; e?: number; t?: number; h?: number; tr?: string; a?: string; k?: string; q?: number }

/** A .lapinprices (a price list as the Price lists library keeps it): `{format: "lapin-price-list", version: 1, name, currency, indicative, rows}`. */
export function readPriceList(bytes: Uint8Array): { name: string; currency: string; rows: PriceRow[] } {
  const o = json(bytes, 'a Lapin price list');
  if (o.format !== 'lapin-price-list') throw new Error('The file is not a Lapin price list.');
  if (typeof o.version !== 'number' || o.version > 1) throw new Error('This price list was written by a newer Lapin: update Lapin to read it.');
  if (typeof o.name !== 'string' || !o.name.trim()) throw new Error('The price list has no name.');
  if (typeof o.currency !== 'string' || !/^[A-Z]{3}$/.test(o.currency)) throw new Error('The price list has no currency (RON, EUR…).');
  if (o.indicative !== true) throw new Error('A price list from an extension says its prices are indicative.');
  if (!Array.isArray(o.rows) || o.rows.length === 0) throw new Error('The price list has no row.');
  const num = (v: unknown) => v === undefined || (typeof v === 'number' && Number.isFinite(v) && v >= 0);
  for (const r of o.rows as Record<string, unknown>[]) {
    if (typeof r?.c !== 'string' || !r.c || typeof r.n !== 'string' || typeof r.u !== 'string') throw new Error('A row of the price list has no code, description or unit.');
    if (!['m', 'l', 'e', 't', 'h', 'q'].every((k) => num(r[k]))) throw new Error(`The row ${r.c} has a price that is not a number.`);
  }
  return { name: o.name, currency: o.currency, rows: o.rows as PriceRow[] };
}

/** A price list written as a .lapinprices (Price lists → Export .lapinprices…): the file the extensions installer reads, always marked indicative. */
export function writePriceList(list: { name: string; currency: string; rows: PriceRow[] }): string {
  return JSON.stringify({ format: 'lapin-price-list', version: 1, name: list.name, currency: list.currency, indicative: true, rows: list.rows }, null, 1);
}

export interface TypePackItem { ref: string; class: string; name: string }

/**
 * A .lapintypes (a type pack: Manage → Type packs… → Export, the engine's types.export): `{format: "lapin-type-pack",
 * version: 1, name?, types: [{ref, class, name, attrs, material?}], resources: [...]}`; its types' names and classes.
 * The engine checks the rest as it brings them in (types.importFrom): each class a kind of type the edition has.
 */
export function readTypePack(bytes: Uint8Array): { name: string | null; types: TypePackItem[]; text: string } {
  const o = json(bytes, 'a Lapin type pack');
  if (o.format !== 'lapin-type-pack') throw new Error('The file is not a Lapin type pack (.lapintypes).');
  if (typeof o.version !== 'number' || o.version > 1) throw new Error('This type pack was written by a newer Lapin: update Lapin to read it.');
  if (!Array.isArray(o.types) || o.types.length === 0) throw new Error('The type pack has no type.');
  const item = (t: Record<string, unknown> | null) => !!t && typeof t.ref === 'string' && !!t.ref && typeof t.class === 'string' && !!t.class
    && typeof t.name === 'string' && !!t.name.trim() && !!t.attrs && typeof t.attrs === 'object' && !Array.isArray(t.attrs);
  const types = o.types as Record<string, unknown>[];
  if (!types.every(item)) throw new Error('Every item of a type pack has a ref, a class, a name and its attrs.');
  if (o.resources !== undefined && (!Array.isArray(o.resources) || !(o.resources as Record<string, unknown>[]).every(item))) throw new Error('Every item of a type pack has a ref, a class, a name and its attrs.');
  if (types.some((t) => !(t.class as string).endsWith('Type'))) throw new Error('A type pack holds types only.');
  return { name: typeof o.name === 'string' && o.name.trim() ? o.name.trim() : null, types: types.map((t) => ({ ref: t.ref as string, class: t.class as string, name: t.name as string })), text: utf8(bytes) };
}

// ------------------------------------------------------------ the libraries' own files (symbols, line types, standards, rules, bar shapes, parameters, mappings)

/** A pack file (JSON) as its kind's engine writes it: its format name and a version Lapin can read. */
function pack(bytes: Uint8Array, what: string, format: string): Record<string, unknown> {
  const o = json(bytes, `a Lapin ${what}`);
  if (o.format !== format) throw new Error(`The file is not a Lapin ${what}.`);
  if (typeof o.version !== 'number' || o.version > 1) throw new Error(`This ${what} was written by a newer Lapin: update Lapin to read it.`);
  return o;
}

const text = (v: unknown): v is string => typeof v === 'string' && v.trim().length > 0;

/** A .lapinsymbols (Symbols → Export): `{format: "lapin-symbols", version: 1, symbols: [{name, def: {items: [...]}}]}`; the symbols' names. */
export function readSymbolPack(bytes: Uint8Array): { name: string | null; names: string[]; text: string } {
  const o = pack(bytes, 'symbol library', 'lapin-symbols');
  const list = o.symbols;
  if (!Array.isArray(list) || list.length === 0) throw new Error('The symbol library has no symbol.');
  for (const s of list as Record<string, unknown>[]) {
    const def = s?.def as Record<string, unknown> | undefined;
    if (!text(s?.name) || !def || typeof def !== 'object' || !Array.isArray(def.items) || def.items.length === 0) throw new Error('A symbol of the library has no name or draws nothing.');
  }
  return { name: text(o.name) ? o.name.trim() : null, names: (list as { name: string }[]).map((s) => s.name.trim()), text: utf8(bytes) };
}

/** A .lin file (AutoCAD's line types): the names of its *NAME lines, each with a pattern line (`A,` then dashes and gaps). */
export function readLineTypes(bytes: Uint8Array): { names: string[]; text: string } {
  const body = utf8(bytes);
  const names: string[] = [];
  let pattern = 0;
  for (const raw of body.split(/\r?\n/)) {
    const line = raw.replace(/;.*$/, '').trim();
    if (!line) continue;
    if (line.startsWith('*')) {
      if (names.length && pattern === 0) throw new Error(`The line type ${names[names.length - 1]} has no pattern.`);
      const name = line.slice(1).split(',')[0].trim();
      if (!name) throw new Error('A line type of the file has no name.');
      names.push(name);
      pattern = 0;
    } else {
      if (!names.length) throw new Error('The file is not a line type file (.lin): it does not start with a *NAME line.');
      if (!/^[Aa]\s*,/.test(line)) throw new Error(`A line of ${names[names.length - 1]} does not start with A, (the pattern line).`);
      pattern++;
    }
  }
  if (!names.length) throw new Error('The file has no line type.');
  if (pattern === 0) throw new Error(`The line type ${names[names.length - 1]} has no pattern.`);
  return { names, text: body };
}

/** A .lapinstandards (Manage → Drafting standards → Export): `{format: "lapin-standards", version: 1, name?, drafting: {...}}`. */
export function readStandards(bytes: Uint8Array): { name: string | null; text: string } {
  const o = pack(bytes, 'drafting standards file', 'lapin-standards');
  if (!o.drafting || typeof o.drafting !== 'object' || Array.isArray(o.drafting)) throw new Error('The standards file has no drafting standards.');
  return { name: text(o.name) ? o.name.trim() : null, text: utf8(bytes) };
}

/** A .lapinrules (Manage → Rules → Export): `{format: "lapin-rules", version: 1, name, indicative: true, …sections}`. Rules from an extension are indicative. */
export function readRulePack(bytes: Uint8Array): { name: string; text: string } {
  const o = pack(bytes, 'rule pack', 'lapin-rules');
  if (!text(o.name)) throw new Error('The rule pack has no name.');
  if (o.indicative === false) throw new Error('A rule pack from an extension says its values are indicative.');
  if (Object.keys(o).filter((k) => !['format', 'version', 'name', 'description', 'indicative'].includes(k)).length === 0) throw new Error('The rule pack has no rules.');
  return { name: o.name.trim(), text: utf8(bytes) };
}

/** A .lapinbarshapes (Bar shapes → Export): `{format: "lapin-bar-shapes", version: 1, shapes: [{code, label, …}]}`; the shapes' codes. */
export function readBarShapes(bytes: Uint8Array): { codes: string[]; text: string } {
  const o = pack(bytes, 'bar shapes file', 'lapin-bar-shapes');
  if (!Array.isArray(o.shapes) || o.shapes.length === 0) throw new Error('The bar shapes file has no shape.');
  for (const s of o.shapes as Record<string, unknown>[]) if (!text(s?.code) || typeof s?.label !== 'string') throw new Error('A shape of the file has no code or label.');
  return { codes: (o.shapes as { code: string; label: string }[]).map((s) => `${s.code} ${s.label}`.trim()), text: utf8(bytes) };
}

/** A .lapinparams (Shared parameters → Export): `{format: "lapin-parameters", version: 1, parameters: [{name, kind, …}]}`; the names. */
export function readParameters(bytes: Uint8Array): { names: string[]; text: string } {
  const o = pack(bytes, 'shared parameters file', 'lapin-parameters');
  if (!Array.isArray(o.parameters) || o.parameters.length === 0) throw new Error('The parameters file has no parameter.');
  for (const p of o.parameters as Record<string, unknown>[]) if (!text(p?.name) || !text(p?.kind)) throw new Error('A parameter of the file has no name or kind.');
  return { names: (o.parameters as { name: string }[]).map((p) => p.name), text: utf8(bytes) };
}

/** A .lapinmapping: a table's columns (`kind: "table"`, its target and the headings' meanings) or a drafting layer map (`kind: "layers"`). */
export function readMapping(bytes: Uint8Array): { kind: 'table' | 'layers'; names: string[]; text: string } {
  const o = json(bytes, 'a Lapin mapping file');
  if (o.app !== 'lapin' || (o.kind !== 'table' && o.kind !== 'layers')) throw new Error('The file is not a Lapin mapping file.');
  if (typeof o.version === 'number' && o.version > 1) throw new Error('This mapping file was written by a newer Lapin: update Lapin to read it.');
  if (o.kind === 'table') {
    if (!text(o.target) || !Array.isArray(o.mapping) || o.mapping.length === 0) throw new Error('The table mapping has no target or no column.');
    if ((o.mapping as Record<string, unknown>[]).some((e) => typeof e?.header !== 'string')) throw new Error('A column of the mapping has no heading.');
    return { kind: 'table', names: [o.target as string], text: utf8(bytes) };
  }
  if (!Array.isArray(o.layers) || o.layers.length === 0) throw new Error('The layer map has no layer.');
  if ((o.layers as Record<string, unknown>[]).some((e) => typeof e?.layer !== 'string')) throw new Error('A layer of the map has no name.');
  return { kind: 'layers', names: (o.layers as { layer: string }[]).map((l) => l.layer), text: utf8(bytes) };
}

/** A .lapinfamily (a parametric family of Studio): `{format: "lapin-family", version: 1, name, def}`. */
export function readFamily(bytes: Uint8Array): { name: string; text: string } {
  const o = pack(bytes, 'family', 'lapin-family');
  if (!text(o.name)) throw new Error('The family has no name.');
  if (!o.def || typeof o.def !== 'object' || Array.isArray(o.def)) throw new Error('The family has no definition.');
  return { name: o.name.trim(), text: utf8(bytes) };
}
