/**
 * Projects on disk: New, Open, Save, Save as, recovery of unsaved work. In
 * the desktop app the paths come from the native dialogs; in a browser
 * (pnpm dev) they are typed in.
 */

import { engine, useSession } from './session';
import { LONG_TIMEOUT_MS } from './client';
import { notify, select, useUi } from '../app/store';

const FILTERS = [{ name: 'Lapin project', extensions: ['lapin'] }];

export const isDesktop = () => Boolean((window as unknown as { __TAURI_INTERNALS__?: unknown }).__TAURI_INTERNALS__);

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
  try { return JSON.parse(localStorage.getItem(RECENT_KEY) ?? '[]') as string[]; } catch { return []; }
}

function remember(path: string): void {
  try { localStorage.setItem(RECENT_KEY, JSON.stringify([path, ...recentProjects().filter((p) => p !== path)].slice(0, 8))); } catch { /* no storage: no list */ }
}

function enter(): void {
  select(null);
  useUi.setState({ screen: 'workspace', storeyId: null, tool: null });
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

export async function openProject(path?: string): Promise<void> {
  if (!(await confirmDiscard('Open another'))) return;
  const target = path ?? (await pickPath('open'));
  if (!target) return;
  try {
    const r = await engine.call<{ notes?: string[] } | null>('project.open', { path: target }, { timeoutMs: LONG_TIMEOUT_MS });
    remember(target);
    enter();
    // An old Lapin project (format 2) is rebuilt natively and opens unsaved; say what it left out.
    if (!useSession.getState().doc?.path) {
      const notes = r?.notes ?? [];
      notify(`Opened an old Lapin project: save it to keep it in the new format.${notes.length ? ` Left out: ${notes.join('; ')}.` : ''}`);
    }
  } catch (e) {
    notify(e instanceof Error ? e.message : String(e));
  }
}

export async function saveProject(as = false): Promise<boolean> {
  const current = useSession.getState().doc?.path ?? null;
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
  if (!isDesktop()) return;
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
const MIME: Record<string, string> = { pdf: 'application/pdf', dxf: 'application/dxf', ifc: 'application/x-step' };

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

/** The project as IFC (docs/spec-ifc.md): IFC4 by default, IFC4X3 on request; parts grouped under their element unless `detail` is 'simple'. */
export async function exportIfc(options: { schema?: string; detail?: string; phase?: string } = {}): Promise<void> {
  const path = useSession.getState().doc?.path ?? null;
  await exportFile('project.exportIfc', options, `${projectTitle(path)}.ifc`, 'ifc');
}

/** An IFC file into the open project: Lapin's own files come back native, others as references (mode 'reference' keeps everything as it is). */
export async function importIfc(mode: 'auto' | 'reference' = 'auto'): Promise<void> {
  const path = await pickPath('open', undefined, [{ name: 'IFC', extensions: ['ifc'] }]);
  if (!path) return;
  try {
    const r = await engine.call<{ elements: number; updated: number; references: number; notes: string[] }>('project.importIfc', { path, mode }, { timeoutMs: LONG_TIMEOUT_MS });
    const parts = [
      r.elements ? `${r.elements} new` : '',
      r.updated ? `${r.updated} updated` : '',
      r.references ? `${r.references} as reference` : '',
    ].filter(Boolean);
    notify(`Imported ${path.split(/[\\/]/).pop()}: ${parts.join(', ') || 'nothing to bring in'}.${r.notes?.length ? ` ${r.notes.join(' ')}` : ''}`);
  } catch (e) {
    notify(e instanceof Error ? e.message : String(e));
  }
}
