/**
 * Hatch patterns made by hand (docs/plan-material-graphics.md, stage 7, decision 684): the line-family editor's model. A family is what
 * a .pat line says (angle, origin, the offset to the next line, dashes), edited as numbers; the commands are `hatch.create`,
 * `hatch.update`, `hatch.delete` (asking `hatch.usage` first) and `drafting.toPattern`.
 */

import type { HatchUnits } from './hatchUnits';

export interface Family { angle: number; origin: [number, number]; deltaX: number; deltaY: number; dashes: number[] }
export interface Tile { width: number; height: number; lines: number[][]; arcs: { c: [number, number]; r: number; start: number; sweep: number }[] }
export type PatternKind = 'solid' | 'lines' | 'tile' | 'batt';

/** `hatch.get`'s answer. */
export interface PatternDef {
  name: string; description: string; units: HatchUnits; kind: PatternKind; project: boolean;
  families?: Family[]; tile?: Tile; uses?: number;
}

/** What the editor holds: a definition being changed (`name` is the saved one; empty for a new pattern). */
export interface Draft { name: string; description: string; units: HatchUnits; families: Family[]; tile?: Tile }

export const MAX_FAMILIES = 64;
export const MAX_DASHES = 24;

export const newFamily = (): Family => ({ angle: 45, origin: [0, 0], deltaX: 0, deltaY: 3, dashes: [] });

/** A new pattern: one continuous line at 45°, 3 mm apart. */
export const newDraft = (units: HatchUnits = 'paper'): Draft => ({ name: '', description: '', units, families: [newFamily()] });

export function draftOf(def: PatternDef): Draft {
  return { name: def.name, description: def.description ?? '', units: def.units === 'model' ? 'model' : 'paper', families: (def.families ?? []).map((f) => ({ ...f, origin: [...f.origin] as [number, number], dashes: [...f.dashes] })), ...(def.tile ? { tile: def.tile } : {}) };
}

// ------------------------------------------------------------ families

export function addFamily(list: readonly Family[]): Family[] {
  if (list.length >= MAX_FAMILIES) return [...list];
  const last = list[list.length - 1];
  return [...list, last ? { ...last, origin: [...last.origin] as [number, number], dashes: [...last.dashes] } : newFamily()];
}

export function removeFamily(list: readonly Family[], i: number): Family[] { return list.filter((_, j) => j !== i); }

export function patchFamily(list: readonly Family[], i: number, patch: Partial<Family>): Family[] { return list.map((f, j) => (j === i ? { ...f, ...patch } : f)); }

/** A family's origin with one of its two numbers changed. */
export function withOrigin(f: Family, axis: 0 | 1, v: number): [number, number] { const o: [number, number] = [f.origin[0], f.origin[1]]; o[axis] = v; return o; }

/** A number typed (a comma or a dot for the decimals); null when it is not one. */
export function parseNum(text: string): number | null {
  const t = text.trim().replace(',', '.');
  if (t === '' || !/^[-+]?(\d+\.?\d*|\.\d+)(e[-+]?\d+)?$/i.test(t)) return null;
  const v = Number(t);
  return Number.isFinite(v) ? v : null;
}

// ------------------------------------------------------------ dashes: positive drawn, negative a gap, zero a dot

export type DashKind = 'dash' | 'gap' | 'dot';
export const dashKind = (v: number): DashKind => (v > 0 ? 'dash' : v < 0 ? 'gap' : 'dot');

/** The list with a dash, a gap or a dot added (lengths in the pattern's units; a dot has none). */
export function addDash(dashes: readonly number[], kind: DashKind, length = 2): number[] {
  if (dashes.length >= MAX_DASHES) return [...dashes];
  return [...dashes, kind === 'dot' ? 0 : kind === 'dash' ? Math.abs(length) : -Math.abs(length)];
}

export function removeDash(dashes: readonly number[], i: number): number[] { return dashes.filter((_, j) => j !== i); }

/** One entry given a length, its kind kept (a dot has no length: it stays a dot). */
export function setDashLength(dashes: readonly number[], i: number, length: number): number[] {
  return dashes.map((d, j) => (j === i ? (d === 0 ? 0 : d > 0 ? Math.abs(length) : -Math.abs(length)) : d));
}

