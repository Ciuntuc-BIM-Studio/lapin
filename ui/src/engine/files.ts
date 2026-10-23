/**
 * Projects on disk: New, Open, Save, Save as, recovery of unsaved work. In
 * the desktop app the paths come from the native dialogs; in a browser
 * (pnpm dev) they are typed in.
 */

import { engine, useSession } from './session';
import { notify, select, useUi } from '../app/store';

const FILTERS = [{ name: 'Lapin project', extensions: ['lapin'] }];

export const isDesktop = () => Boolean((window as unknown as { __TAURI_INTERNALS__?: unknown }).__TAURI_INTERNALS__);

export interface RecoverableWork { file: string; path: string | null; at: string; edits: number; lastEdit: string | null }

async function pickPath(mode: 'open' | 'save', defaultName?: string): Promise<string | null> {
  if (isDesktop()) {
    const dialog = await import('@tauri-apps/plugin-dialog');
    const r = mode === 'open'
      ? await dialog.open({ multiple: false, directory: false, filters: FILTERS })
      : await dialog.save({ filters: FILTERS, defaultPath: defaultName });
    return typeof r === 'string' ? r : null;
  }
  const p = window.prompt(mode === 'open' ? 'Path of the .lapin project to open:' : 'Save as (full path, .lapin):', defaultName ?? '');
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
  await engine.call('project.new');
  enter();
}

export async function openProject(path?: string): Promise<void> {
  if (!(await confirmDiscard('Open another'))) return;
  const target = path ?? (await pickPath('open'));
  if (!target) return;
  try {
    await engine.call('project.open', { path: target });
    remember(target);
    enter();
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
    await engine.call('project.save', { path });
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
  await engine.call('recovery.restore', { path: file });
  enter();
  notify('Your unsaved work is back. Save it to keep it.');
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
    if (save && !(await saveProject())) return;
    // Not saved on purpose: the work is let go, so no recovery is offered next time.
    if (!save) await engine.call('project.new').catch(() => undefined);
    await win.destroy();
  });
}
