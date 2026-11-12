/**
 * The Script panel's state and its calls to the engine's host (docs/plan-python-scripting.md, stage 2; decision 489):
 * script.run, script.stop, script.restart, script.status, and the user's own scripts (script.files, script.load,
 * script.save, script.delete, script.reveal). What the kernel prints comes back as script.output events, folded into
 * the console (logic.ts). The desktop's only: in the browser there is no Python (the panel says so).
 */

import { create } from 'zustand';
import { engine, engineInBrowser } from '../../engine/session';
import { usePrefs } from '../../app/prefs';
import { foldOutput, remember, stepName, type Catalogue, type ConsoleLine, type History, type ScriptEvent } from './logic';

export interface ScriptStatus { enabled: boolean; ok: boolean; python?: string | null; version?: string | null; message?: string | null; alive?: boolean; running?: boolean; scripts?: string | null }
export interface ScriptFile { name: string; modified: string; bytes: number }

export interface ScriptingState {
  open: boolean;
  lines: ConsoleLine[];
  /** The run going on (its id), and whose it is. */
  running: number | null;
  runningName: string | null;
  status: ScriptStatus | null;
  files: ScriptFile[];
  /** The script in the editor: its file in Mine (null: not saved yet), its code, the code as last saved or read. */
  file: string | null;
  code: string;
  saved: string;
  modified: string | null;
  /** The line of the last error in the editor's script, to show. */
  errorLine: number | null;
  history: History;
  height: number;
  catalogue: Catalogue | null;
}

export const NEW_SCRIPT = `# A Lapin script: Run (Ctrl+Enter) makes all it does one undo step.
# In the panel \`lapin\` is the lapinbim package; from a terminal: import lapinbim as lapin

storey = next(s.id for s in lapin.model.storeys if s.elevation == 0)
corners = [(0, 0), (8, 0), (8, 6), (0, 6)]
for (x1, y1), (x2, y2) in zip(corners, corners[1:] + corners[:1]):
    lapin.architecture.add_wall(storey_id=storey, x1=x1, y1=y1, x2=x2, y2=y2, height=2.8)
print(len(lapin.model.walls), "walls")
`;

const HEIGHT_KEY = 'lapin.scriptPanel.height';
const readHeight = () => { try { return Math.max(160, Math.min(900, Number(localStorage.getItem(HEIGHT_KEY)) || 300)); } catch { return 300; } };

export const useScripting = create<ScriptingState>(() => ({
  open: false, lines: [], running: null, runningName: null, status: null, files: [], file: null, code: NEW_SCRIPT, saved: NEW_SCRIPT, modified: null,
  errorLine: null, history: { items: [], at: null }, height: readHeight(), catalogue: null,
}));

const set = useScripting.setState;
const get = useScripting.getState;
const say = (kind: ConsoleLine['kind'], text: string) => set({ lines: [...get().lines, { kind, text }] });
const message = (e: unknown) => (e instanceof Error ? e.message : String(e));

export function setHeight(h: number): void {
  const height = Math.max(160, Math.min(Math.round(window.innerHeight * 0.85), Math.round(h)));
  set({ height });
  try { localStorage.setItem(HEIGHT_KEY, String(height)); } catch { /* kept for this session */ }
}

/** The kernel's events into the console; started once (the module's background). */
let listening = false;
export function listen(): void {
  if (listening || engineInBrowser) return;
  listening = true;
  engine.onEvent('script.output', (d) => {
    const e = d as ScriptEvent;
    const s = get();
    const patch: Partial<ScriptingState> = { lines: foldOutput(s.lines, e) };
    if (e.type === 'start') { patch.running = e.id ?? null; patch.runningName = e.name ?? null; }
    if (e.type === 'done' && (s.running === e.id || s.running === null)) {
      patch.running = null;
      patch.runningName = null;
      if (!e.console) patch.errorLine = e.ok ? null : e.line ?? null;
    }
    if (e.type === 'exit') { patch.running = null; patch.runningName = null; }
    set(patch);
  });
}

/** The panel opened (View → Script): Python looked for, the user's scripts listed, the catalogue read for completion. */
export async function openPanel(): Promise<void> {
  set({ open: true });
  if (engineInBrowser) return;
  listen();
  await Promise.all([refreshStatus(), refreshFiles(), readCatalogue()]);
}