/** One entry made another kind, its length kept. */
export function setDashKind(dashes: readonly number[], i: number, kind: DashKind): number[] {
  return dashes.map((d, j) => (j === i ? (kind === 'dot' ? 0 : (kind === 'dash' ? 1 : -1) * (Math.abs(d) || 2)) : d));
}

/** The dashes as a .pat line gives them: "5 -2 0 -2". */
export const dashText = (dashes: readonly number[]): string => dashes.join(' ');

/** "5 -2 0 -2" (spaces or commas) read back; null when it is not a list of numbers or has too many. */
export function parseDashes(text: string): number[] | null {
  const parts = text.split(/[\s;]+/).filter(Boolean);
  const out: number[] = [];
  for (const p of parts) { const v = parseNum(p); if (v === null) return null; out.push(v); }
  return out.length <= MAX_DASHES ? out : null;
}

// ------------------------------------------------------------ names and checks

/** A pattern's name: one word of letters, digits, _ and -; stored in capitals. */
export const validName = (name: string): boolean => /^[A-Za-z0-9_-]+$/.test(name.trim());
export const patternName = (name: string): string => name.trim().toUpperCase();

/** A free name from `base` (…-2, …-3) among the patterns' names. */
export function freeName(base: string, taken: readonly string[]): string {
  const have = new Set(taken.map((n) => n.toUpperCase()));
  const root = patternName(base) || 'PATTERN';
  if (!have.has(root)) return root;
  for (let i = 2; ; i++) if (!have.has(`${root}-${i}`)) return `${root}-${i}`;
}

/** What is wrong with a draft (a phrase of the interface), or null. */
export function draftProblem(d: Draft, taken: readonly string[] = [], current?: string): string | null {
  if (!validName(d.name)) return 'A pattern\'s name is one word of letters, digits, _ and -.';
  const upper = patternName(d.name);
  if (taken.some((n) => n.toUpperCase() === upper) && upper !== (current ?? '').toUpperCase()) return 'A pattern of that name exists already.';
  if (d.tile) return null;
  if (d.families.length === 0) return 'Give the pattern at least one line family.';
  for (const f of d.families) {
    if (![f.angle, f.deltaX, f.deltaY, f.origin[0], f.origin[1]].every(Number.isFinite)) return 'A family\'s numbers must be numbers.';
    if (Math.abs(f.deltaY) < 1e-6 && Math.abs(f.deltaX) < 1e-6) return 'A family needs a distance to the next line.';
    if (f.dashes.length > 0 && !(f.dashes.reduce((s, x) => s + Math.abs(x), 0) > 0.01)) return 'A family\'s dashes and gaps must add up to more than 0.01 mm.';
  }
  return null;
}

// ------------------------------------------------------------ the commands' payloads

const definition = (d: Draft) => ({ units: d.units, ...(d.tile ? { tile: d.tile } : { families: d.families.map((f) => ({ angle: f.angle, origin: f.origin, deltaX: f.deltaX, deltaY: f.deltaY, dashes: f.dashes })) }) });

/** `hatch.create`. */
export function createArgs(d: Draft): Record<string, unknown> {
  return { name: patternName(d.name), ...(d.description.trim() ? { description: d.description.trim() } : {}), ...definition(d) };
}

/**
 * `hatch.update` of the saved pattern `was`: a new name when it changed, the description, the units and the lines. A tile pattern is not
 * sent its tile back (it is changed by drawing it again): only its name, description and units.
 */
export function updateArgs(was: string, d: Draft): Record<string, unknown> {
  const next = patternName(d.name);
  const def = d.tile ? { units: d.units } : definition(d);
  return { name: was, ...(next !== patternName(was) ? { newName: next } : {}), description: d.description.trim(), ...def };
}

/** `hatch.preview` of a draft not saved, at the scale chosen: a paper pattern's factor, or a real-size one's view scale (1:n). */
export function previewArgs(d: Draft, scale: number, size: number): Record<string, unknown> {
  return {
    pattern: definition(d), size,
    ...(d.units === 'model' ? { viewScale: scale, scale: 1 } : { scale }),
  };
}

