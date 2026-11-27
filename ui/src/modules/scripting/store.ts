/**
 * The Script Notebook view's state and its calls to the engine's host
 * (docs/plan-python-scripting.md, decision 489; docs/plan-python-notebook.md):
 * manages notebook cells (`# %%` percent format in `.py` files), per-cell & Run-All
 * execution on the persistent lapinbim kernel, the global Terminal (Console REPL +
 * Recorder with Insert as cell), Ollama AI prompt cell generation, and deterministic
 * / AI-assisted Code Cell → Visual Graph conversion.
 */

import { create } from 'zustand';
import { createMessageLog } from '../../platform/messages';
import { runMessages } from './messages';
import { engine, engineInBrowser, execute, query, useSession } from '../../engine/session';
import { usePrefs } from '../../app/prefs';
import { focusPane, notify, useUi } from '../../app/store';
import { foldOutput, own, remember as rememberLine, stepName, type Catalogue, type ConsoleLine, type History, type ScriptEvent } from './logic';
import { Recording, type RecordedCall } from './recorder';
import { asksTrust, EMPTY_TRUST, remember, sha256, type ScriptSource, type TrustState } from './trust';
import {
  codeContextAbove,
  combineCodeCells,
  createCell,
  insertCellAt,
  moveCell,
  parseNotebook,
  removeCell,
  serializeNotebook,
  targetCodeCellIndex,
  type CellKind,
  type NotebookCell,
} from './notebook';
import { generateCellCode, probeOllama } from './ai';
import type { KernelVar } from './contextPack';
import { convertCellToGraphWithAi, pythonToGraph, type UnsupportedLine } from './toGraph';
import { defaultCodeModel, saveSettings, useAssistantSettings, type OllamaStatus } from '../../assistant/ollama';
import type { GraphJson } from '../graphs/graph';
import { openGraph, openPanel as openGraphsPanel, projectGraphs } from '../graphs/store';

export const VIEW_KIND = 'scripting';

export interface ScriptStatus { enabled: boolean; ok: boolean; python?: string | null; version?: string | null; message?: string | null; alive?: boolean; running?: boolean; scripts?: string | null }
/** One of Mine: its name, and (stage 4) its first lines, where its plug-in header is, and the hash of its code. */
export interface ScriptFile { name: string; modified: string; bytes: number; head?: string; sha256?: string }
/** A project's script (a LapinScript entity) as the panel lists it. */
export interface ProjectScript { id: string; name: string; code: string; sha256: string }

export const SCRIPT_CLASS = 'LapinScript';

export interface GraphWarning {
  cellId: string;
  unsupported: UnsupportedLine[];
  graph: GraphJson;
}

export interface ScriptingState {
  open: boolean;
  lines: ConsoleLine[];
  /** The run going on (its id), its name, and the notebook cell it belongs to (null for console / Run All). */
  running: number | null;
  runningName: string | null;
  runningCellId: string | null;
  status: ScriptStatus | null;
  files: ScriptFile[];
  /** The script in the editor: its file in Mine (null: not one of Mine), or the project's script it is (its id), its code, the code as last saved or read. */
  file: string | null;
  projectId: string | null;
  code: string;
  saved: string;
  modified: string | null;
  /** Notebook cells parsed from `code` (kept in sync on every edit). */
  cells: NotebookCell[];
  activeCellId: string | null;
  /** Monotonic counter for cell execution badges `[1]`, `[2]`, … (reset on Restart). */
  execSeq: number;
  /** The line of the last error in the editor's script, to show. */
  errorLine: number | null;
  history: History;
  /** Terminal height inside the notebook view. */
  height: number;
  terminalOpen: boolean;
  libraryOpen: boolean;
  catalogue: Catalogue | null;
  /** The console's tab: the console itself, or the recorder. */
  tab: 'console' | 'recorder';
  recording: boolean;
  recorded: string[];
  recordSelection: boolean;
  trust: TrustState;
  asking: { code: string; sha: string; name: string; cellId?: string | null; echo?: boolean } | null;
  /** Ollama status and AI prompt cell generation state. */
  ollama: OllamaStatus | null;
  aiBusyCellId: string | null;
  aiStatus: string | null;
  /** Pending partial conversion warning when a cell has lines not natively convertible to a graph. */
  graphWarning: GraphWarning | null;
}

