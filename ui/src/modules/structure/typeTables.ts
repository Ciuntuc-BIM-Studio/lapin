/**
 * Structure types from a table (report #135), the logic the import dialog keeps apart from its look: what each field is
 * called, the libraries sent to the engine, how many rows each outcome has.
 */

import type { LibraryEntry } from '../core/profileLibraries/model';

export type ColumnMap = Record<string, { index: number; unit?: string } | null>;
export interface PreviewRow { line: number; name: string; kind: string; section?: string | null; material?: string; mark?: string; bearing?: number; manufacturer?: string; lengths: number[]; status: 'new' | 'same' | 'differs' | 'error'; problem?: string; differs?: string[] }
export interface Preview {
  rows: PreviewRow[]; conflicts: { key: string; name: string; label?: string; differs: string[] }[];
  header: number; headings: string[]; map: ColumnMap; fields: { key: string; dimension: string }[]; units: string[]; mergeable: boolean;
}

/** Each field as the column picker names it. */
export const FIELD_LABELS: Record<string, string> = {
  name: 'Name', kind: 'Kind (beam, column, lintel)', profile: 'Profile', h: 'Height h', b: 'Width b', material: 'Material', mark: 'Mark',
  bearing: 'Bearing', manufacturer: 'Manufacturer', code: 'Code', length: 'Lengths', codes: 'Codes of the lengths',
};

/** The profile libraries the engine resolves a row's profile in besides its own (this computer's and the shared folders'). */
export function librariesArg(entries: LibraryEntry[]): { name: string; profiles: unknown[] }[] {
  return entries.filter((e) => e.origin === 'computer' || e.origin === 'folder').map((e) => ({ name: e.library.name, profiles: e.library.profiles }));
}

/** How many rows are made, the same as the project's, differ from it, or cannot be read. */
export function counts(rows: PreviewRow[]): Record<PreviewRow['status'], number> {
  const o = { new: 0, same: 0, differs: 0, error: 0 };
  for (const r of rows) o[r.status]++;
  return o;
}

/** The map with one field taken from another column (-1: none); a column taken by one field only. */
export function setColumn(map: ColumnMap, field: string, index: number, unit?: string): ColumnMap {
  const o: ColumnMap = {};
  for (const [k, v] of Object.entries(map)) o[k] = v && v.index === index && k !== field ? null : v;
  o[field] = index < 0 ? null : { index, ...(unit ? { unit } : map[field]?.unit ? { unit: map[field]!.unit } : {}) };
  return o;
}
