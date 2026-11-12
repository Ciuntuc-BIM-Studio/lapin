/**
 * A script's plug-in header (docs/plan-python-scripting.md §6, stage 4; decision 489): `# lapin:` lines among the comments
 * at the top of a script, each a list of `key=value` (a value in quotes, or bare up to the next comma), read without
 * running anything — by the Script panel (Mine, the project's), by Lapin Extensions (the `script` kind) and by the
 * catalogue's tools (lapin-extensions/tools/lib.mjs):
 *
 *     # lapin: name="Perimeter walls", version="1.1.0", requires="scripting, lapin>=0.1.900"
 *     # lapin: tool="Perimeter walls", icon="square", tooltip="Four walls round a rectangle", run="perimeter_tool"
 *
 * `tool` makes the script a tool in My tools (`tool=yes`: named after the script); `run` names the function the tool calls
 * (without it the tool runs the whole script); `icon` is one of Lapin's or lucide's icons by name, else a letter;
 * `requires` lists modules the edition must have and `lapin>=0.1.N`, the oldest Lapin it works with.
 */

import { compareVersions } from '../../platform/extensions/manifest';

export interface ScriptHeader {
  name?: string;
  version?: string;
  description?: string;
  /** The tool's name, when the script is a tool. */
  tool?: string;
  icon?: string;
  tooltip?: string;
  /** The function the tool calls (a Python name); none: the tool runs the whole script. */
  run?: string;
  requires: string[];
  /** Every key as written (lowercase), the ones above too. */
  all: Record<string, string>;
}

/** How far down a header is looked for: the first lines of comments. */
export const HEADER_LINES = 40;

const PAIR = /\s*([A-Za-z_][\w-]*)\s*=\s*("(?:[^"\\]|\\.)*"|'(?:[^'\\]|\\.)*'|[^,]*)\s*(?:,|$)/y;
const PY_NAME = /^[A-Za-z_][A-Za-z0-9_]{0,99}$/;
const YES = new Set(['yes', 'true', '1', 'on']);
const NO = new Set(['no', 'false', '0', 'off', '']);

function unquote(v: string): string {
  const t = v.trim();
  if (t.length >= 2 && (t[0] === '"' || t[0] === "'") && t[t.length - 1] === t[0]) return t.slice(1, -1).replace(/\\(.)/g, '$1');
  return t;
}

/** The pairs of one `# lapin:` line, in order. */
export function parsePairs(text: string): [string, string][] {
  const out: [string, string][] = [];
  PAIR.lastIndex = 0;
  let at = 0;
  while (at < text.length) {
    PAIR.lastIndex = at;
    const m = PAIR.exec(text);
    if (!m || m[0].length === 0) break;
    out.push([m[1].toLowerCase(), unquote(m[2])]);
    at = PAIR.lastIndex;
  }
  return out;
}

/**
 * The header of a script, from its first lines: the `# lapin:` lines among the comments before its code (a shebang, an
 * encoding line, blank lines and other comments may come between); null when it has none.
 */
export function parseHeader(code: string, fileName?: string): ScriptHeader | null {
  const all: Record<string, string> = {};
  const requires: string[] = [];
  let found = false;
  const lines = code.replace(/^﻿/, '').split(/\r?\n/, HEADER_LINES);
  for (const line of lines) {
    const t = line.trim();
    if (!t) continue;
    if (!t.startsWith('#')) break;
    const m = /^#\s*lapin\s*:(.*)$/i.exec(t);
    if (!m) continue;
    found = true;
    for (const [k, v] of parsePairs(m[1])) {
      if (k === 'requires') requires.push(...v.split(',').map((x) => x.trim()).filter(Boolean));
      else all[k] = v;
    }
  }
  if (!found) return null;
  if (requires.length) all.requires = requires.join(', ');
  const h: ScriptHeader = { requires, all };
  const text = (k: string, max: number) => (all[k]?.trim() ? all[k].trim().slice(0, max) : undefined);
  h.name = text('name', 120);
  h.version = text('version', 40);
  h.description = text('description', 500);
  h.icon = text('icon', 40);
  h.tooltip = text('tooltip', 300);
  const tool = all.tool?.trim();
  if (tool !== undefined && !NO.has(tool.toLowerCase())) {
    h.tool = (YES.has(tool.toLowerCase()) ? h.name ?? fileName?.replace(/\.py$/i, '') ?? 'Script' : tool).slice(0, 120);
  }
  if (all.run && PY_NAME.test(all.run.trim())) h.run = all.run.trim();
  return h;
}

/** What the edition and the app are, for a header's `requires`. */
export interface RequireFacts { version: string; hasModule: (id: string) => boolean }

/** What a header requires that this Lapin lacks (a module, or a newer Lapin), said as the user reads it; empty when nothing. */
export function missingRequirements(requires: readonly string[], facts: RequireFacts): string[] {
  const missing: string[] = [];
  for (const r of requires) {
    const v = /^lapin\s*>=\s*([\d.]+)$/i.exec(r);
    if (v) { if (compareVersions(facts.version, v[1]) < 0) missing.push(`Lapin ${v[1]} or newer`); continue; }
    if (/^lapin$/i.test(r)) continue;
    if (/^[a-z][a-z0-9-]*$/.test(r)) { if (!facts.hasModule(r)) missing.push(`the ${r} module`); continue; }
    missing.push(r);
  }
  return missing;
}