export const NEW_SCRIPT = `# %% [markdown]
# ## Lapin Python Notebook
# Run a cell with **Shift+Enter** (one undo step). \`lapin\` is pre-bound to \`lapinbim\`.

# %%
storey = next(s.id for s in lapin.model.storeys if s.elevation == 0)
corners = [(0, 0), (8, 0), (8, 6), (0, 6)]
for (x1, y1), (x2, y2) in zip(corners, corners[1:] + corners[:1]):
    lapin.architecture.add_wall(storey_id=storey, x1=x1, y1=y1, x2=x2, y2=y2, height=2.8)
len(lapin.model.walls)
`;

const HEIGHT_KEY = 'lapin.scriptPanel.terminalHeight';
const readHeight = () => { try { return Math.max(120, Math.min(600, Number(localStorage.getItem(HEIGHT_KEY)) || 200)); } catch { return 200; } };

const initialCells = parseNotebook(NEW_SCRIPT);

export const useScripting = create<ScriptingState>(() => ({
  open: false,
  lines: [],
  running: null,
  runningName: null,
  runningCellId: null,
  status: null,
  files: [],
  file: null,
  projectId: null,
  code: NEW_SCRIPT,
  saved: NEW_SCRIPT,
  modified: null,
  cells: initialCells,
  activeCellId: initialCells[initialCells.length - 1]?.id ?? null,
  execSeq: 0,
  errorLine: null,
  history: { items: [], at: null },
  height: readHeight(),
  terminalOpen: true,
  libraryOpen: true,
  catalogue: null,
  tab: 'console',
  recording: false,
  recorded: [],
  recordSelection: true,
  trust: EMPTY_TRUST,
  asking: null,
  ollama: null,
  aiBusyCellId: null,
  aiStatus: null,
  graphWarning: null,
}));

/** The panel's messages console (decision 538): kept while the panel is closed, so a tool that failed is there when it opens. */
export const scriptLog = createMessageLog('script');

const set = useScripting.setState;
const get = useScripting.getState;

const say = (kind: ConsoleLine['kind'], text: string) => {
  set({ lines: [...get().lines, own(kind, text)] });
  if (kind === 'err') scriptLog.add({ kind: 'error', source: 'Python', text });
};
const message = (e: unknown) => (e instanceof Error ? e.message : String(e));

export function setHeight(h: number): void {
  const height = Math.max(120, Math.min(Math.round(window.innerHeight * 0.7), Math.round(h)));
  set({ height });
  try { localStorage.setItem(HEIGHT_KEY, String(height)); } catch { /* kept for this session */ }
}

/** Map of runId → cellId so kernel events route into the right cell's output list too. */
const runCellMap = new Map<number, string>();
let pendingCellId: string | null = null;

/** The kernel's events into the console & active cell outputs; started once. */
let listening = false;
export function listen(): void {
  if (listening || engineInBrowser) return;
  listening = true;
  const runs = new Map<number, string | null>();
  engine.onEvent('script.output', (d) => {
    const e = d as ScriptEvent;
    const s = get();
    if (typeof e.id === 'number' && e.type === 'start') {
      runs.set(e.id, e.console ? null : e.name ?? 'Script');
      if (pendingCellId && !e.console) {
        runCellMap.set(e.id, pendingCellId);
        pendingCellId = null;
      }
    }
    const row = runMessages(e, typeof e.id === 'number' ? runs.get(e.id) ?? (e.type === 'done' && !e.console ? e.name ?? 'Script' : null) : null);
    if (row.length) scriptLog.add(...row);
    const cellId = typeof e.id === 'number' ? runCellMap.get(e.id) ?? s.runningCellId : s.runningCellId;

    const patch: Partial<ScriptingState> = { lines: foldOutput(s.lines, e) };

    if (cellId && (e.type === 'out' || e.type === 'err' || e.type === 'done')) {
      const cells = s.cells.map((c) => {
        if (c.id !== cellId) return c;
        const outputs = [...(c.outputs ?? [])];
        if (e.type === 'out' && e.text) {
          const last = outputs[outputs.length - 1];
          if (last && last.kind === 'out' && !last.text.endsWith('\n')) {
            outputs[outputs.length - 1] = { ...last, text: last.text + e.text };
          } else outputs.push({ kind: 'out', text: e.text });
          return { ...c, outputs };
        }
        if (e.type === 'err' && e.text) {
          outputs.push({ kind: 'err', text: e.text });
          return { ...c, outputs };
        }
        if (e.type === 'done') {
          if (e.ok) {
            return { ...c, outputs, errorLine: null, lastError: null, stale: false };
          }
          const errText = e.traceback?.trim() || e.error || 'The cell failed.';
          outputs.push({ kind: 'err', text: errText });
          return { ...c, outputs, errorLine: e.line ?? null, lastError: errText };
        }
        return c;
      });
      patch.cells = cells;
    }

    if (e.type === 'start') {
      patch.running = e.id ?? null;
      patch.runningName = e.name ?? null;
    }
    if (e.type === 'done' && typeof e.id === 'number') {
      runs.delete(e.id);
      runCellMap.delete(e.id);
    }
    if (e.type === 'done' && (s.running === e.id || s.running === null)) {
      patch.running = null;
      patch.runningName = null;
      patch.runningCellId = null;
      if (!e.console) patch.errorLine = e.ok ? null : e.line ?? null;
    }
    if (e.type === 'exit') {
      patch.running = null;
      patch.runningName = null;
      patch.runningCellId = null;
      runCellMap.clear();
      pendingCellId = null;
    }
    set(patch);
  });
}

