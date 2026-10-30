/**
 * Projects on disk: New, Open, Save, Save as, recovery of unsaved work. In
 * the desktop app the paths come from the native dialogs; in a browser
 * against the local engine (pnpm dev) they are typed in; with the engine in
 * the browser (the web build) the project comes from a file the user picks
 * and goes back to it (or to a download), as bytes.
 */

import { engine, engineInBrowser, useSession } from './session';
import { LONG_TIMEOUT_MS } from './client';
import { surveyIfc } from './ifcImport';
import { emptyPane, notify, select, useUi } from '../app/store';

const FILTERS = [{ name: 'Lapin project', extensions: ['lapin'] }];

export const isDesktop = () => Boolean((window as unknown as { __TAURI_INTERNALS__?: unknown }).__TAURI_INTERNALS__);

// ─── Files as bytes (the engine in the browser) ──────────────────────────

interface PickerWindow {
  showOpenFilePicker?: (o: { types: { description: string; accept: Record<string, string[]> }[] }) => Promise<FileSystemFileHandle[]>;
  showSaveFilePicker?: (o: { suggestedName: string; types: { description: string; accept: Record<string, string[]> }[] }) => Promise<FileSystemFileHandle>;
}
const picker = () => window as unknown as PickerWindow;

/** The file the project came from or was last saved to, where the browser lets a page write back to it (Chrome, Edge). */
let projectHandle: FileSystemFileHandle | null = null;

/** A file the user picks: its name and bytes (null: none picked). */
async function pickFile(description: string, ext: string): Promise<{ name: string; bytes: Uint8Array; handle: FileSystemFileHandle | null } | null> {
  const open = picker().showOpenFilePicker;
  if (open) {
    try {
      const [handle] = await open({ types: [{ description, accept: { 'application/octet-stream': [`.${ext}`] } }] });
      const file = await handle.getFile();
      return { name: file.name, bytes: new Uint8Array(await file.arrayBuffer()), handle };
    } catch { return null; }   // cancelled
  }
  return new Promise((resolve) => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = `.${ext}`;
    input.onchange = async () => {
      const file = input.files?.[0];
      resolve(file ? { name: file.name, bytes: new Uint8Array(await file.arrayBuffer()), handle: null } : null);
    };
    input.oncancel = () => resolve(null);
    input.click();
  });
}

/** Bytes as base64, the way the protocol carries them (in pieces: a large project would overflow one call). */
export function toBase64(bytes: Uint8Array): string {
  let s = '';
  for (let i = 0; i < bytes.length; i += 0x8000) s += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
  return btoa(s);
}

const fromBase64 = (data: string) => Uint8Array.from(atob(data), (c) => c.charCodeAt(0));

/** Bytes handed to the user as a download. */
function download(bytes: Uint8Array, name: string, type = 'application/octet-stream'): void {
  const url = URL.createObjectURL(new Blob([bytes as BlobPart], { type }));
  const a = document.createElement('a');
  a.href = url; a.download = name; a.click();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}

/** Bytes written into a file the user chose (where the browser can), else downloaded; false: the user cancelled. */
async function keep(bytes: Uint8Array, name: string, handle: FileSystemFileHandle | null, ask: boolean): Promise<{ handle: FileSystemFileHandle | null } | false> {
  let target = ask ? null : handle;
  const save = picker().showSaveFilePicker;
  if (!target && save) {
    try { target = await save({ suggestedName: name, types: [{ description: 'Lapin project', accept: { 'application/zip': ['.lapin'] } }] }); } catch { return false; }
  }
  if (target) {
    const w = await target.createWritable();
    await w.write(bytes as BlobPart);
    await w.close();
    return { handle: target };
  }
  download(bytes, name);
  return { handle: null };
}

export interface RecoverableWork { file: string; path: string | null; at: string; edits: number; lastEdit: string | null }

