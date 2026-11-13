/**
 * My tools (decision 479): a ribbon tab of the user's own, made of the tools they take from the whole application, in named
 * groups they create, rename, order and delete; each tool large (icon and name) or small (a line). Put in by right-clicking a
 * tool in the ribbon, by dragging one onto the tab's header, or in the Customize My tools… dialog; reordered by dragging in the
 * ribbon itself or in the dialog. Kept per user in the browser's storage, like the quick tools (decision 259); a tool this
 * edition lacks stays in the file but is not shown, and comes back with its module. The data holds a list of tabs, so more
 * personal tabs can follow; one ships now (`MY_TAB`).
 *
 * Everything here is pure functions on a tab, a store with an undo stack of the last changes, and the file a tab is exported to.
 */

import { create } from 'zustand';

/** The tab's id in the ribbon, and the tab of the one ship now. */
export const MY_TAB = 'my-tools';
/** What a tool dragged inside My tools carries (the id of the tool); the source is kept in `dragged`, since a drag's data cannot be read while it passes over. */
export const MY_DRAG = 'application/x-lapin-my-tool';
export const MY_GROUP_DRAG = 'application/x-lapin-my-group';

export interface MyItem { tool: string; large: boolean }
export interface MyGroup { id: string; label: string; items: MyItem[] }
export interface MyTab { id: string; label: string; groups: MyGroup[] }

/** The group a tool goes in when there is none yet, and the name of one the user makes. */
export const FIRST_GROUP = 'Favourites';
export const NEW_GROUP = 'New group';

let counter = 0;
export const newId = (prefix = 'g'): string => `${prefix}${Date.now().toString(36)}${(counter++).toString(36)}${Math.random().toString(36).slice(2, 5)}`;

export const emptyTab = (id = MY_TAB, label = 'My tools'): MyTab => ({ id, label, groups: [] });

/** Where a tool goes: a group (null: the last one, made if there is none) and a place in it (default the end). */
export interface Place { group: string | null; index?: number }

export const toolsOf = (tab: MyTab): string[] => tab.groups.flatMap((g) => g.items.map((i) => i.tool));
export const has = (tab: MyTab, tool: string): boolean => tab.groups.some((g) => g.items.some((i) => i.tool === tool));
export const findGroup = (tab: MyTab, id: string | null): MyGroup | undefined => (id ? tab.groups.find((g) => g.id === id) : undefined);

/** Where a tool stands: its group's index and its place in it. */
export function locate(tab: MyTab, tool: string): { g: number; i: number } | null {
  for (let g = 0; g < tab.groups.length; g++) {
    const i = tab.groups[g].items.findIndex((x) => x.tool === tool);
    if (i >= 0) return { g, i };
  }
  return null;
}

const clamp = (n: number, lo: number, hi: number) => Math.max(lo, Math.min(hi, n));

const mapGroups = (tab: MyTab, f: (g: MyGroup) => MyGroup): MyTab => ({ ...tab, groups: tab.groups.map(f) });

/** The tab without a tool. */
export const removeTool = (tab: MyTab, tool: string): MyTab => (has(tab, tool) ? mapGroups(tab, (g) => ({ ...g, items: g.items.filter((i) => i.tool !== tool) })) : tab);

/**
 * A tool put in a place. A tool stands once in the tab: one already there is moved, keeping its size; `index` counts places of the
 * target as drawn before the move (dropping a tool just after itself changes nothing, and nothing is lost).
 */
export function addTool(tab: MyTab, tool: string, at: Place = { group: null }, large = true): MyTab {
  const from = locate(tab, tool);
  const keep = from ? tab.groups[from.g].items[from.i] : { tool, large };
  let base = tab;
  let target = findGroup(base, at.group) ?? base.groups[base.groups.length - 1];
  if (!target) {
    target = { id: newId(), label: FIRST_GROUP, items: [] };
    base = { ...base, groups: [target] };
  }
  const tid = target.id;
  let index = at.index ?? target.items.length;
  if (from && base.groups[from.g].id === tid && from.i < index) index--;
  const without = removeTool(base, tool);
  return mapGroups(without, (g) => {
    if (g.id !== tid) return g;
    const items = [...g.items];
    items.splice(clamp(index, 0, items.length), 0, keep);
    return { ...g, items };
  });
}