/** Open the Scripting Notebook view beside the current view (split on web/desktop, detachable into its own Tauri window). */
export async function openPanel(): Promise<void> {
  set({ open: true });
  const ui = useUi.getState();
  const at = ui.panes.findIndex((p) => p.view === VIEW_KIND);
  if (at >= 0) {
    focusPane(at);
  } else if (ui.panes.length < 2) {
    const first = ui.panes[0]!;
    useUi.setState({ panes: [first, { ...first, view: VIEW_KIND }], split: 'row', pane: 1 });
    useUi.setState({ view: VIEW_KIND });
  } else {
    focusPane(1);
    useUi.setState({ view: VIEW_KIND });
  }
  if (engineInBrowser) return;
  listen();
  await Promise.all([refreshStatus(), refreshFiles(), readCatalogue(), refreshOllama()]);
}

export const closePanel = () => set({ open: false });

export async function refreshStatus(refresh = false): Promise<void> {
  try { set({ status: await engine.call<ScriptStatus>('script.status', { python: usePrefs.getState().pythonPath, refresh }) }); }
  catch (e) { set({ status: { enabled: false, ok: false, message: message(e) } }); }
}

export async function refreshOllama(): Promise<void> {
  if (engineInBrowser) return;
  try {
    const status = await probeOllama();
    set({ ollama: status });
    const s = useAssistantSettings.getState();
    if (status.running && !s.codeModel && status.models.length) {
      const picked = defaultCodeModel(status.models);
      if (picked) saveSettings({ codeModel: picked });
    }
  } catch {
    set({ ollama: { running: false, models: [], tools: null, error: 'Ollama is not running: start it with `ollama serve` or the Ollama app.' } });
  }
}

async function readCatalogue(): Promise<Catalogue | null> {
  const had = get().catalogue;
  if (had) return had;
  try { const c = await engine.call<Catalogue>('session.catalogue'); set({ catalogue: c }); return c; } catch { return null; }
}

// ------------------------------------------------------------ notebook cell actions

function syncCells(cells: NotebookCell[], activeCellId?: string | null): void {
  const code = serializeNotebook(cells);
  set({
    cells,
    code,
    ...(activeCellId !== undefined ? { activeCellId } : {}),
  });
}

export function setActiveCell(id: string | null): void {
  set({ activeCellId: id });
}

export function updateCell(id: string, source: string): void {
  const cells = get().cells.map((c) =>
    c.id === id ? { ...c, source, stale: c.execCount != null ? true : c.stale } : c,
  );
  syncCells(cells);
}

export function addCell(kind: CellKind, afterIndex?: number, source = ''): NotebookCell {
  const s = get();
  const cell = createCell(kind, source);
  const idx = afterIndex !== undefined ? afterIndex + 1 : s.cells.length;
  const cells = insertCellAt(s.cells, idx, cell);
  syncCells(cells, cell.id);
  return cell;
}

export function deleteCell(id: string): void {
  const s = get();
  const idx = s.cells.findIndex((c) => c.id === id);
  const cells = removeCell(s.cells, id);
  const nextActive = s.activeCellId === id ? (cells[Math.min(idx, cells.length - 1)]?.id ?? null) : s.activeCellId;
  syncCells(cells, nextActive);
}