async function pickPath(mode: 'open' | 'save', defaultName?: string, filters = FILTERS): Promise<string | null> {
  if (isDesktop()) {
    const dialog = await import('@tauri-apps/plugin-dialog');
    const r = mode === 'open'
      ? await dialog.open({ multiple: false, directory: false, filters })
      : await dialog.save({ filters, defaultPath: defaultName });
    return typeof r === 'string' ? r : null;
  }
  const ext = filters[0]?.extensions[0] ?? 'lapin';
  const p = window.prompt(mode === 'open' ? `Path of the .${ext} file to open:` : `Save as (full path, .${ext}):`, defaultName ?? '');
  return p?.trim() || null;
}

async function confirmDiscard(what: string): Promise<boolean> {
  if (!useSession.getState().doc?.dirty) return true;
  const question = `The project has unsaved changes. ${what} anyway?`;
  if (isDesktop()) {
    const dialog = await import('@tauri-apps/plugin-dialog');
    return dialog.ask(question, { title: 'Lapin', kind: 'warning' });
  }
  return window.confirm(question);
}

/** The project's name: its file's, without the folder and the extension. */
export const projectTitle = (path: string | null | undefined) => path?.split(/[\\/]/).pop()?.replace(/\.[^.]+$/, '') || 'New project';

const RECENT_KEY = 'lapin.recent';

/** The projects opened or saved last on this computer (a convenience: kept by the browser). */
export function recentProjects(): string[] {
  // The engine in the browser opens what the user picks: there are no paths to go back to.
  if (engineInBrowser) return [];
  try { return JSON.parse(localStorage.getItem(RECENT_KEY) ?? '[]') as string[]; } catch { return []; }
}

function remember(path: string): void {
  try { localStorage.setItem(RECENT_KEY, JSON.stringify([path, ...recentProjects().filter((p) => p !== path)].slice(0, 8))); } catch { /* no storage: no list */ }
}

/** Into the project: the ground floor's plan in front, the 3D open in a tab beside it. */
function enter(): void {
  select(null);
  const plan = emptyPane('plan');
  useUi.setState({ screen: 'workspace', storeyId: null, tool: null, panes: [plan], pane: 0, ...plan });
  useUi.setState((s) => ({ viewTabs: [...s.viewTabs, { ...emptyPane('3d'), storeyId: null }] }));
}

export async function newProject(): Promise<void> {
  if (!(await confirmDiscard('Start a new one'))) return;
  try {
    await engine.call('project.new');
    enter();
  } catch (e) {
    notify(e instanceof Error ? e.message : String(e));
  }
}

function tellOld(notes: string[]): void {
  notify(`Opened an old Lapin project: save it to keep it in the new format.${notes.length ? ` Left out: ${notes.join('; ')}.` : ''}`);
}

export async function openProject(path?: string): Promise<void> {
  if (!(await confirmDiscard('Open another'))) return;
  if (engineInBrowser) {
    const picked = await pickFile('Lapin project', 'lapin');
    if (!picked) return;
    try {
      const r = await engine.call<{ notes?: string[] } | null>('project.openBytes', { name: picked.name, data: toBase64(picked.bytes) }, { timeoutMs: LONG_TIMEOUT_MS });
      // An old project (format 2) opens unsaved: saving must not write the new format over the old file.
      const old = !useSession.getState().doc?.path;
      projectHandle = old ? null : picked.handle;
      enter();
      if (old) tellOld(r?.notes ?? []);
    } catch (e) {
      notify(e instanceof Error ? e.message : String(e));
    }
    return;
  }
  const target = path ?? (await pickPath('open'));
  if (!target) return;
  try {
    const r = await engine.call<{ notes?: string[] } | null>('project.open', { path: target }, { timeoutMs: LONG_TIMEOUT_MS });
    remember(target);
    enter();
    // An old Lapin project (format 2) is rebuilt natively and opens unsaved; say what it left out.
    if (!useSession.getState().doc?.path) tellOld(r?.notes ?? []);
  } catch (e) {
    notify(e instanceof Error ? e.message : String(e));
  }
}

