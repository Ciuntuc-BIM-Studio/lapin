/**
 * Scripts' tools and questions in the window (docs/plan-python-scripting.md §6, stage 4; decision 489): the store that
 * keeps the script tools in My tools (tools.ts says which; platform/extraTools.ts puts them among every tool), runs one
 * when it is pressed (its script's code read, trust asked for a project's or an installed script whose code is not
 * trusted here, then `script.run {tool}`: one Run, one undo step named after the tool), and keeps the questions scripts ask
 * (`lapin.ui.ask`, the host's `script.ask`) for the dialog (AskDialog.tsx) until one window answers. Works with Developer
 * extras off too: a tool is for anyone; the Script panel is the developer's. The desktop's only.
 *
 * Decision 526: a script's menu is a pulldown tool (its tools as `items`, shell/pulldown.ts), a palette a tool that opens
 * or closes its floating panel (PaletteWindow.tsx); a palette's button runs like a tool (one Run, one undo step named after the
 * button), carrying the palette's values (`script.run {tool: {name, call, palette, values}}`).
 */

import { create } from 'zustand';
import { engine, engineInBrowser, useSession } from '../../engine/session';
import { notify } from '../../app/store';
import { usePrefs } from '../../app/prefs';
import { changeMyTab, myTab, removeTool } from '../../app/myTools';
import { replaceExtraTools } from '../../platform/extraTools';
import { hasModule } from '../../platform/brand';
import { scriptRows, useExtensions, type ScriptRow } from '../../platform/extensions/runtime';
import type { ToolDef } from '../../platform/module';
import { readQuestion, type AskQuestion } from './ask';
import { scriptIcon } from './icons';
import { forgetSentSelection, listen, onScriptDeleted, projectScripts, refreshFiles, tellSelection, useScripting } from './store';
import { addNewTools, placed, readRegistered, scriptTools, TOOL_PREFIX, type RegisteredTool, type ScriptTool } from './tools';
import { openPulldown } from '../../shell/pulldown';
import { remember, sha256 } from './trust';

export interface ScriptToolsState {
  /** The questions scripts ask, oldest first: the dialog shows the first. */
  questions: AskQuestion[];
  /** A tool whose script is not trusted here, waiting for the user's answer (Run once / Trust / Cancel); a palette's button pressed. */
  trustAsk: { tool: ScriptTool; code: string; sha: string; button?: PaletteButtonRun } | null;
  /** The scripts' palettes now (decision 526), and the ones open, by id, in the order they were opened. */
  palettes: ScriptTool[];
  open: string[];
}

/** A palette's button pressed: its name, the function it calls, the palette's values as the window holds them. */
export interface PaletteButtonRun { name: string; call: string | null; values: Record<string, unknown> }

export const useScriptTools = create<ScriptToolsState>(() => ({ questions: [], trustAsk: null, palettes: [], open: [] }));

/** A palette opened, or closed when it is open. */
export function togglePalette(id: string): void {
  useScriptTools.setState((s) => ({ open: s.open.includes(id) ? s.open.filter((x) => x !== id) : [...s.open, id] }));
}

const REGISTERED_KEY = 'lapin.scriptTools.registered';
const ADDED_KEY = 'lapin.scriptTools.added';

function readStored<T>(key: string, read: (v: unknown) => T, otherwise: T): T {
  try { const raw = localStorage.getItem(key); return raw ? read(JSON.parse(raw)) : otherwise; } catch { return otherwise; }
}
function store(key: string, v: unknown): void { try { localStorage.setItem(key, JSON.stringify(v)); } catch { /* this session only */ } }