export function moveCellBy(index: number, delta: -1 | 1): void {
  const cells = moveCell(get().cells, index, delta);
  syncCells(cells);
}

export function clearCellOutputs(id?: string): void {
  const cells = get().cells.map((c) => (!id || c.id === id ? { ...c, outputs: [], errorLine: null, lastError: null } : c));
  set({ cells });
}

// ------------------------------------------------------------ execution

export function sourceOf(s: ScriptingState = get()): ScriptSource {
  if (s.projectId) return { kind: 'project', id: s.projectId, name: projectScript(s.projectId)?.name ?? 'Script' };
  if (s.file) return { kind: 'mine', name: s.file };
  return { kind: 'new' };
}

export function sourceKeyOf(source: ScriptSource): string | undefined {
  return source.kind === 'mine' ? `mine:${source.name}` : source.kind === 'project' ? `project:${source.name}` : undefined;
}

/** Run arbitrary code (console REPL or whole script). A project's script asks trust first. */
export async function run(code: string, console: boolean, opts?: { cellId?: string | null; echo?: boolean; label?: string }): Promise<void> {
  if (engineInBrowser || get().running != null || !code.trim()) return;
  const s = get();
  const name = opts?.label ?? stepName(s.projectId ? projectScript(s.projectId)?.name ?? null : s.file, console);
  if (!console) {
    const source = sourceOf(s);
    if (source.kind === 'project') {
      // Trust is keyed on the whole project script's SHA-256 (same as before).
      const sha = await sha256(s.code);
      if (asksTrust(source, sha, s.trust)) {
        const known = await engine.call<{ trusted: boolean }>('script.trusted', { sha256: sha }).then((r) => r.trusted, () => false);
        if (!known) { set({ asking: { code, sha, name: source.name, cellId: opts?.cellId ?? null, echo: opts?.echo } }); return; }
        set({ trust: remember(get().trust, sha, 'trust') });
      }
    }
  }
  await start(code, name, console, console ? undefined : sourceKeyOf(sourceOf(s)), opts?.cellId ?? null, opts?.echo ?? false);
}

/** Run a single notebook code cell (with `echo: true` and its own execution count). */
export async function runCell(cellId: string): Promise<void> {
  const s = get();
  if (engineInBrowser || s.running != null) return;
  const idx = s.cells.findIndex((c) => c.id === cellId);
  const cell = s.cells[idx];
  if (!cell || cell.kind !== 'code' || !cell.source.trim()) return;

  const nextSeq = s.execSeq + 1;
  const cells = s.cells.map((c) => (c.id === cellId ? { ...c, execCount: nextSeq, stale: false, outputs: [], errorLine: null, lastError: null } : c));
  set({ cells, execSeq: nextSeq, activeCellId: cellId, graphWarning: null });

  const base = stepName(s.projectId ? projectScript(s.projectId)?.name ?? null : s.file, false);
  await run(cell.source, false, { cellId, echo: true, label: `${base} [#${idx + 1}]` });
}

/** Run all code cells in the notebook as a single atomic undo step. */
export async function runAllCells(): Promise<void> {
  const s = get();
  if (engineInBrowser || s.running != null) return;
  const combined = combineCodeCells(s.cells);
  if (!combined.trim()) return;
  let seq = s.execSeq;
  const cells = s.cells.map((c) =>
    c.kind === 'code' && c.source.trim()
      ? { ...c, execCount: ++seq, stale: false, outputs: [], errorLine: null, lastError: null }
      : c,
  );
  set({ cells, execSeq: seq, graphWarning: null });
  await run(combined, false, { echo: true });
}

export async function restartAndRunAll(): Promise<void> {
  await restart();
  await runAllCells();
}

async function start(code: string, name: string, console: boolean, source?: string, cellId: string | null = null, echo = false): Promise<void> {
  const s = get();
  pendingCellId = cellId;
  if (console) set({ lines: [...s.lines, { kind: 'in', text: code }], history: rememberLine(s.history, code) });
  else set({ errorLine: null, runningCellId: cellId, lines: [...s.lines, { kind: 'info', text: `▶ ${name}` }] });
  try {
    await engine.call<{ run: number }>('script.run', {
      code,
      name,
      console,
      echo,
      python: usePrefs.getState().pythonPath,
      ...(source && !console ? { source } : {}),
    });
  } catch (e) {
    pendingCellId = null;
    set({ runningCellId: null });
    say('err', message(e));
    void refreshStatus();
  }
}

