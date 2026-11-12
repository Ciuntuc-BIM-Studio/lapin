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