/** A new group, at the end or at a place among the groups; its id. */
export function addGroup(tab: MyTab, label = NEW_GROUP, index?: number): { tab: MyTab; id: string } {
  const g: MyGroup = { id: newId(), label, items: [] };
  const groups = [...tab.groups];
  groups.splice(clamp(index ?? groups.length, 0, groups.length), 0, g);
  return { tab: { ...tab, groups }, id: g.id };
}

/** A tool put in a new group made for it. */
export function addToNewGroup(tab: MyTab, tool: string, label = NEW_GROUP, large = true): MyTab {
  const { tab: t, id } = addGroup(tab, label);
  return addTool(t, tool, { group: id }, large);
}

export const renameGroup = (tab: MyTab, id: string, label: string): MyTab => (label.trim() ? mapGroups(tab, (g) => (g.id === id ? { ...g, label: label.trim() } : g)) : tab);

/** A group deleted with its tools. */
export const deleteGroup = (tab: MyTab, id: string): MyTab => ({ ...tab, groups: tab.groups.filter((g) => g.id !== id) });

/** A group moved to a place among the groups, counted as drawn before the move. */
export function moveGroup(tab: MyTab, id: string, index: number): MyTab {
  const from = tab.groups.findIndex((g) => g.id === id);
  if (from < 0) return tab;
  const groups = [...tab.groups];
  const [g] = groups.splice(from, 1);
  groups.splice(clamp(from < index ? index - 1 : index, 0, groups.length), 0, g);
  return { ...tab, groups };
}

export const setSize = (tab: MyTab, tool: string, large: boolean): MyTab =>
  mapGroups(tab, (g) => ({ ...g, items: g.items.map((i) => (i.tool === tool && i.large !== large ? { ...i, large } : i)) }));

/** A tool moved a place: up or down within its group, left or right to the end of the group before or the start of the one after. */
export function nudgeTool(tab: MyTab, tool: string, dir: 'up' | 'down' | 'left' | 'right'): MyTab {
  const at = locate(tab, tool);
  if (!at) return tab;
  const g = tab.groups[at.g];
  if (dir === 'up') return at.i > 0 ? addTool(tab, tool, { group: g.id, index: at.i - 1 }) : tab;
  if (dir === 'down') return at.i < g.items.length - 1 ? addTool(tab, tool, { group: g.id, index: at.i + 2 }) : tab;
  const other = tab.groups[at.g + (dir === 'left' ? -1 : 1)];
  if (!other) return tab;
  return addTool(tab, tool, { group: other.id, index: dir === 'left' ? other.items.length : 0 });
}

/** A tool moved one place earlier (-1) or later (1) in the reading order, crossing into the group before or after at its ends. */
export function stepTool(tab: MyTab, tool: string, by: -1 | 1): MyTab {
  const within = nudgeTool(tab, tool, by < 0 ? 'up' : 'down');
  return within !== tab ? within : nudgeTool(tab, tool, by < 0 ? 'left' : 'right');
}

/** A group moved one place earlier (-1) or later (1). */
export function nudgeGroup(tab: MyTab, id: string, by: -1 | 1): MyTab {
  const i = tab.groups.findIndex((g) => g.id === id);
  if (i < 0 || i + by < 0 || i + by >= tab.groups.length) return tab;
  return moveGroup(tab, id, by < 0 ? i - 1 : i + 2);
}

