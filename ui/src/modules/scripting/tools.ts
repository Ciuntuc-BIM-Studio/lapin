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
 */

import { addGroup, addTool, has, type MyTab } from '../../app/myTools';
import { missingRequirements, parseHeader, type RequireFacts } from './header';

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

/** A tool a script registered with lapin.ui.tool, as the kernel says it (python/lapinbim/_ui.py). */
export interface RegisteredTool { name: string; icon?: string | null; tooltip?: string | null; call?: string | null; main?: boolean }

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
}

export const TOOL_PREFIX = 'script:';
export const SCRIPTS_GROUP = 'Scripts';

export const sourceKey = (kind: ToolSource['kind'], key: string): string => `${kind}:${key}`;
export const toolId = (source: string, name: string): string => `${TOOL_PREFIX}${source}#${name}`;

/** A script's tools: its header's (when this Lapin has what it requires), then those it registered, each name once. */
export function toolsOf(source: ToolSource, code: string, registered: readonly RegisteredTool[] | undefined, facts: RequireFacts): ScriptTool[] {
  const out: ScriptTool[] = [];
  const h = parseHeader(code, source.label);
  if (h?.tool && missingRequirements(h.requires, facts).length === 0)
    out.push({ id: toolId(source.key, h.tool), source, name: h.tool, icon: h.icon, tooltip: h.tooltip, call: h.run ?? null, main: !h.run, from: 'header' });
  for (const r of registered ?? []) {
    const name = typeof r.name === 'string' ? r.name.trim().slice(0, 120) : '';
    if (!name || out.some((t) => t.name === name)) continue;
    out.push({
      id: toolId(source.key, name), source, name, icon: r.icon ?? undefined, tooltip: r.tooltip ?? undefined,
      call: typeof r.call === 'string' && /^[A-Za-z_]\w{0,99}$/.test(r.call) ? r.call : null, main: r.main === true && !r.call, from: 'registered',
    });
  }
  return out;
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

/**
 * The tools seen for the first time put in My tools, in the group "Scripts" (made at the end when missing); `added`
 * remembers each one put in, so a tool the user took out stays out.
 */
export function addNewTools(tab: MyTab, ids: readonly string[], added: ReadonlySet<string>): { tab: MyTab; added: Set<string> } {
  const next = new Set(added);
  let t = tab;
  for (const id of ids) {
    if (next.has(id)) continue;
    next.add(id);
    if (has(t, id)) continue;
    let group = t.groups.find((g) => g.label === SCRIPTS_GROUP)?.id;
    if (!group) { const made = addGroup(t, SCRIPTS_GROUP); t = made.tab; group = made.id; }
    t = addTool(t, id, { group });
  }
  return { tab: t, added: next };
}

/** The registered tools as kept, read so that anything unreadable is left out. */
export function readRegistered(stored: unknown): Record<string, RegisteredTool[]> {
  const out: Record<string, RegisteredTool[]> = {};
  if (!stored || typeof stored !== 'object') return out;
  for (const [k, v] of Object.entries(stored as Record<string, unknown>)) {
    if (!/^(mine|project):/.test(k) || !Array.isArray(v)) continue;
    const list = v.filter((x): x is RegisteredTool => !!x && typeof x === 'object' && typeof (x as RegisteredTool).name === 'string').slice(0, 50);
    if (list.length) out[k] = list;
  }
  return out;
}
