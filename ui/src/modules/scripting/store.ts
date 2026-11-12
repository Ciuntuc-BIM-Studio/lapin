/**
 * The Script panel's state and its calls to the engine's host (docs/plan-python-scripting.md, stages 2 and 3; decision 489):
 * script.run, script.stop, script.restart, script.status, the user's own scripts (script.files, script.load,
 * script.save, script.delete, script.reveal), the project's scripts (the scripting module's LapinScript entities,
 * scripting.save / rename / delete, one undo step each), trust in them (script.trusted, script.trust), the window's
 * selection told to scripts (script.selected) and the macro recorder (recorder.ts, fed by every call the window makes).
 * What the kernel prints comes back as script.output events, folded into the console (logic.ts). The desktop's only:
 * in the browser there is no Python (the panel says so).
 */

import { create } from 'zustand';
import { engine, engineInBrowser, useSession } from '../../engine/session';
import { usePrefs } from '../../app/prefs';
import { useUi } from '../../app/store';
import { foldOutput, own, remember as rememberLine, stepName, type Catalogue, type ConsoleLine, type History, type ScriptEvent } from './logic';
import { Recording, type RecordedCall } from './recorder';
import { asksTrust, EMPTY_TRUST, remember, sha256, type ScriptSource, type TrustState } from './trust';

export interface ScriptStatus { enabled: boolean; ok: boolean; python?: string | null; version?: string | null; message?: string | null; alive?: boolean; running?: boolean; scripts?: string | null }
export interface ScriptFile { name: string; modified: string; bytes: number }

/** A project's script (a LapinScript entity) as the panel lists it. */
export interface ProjectScript { id: string; name: string; code: string; sha256: string }

export const SCRIPT_CLASS = 'LapinScript';

export interface ScriptingState {
  open: boolean;
  lines: ConsoleLine[];
  /** The run going on (its id), and whose it is. */
  running: number | null;
  runningName: string | null;
  status: ScriptStatus | null;
  files: ScriptFile[];
  /** The script in the editor: its file in Mine (null: not one of Mine), or the project's script it is (its id), its code, the code as last saved or read. */
  file: string | null;
  projectId: string | null;
  code: string;
  saved: string;
  modified: string | null;
  /** The line of the last error in the editor's script, to show. */
  errorLine: number | null;
  history: History;
  height: number;
  catalogue: Catalogue | null;
  /** The console's tab: the console itself, or the recorder. */
  tab: 'console' | 'recorder';
  recording: boolean;
  /** The recorder's lines so far (the script it makes, without its head). */
  recorded: string[];
  /** The window's selection, as a command's ids, written `lapin.selection()`. */
  recordSelection: boolean;
  trust: TrustState;
  /** A project's script waiting for the user's trust before it runs. */
  asking: { code: string; sha: string; name: string } | null;
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
  open: false, lines: [], running: null, runningName: null, status: null, files: [], file: null, projectId: null, code: NEW_SCRIPT, saved: NEW_SCRIPT, modified: null,
  errorLine: null, history: { items: [], at: null }, height: readHeight(), catalogue: null,
  tab: 'console', recording: false, recorded: [], recordSelection: true, trust: EMPTY_TRUST, asking: null,
}));

const set = useScripting.setState;
const get = useScripting.getState;
/** A line of Lapin's own (translated as the interface is), or the script's (shown as it is). */
const say = (kind: ConsoleLine['kind'], text: string) => set({ lines: [...get().lines, own(kind, text)] });
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

async function readCatalogue(): Promise<Catalogue | null> {
  const had = get().catalogue;
  if (had) return had;
  try { const c = await engine.call<Catalogue>('session.catalogue'); set({ catalogue: c }); return c; } catch { return null; /* completion without the catalogue's names */ }
}

/** Where the editor's script comes from: a project's (asks trust), one of Mine, or new. */
export function sourceOf(s: ScriptingState = get()): ScriptSource {
  if (s.projectId) return { kind: 'project', id: s.projectId, name: projectScript(s.projectId)?.name ?? 'Script' };
  if (s.file) return { kind: 'mine', name: s.file };
  return { kind: 'new' };
}

/** Code run in the kernel: the editor's script (named after its file) or a line of the console. A project's script asks trust first. */
export async function run(code: string, console: boolean): Promise<void> {
  if (engineInBrowser || get().running != null || !code.trim()) return;
  const s = get();
  const name = stepName(s.projectId ? projectScript(s.projectId)?.name ?? null : s.file, console);
  if (!console) {
    const source = sourceOf(s);
    if (source.kind === 'project') {
      const sha = await sha256(code);
      if (asksTrust(source, sha, s.trust)) {
        // Kept on this computer before (another session, another window)? Asked of the host first.
        const known = await engine.call<{ trusted: boolean }>('script.trusted', { sha256: sha }).then((r) => r.trusted, () => false);
        if (!known) { set({ asking: { code, sha, name: source.name } }); return; }
        set({ trust: remember(get().trust, sha, 'trust') });
      }
    }
  }
  await start(code, name, console);
}

