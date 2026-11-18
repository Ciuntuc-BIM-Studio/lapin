/**
 * Material graphics sets (docs/plan-material-graphics.md, stage 4): a set is an ordered list of entries, each saying which
 * materials it takes (by id, by a word of the name, by category, or all) and what they draw in a cut, a plan and an elevation.
 * The most particular matching entry wins (id, then name, then category, then all), ties to the first in the list.
 */

import { applyGraphicsPatch, type RuleGraphics, type RuleKind } from './materialStandardsModel';

export interface SetMatch { material?: string; materialName?: string; category?: string }
export interface SetEntry { match: SetMatch; section?: RuleGraphics; plan?: RuleGraphics; elevation?: RuleGraphics }
export interface GraphicsSetInfo { id: string; name: string; entries: SetEntry[]; builtIn: boolean; edited: boolean; styles: string[] }
export interface GraphicsSetList { sets: GraphicsSetInfo[]; categories: string[] }

/** What an entry matches by. */
export type MatchMode = 'material' | 'name' | 'category' | 'all';
export const MATCH_MODES: [MatchMode, string][] = [['material', 'A material'], ['name', 'A word in the name'], ['category', 'A category'], ['all', 'Every material']];

export function modeOf(m: SetMatch): MatchMode {
  if (m.material) return 'material';
  if (m.materialName) return 'name';
  if (m.category) return 'category';
  return 'all';
}

/** How particular an entry is: material 3, name 2, category 1, all 0 (the engine's rule). */
export const specificity = (m: SetMatch): number => (m.material ? 3 : m.materialName ? 2 : m.category ? 1 : 0);

/** The match of a mode, keeping what the entry had for it (a material's id, a name, a category), else a placeholder. */
export function matchFor(mode: MatchMode, was: SetMatch, categories: readonly string[]): SetMatch {
  switch (mode) {
    case 'material': return { material: was.material ?? '' };
    case 'name': return { materialName: was.materialName ?? '' };
    case 'category': return { category: was.category ?? categories[0] ?? 'Structure' };
    default: return {};
  }
}

export const emptyEntry = (): SetEntry => ({ match: {}, section: { hatch: null } });

/** An entry as the list reads it. */
export function entryLabel(e: SetEntry, materialName: (id: string) => string | undefined): string {
  if (e.match.material) return materialName(e.match.material) ?? e.match.material;
  if (e.match.materialName) return `“${e.match.materialName}”`;
  if (e.match.category) return `[${e.match.category}]`;
  return '*';
}

export const KINDS_OF_ENTRY: readonly RuleKind[] = ['section', 'plan', 'elevation'];

export function moveEntry(list: SetEntry[], i: number, by: number): SetEntry[] {
  const j = i + by;
  if (i < 0 || i >= list.length || j < 0 || j >= list.length) return list;
  const next = [...list];
  [next[i], next[j]] = [next[j], next[i]];
  return next;
}
export function moveEntryTo(list: SetEntry[], from: number, to: number): SetEntry[] {
  if (from === to || from < 0 || to < 0 || from >= list.length || to >= list.length) return list;
  const next = [...list];
  const [e] = next.splice(from, 1);
  next.splice(to, 0, e);
  return next;
}
export const updateEntry = (list: SetEntry[], i: number, f: (e: SetEntry) => SetEntry): SetEntry[] => list.map((e, k) => (k === i ? f(e) : e));
export const removeEntryAt = (list: SetEntry[], i: number): SetEntry[] => list.filter((_, k) => k !== i);
export function duplicateEntry(list: SetEntry[], i: number): SetEntry[] {
  if (i < 0 || i >= list.length) return list;
  const next = [...list];
  next.splice(i + 1, 0, structuredClone(list[i]));
  return next;
}

/** An entry with a kind's graphics changed by a patch of the material editor's fields (null clears the kind). */
export function patchEntryKind(e: SetEntry, kind: RuleKind, patch: Record<string, unknown> | null): SetEntry {
  const g = applyGraphicsPatch(e[kind], patch);
  const { [kind]: _old, ...rest } = e;
  return g ? { ...rest, [kind]: g } : rest;
}

/** What is wrong with a list of entries before it is sent, or null (the engine checks it too). */
export function entriesProblem(list: SetEntry[]): string | null {
  const i = list.findIndex((e) => (e.match.material !== undefined && e.match.material === '') || (e.match.materialName !== undefined && !e.match.materialName.trim()));
  return i >= 0 ? `Entry ${i + 1} names no material or word: pick one or choose another match.` : null;
}

/** The names of the styles that use a set, for its badge. */
export const usedBy = (set: Pick<GraphicsSetInfo, 'styles'>, styleName: (id: string) => string | undefined): string[] =>
  set.styles.map((id) => styleName(id) ?? id);

/** A copy's name that no other set has. */
export function copyName(name: string, taken: readonly string[]): string {
  const lower = taken.map((t) => t.toLowerCase());
  let n = `${name} (copy)`;
  for (let k = 2; lower.includes(n.toLowerCase()); k++) n = `${name} (copy ${k})`;
  return n;
}
