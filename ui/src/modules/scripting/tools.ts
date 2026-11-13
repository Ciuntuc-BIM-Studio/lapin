/**
 * A script's tools (docs/plan-python-scripting.md §6, stage 4; decision 489), their logic: which scripts give tools and
 * which tools, their ids, and their place in My tools (decision 479). Pure: the store (scriptTools.ts) gives what it read.
 *
 * - **Where from**: the user's own scripts (Mine), a project's scripts **trusted on this computer** (an untrusted one
 *   never puts a button in the user's ribbon), and the scripts installed from Lapin Extensions (installed on purpose:
 *   their tools show, and the first click asks trust).
 * - **What**: the header's tool (`# lapin: tool="…"`, read without running anything; header.ts) and the tools the script
 *   registered with `lapin.ui.tool(...)` the last time the Script panel ran it (said back by the kernel, kept here).
 * - **Their id**: `script:<source>#<name>` (`script:mine:walls.py#Perimeter walls`), so My tools keeps a tool by it:
 *   a script gone (deleted, uninstalled, another project open, its trust taken back) hides its tools, which come back
 *   with it.
 * - **My tools**: a tool seen for the first time is put in the group "Scripts" (made when missing), once; taken out by
 *   the user, it stays out.
 * - **Panels, sizes, menus, palettes** (decision 528): a tool of a panel (`lapin.ui.panel`, the header's `panel=`) goes
 *   into a group of that name instead (made when missing, among the script's other panels in their order); a small one
 *   (`size="small"`) is put small. A menu (`lapin.ui.menu`) is one button whose pulldown lists its tools, each still a
 *   tool of its own (Commands… finds it, Customize My tools… can place it) but not put in My tools by itself. A palette
 *   (`lapin.ui.palette`) is a button opening a floating panel of the window's own (palette.ts reads its rows).
 */

import { addGroup, addTool, has, type MyTab } from '../../app/myTools';
import { missingRequirements, parseHeader, type RequireFacts } from './header';
import { readRows, type PaletteRow } from './palette';

/** Where a script is: its kind and its key for the window (a file name of Mine, a project script's name, an extension's id). */
export interface ToolSource {
  kind: 'mine' | 'project' | 'extension';
  /** `mine:<file>`, `project:<name>`, `extension:<id>`. */
  key: string;
  /** What the user calls it (its file name, its extension's name). */
  label: string;
  /** A project script's entity id; an extension's id. */
  id?: string;
}

/**
 * A tool a script registered with lapin.ui.tool, as the kernel says it (python/lapinbim/_ui.py); with decision 528 also
 * a menu or a palette (`kind`), and where it goes (`panel`, `size`, a tool's `menu`). What older builds kept has none.
 */
export interface RegisteredTool {
  name: string; icon?: string | null; tooltip?: string | null; call?: string | null; main?: boolean;
  kind?: string; panel?: string | null; size?: string | null; menu?: string | null; rows?: unknown;
}

export type ScriptToolKind = 'tool' | 'menu' | 'palette';

/** One tool of a script: its id, where its script is, its name, icon and tooltip, and what its run calls. */
export interface ScriptTool {
  id: string;
  source: ToolSource;
  name: string;
  icon?: string;
  tooltip?: string;
  /** The script's function it calls; null with `main`: the whole script runs. */
  call: string | null;
  main: boolean;
  from: 'header' | 'registered';
  /** A tool, a pulldown of tools (decision 528), or a palette's button. */
  kind: ScriptToolKind;
  /** The group of My tools it goes in the first time (none: "Scripts"). */
  panel?: string;
  /** Put small (a line, three stacked) the first time. */
  small: boolean;
  /** The menu of the script it is listed in (a tool's; only a menu the script has). */
  menu?: string;
  /** A palette's rows. */
  rows?: PaletteRow[];
}

export const TOOL_PREFIX = 'script:';
export const SCRIPTS_GROUP = 'Scripts';

export const sourceKey = (kind: ToolSource['kind'], key: string): string => `${kind}:${key}`;
export const toolId = (source: string, name: string): string => `${TOOL_PREFIX}${source}#${name}`;

const text = (v: unknown, max: number): string | undefined => (typeof v === 'string' && v.trim() ? v.trim().slice(0, max) : undefined);