export async function answerTrust(answer: 'trust' | 'once' | 'cancel'): Promise<void> {
  const a = get().asking;
  const source = sourceKeyOf(sourceOf());
  set({ asking: null });
  if (!a || answer === 'cancel') return;
  if (answer === 'trust') {
    try { await engine.call('script.trust', { sha256: a.sha }); } catch (e) { say('err', message(e)); return; }
    set({ trust: remember(get().trust, a.sha, 'trust') });
  }
  await start(a.code, stepName(a.name, false), false, source, a.cellId ?? null, a.echo ?? false);
}

export async function stop(): Promise<void> {
  try { await engine.call('script.stop'); } catch (e) { say('err', message(e)); }
}

export async function restart(): Promise<void> {
  try {
    const status = await engine.call<ScriptStatus>('script.restart', { python: usePrefs.getState().pythonPath });
    const cells = get().cells.map((c) => ({ ...c, execCount: null, stale: false }));
    set({ status, execSeq: 0, cells, runningCellId: null });
    say('info', 'Python restarted: the notebook’s variables are cleared.');
  } catch (e) { say('err', message(e)); }
}

export const clearConsole = () => set({ lines: [] });

export async function showScriptAt(target: { script?: string; line?: number } | undefined): Promise<void> {
  const name = target?.script;
  if (!name) return;
  const base = (x: string) => x.replace(/\.py$/i, '');
  const s = get();
  const open = s.projectId ? projectScript(s.projectId)?.name ?? null : s.file;
  if (!(open !== null && base(open) === name)) {
    const mine = s.files.find((f) => base(f.name) === name);
    const project = projectScripts().find((p) => base(p.name) === name);
    if (mine) await openScript(mine.name);
    else if (project) openProjectScript(project.id);
    else return;
  }
  set({ errorLine: target?.line ?? null });
}

// ------------------------------------------------------------ Ollama AI prompt cell

let aiAbort: AbortController | null = null;

export function stopAi(): void {
  aiAbort?.abort();
  aiAbort = null;
  set({ aiBusyCellId: null, aiStatus: null });
}

/** Snapshot the live user variables in the running Python kernel (read-only, never starts Python). */
async function inspectKernel(): Promise<KernelVar[]> {
  if (engineInBrowser || get().running != null) return [];
  try {
    const r = await engine.call<{ vars?: KernelVar[] }>('script.inspect');
    return Array.isArray(r?.vars) ? r.vars : [];
  } catch {
    return [];
  }
}

/** Run an AI prompt cell: generates Python code with Ollama and streams it into the code cell below. */
export async function runPromptCell(promptCellId: string): Promise<void> {
  const s = get();
  if (s.aiBusyCellId) return;
  const pIdx = s.cells.findIndex((c) => c.id === promptCellId);
  const promptCell = s.cells[pIdx];
  if (!promptCell || promptCell.kind !== 'prompt' || !promptCell.source.trim()) return;

  // Ensure a code cell exists immediately below the prompt cell.
  let targetIdx = targetCodeCellIndex(s.cells, pIdx);
  let targetId: string;
  if (targetIdx === null) {
    const created = addCell('code', pIdx, '');
    targetId = created.id;
    targetIdx = pIdx + 1;
  } else {
    targetId = s.cells[targetIdx].id;
  }

  const [catalogue, kernelVars] = await Promise.all([readCatalogue(), inspectKernel()]);
  const currentTarget = get().cells.find((c) => c.id === targetId);
  const contextAbove = codeContextAbove(get().cells, promptCellId);

  aiAbort = new AbortController();
  set({ aiBusyCellId: promptCellId, aiStatus: 'Asking Ollama…', activeCellId: targetId });
  try {
    const res = await generateCellCode({
      prompt: promptCell.source,
      catalogue,
      contextAbove,
      kernelVars,
      supportsTools: get().ollama?.tools,
      currentCode: currentTarget?.source ?? '',
      signal: aiAbort.signal,
      onCode: (code) => updateCell(targetId, code),
      onStatus: (aiStatus) => set({ aiStatus }),
    });
    if (res.code) updateCell(targetId, res.code);
    // Lapin's own lines, each one phrase the interface translates (its patterns): the model and the numbers in them.
    const stats = res.tokens && res.seconds ? ` in ${res.seconds.toFixed(1)} s (${res.tokens} tokens)` : '';
    say('info', `Cell written by ${res.model}${stats}: review it, then Shift+Enter runs it.`);
    if (res.repaired) say('info', `${res.repaired} call(s) to Lapin repaired against the catalogue.`);
  } catch (e) {
    if (!aiAbort?.signal.aborted) say('err', `Ollama: ${message(e)}`);
  } finally {
    aiAbort = null;
    set({ aiBusyCellId: null, aiStatus: null });
  }
}