/** What each script registered with lapin.ui.tool the last time the panel ran it (python/lapinbim/kernel.py says it back). */
const registered: Record<string, RegisteredTool[]> = readStored(REGISTERED_KEY, readRegistered, {});
/** The tools put in My tools once (one the user takes out stays out). */
let added: Set<string> = readStored(ADDED_KEY, (v) => new Set(Array.isArray(v) ? v.filter((x): x is string => typeof x === 'string') : []), new Set<string>());
/** The scripts installed from Lapin Extensions. */
let extensions: ScriptRow[] = [];
/** Code known trusted, or not, on this computer, by its hash (asked of the host once). */
const trustKnown = new Map<string, boolean>();
/** The tools now, by id. */
let tools = new Map<string, ScriptTool>();
/** The runs that are tools' (a failure is told even with the panel closed). */
const toolRuns = new Set<number>();

const message = (e: unknown) => (e instanceof Error ? e.message : String(e));

function trustedNow(): Set<string> {
  const s = new Set(useScripting.getState().trust.trusted);
  for (const [sha, yes] of trustKnown) if (yes) s.add(sha);
  return s;
}

/** A script's tools as the ribbon's: a menu's tools its `items` (each a tool of its own too), a palette opening its panel. */
function toToolDefs(list: readonly ScriptTool[]): ToolDef[] {
  const defs = list.map((t): ToolDef => ({
    id: t.id, label: t.name, icon: scriptIcon(t.icon, t.name), tooltip: t.tooltip,
    run: t.kind === 'palette' ? () => togglePalette(t.id) : () => void runTool(t.id),
  }));
  return defs.map((d, i) => {
    const t = list[i];
    if (t.kind !== 'menu') return d;
    const menu: ToolDef = { ...d, items: defs.filter((_, j) => list[j].menu === t.name) };
    menu.run = () => openPulldown(menu);
    return menu;
  });
}

let refreshing: Promise<void> | null = null;
let again = false;

/** The script tools worked out again (and the new ones put in My tools); calls while one runs make one more after it. */
export function refreshTools(): Promise<void> {
  if (refreshing) { again = true; return refreshing; }
  refreshing = (async () => {
    do {
      again = false;
      await refreshOnce();
    } while (again);
  })().finally(() => { refreshing = null; });
  return refreshing;
}

async function refreshOnce(): Promise<void> {
  const project = projectScripts();
  // A project's script gives tools only when trusted here: asked of the host once per code.
  const unknown = project.filter((p) => p.sha256 && !trustKnown.has(p.sha256) && !useScripting.getState().trust.trusted.has(p.sha256));
  await Promise.all(unknown.map(async (p) => {
    trustKnown.set(p.sha256, await engine.call<{ trusted: boolean }>('script.trusted', { sha256: p.sha256 }).then((r) => r.trusted === true, () => false));
  }));
  const s = useScripting.getState();
  const byScript = scriptTools({
    mine: s.files, project, extensions, trusted: trustedNow(), registered,
    facts: { version: useSession.getState().info?.engineVersion ?? '0.0.0', hasModule },
  });
  tools = new Map(Object.values(byScript).flat().map((t) => [t.id, t]));
  replaceExtraTools(TOOL_PREFIX, Object.fromEntries(Object.entries(byScript).map(([k, list]) => [`${TOOL_PREFIX}${k}`, toToolDefs(list)])));
  // A palette whose script is gone (or no longer has it) closes.
  const palettes = [...tools.values()].filter((t) => t.kind === 'palette');
  useScriptTools.setState((s) => ({ palettes, open: s.open.filter((id) => tools.get(id)?.kind === 'palette') }));
  const r = addNewTools(myTab(), [...tools.values()].map(placed), added);
  if (r.added.size !== added.size) {
    added = r.added;
    store(ADDED_KEY, [...added]);
    changeMyTab(() => r.tab);
  }
}

/** A script's tools taken out of My tools (its script deleted or uninstalled): a script made again puts them back. */
function forgetSource(key: string): void {
  const prefix = `${TOOL_PREFIX}${key}#`;
  const gone = [...added].filter((id) => id.startsWith(prefix));
  if (gone.length) {
    for (const id of gone) added.delete(id);
    store(ADDED_KEY, [...added]);
    changeMyTab((t) => gone.reduce((x, id) => removeTool(x, id), t));
  }
  if (registered[key]) { delete registered[key]; store(REGISTERED_KEY, registered); }
}