/** The tab as the ribbon shows it: a tool this edition lacks left out (it stays in the data), the user's groups kept even when empty. */
export function visibleTab(tab: MyTab, known: (tool: string) => boolean): MyTab {
  return mapGroups(tab, (g) => ({ ...g, items: g.items.filter((i) => known(i.tool)) }));
}

/** Whether the tab shows no tool at all. */
export const isEmpty = (tab: MyTab): boolean => tab.groups.every((g) => g.items.length === 0);

/** The tabs as kept, read so that anything unreadable is left out and a tool or a group stands once. */
export function readMyTools(stored: unknown): MyTab[] {
  const raw = stored && typeof stored === 'object' ? (stored as { tabs?: unknown }).tabs : null;
  if (!Array.isArray(raw)) return [];
  const out: MyTab[] = [];
  for (const t of raw) {
    const tab = readTab(t);
    if (tab && !out.some((x) => x.id === tab.id)) out.push(tab);
  }
  return out;
}

function readTab(v: unknown, fresh = false): MyTab | null {
  if (!v || typeof v !== 'object') return null;
  const o = v as { id?: unknown; label?: unknown; groups?: unknown };
  if (!Array.isArray(o.groups)) return null;
  const seenTools = new Set<string>(), seenGroups = new Set<string>();
  const groups: MyGroup[] = [];
  for (const g of o.groups) {
    if (!g || typeof g !== 'object') continue;
    const x = g as { id?: unknown; label?: unknown; items?: unknown };
    const label = typeof x.label === 'string' && x.label.trim() ? x.label.trim().slice(0, 60) : NEW_GROUP;
    let id = !fresh && typeof x.id === 'string' && x.id ? x.id : newId();
    if (seenGroups.has(id)) id = newId();
    seenGroups.add(id);
    const items: MyItem[] = [];
    for (const it of Array.isArray(x.items) ? x.items : []) {
      const tool = typeof it === 'string' ? it : it && typeof it === 'object' && typeof (it as { tool?: unknown }).tool === 'string' ? (it as { tool: string }).tool : null;
      if (!tool || seenTools.has(tool)) continue;
      seenTools.add(tool);
      items.push({ tool, large: !(it && typeof it === 'object' && (it as { large?: unknown }).large === false) });
    }
    groups.push({ id, label, items });
  }
  const id = !fresh && typeof o.id === 'string' && o.id ? o.id : MY_TAB;
  return { id, label: typeof o.label === 'string' && o.label.trim() ? o.label.trim().slice(0, 60) : 'My tools', groups };
}

/** The file a tab is exported to: small JSON a colleague imports. */
export const EXPORT_FORMAT = 'lapin-my-tools';

export function exportTab(tab: MyTab): string {
  return JSON.stringify({ format: EXPORT_FORMAT, version: 1, label: tab.label, groups: tab.groups.map((g) => ({ label: g.label, items: g.items.map((i) => ({ tool: i.tool, large: i.large })) })) }, null, 2);
}

/** A tab read from an exported file (new ids, so it never meets the ones in use); null when the text is not one. */
export function importTab(text: string, id = MY_TAB): MyTab | null {
  let v: unknown;
  try { v = JSON.parse(text); } catch { return null; }
  if (!v || typeof v !== 'object' || (v as { format?: unknown }).format !== EXPORT_FORMAT) return null;
  const tab = readTab(v, true);
  return tab ? { ...tab, id } : null;
}

/** A tool as My tools first takes it: its id, its group (none: the default one), whether small, whether it is in a menu (then not put alone). */
export interface NewTool { id: string; panel?: string; small?: boolean; menu?: string }

/**
 * The tools seen for the first time put in My tools (a script's, a graph's): in their own group (`panel`), else in
 * `group` (a group made when missing: after the group of the nearest one before it in the tools' order, else before the
 * nearest after it, else at the end); small ones small. `added` remembers each one put in, so a tool the user took out
 * stays out. A tool in a menu is reached through its menu: it is not put in alone.
 */