export async function saveProject(as = false): Promise<boolean> {
  const current = useSession.getState().doc?.path ?? null;
  if (engineInBrowser) {
    try {
      const r = await engine.call<{ name: string; data: string }>('project.saveBytes', { name: current ?? 'project.lapin' }, { timeoutMs: LONG_TIMEOUT_MS });
      const kept = await keep(fromBase64(r.data), r.name, projectHandle, as);
      if (!kept) return false;
      projectHandle = kept.handle;
      notify(`Saved ${projectTitle(r.name)}.`);
      return true;
    } catch (e) {
      notify(e instanceof Error ? e.message : String(e));
      return false;
    }
  }
  const target = as || !current ? await pickPath('save', current ?? 'project.lapin') : current;
  if (!target) return false;
  const path = target.endsWith('.lapin') ? target : `${target}.lapin`;
  try {
    await engine.call('project.save', { path }, { timeoutMs: LONG_TIMEOUT_MS });
    remember(path);
    notify(`Saved ${projectTitle(path)}.`);
    return true;
  } catch (e) {
    notify(e instanceof Error ? e.message : String(e));
    return false;
  }
}

// ─── Versions (docs/versions.md) ─────────────────────────────────────────

export interface VersionRevision { at: string; note: string | null; entities: number; size: number }
export interface ProjectVersion { number: number; name: string; note: string | null; at: string; updatedAt: string; entities: number; size: number; parts: number; app: string; revisions: VersionRevision[] }

/** The open project's versions, as its file holds them. */
export const listVersions = () => engine.call<ProjectVersion[]>('project.versions').catch(() => [] as ProjectVersion[]);

/**
 * A version call that writes the project's file (a version lives there): on
 * the desktop in place, so the project needs a file first (Save as); with the
 * engine in the browser the file comes back as bytes and is kept like a save.
 * False: the user stepped back; a refusal is thrown.
 */
export async function writeVersions(method: string, args: Record<string, unknown>): Promise<boolean> {
  if (engineInBrowser) {
    const r = await engine.call<{ name: string; data: string }>(method, { ...args, bytes: true }, { timeoutMs: LONG_TIMEOUT_MS });
    const kept = await keep(fromBase64(r.data), r.name, projectHandle, false);
    if (!kept) return false;
    projectHandle = kept.handle;
    return true;
  }
  if (!useSession.getState().doc?.path) {
    notify('Save the project first: its versions live in its file.');
    if (!await saveProject(true)) return false;
  }
  await engine.call(method, args, { timeoutMs: LONG_TIMEOUT_MS });
  return true;
}

/** A version as a .lapin file of its own, where the user says (the desktop) or as a download. */
export const exportVersion = (number: number, revision: number | null, name: string) =>
  exportFile('project.exportVersion', { number, ...(revision == null ? {} : { revision }) }, name, 'lapin');

/** A version written to a file of its own and opened in another Lapin window (the desktop), to look at or edit there. */
export async function openVersionSeparately(number: number, revision: number | null): Promise<void> {
  const r = await engine.call<{ path: string }>('project.exportVersion', { number, ...(revision == null ? {} : { revision }), temp: true }, { timeoutMs: LONG_TIMEOUT_MS });
  const { invoke } = await import('@tauri-apps/api/core');
  await invoke('open_separately', { path: r.path });
  notify('Opening the version in another Lapin window.');
}

export const listRecovery = () => engine.call<RecoverableWork[]>('recovery.list').catch(() => [] as RecoverableWork[]);

export async function restoreWork(file: string): Promise<void> {
  try {
    await engine.call('recovery.restore', { path: file }, { timeoutMs: LONG_TIMEOUT_MS });
    enter();
    notify('Your unsaved work is back. Save it to keep it.');
  } catch (e) {
    notify(e instanceof Error ? e.message : String(e));
  }
}

export const discardWork = (file: string) => engine.call('recovery.discard', { path: file });