// ------------------------------------------------------------ running a tool

/** The code of a tool's script as it is now; null when the script is gone. */
async function codeOf(t: ScriptTool): Promise<string | null> {
  switch (t.source.kind) {
    case 'mine': return engine.call<{ code: string }>('script.load', { name: t.source.label }).then((r) => r.code, () => null);
    case 'project': return projectScripts().find((p) => p.name === t.source.label)?.code ?? null;
    case 'extension': return (await scriptRows.all()).find((r) => r.id === t.source.id)?.text ?? null;
  }
}

/**
 * A script tool pressed: its script run as one Run (one undo step named after the tool), after trust for a project's or
 * an installed script. A palette's button (`button`) runs the same way, named after the button, with the palette's values.
 */
export async function runTool(id: string, button?: PaletteButtonRun): Promise<void> {
  const t = tools.get(id);
  if (!t) return;
  if (engineInBrowser) { notify('Python runs in the desktop app.'); return; }
  if (useScripting.getState().running != null) { notify('A script is running: stop it first.'); return; }
  const code = await codeOf(t);
  if (code === null) { notify('The script of this tool is gone.'); return; }
  if (t.source.kind !== 'mine') {
    const sha = await sha256(code);
    if (!trustedNow().has(sha)) {
      const known = await engine.call<{ trusted: boolean }>('script.trusted', { sha256: sha }).then((r) => r.trusted, () => false);
      if (!known) { useScriptTools.setState({ trustAsk: { tool: t, code, sha, button } }); return; }
      trustKnown.set(sha, true);
    }
  }
  await startTool(t, code, button);
}

async function startTool(t: ScriptTool, code: string, button?: PaletteButtonRun): Promise<void> {
  // The window's selection as it is now, for lapin.selection() (told as it changes only with Developer extras on).
  forgetSentSelection();
  tellSelection();
  const name = button?.name ?? t.name;
  useScripting.setState((s) => ({ lines: [...s.lines, { kind: 'info', text: `▶ ${name}` }] }));
  const tool = button ? { name: button.name, call: button.call, main: false, palette: t.name, values: button.values } : { name: t.name, call: t.call, main: t.main };
  try {
    await engine.call('script.run', { code, name, tool, source: t.source.key, python: usePrefs.getState().pythonPath });
  } catch (e) { notify(message(e)); }
}

/** The answer to a tool's trust question: Trust (kept on this computer by the code's hash), Run once, or Cancel. */
export async function answerToolTrust(answer: 'trust' | 'once' | 'cancel'): Promise<void> {
  const a = useScriptTools.getState().trustAsk;
  useScriptTools.setState({ trustAsk: null });
  if (!a || answer === 'cancel') return;
  if (answer === 'trust') {
    try { await engine.call('script.trust', { sha256: a.sha }); } catch (e) { notify(message(e)); return; }
    trustKnown.set(a.sha, true);
    useScripting.setState((s) => ({ trust: remember(s.trust, a.sha, 'trust') }));
  }
  await startTool(a.tool, a.code, a.button);
}

/** Trust in a code taken back on this computer: a project's script's tools leave, an installed one asks again. */
export async function forgetTrust(sha: string): Promise<void> {
  try { await engine.call('script.untrust', { sha256: sha }); } catch (e) { notify(message(e)); return; }
  trustKnown.set(sha, false);
  useScripting.setState((s) => ({ trust: { trusted: new Set([...s.trust.trusted].filter((x) => x !== sha)) } }));
  void refreshTools();
}

/** Whether a code is trusted here, as far as the window knows. */
export const isTrusted = (sha: string): boolean => trustedNow().has(sha);