export function addNewToolsTo(tab: MyTab, tools: readonly (string | NewTool)[], added: ReadonlySet<string>, group: string): { tab: MyTab; added: Set<string> } {
  const list = tools.map((x) => (typeof x === 'string' ? { id: x } : x));
  const order = [...new Set(list.map((x) => x.panel || group))];
  const next = new Set(added);
  let t = tab;
  const groupFor = (label: string): string => {
    const found = t.groups.find((g) => g.label === label);
    if (found) return found.id;
    const at = order.indexOf(label);
    let index: number | undefined;
    for (let i = at - 1; i >= 0 && index === undefined; i--) { const g = t.groups.findIndex((x) => x.label === order[i]); if (g >= 0) index = g + 1; }
    for (let i = at + 1; i < order.length && index === undefined; i++) { const g = t.groups.findIndex((x) => x.label === order[i]); if (g >= 0) index = g; }
    const made = addGroup(t, label, index);
    t = made.tab;
    return made.id;
  };
  for (const x of list) {
    if (x.menu || next.has(x.id)) continue;
    next.add(x.id);
    if (has(t, x.id)) continue;
    const into = groupFor(x.panel || group);
    t = addTool(t, x.id, { group: into }, !x.small);
  }
  return { tab: t, added: next };
}

// ---- the store

const KEY = 'lapin.myTools';
const UNDO_DEPTH = 50;

function readStored(): unknown {
  try { const raw = localStorage.getItem(KEY); return raw ? JSON.parse(raw) : null; } catch { return null; }
}

interface MyToolsState { tabs: MyTab[]; past: MyTab[][] }

export const useMyTools = create<MyToolsState>(() => ({ tabs: readMyTools(readStored()), past: [] }));

function keep(tabs: MyTab[]): void {
  try { localStorage.setItem(KEY, JSON.stringify({ version: 1, tabs })); } catch { /* kept for this session */ }
}

/** A tab by its id, the first when none is named; an empty one when there is none yet. */
export const myTab = (tabs = useMyTools.getState().tabs, id = MY_TAB): MyTab => tabs.find((t) => t.id === id) ?? emptyTab(id);

/** A change to one tab: kept, and one step more to undo. */
export function changeMyTab(f: (tab: MyTab) => MyTab, id = MY_TAB): void {
  const { tabs, past } = useMyTools.getState();
  const before = myTab(tabs, id);
  const after = f(before);
  if (after === before || JSON.stringify(after) === JSON.stringify(before)) return;
  const next = tabs.some((t) => t.id === id) ? tabs.map((t) => (t.id === id ? after : t)) : [...tabs, after];
  useMyTools.setState({ tabs: next, past: [...past, tabs].slice(-UNDO_DEPTH) });
  keep(next);
}

/** The last change undone; false when there is none. */
export function undoMyTools(): boolean {
  const { past } = useMyTools.getState();
  if (!past.length) return false;
  const tabs = past[past.length - 1];
  useMyTools.setState({ tabs, past: past.slice(0, -1) });
  keep(tabs);
  return true;
}

/** The tab emptied (one step to undo). */
export const resetMyTab = (id = MY_TAB): void => changeMyTab((t) => (t.groups.length ? { ...t, groups: [] } : t), id);

/** The tab replaced by an imported one, as one step to undo; false when the text is not an exported tab. */
export function importMyTab(text: string, id = MY_TAB): boolean {
  const tab = importTab(text, id);
  if (!tab) return false;
  changeMyTab((cur) => ({ ...tab, id, label: cur.label }), id);
  return true;
}

/** The tool being dragged inside the ribbon or the dialog: its id and where it came from (the catalogue, or a group of the tab). */
export type Dragged = { kind: 'tool'; tool: string; from?: 'tab' | 'catalogue' } | { kind: 'group'; id: string } | null;
let dragged: Dragged = null;
export const setDragged = (d: Dragged): void => { dragged = d; };
export const getDragged = (): Dragged => dragged;
