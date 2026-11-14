/**
 * The colour picker's swatches (forum topic 97): the colours the user chose lately (recent, automatic), their own palettes
 * (named lists they create, rename, reorder and delete; kept on this computer like the other preferences, `lapin.colours`),
 * and the project's palette (kept in the project, `project.setColourPalette`). Colours are #rrggbb, lower case, each once in a
 * list. The functions here are pure (they return the next list); the store at the end keeps and persists them.
 */

import { create } from 'zustand';

const HEX6 = /^#[0-9a-f]{6}$/i;
const HEX3 = /^#[0-9a-f]{3}$/i;
const HEX8 = /^#[0-9a-f]{8}$/i;

/** A colour as #rrggbb (lower case): from #rgb, #rrggbb, or #rrggbbaa (the opacity left off); null when it is not one. */
export function normalise(c: unknown): string | null {
  if (typeof c !== 'string') return null;
  const t = c.trim();
  if (HEX6.test(t)) return t.toLowerCase();
  if (HEX3.test(t)) return `#${t[1]}${t[1]}${t[2]}${t[2]}${t[3]}${t[3]}`.toLowerCase();
  if (HEX8.test(t)) return t.slice(0, 7).toLowerCase();
  return null;
}

/** A value with an opacity (#rrggbbaa) split: its colour for the picker and the opacity to give back (empty when it has none). */
export function splitAlpha(value: string): { rgb: string; alpha: string } {
  return HEX8.test(value) ? { rgb: value.slice(0, 7), alpha: value.slice(7) } : { rgb: value, alpha: '' };
}

/** The colours of a list, cleaned: valid, lower case, each once, in order. */
export function cleanColours(raw: unknown): string[] {
  const out: string[] = [];
  if (Array.isArray(raw)) for (const c of raw) { const n = normalise(c); if (n && !out.includes(n)) out.push(n); }
  return out;
}

export const RECENT_MAX = 16;

/** The recent colours with `c` first (an earlier copy of it moved up), at most {@link RECENT_MAX}. */
export function pushRecent(recent: readonly string[], c: string): string[] {
  const n = normalise(c);
  if (!n) return [...recent];
  return [n, ...recent.filter((x) => x !== n)].slice(0, RECENT_MAX);
}

export interface UserPalette { id: string; name: string; colours: string[] }

/** The palettes as kept: each with a name and its colours; anything unreadable left out. */
export function readPalettes(raw: unknown): UserPalette[] {
  if (!Array.isArray(raw)) return [];
  const out: UserPalette[] = [];
  for (const p of raw) {
    if (!p || typeof p !== 'object') continue;
    const o = p as Record<string, unknown>;
    const name = typeof o.name === 'string' && o.name.trim() ? o.name.trim() : null;
    if (!name) continue;
    const id = typeof o.id === 'string' && o.id && !out.some((x) => x.id === o.id) ? o.id : `p${out.length + 1}-${Math.random().toString(36).slice(2, 7)}`;
    out.push({ id, name, colours: cleanColours(o.colours) });
  }
  return out;
}

const withPalette = (list: readonly UserPalette[], id: string, f: (p: UserPalette) => UserPalette): UserPalette[] => list.map((p) => (p.id === id ? f(p) : p));

/** A new, empty palette at the end; the name made different from the others' when it repeats one. */
export function createPalette(list: readonly UserPalette[], name: string, id: string = `p${Date.now().toString(36)}${Math.random().toString(36).slice(2, 5)}`): UserPalette[] {
  return [...list, { id, name: uniqueName(list, name.trim() || 'Palette'), colours: [] }];
}

function uniqueName(list: readonly UserPalette[], name: string, except?: string): string {
  const taken = new Set(list.filter((p) => p.id !== except).map((p) => p.name.toLowerCase()));
  if (!taken.has(name.toLowerCase())) return name;
  for (let i = 2; ; i++) if (!taken.has(`${name} ${i}`.toLowerCase())) return `${name} ${i}`;
}

export const renamePalette = (list: readonly UserPalette[], id: string, name: string): UserPalette[] =>
  name.trim() ? withPalette(list, id, (p) => ({ ...p, name: uniqueName(list, name.trim(), id) })) : [...list];
export const deletePalette = (list: readonly UserPalette[], id: string): UserPalette[] => list.filter((p) => p.id !== id);

/** The palette moved one place up (−1) or down (+1). */
export function movePalette(list: readonly UserPalette[], id: string, by: -1 | 1): UserPalette[] {
  const i = list.findIndex((p) => p.id === id), j = i + by;
  if (i < 0 || j < 0 || j >= list.length) return [...list];
  const next = [...list];
  [next[i], next[j]] = [next[j], next[i]];
  return next;
}

/** Colours added to the end of a list, those it has already left out. */
export const addColours = (colours: readonly string[], add: readonly string[]): string[] => cleanColours([...colours, ...add]);
export const addColour = (list: readonly UserPalette[], id: string, c: string): UserPalette[] => withPalette(list, id, (p) => ({ ...p, colours: addColours(p.colours, [c]) }));
export const removeColour = (colours: readonly string[], index: number): string[] => colours.filter((_, i) => i !== index);

/** The colour at `from` moved to `to` (both clamped into the list). */
export function moveColour(colours: readonly string[], from: number, to: number): string[] {
  if (from < 0 || from >= colours.length) return [...colours];
  const next = [...colours];
  const [c] = next.splice(from, 1);
  next.splice(Math.max(0, Math.min(next.length, to)), 0, c);
  return next;
}

/** The colours of the materials (in use only, by default), each once, in the list's order. */
export function coloursOfMaterials(rows: readonly { color?: string | null; inUse?: boolean }[], inUseOnly = true): string[] {
  return cleanColours(rows.filter((r) => !inUseOnly || r.inUse).map((r) => r.color));
}

interface Kept { recent: string[]; palettes: UserPalette[] }
const KEY = 'lapin.colours';

export function readKept(stored: unknown): Kept {
  const o = (stored && typeof stored === 'object' ? stored : {}) as Record<string, unknown>;
  return { recent: cleanColours(o.recent).slice(0, RECENT_MAX), palettes: readPalettes(o.palettes) };
}

function read(): Kept {
  try { return readKept(JSON.parse(localStorage.getItem(KEY) ?? 'null')); } catch { return readKept(null); }
}

export const useColours = create<Kept>(() => read());

function keep(patch: Partial<Kept>): void {
  useColours.setState(patch);
  try { localStorage.setItem(KEY, JSON.stringify(useColours.getState())); } catch { /* kept for this session */ }
}

export const rememberColour = (c: string): void => keep({ recent: pushRecent(useColours.getState().recent, c) });
export const setPalettes = (next: UserPalette[]): void => keep({ palettes: next });