async function start(code: string, name: string, console: boolean): Promise<void> {
  const s = get();
  if (console) set({ lines: [...s.lines, { kind: 'in', text: code }], history: rememberLine(s.history, code) });
  else set({ errorLine: null, lines: [...s.lines, { kind: 'info', text: `▶ ${name}` }] });
  try {
    // The run's start and end come as events (a quick one may end before this answer): its id is theirs.
    await engine.call<{ run: number }>('script.run', { code, name, console, python: usePrefs.getState().pythonPath });
  } catch (e) {
    say('err', message(e));
    void refreshStatus();
  }
}

/** The answer to the trust question: Trust (kept on this computer by the code's SHA-256), Run once, or Cancel. */
export async function answerTrust(answer: 'trust' | 'once' | 'cancel'): Promise<void> {
  const a = get().asking;
  set({ asking: null });
  if (!a || answer === 'cancel') return;
  if (answer === 'trust') {
    try { await engine.call('script.trust', { sha256: a.sha }); } catch (e) { say('err', message(e)); return; }
    set({ trust: remember(get().trust, a.sha, 'trust') });
  }
  await start(a.code, stepName(a.name, false), false);
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

export function newScript(code = NEW_SCRIPT): void {
  set({ file: null, projectId: null, code, saved: code === NEW_SCRIPT ? NEW_SCRIPT : '', modified: null, errorLine: null });
}

export async function openScript(name: string): Promise<void> {
  try {
    const r = await engine.call<{ name: string; code: string; modified: string }>('script.load', { name });
    set({ file: r.name, projectId: null, code: r.code, saved: r.code, modified: r.modified, errorLine: null });
  } catch (e) { say('err', message(e)); }
}

/** The editor's script saved in Mine, under its own name or <name>. */
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

// ------------------------------------------------------------ the project's scripts (LapinScript)

/** The project's scripts, from the model the window holds (each LapinScript entity), by name. */
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
  set({ file: null, projectId: p.id, code: p.code, saved: p.code, modified: null, errorLine: null });
}

/** A command of the scripting module, its refusal said in the console. */
async function command<T>(name: string, args: Record<string, unknown>): Promise<T | null> {
  try { return await engine.call<T>('doc.execute', { command: name, args }); } catch (e) { say('err', message(e)); return null; }
}

/**
 * The editor's script kept in the project as <name> (one undo step): the open project script changed (or renamed), or
 * a new one. Code this user saves is theirs: trusted on this computer as it is saved.
 */
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

/** The commands the catalogue knows, for the recorder to write `lapin.<ns>.<name>(…)` (else `lapin.execute(…)`). */
function knownCommands(c: Catalogue | null): ((command: string) => boolean) | null {
  if (!c) return null;
  const names = new Set(c.commands.map((x) => x.name));
  return (command) => names.has(command);
}

/** The model as a line is written: an id's entity, with its class's category (a type, a resource). */
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

/** Record: every edit the window makes from now on becomes a line of Python, shown live in the recorder's tab. */
export async function startRecording(): Promise<void> {
  if (get().recording) return;
  const catalogue = await readCatalogue();
  recording = new Recording();
  set({ recording: true, recorded: [], tab: 'recorder' });
  stopHearing?.();
  stopHearing = engine.onCall((method, params) => {
    if (!recording || (method !== 'doc.execute' && method !== 'doc.undo' && method !== 'doc.redo')) return;
    // The panel's own commands (a project's script saved) are not the user's work.
    if (method === 'doc.execute' && typeof params.command === 'string' && params.command.startsWith('scripting.')) return;
    // The model and the selection as they are when the call goes (a deleted element's id still names it).
    const ctx = contextNow(catalogue);
    const rec = recording;
    return ({ result, error }) => {
      if (error || rec !== recording) return;
      const call: RecordedCall = { method, params, result };
      if (rec.add(call, ctx).length) set({ recorded: [...rec.lines] });
    };
  });
}

/** Stop: the recording kept, to copy or save. */
export function stopRecording(): void {
  stopHearing?.();
  stopHearing = null;
  set({ recording: false });
}

/** The recorded script, whole (its head, the import, its lines). */
export const recordedText = (): string => Recording.text(get().recorded);

export async function copyRecording(): Promise<void> {
  try { await navigator.clipboard.writeText(recordedText()); say('info', 'The recorded script is copied.'); } catch (e) { say('err', message(e)); }
}

/** Save as script: the recording into the editor and into Mine, as Recorded <n>.py. */
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
/** The window's selection told to the engine's host (a call wanting no answer), so a script's lapin.selection() reads it. */
export function tellSelection(): void {
  if (engineInBrowser) return;
  const ui = useUi.getState();
  const ids = ui.selected.length ? ui.selected : ui.selection ? [ui.selection] : [];
  const text = ids.join('\n');
  if (text === lastSent) return;
  lastSent = text;
  engine.notify('script.selected', { ids: ids.slice(0, 100_000) });
}

/** Forgotten after a reconnection: the next change tells it again. */
export const forgetSentSelection = () => { lastSent = '\u0000'; };