export const closePanel = () => set({ open: false });

export async function refreshStatus(refresh = false): Promise<void> {
  try { set({ status: await engine.call<ScriptStatus>('script.status', { python: usePrefs.getState().pythonPath, refresh }) }); }
  catch (e) { set({ status: { enabled: false, ok: false, message: message(e) } }); }
}

async function readCatalogue(): Promise<void> {
  if (get().catalogue) return;
  try { set({ catalogue: await engine.call<Catalogue>('session.catalogue') }); } catch { /* completion without the catalogue's names */ }
}

/** Code run in the kernel: the editor's script (named after its file) or a line of the console. */
export async function run(code: string, console: boolean): Promise<void> {
  if (engineInBrowser || get().running != null || !code.trim()) return;
  const s = get();
  if (console) set({ lines: [...s.lines, { kind: 'in', text: code }], history: remember(s.history, code) });
  else set({ errorLine: null, lines: [...s.lines, { kind: 'info', text: `▶ ${stepName(s.file, false)}` }] });
  try {
    // The run's start and end come as events (a quick one may end before this answer): its id is theirs.
    await engine.call<{ run: number }>('script.run', { code, name: stepName(s.file, console), console, python: usePrefs.getState().pythonPath });
  } catch (e) {
    say('err', message(e));
    void refreshStatus();
  }
}

export async function stop(): Promise<void> {
  try { await engine.call('script.stop'); } catch (e) { say('err', message(e)); }
}

export async function restart(): Promise<void> {
  try {
    set({ status: await engine.call<ScriptStatus>('script.restart', { python: usePrefs.getState().pythonPath }) });
    say('info', 'Python restarted: the console’s names are gone.');
  } catch (e) { say('err', message(e)); }
}

export const clearConsole = () => set({ lines: [] });

// ------------------------------------------------------------ Mine: the user's scripts

export async function refreshFiles(): Promise<void> {
  try { set({ files: (await engine.call<{ files: ScriptFile[] }>('script.files')).files }); } catch { set({ files: [] }); }
}

export const dirty = (s: ScriptingState = get()) => s.code !== s.saved;

export function newScript(): void {
  set({ file: null, code: NEW_SCRIPT, saved: NEW_SCRIPT, modified: null, errorLine: null });
}

export async function openScript(name: string): Promise<void> {
  try {
    const r = await engine.call<{ name: string; code: string; modified: string }>('script.load', { name });
    set({ file: r.name, code: r.code, saved: r.code, modified: r.modified, errorLine: null });
  } catch (e) { say('err', message(e)); }
}

/** The editor's script saved in Mine, under its own name or <name>. */
export async function saveScript(name?: string): Promise<boolean> {
  const s = get();
  const as = name ?? s.file;
  if (!as) return false;
  try {
    const r = await engine.call<{ name: string; modified: string }>('script.save', { name: as, code: s.code });
    set({ file: r.name, saved: s.code, modified: r.modified });
    await refreshFiles();
    return true;
  } catch (e) { say('err', message(e)); return false; }
}

export async function deleteScript(): Promise<void> {
  const s = get();
  if (!s.file) return;
  try { await engine.call('script.delete', { name: s.file }); newScript(); await refreshFiles(); } catch (e) { say('err', message(e)); }
}

/** The script opened in an external editor (Preferences → Developer extras names it), saved first; the panel follows the file. */
export async function reveal(): Promise<void> {
  const s = get();
  if (!s.file) return;
  if (dirty(s) && !(await saveScript())) return;
  try { await engine.call('script.reveal', { name: s.file, editor: usePrefs.getState().externalEditor }); } catch (e) { say('err', message(e)); }
}

/** The file edited elsewhere (an external editor): read again when the editor has nothing unsaved. Asked every few seconds while the panel is open. */
export async function follow(): Promise<void> {
  const s = get();
  if (!s.open || !s.file || dirty(s) || engineInBrowser) return;
  try {
    const r = await engine.call<{ name: string; code: string; modified: string }>('script.load', { name: s.file });
    const now = get();
    if (now.file === r.name && !dirty(now) && r.modified !== now.modified) set({ code: r.code, saved: r.code, modified: r.modified });
  } catch { /* gone: left as it is */ }
}