/** An installed script run from Extensions (Installed → Run): its header's tool, else the whole script. */
export async function runExtension(id: string): Promise<void> {
  const own = [...tools.values()].find((t) => t.source.kind === 'extension' && t.source.id === id);
  if (own) { await runTool(own.id); return; }
  const row = (await scriptRows.all()).find((r) => r.id === id);
  if (!row) return;
  const t: ScriptTool = { id: `${TOOL_PREFIX}extension:${id}#`, source: { kind: 'extension', key: `extension:${id}`, label: row.name, id }, name: row.name, call: null, main: true, from: 'header', kind: 'tool', small: false };
  tools.set(t.id, t);
  await runTool(t.id);
}

// ------------------------------------------------------------ questions (lapin.ui.ask)

/** The answer to a question: the values, or cancelled (null); the dialog closes in every window. */
export async function answer(ask: string, values: Record<string, unknown> | null): Promise<void> {
  useScriptTools.setState((s) => ({ questions: s.questions.filter((q) => q.ask !== ask) }));
  try { await engine.call('script.answer', values ? { ask, values } : { ask, cancel: true }); } catch (e) { notify(message(e)); }
}

// ------------------------------------------------------------ started once, with the module

let started = false;

/** What keeps the tools and questions up to date (the module's background, on the desktop). */
export function startScriptTools(): void {
  if (started || engineInBrowser) return;
  started = true;
  listen();
  engine.onEvent('script.output', (d) => {
    const e = d as { type?: string; id?: number; source?: string; tools?: unknown; ask?: string; tool?: unknown; ok?: boolean; error?: string | null; interrupted?: boolean };
    switch (e.type) {
      case 'tools':
        if (typeof e.source === 'string' && /^(mine|project):/.test(e.source)) {
          const list = readRegistered({ [e.source]: e.tools })[e.source] ?? [];
          if (list.length) registered[e.source] = list; else delete registered[e.source];
          store(REGISTERED_KEY, registered);
          void refreshTools();
        }
        break;
      case 'ask': {
        const q = readQuestion(e);
        if (q) useScriptTools.setState((s) => ({ questions: [...s.questions.filter((x) => x.ask !== q.ask), q] }));
        break;
      }
      case 'asked':
        useScriptTools.setState((s) => ({ questions: s.questions.filter((q) => q.ask !== e.ask) }));
        break;
      case 'start':
        if (e.tool && typeof e.id === 'number') toolRuns.add(e.id);
        break;
      case 'done':
        if (typeof e.id === 'number' && toolRuns.delete(e.id) && !e.ok && !e.interrupted && !(useScripting.getState().open && usePrefs.getState().developerExtras))
          notify(e.error || 'The script failed.');
        break;
    }
  });
  // Connected (again): Mine read, the tools worked out; the questions of a lost engine are gone.
  const connected = async () => { await refreshFiles(); extensions = await scriptRows.all().catch(() => []); await refreshTools(); };
  engine.onStatus((c) => { if (c) void connected(); else useScriptTools.setState({ questions: [] }); });
  if (useSession.getState().connected) void connected();
  // The project's scripts changed (opened, saved, deleted, undone): their tools again.
  let projectSig = '';
  useSession.subscribe((s, was) => {
    if (s.nodes === was.nodes) return;
    const sig = projectScripts(s.nodes).map((p) => `${p.name}\u0000${p.sha256}`).join('\n');
    if (sig !== projectSig) { projectSig = sig; void refreshTools(); }
  });
  useScripting.subscribe((s, was) => { if (s.files !== was.files || s.trust !== was.trust) void refreshTools(); });
  // An extension installed or uninstalled: its script's tools come or go.
  useExtensions.subscribe((s, was) => {
    if (s.installed === was.installed) return;
    const now = new Set(s.installed.filter((x) => x.kind === 'script').map((x) => x.id));
    for (const x of was.installed) if (x.kind === 'script' && !now.has(x.id)) forgetSource(`extension:${x.id}`);
    void scriptRows.all().catch(() => []).then((rows) => { extensions = rows; void refreshTools(); });
  });
  onScriptDeleted((name) => forgetSource(`mine:${name}`));
}