/** Ask Ollama to fix the last error of a code cell and stream the fix directly into it. */
export async function fixCellWithAi(codeCellId: string): Promise<void> {
  const s = get();
  if (s.aiBusyCellId) return;
  const cell = s.cells.find((c) => c.id === codeCellId);
  if (!cell || cell.kind !== 'code' || !cell.lastError) return;

  const [catalogue, kernelVars] = await Promise.all([readCatalogue(), inspectKernel()]);
  const contextAbove = codeContextAbove(s.cells, codeCellId);
  aiAbort = new AbortController();
  set({ aiBusyCellId: codeCellId, aiStatus: 'Asking Ollama for a fix…', activeCellId: codeCellId });
  try {
    const res = await generateCellCode({
      prompt: '',
      catalogue,
      contextAbove,
      kernelVars,
      supportsTools: get().ollama?.tools,
      currentCode: cell.source,
      errorText: cell.lastError,
      signal: aiAbort.signal,
      onCode: (code) => updateCell(codeCellId, code),
      onStatus: (aiStatus) => set({ aiStatus }),
    });
    if (res.code) updateCell(codeCellId, res.code);
    say('info', `Fix proposed by ${res.model}: Shift+Enter runs the cell again.`);
  } catch (e) {
    if (!aiAbort?.signal.aborted) say('err', `Ollama: ${message(e)}`);
  } finally {
    aiAbort = null;
    set({ aiBusyCellId: null, aiStatus: null });
  }
}

// ------------------------------------------------------------ Code cell → Visual Graph

/** Save a generated GraphJson as a new LapinGraph entity and open it in the Graphs panel. */
export async function openConvertedGraph(graph: GraphJson, baseName = 'From script'): Promise<void> {
  const taken = new Set(projectGraphs(useSession.getState().nodes).map((g) => g.name.toLowerCase()));
  let name = baseName;
  let i = 2;
  while (taken.has(name.toLowerCase())) name = `${baseName} ${i++}`;
  const r = await execute('graphs.save', { name, graph }) as { id?: string } | null;
  if (r?.id) {
    set({ graphWarning: null });
    openGraphsPanel();
    openGraph(r.id);
    notify(`Graph „${name}” made from the Python cell.`);
  }
}

/** Convert a code cell deterministically into a native Lapin Visual Graph. */
export async function cellToGraph(cellId: string): Promise<void> {
  const s = get();
  const idx = s.cells.findIndex((c) => c.id === cellId);
  const cell = s.cells[idx];
  if (!cell || cell.kind !== 'code' || !cell.source.trim()) return;

  const catalogue = await readCatalogue();
  const res = pythonToGraph(cell.source, catalogue);
  const base = (s.projectId ? projectScript(s.projectId)?.name : s.file)?.replace(/\.py$/i, '') || 'Notebook';
  const graphName = `${base} cell ${idx + 1}`;

  if (res.unsupported.length === 0 && res.graph.nodes.length > 0) {
    await openConvertedGraph(res.graph, graphName);
    return;
  }
  if (res.graph.nodes.length === 0 && res.unsupported.length === 0) {
    notify('Nothing to convert: the cell has no parameters or lapin.* commands.');
    return;
  }
  set({ graphWarning: { cellId, unsupported: res.unsupported, graph: res.graph } });
}

/** Fallback: ask Ollama to convert a complex Python cell into a GraphJson and validate it via graphs.evaluate. */
export async function cellToGraphWithOllama(cellId: string): Promise<void> {
  const s = get();
  if (s.aiBusyCellId) return;
  const idx = s.cells.findIndex((c) => c.id === cellId);
  const cell = s.cells[idx];
  if (!cell || cell.kind !== 'code') return;

  const catalogue = await readCatalogue();
  aiAbort = new AbortController();
  set({ aiBusyCellId: cellId, aiStatus: 'Converting the cell to a graph with Ollama…' });
  try {
    const graph = await convertCellToGraphWithAi(cell.source, catalogue, aiAbort.signal);
    // Validate the generated graph against the engine before saving.
    await query('graphs.evaluate', { graph });
    const base = (s.projectId ? projectScript(s.projectId)?.name : s.file)?.replace(/\.py$/i, '') || 'Notebook';
    await openConvertedGraph(graph, `${base} cell ${idx + 1}`);
  } catch (e) {
    if (!aiAbort?.signal.aborted) say('err', `The graph could not be made: ${message(e)}`);
  } finally {
    aiAbort = null;
    set({ aiBusyCellId: null, aiStatus: null });
  }
}