/** A script's tools: its header's (when this Lapin has what it requires), then those it registered, each name once. */
export function toolsOf(source: ToolSource, code: string, registered: readonly RegisteredTool[] | undefined, facts: RequireFacts): ScriptTool[] {
  const out: ScriptTool[] = [];
  const h = parseHeader(code, source.label);
  if (h?.tool && missingRequirements(h.requires, facts).length === 0)
    out.push({ id: toolId(source.key, h.tool), source, name: h.tool, icon: h.icon, tooltip: h.tooltip, call: h.run ?? null, main: !h.run, from: 'header', kind: 'tool', panel: h.panel, small: h.size === 'small' });
  for (const r of registered ?? []) {
    const name = text(r.name, 120) ?? '';
    if (!name || out.some((t) => t.name === name)) continue;
    const kind: ScriptToolKind = r.kind === 'menu' || r.kind === 'palette' ? r.kind : 'tool';
    const t: ScriptTool = {
      id: toolId(source.key, name), source, name, icon: text(r.icon, 40), tooltip: text(r.tooltip, 300),
      call: kind === 'tool' && typeof r.call === 'string' && /^[A-Za-z_]\w{0,99}$/.test(r.call) ? r.call : null,
      main: kind === 'tool' && r.main === true && !r.call, from: 'registered', kind, panel: text(r.panel, 60), small: r.size === 'small',
    };
    if (kind === 'tool') { const m = text(r.menu, 120); if (m) t.menu = m; }
    if (kind === 'palette') t.rows = readRows(r.rows);
    out.push(t);
  }
  // A tool names only a menu its script has; a menu with no tool is no button.
  const menus = new Set(out.filter((t) => t.kind === 'menu').map((t) => t.name));
  for (const t of out) if (t.menu && !menus.has(t.menu)) delete t.menu;
  const used = new Set(out.flatMap((t) => (t.menu ? [t.menu] : [])));
  return out.filter((t) => t.kind !== 'menu' || used.has(t.name));
}

/** What the store read: the user's scripts (their first lines), the project's (their code and hash), the installed ones. */
export interface ToolInputs {
  mine: readonly { name: string; head?: string }[];
  project: readonly { id: string; name: string; code: string; sha256: string }[];
  extensions: readonly { id: string; name: string; text: string }[];
  /** The hashes of code trusted on this computer (as far as the window knows). */
  trusted: ReadonlySet<string>;
  /** What each script registered with lapin.ui.tool the last time the panel ran it, by its source key. */
  registered: Readonly<Record<string, readonly RegisteredTool[]>>;
  facts: RequireFacts;
}

/** Every script tool now, by its script's source key. */
export function scriptTools(i: ToolInputs): Record<string, ScriptTool[]> {
  const out: Record<string, ScriptTool[]> = {};
  const put = (source: ToolSource, code: string, registered = true) => {
    const tools = toolsOf(source, code, registered ? i.registered[source.key] : undefined, i.facts);
    if (tools.length) out[source.key] = tools;
  };
  for (const f of i.mine) put({ kind: 'mine', key: sourceKey('mine', f.name), label: f.name }, f.head ?? '');
  // A project's script gives tools only when its code is trusted here.
  for (const p of i.project) if (i.trusted.has(p.sha256)) put({ kind: 'project', key: sourceKey('project', p.name), label: p.name, id: p.id }, p.code);
  // An extension's script runs only from its header (the panel never runs it): what it once registered is not kept.
  for (const x of i.extensions) put({ kind: 'extension', key: sourceKey('extension', x.id), label: x.name, id: x.id }, x.text, false);
  return out;
}

/** A tool as My tools first takes it: its id, its panel (none: "Scripts"), whether small, whether it is in a menu (then not put alone). */
export interface NewTool { id: string; panel?: string; small?: boolean; menu?: string }

/** A script tool as My tools first takes it. */
export const placed = (t: ScriptTool): NewTool => ({ id: t.id, panel: t.panel, small: t.small, menu: t.menu });

/**
 * The tools seen for the first time put in My tools: in their panel's group, else in "Scripts" (a group made when
 * missing: after the group of the nearest panel before it in the tools' order, else before the nearest after it, else
 * at the end); small ones small. `added` remembers each one put in, so a tool the user took out stays out. A tool in a
 * menu is reached through its menu: it is not put in alone.
 */
export function addNewTools(tab: MyTab, tools: readonly (string | NewTool)[], added: ReadonlySet<string>): { tab: MyTab; added: Set<string> } {
  const list = tools.map((x) => (typeof x === 'string' ? { id: x } : x));
  const order = [...new Set(list.map((x) => x.panel || SCRIPTS_GROUP))];
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
    const group = groupFor(x.panel || SCRIPTS_GROUP);
    t = addTool(t, x.id, { group }, !x.small);
  }
  return { tab: t, added: next };
}

/** The registered tools as kept, read so that anything unreadable is left out. */
export function readRegistered(stored: unknown): Record<string, RegisteredTool[]> {
  const out: Record<string, RegisteredTool[]> = {};
  if (!stored || typeof stored !== 'object') return out;
  for (const [k, v] of Object.entries(stored as Record<string, unknown>)) {
    if (!/^(mine|project):/.test(k) || !Array.isArray(v)) continue;
    const list = v.filter((x): x is RegisteredTool => !!x && typeof x === 'object' && typeof (x as RegisteredTool).name === 'string').slice(0, 100);
    if (list.length) out[k] = list;
  }
  return out;
}