/** The paper millimetres one millimetre of a real-size pattern takes at 1:n. */
export const paperPerMetre = (scale: number): number => (scale > 0 ? 1 / scale : 0);

export const PAPER_SCALES = [0.5, 1, 2, 4];
export const MODEL_SCALES = [20, 50, 100, 200, 500];

/** The note under a preview: what the scale means for the pattern's units. */
export function realSizeNote(units: HatchUnits, scale: number): Note | null { return units === 'model' ? { text: '1 mm of the object is {mm} mm on paper at 1:{scale}.', vars: { mm: +(paperPerMetre(scale)).toPrecision(3), scale } } : null; }

/** A phrase of the interface with its `{name}` places (shown through `phrase`). */
export interface Note { text: string; vars: Record<string, string | number> }

// ------------------------------------------------------------ rename and delete

/** Whether a rename was asked for and how many places follow it (shown beside the name before saving). */
export function renameImpact(was: string, name: string, uses: number): { renamed: boolean; uses: number } {
  return { renamed: patternName(name) !== patternName(was) && patternName(name) !== '', uses };
}

export interface DeleteDeps {
  usage: (name: string) => Promise<number | null>;
  /** Asks the user; the phrase is the interface's, with `{name}` and `{count}` places. */
  confirm: (text: string, vars: Record<string, string | number>) => boolean;
  remove: (name: string, force: boolean) => Promise<boolean>;
}
export type DeleteResult = 'deleted' | 'cancelled' | 'failed';

/**
 * Delete a project pattern: `hatch.usage` first. A pattern nobody names is asked about plainly; one that is used says in how many
 * places, and only an explicit yes sends `force` (the materials lose the hatch, drafted hatches fall back to LINES45).
 */
export async function deletePattern(name: string, d: DeleteDeps): Promise<DeleteResult> {
  const count = await d.usage(name);
  if (count === null) return 'failed';
  if (count > 0) {
    if (!d.confirm('The pattern {name} is named in {count} place(s). Delete it anyway? Materials and standards lose the hatch, and drafted hatches take LINES45.', { name, count })) return 'cancelled';
    return (await d.remove(name, true)) ? 'deleted' : 'failed';
  }
  if (!d.confirm('Delete the pattern {name}?', { name })) return 'cancelled';
  return (await d.remove(name, false)) ? 'deleted' : 'failed';
}

/** What a .pat export left out (tile patterns have no .pat form). */
export function skippedNote(skipped: readonly string[]): Note | null {
  return skipped.length ? { text: 'Left out of the .pat file: {names} (tile patterns have no .pat form).', vars: { names: skipped.join(', ') } } : null;
}

// ------------------------------------------------------------ Save as hatch pattern

export interface Cell { width: number; height: number; origin?: [number, number] }

/** The cell between two clicked corners (the view's metres); null when they are the same line or point. */
export function cellOf(a: { x: number; y: number }, b: { x: number; y: number }): Cell | null {
  const width = Math.abs(b.x - a.x), height = Math.abs(b.y - a.y);
  if (!(width > 1e-9) || !(height > 1e-9)) return null;
  return { width, height, origin: [Math.min(a.x, b.x), Math.min(a.y, b.y)] };
}

/** `drafting.toPattern`; with no cell the linework's bounds. */
export function toPatternArgs(viewId: string, ids: readonly string[], cell: Cell | null, name: string, description: string, units: HatchUnits): Record<string, unknown> {
  return { viewId, ids: [...ids], ...(cell ? { cell: { width: cell.width, height: cell.height, ...(cell.origin ? { origin: cell.origin } : {}) } } : {}), name: patternName(name), ...(description.trim() ? { description: description.trim() } : {}), units };
}

/** How `drafting.toPattern`'s answer reads: exact line families or a tile. */
export function resultNote(r: { kind: 'lines' | 'tile'; families?: number | unknown[] }): Note {
  if (r.kind === 'tile') return { text: 'It became a tile pattern: the cell repeated as drawn.', vars: {} };
  const n = Array.isArray(r.families) ? r.families.length : (r.families ?? 0);
  return { text: 'It became exact line families: {n}.', vars: { n } };
}