export const dismissGraphWarning = () => set({ graphWarning: null });

// ------------------------------------------------------------ Mine: the user's scripts

export async function refreshFiles(): Promise<void> {
  try { set({ files: (await engine.call<{ files: ScriptFile[] }>('script.files')).files }); } catch { set({ files: [] }); }
}

export const dirty = (s: ScriptingState = get()) => s.code !== s.saved;

function loadDocument(patch: Partial<ScriptingState> & { code: string }): void {
  const cells = parseNotebook(patch.code);
  set({
    ...patch,
    cells,
    activeCellId: cells[0]?.id ?? null,
    errorLine: null,
    graphWarning: null,
  });
}

export function newScript(code = NEW_SCRIPT): void {
  loadDocument({ file: null, projectId: null, code, saved: code === NEW_SCRIPT ? NEW_SCRIPT : '', modified: null });
}

export async function openScript(name: string): Promise<void> {
  try {
    const r = await engine.call<{ name: string; code: string; modified: string }>('script.load', { name });
    loadDocument({ file: r.name, projectId: null, code: r.code, saved: r.code, modified: r.modified });
  } catch (e) { say('err', message(e)); }
}

export async function saveScript(name?: string): Promise<boolean> {
  const s = get();
  const as = name ?? s.file;
  if (!as) return false;
  try {
    const r = await engine.call<{ name: string; modified: string }>('script.save', { name: as, code: s.code });
    set({ file: r.name, projectId: null, saved: s.code, modified: r.modified });
    await refreshFiles();
    return true;
  } catch (e) { say('err', message(e)); return false; }
}

const deletedListeners = new Set<(name: string) => void>();
export const onScriptDeleted = (l: (name: string) => void): (() => void) => { deletedListeners.add(l); return () => deletedListeners.delete(l); };

export async function deleteScript(): Promise<void> {
  const s = get();
  if (!s.file) return;
  const name = s.file;
  try {
    await engine.call('script.delete', { name });
    newScript();
    await refreshFiles();
    for (const l of deletedListeners) l(name);
  } catch (e) { say('err', message(e)); }
}

export async function reveal(): Promise<void> {
  const s = get();
  if (!s.file) return;
  if (dirty(s) && !(await saveScript())) return;
  try { await engine.call('script.reveal', { name: s.file, editor: usePrefs.getState().externalEditor }); } catch (e) { say('err', message(e)); }
}

export async function follow(): Promise<void> {
  const s = get();
  if (!s.open || !s.file || dirty(s) || engineInBrowser) return;
  try {
    const r = await engine.call<{ name: string; code: string; modified: string }>('script.load', { name: s.file });
    const now = get();
    if (now.file === r.name && !dirty(now) && r.modified !== now.modified) {
      loadDocument({ code: r.code, saved: r.code, modified: r.modified });
    }
  } catch { /* gone: left as it is */ }
}

// ------------------------------------------------------------ the project's scripts (LapinScript)

export function projectScripts(nodes = useSession.getState().nodes): ProjectScript[] {
  const o: ProjectScript[] = [];
  for (const n of nodes.values())
    if (n.type === SCRIPT_CLASS) o.push({ id: n.id, name: n.name, code: String(n.props.code ?? ''), sha256: String(n.props.sha256 ?? '') });
  return o.sort((a, b) => a.name.localeCompare(b.name));
}

export const projectScript = (id: string) => projectScripts().find((x) => x.id === id) ?? null;

export function openProjectScript(id: string): void {
  const p = projectScript(id);
  if (!p) return;
  loadDocument({ file: null, projectId: p.id, code: p.code, saved: p.code, modified: null });
}

async function command<T>(name: string, args: Record<string, unknown>): Promise<T | null> {
  try { return await engine.call<T>('doc.execute', { command: name, args }); } catch (e) { say('err', message(e)); return null; }
}

