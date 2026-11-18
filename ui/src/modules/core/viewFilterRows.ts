/**
 * A view's filter row (docs/plan-material-graphics.md, stage 5): the old fields (shown, colour, halftone, transparency, pen, pattern)
 * and the hatch and fill fields of decision 683. `view.visibility` lists the fields the engine knows (`filterFields`); the engine
 * refuses any other, so a row is sent with only those.
 */

export interface CutLine { color?: string; weight?: number }
export interface FilterRow {
  id: string; hidden?: boolean; color?: string; halftone?: boolean; transparency?: number; pen?: number; pattern?: string;
  /** A pattern name, or `none` for no hatch. */ cutHatch?: string;
  /** `#rrggbb`, or `none` for no fill. */ cutFill?: string;
  hatchColor?: string; hatchHalftone?: boolean; cutLine?: CutLine;
  surfaceHatch?: string; surfaceFill?: string;
}

/** The fields the engine knew when this was written (used until `view.visibility` says which it knows). */
export const KNOWN_FILTER_FIELDS = ['id', 'hidden', 'color', 'halftone', 'transparency', 'pen', 'pattern', 'cutHatch', 'cutFill', 'hatchColor', 'hatchHalftone', 'cutLine', 'surfaceHatch', 'surfaceFill'];

/** Whether the engine takes a field (all the known ones when it did not say). */
export const fieldAllowed = (fields: readonly string[] | undefined, field: string): boolean => (fields ?? KNOWN_FILTER_FIELDS).includes(field);

/** A row as `view.graphics` takes it: nothing undefined, an empty cut line left out, and no field the engine does not know. */
export function filterRowPayload(row: FilterRow, fields?: readonly string[]): Record<string, unknown> {
  const out: Record<string, unknown> = {};
  for (const [k, v] of Object.entries(row)) {
    if (v === undefined || v === null || !fieldAllowed(fields, k)) continue;
    if (k === 'cutLine') {
      const l = Object.fromEntries(Object.entries(v as CutLine).filter(([, x]) => x !== undefined && x !== null));
      if (Object.keys(l).length) out[k] = l;
      continue;
    }
    out[k] = v;
  }
  return out;
}

/** The whole list as `view.graphics` takes it. */
export const filtersPayload = (rows: readonly FilterRow[], fields?: readonly string[]): Record<string, unknown>[] => rows.map((r) => filterRowPayload(r, fields));

/** A row with its cut line's colour or weight changed (undefined clears it; an empty line goes). */
export function withCutLine(row: FilterRow, patch: Partial<CutLine>): FilterRow {
  const line: CutLine = { ...row.cutLine, ...patch };
  for (const k of Object.keys(line) as (keyof CutLine)[]) if (line[k] === undefined) delete line[k];
  const { cutLine: _gone, ...rest } = row;
  void _gone;
  return Object.keys(line).length ? { ...rest, cutLine: line } : rest;
}

/** What a fill or a hatch cell shows: not set, none (the filter takes it away), or a value. */
export type CellMode = 'unset' | 'none' | 'value';
export const cellMode = (v: string | undefined): CellMode => (v === undefined || v === '' ? 'unset' : v === 'none' ? 'none' : 'value');

/** The value a cell takes when its mode is chosen (`last`: the value it had, or a default). */
export function cellValue(mode: CellMode, last: string | undefined, fallback: string): string | undefined {
  if (mode === 'unset') return undefined;
  if (mode === 'none') return 'none';
  return last && last !== 'none' ? last : fallback;
}
