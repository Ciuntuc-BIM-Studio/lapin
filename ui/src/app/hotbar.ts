/**
 * The quick tools 1–9 the user chooses (decision 259): nine slots, each a
 * tool's id or empty, one set per workspace (an architect's walls and doors,
 * an engineer's columns and beams). A workspace no one has set takes the
 * modules' own quick tools in their order. A tool stands in one slot only:
 * put in another, it leaves the first. Set by dragging a ribbon tool onto a
 * slot, by dragging one slot onto another (they swap), from a slot's menu,
 * or in View → Quick tools…; kept per user in the browser's storage.
 *
 * A slot may hold the tool with one of its types (decision 260): „Wall ·
 * Brick 285”. The type is kept by its name, since the slots are the user's
 * and the types the project's: pressed, the slot makes the project's type of
 * that name the tool's current one; a project without it leaves the tool's.
 */

import { create } from 'zustand';

export const SLOTS = 9;

/** What a ribbon tool carries when dragged onto a quick slot, and what a slot carries onto another. */
export const TOOL_DRAG = 'application/x-lapin-tool';
export const SLOT_DRAG = 'application/x-lapin-slot';

/** Nine slots: a tool's id (with `|` and a type's name after it), or null for an empty one. */
export type Slots = (string | null)[];

/** The slots as kept: per workspace, only those the user set. */
export type Hotbars = Record<string, Slots>;

/** A slot's tool and the name of its type, if it has one. */
export function parseSlot(slot: string | null): { tool: string; type: string | null } | null {
  if (!slot) return null;
  const at = slot.indexOf('|');
  return at < 0 ? { tool: slot, type: null } : { tool: slot.slice(0, at), type: slot.slice(at + 1) || null };
}

/** A slot holding a tool, with a type's name or without. */
export const slotOf = (tool: string, type?: string | null): string => (type ? `${tool}|${type}` : tool);

/** The modules' quick tools laid in the slots, the first nine. */
export function defaultSlots(defaults: readonly string[]): Slots {
  return Array.from({ length: SLOTS }, (_, i) => defaults[i] ?? null);
}

/** A workspace's slots: the user's, a tool this edition lacks left empty; else the modules' own. */
export function slotsOf(hotbars: Hotbars, workspace: string, defaults: readonly string[], known: (id: string) => boolean): Slots {
  const own = hotbars[workspace];
  if (!own) return defaultSlots(defaults);
  return Array.from({ length: SLOTS }, (_, i) => { const slot = own[i] ?? null; return slot && known(parseSlot(slot)!.tool) ? slot : null; });
}

/** A tool put in slot `i` (null empties it); the tool (with the same type) leaves any other slot it stood in. */
export function withSlot(slots: Slots, i: number, id: string | null): Slots {
  if (i < 0 || i >= SLOTS) return slots;
  const out = slots.map((s) => (id && s === id ? null : s));
  out[i] = id;
  return out;
}

/** Slot `i`'s tool given a type by its name, or none (null). */
export function withSlotType(slots: Slots, i: number, type: string | null): Slots {
  const s = parseSlot(slots[i] ?? null);
  return s ? withSlot(slots, i, slotOf(s.tool, type)) : slots;
}

/** Two slots swapped. */
export function withSwap(slots: Slots, i: number, j: number): Slots {
  if (i === j || i < 0 || j < 0 || i >= SLOTS || j >= SLOTS) return slots;
  const out = [...slots];
  [out[i], out[j]] = [out[j], out[i]];
  return out;
}

/** How many slots the number keys reach: up to the last one holding a tool. */
export const reach = (slots: Slots): number => slots.reduce((n, s, i) => (s ? i + 1 : n), 0);

/** What was kept, anything unreadable left out. */
export function readHotbars(stored: unknown): Hotbars {
  const out: Hotbars = {};
  if (!stored || typeof stored !== 'object') return out;
  for (const [ws, v] of Object.entries(stored as Record<string, unknown>)) {
    if (!Array.isArray(v)) continue;
    out[ws] = Array.from({ length: SLOTS }, (_, i) => (typeof v[i] === 'string' ? v[i] as string : null));
  }
  return out;
}

const KEY = 'lapin.hotbar';

function readStored(): unknown {
  try { const raw = localStorage.getItem(KEY); return raw ? JSON.parse(raw) : null; } catch { return null; }
}

export const useHotbars = create<Hotbars>(() => readHotbars(readStored()));

/** One workspace's slots set (null: back to the modules' own). */
export function setSlots(workspace: string, slots: Slots | null): void {
  const next = { ...useHotbars.getState() };
  if (slots) next[workspace] = slots; else delete next[workspace];
  useHotbars.setState(next, true);
  try { localStorage.setItem(KEY, JSON.stringify(next)); } catch { /* kept for this session */ }
}