/** The desktop window asks before closing on unsaved work. */
export async function guardWindowClose(): Promise<void> {
  if (!isDesktop()) {
    // A browser tab: the browser asks before leaving unsaved work (the journal keeps it besides).
    window.addEventListener('beforeunload', (e) => { if (useSession.getState().doc?.dirty) e.preventDefault(); });
    return;
  }
  const { getCurrentWindow } = await import('@tauri-apps/api/window');
  const win = getCurrentWindow();
  await win.onCloseRequested(async (e) => {
    if (!useSession.getState().doc?.dirty) return;
    e.preventDefault();
    const dialog = await import('@tauri-apps/plugin-dialog');
    const save = await dialog.ask('Save the changes to the project before closing?', { title: 'Lapin', kind: 'warning', okLabel: 'Save', cancelLabel: "Don't save" });
    // Saving cancelled in the file dialog: the window stays.
    if (save && !(await saveProject())) return;
    if (!save) {
      // A second thought is still possible; only a confirmed "close" lets the work go (and no recovery is offered next time).
      if (!(await dialog.confirm('Close without saving? The changes will be lost.', { title: 'Lapin', kind: 'warning', okLabel: 'Close', cancelLabel: 'Cancel' }))) return;
      await engine.call('project.new').catch(() => undefined);
    }
    await win.destroy();
  });
}

/**
 * An export from the engine (PDF, DXF): to the path the save dialog gives on
 * the desktop, else handed back and downloaded by the browser.
 */
const MIME: Record<string, string> = { pdf: 'application/pdf', dxf: 'application/dxf', ifc: 'application/x-step', glb: 'model/gltf-binary', gltf: 'model/gltf+json', usdz: 'model/vnd.usdz+zip', usda: 'model/vnd.usda', dae: 'model/vnd.collada+xml', frag: 'application/octet-stream' };

export async function exportFile(method: string, args: Record<string, unknown>, name: string, ext: string): Promise<void> {
  try {
    if (isDesktop()) {
      const dialog = await import('@tauri-apps/plugin-dialog');
      const path = await dialog.save({ defaultPath: name, filters: [{ name: ext.toUpperCase(), extensions: [ext] }] });
      if (typeof path !== 'string') return;
      await engine.call(method, { ...args, path }, { timeoutMs: LONG_TIMEOUT_MS });
      notify(`Exported ${path.split(/[\\/]/).pop()}.`);
      return;
    }
    const r = await engine.call<{ data: string }>(method, args, { timeoutMs: LONG_TIMEOUT_MS });
    const bytes = Uint8Array.from(atob(r.data), (c) => c.charCodeAt(0));
    const url = URL.createObjectURL(new Blob([bytes], { type: MIME[ext] ?? 'application/octet-stream' }));
    const a = document.createElement('a');
    a.href = url; a.download = name; a.click();
    setTimeout(() => URL.revokeObjectURL(url), 10_000);
    notify(`Exported ${name}.`);
  } catch (e) {
    notify(e instanceof Error ? e.message : String(e));
  }
}

/**
 * The project as IFC (docs/spec-ifc.md): IFC4 by default, IFC4X3 on request; parts grouped under their element unless `detail` is 'simple';
 * bodies as faceted B-reps, or as face sets (`geometry: 'faceset'`), simplified within `tolerance` (metres) when one is given.
 */
export async function exportIfc(options: { schema?: string; detail?: string; phase?: string; geometry?: string; tolerance?: number } = {}): Promise<void> {
  const path = useSession.getState().doc?.path ?? null;
  await exportFile('project.exportIfc', options, `${projectTitle(path)}.ifc`, 'ifc');
}

/**
 * The file this instance of the desktop app was started for (a link opened on
 * its own from Manage → Links): a new project with the IFC imported natively,
 * or the DXF import dialog on a new project's plan.
 */
