/**
 * The pure side of line types (docs/plan-user-extensibility.md §3.6): a pattern's dashes (paper mm, dash then gap, repeating)
 * cut into the segments a preview draws, the dash/gap pairs the editor shows, the names offered where a line type is chosen,
 * and what a .lin import does with each type. The engine checks the same limits (2 to 24 values, each above 0 and under 100 mm).
 */

export interface BuiltinLineType { name: string; dashes: number[] }
export interface OwnLineType { id: string; name: string; dashes: number[]; description?: string | null }
export interface LineTypesDto { builtin: BuiltinLineType[]; own: OwnLineType[] }

export const MAX_DASH_VALUES = 24;

/** Where the dashes of a pattern fall along a line of `length` mm, as [from, to] pairs (the gaps are what lies between). */
export function dashSegments(dashes: number[], length: number): [number, number][] {
  const out: [number, number][] = [];
  const total = dashes.reduce((a, b) => a + b, 0);
  if (dashes.length < 2 || !(total > 0) || !(length > 0)) return length > 0 ? [[0, length]] : [];
  let x = 0;
  let i = 0;
  // A runaway pattern (a hundredth of a millimetre over a metre) is cut short rather than drawn.
  while (x < length && out.length < 2000) {
    const step = dashes[i % dashes.length];
    if (i % 2 === 0) out.push([x, Math.min(length, x + step)]);
    x += step;
    i++;
  }
  return out;
}

/** The dashes as editable rows: a dash and the gap after it; an odd list gets a gap of 1 mm at its end. */
export function toPairs(dashes: number[]): [number, number][] {
  const pairs: [number, number][] = [];
  for (let i = 0; i < dashes.length; i += 2) pairs.push([dashes[i], dashes[i + 1] ?? 1]);
  return pairs;
}

export const fromPairs = (pairs: [number, number][]): number[] => pairs.flat();

/** A new pair added after the last (as long as the engine takes it), the last one's values. */
export function addPair(pairs: [number, number][]): [number, number][] {
  if (pairs.length * 2 + 2 > MAX_DASH_VALUES) return pairs;
  const last = pairs[pairs.length - 1] ?? [3, 1.5];
  return [...pairs, [last[0], last[1]]];
}

export const removePair = (pairs: [number, number][], at: number): [number, number][] => (pairs.length <= 1 ? pairs : pairs.filter((_, i) => i !== at));

export function setPairValue(pairs: [number, number][], at: number, part: 0 | 1, value: number): [number, number][] {
  return pairs.map((p, i) => (i === at ? (part === 0 ? [value, p[1]] : [p[0], value]) as [number, number] : p));
}

/** Whether every value is one the engine takes: above 0 and under 100 mm. */
export const validDashes = (dashes: number[]): boolean =>
  dashes.length >= 2 && dashes.length <= MAX_DASH_VALUES && dashes.every((d) => Number.isFinite(d) && d > 0 && d < 100);

/** Dashes typed as numbers separated by spaces, commas or semicolons (a comma may be the decimal mark when alone); null when they do not read. */
export function parseDashes(text: string): number[] | null {
  const parts = text.trim().split(/[\s;]+/).filter(Boolean).flatMap((p) => (/^\d+,\d+$/.test(p) ? [p.replace(',', '.')] : p.split(',').filter(Boolean)));
  const nums = parts.map((p) => Number(p));
  return nums.length >= 2 && validDashes(nums) ? nums : null;
}

export const dashesText = (dashes: number[]): string => dashes.map((d) => String(Math.round(d * 1000) / 1000)).join('  ');

/** The names a line type is chosen from: continuous, Lapin's, then the project's own (each once). */
export function lineTypeNames(lt: LineTypesDto | null, base: string[] = ['continuous']): string[] {
  const names = [...base];
  for (const n of [...(lt?.builtin ?? []).map((b) => b.name), ...(lt?.own ?? []).map((o) => o.name)]) if (!names.includes(n)) names.push(n);
  return names;
}

/** `value` among `names`: a name the lists no longer have (a removed type) stays in the list so the select still shows it. */
export const withCurrent = (names: string[], value: string | null | undefined): string[] => (value && !names.includes(value) ? [value, ...names] : names);

/** The line types a .lin import brings that differ from the project's own (the ones that ask what to do). */
export interface ReadLineType { name: string; description?: string | null; complex: boolean; builtin: boolean; dashes: number[]; exists: boolean; same: boolean }
export const lineConflicts = (read: ReadLineType[]): ReadLineType[] => read.filter((l) => l.exists && !l.same);

/** Everything the import takes by default: the new ones and those that match are not asked about. */
export const defaultLineChoices = (read: ReadLineType[]): Record<string, string> => Object.fromEntries(lineConflicts(read).map((l) => [l.name, 'update']));

/** A pattern picker's [value, label] choices with the project's own line types after Lapin's. */
export function patternChoices(base: [string, string][], lt: LineTypesDto | null, current?: string | null): [string, string][] {
  const have = new Set(base.map(([k]) => k));
  const extra = (lt?.own ?? []).map((o) => o.name).filter((n) => !have.has(n));
  if (current && !have.has(current) && !extra.includes(current)) extra.push(current);
  return [...base, ...extra.map((n) => [n, n] as [string, string])];
}