export async function saveToProject(name: string): Promise<boolean> {
  const s = get();
  const open = s.projectId ? projectScript(s.projectId) : null;
  const r = open && open.code === s.code && open.name !== name
    ? await command<{ id: string; name: string; sha256: string }>('scripting.rename', { id: open.id, name })
    : await command<{ id: string; name: string; sha256: string }>('scripting.save', { ...(open ? { id: open.id } : {}), name, code: s.code });
  if (!r) return false;
  set({ projectId: r.id, file: null, saved: s.code, modified: null });
  const sha = r.sha256 || await sha256(s.code);
  try { await engine.call('script.trust', { sha256: sha }); set({ trust: remember(get().trust, sha, 'trust') }); } catch { /* asked at its first run */ }
  return true;
}

export async function deleteFromProject(): Promise<void> {
  const id = get().projectId;
  if (!id) return;
  if (await command('scripting.delete', { id }) !== null || !projectScript(id)) newScript();
}

// ------------------------------------------------------------ the recorder

let recording: Recording | null = null;
let stopHearing: (() => void) | null = null;

function knownCommands(c: Catalogue | null): ((command: string) => boolean) | null {
  if (!c) return null;
  const names = new Set(c.commands.map((x) => x.name));
  return (command) => names.has(command);
}

function contextNow(c: Catalogue | null) {
  const nodes = useSession.getState().nodes;
  const categories = new Map((useSession.getState().info?.nodeTypes ?? []).map((t) => [t.type, t.category]));
  const ui = useUi.getState();
  const selection = ui.selected.length ? [...ui.selected] : ui.selection ? [ui.selection] : [];
  return {
    node: (id: string) => { const n = nodes.get(id); return n ? { type: n.type, name: n.name, props: n.props as Record<string, unknown>, category: categories.get(n.type) } : undefined; },
    known: knownCommands(c),
    selection,
    useSelection: get().recordSelection,
  };
}

export async function startRecording(): Promise<void> {
  if (get().recording) return;
  const catalogue = await readCatalogue();
  recording = new Recording();
  set({ recording: true, recorded: [], tab: 'recorder', terminalOpen: true });
  stopHearing?.();
  stopHearing = engine.onCall((method, params) => {
    if (!recording || (method !== 'doc.execute' && method !== 'doc.undo' && method !== 'doc.redo')) return;
    if (method === 'doc.execute' && typeof params.command === 'string' && params.command.startsWith('scripting.')) return;
    const ctx = contextNow(catalogue);
    const rec = recording;
    return ({ result, error }) => {
      if (error || rec !== recording) return;
      const call: RecordedCall = { method, params, result };
      if (rec.add(call, ctx).length) set({ recorded: [...rec.lines] });
    };
  });
}

export function stopRecording(): void {
  stopHearing?.();
  stopHearing = null;
  set({ recording: false });
}

export const recordedText = (): string => Recording.text(get().recorded);

export async function copyRecording(): Promise<void> {
  try { await navigator.clipboard.writeText(recordedText()); say('info', 'The recorded script is copied.'); } catch (e) { say('err', message(e)); }
}

/** Insert the recorded steps as a new code cell in the current notebook. */
export function insertRecordingAsCell(): void {
  const lines = get().recorded;
  if (!lines.length) return;
  const activeIdx = get().cells.findIndex((c) => c.id === get().activeCellId);
  addCell('code', activeIdx >= 0 ? activeIdx : undefined, lines.join('\n'));
}

export async function saveRecording(): Promise<void> {
  const taken = new Set(get().files.map((f) => f.name.toLowerCase()));
  let n = 1;
  while (taken.has(`recorded ${n}.py`)) n++;
  newScript(recordedText());
  await saveScript(`Recorded ${n}.py`);
}

export const clearRecording = () => { if (recording) recording = new Recording(); set({ recorded: [] }); };

// ------------------------------------------------------------ the window's selection, for lapin.selection()

let lastSent = '';
export function tellSelection(): void {
  if (engineInBrowser) return;
  const ui = useUi.getState();
  const ids = ui.selected.length ? ui.selected : ui.selection ? [ui.selection] : [];
  const text = ids.join('\n');
  if (text === lastSent) return;
  lastSent = text;
  engine.notify('script.selected', { ids: ids.slice(0, 100_000) });
}

export const forgetSentSelection = () => { lastSent = '\u0000'; };