export async function openStartupFile(): Promise<void> {
  if (!isDesktop()) return;
  try {
    const { invoke } = await import('@tauri-apps/api/core');
    const path = await invoke<string | null>('startup_file');
    if (!path) return;
    const name = path.split(/[\\/]/).pop() ?? path;
    // An IFC brings its own storeys: the new project starts with a ground floor only.
    await engine.call('project.new', { plain: !/\.dxf$/i.test(name) });
    enter();
    if (/\.dxf$/i.test(name)) { dxfFile.current = { name, path }; useUi.setState({ dialog: 'importDxf' }); return; }
    // An IFC opened on its own: what of it comes in chosen first, as for an import.
    await surveyIfc({ name, path }, 'auto');
  } catch (e) {
    notify(e instanceof Error ? e.message : String(e));
  }
}

/** The DXF the user picked, waiting for the import dialog: by path (the desktop), or its bytes (the browser). */
export const dxfFile: { current: { name: string; path?: string; data?: string } | null } = { current: null };

/** A DXF into the view in front, as Lapin's own drafting: the file is picked here, the rest is asked in the import dialog. */
export async function importDxf(): Promise<void> {
  const picked = isDesktop() ? null : await pickFile('DXF', 'dxf');
  const path = isDesktop() ? await pickPath('open', undefined, [{ name: 'DXF', extensions: ['dxf'] }]) : picked?.name ?? null;
  if (!path) return;
  dxfFile.current = picked ? { name: picked.name, data: toBase64(picked.bytes) } : { name: path.split(/[\\/]/).pop() ?? path, path };
  useUi.setState({ dialog: 'importDxf' });
}

/**
 * A That Open Fragments file (.frag, docs/fragments.md) into the open project: its items as references with their
 * bodies, on their storeys, identical bodies shared. By path on the desktop, by its bytes in a browser.
 */
export async function importFragments(): Promise<void> {
  const picked = isDesktop() ? null : await pickFile('Fragments', 'frag');
  const path = isDesktop() ? await pickPath('open', undefined, [{ name: 'Fragments', extensions: ['frag'] }]) : picked?.name ?? null;
  if (!path) return;
  const source = picked ? { name: picked.name, data: toBase64(picked.bytes) } : { name: path.split(/[\\/]/).pop() ?? path, path };
  try {
    const r = await engine.call<{ elements: number; shared: number; storeys: number; skipped: number; notes: string[] }>('project.importFragments', source, { timeoutMs: LONG_TIMEOUT_MS });
    const parts = [`${r.elements} element${r.elements === 1 ? '' : 's'} as references`];
    if (r.shared) parts.push(`${r.shared} shared bod${r.shared === 1 ? 'y' : 'ies'}`);
    if (r.storeys) parts.push(`${r.storeys} storey${r.storeys === 1 ? '' : 's'} made`);
    if (r.skipped) parts.push(`${r.skipped} left out`);
    notify(`Imported ${source.name}: ${parts.join(', ')}.${r.notes.length ? ' ' + r.notes.join(' ') : ''}`);
  } catch (e) {
    notify(e instanceof Error ? e.message : String(e));
  }
}

/**
 * An IFC file into the open project, in two steps (ifcImport.ts): the file looked at, the Import IFC dialog to choose
 * what of it comes in (by discipline, kind and storey) and how (recognised, as references, linked), then the import
 * with its stages shown.
 */
export async function importIfc(mode: 'auto' | 'reference' | 'link' = 'auto'): Promise<void> {
  // By path on the desktop (the native dialog); in a browser, the file's bytes from the browser's own
  // picker, whether the engine runs in the page or as the local process of pnpm dev: a page never knows a path.
  const picked = isDesktop() ? null : await pickFile('IFC', 'ifc');
  const path = isDesktop() ? await pickPath('open', undefined, [{ name: 'IFC', extensions: ['ifc'] }]) : picked?.name ?? null;
  if (!path) return;
  const source = picked ? { name: picked.name, data: toBase64(picked.bytes) } : { name: path.split(/[\\/]/).pop() ?? path, path };
  await surveyIfc(source, mode);
}
