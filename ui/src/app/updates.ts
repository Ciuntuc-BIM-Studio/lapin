/**
 * Updates of the desktop app (Tauri's updater): a newer build of this edition
 * is looked for at start and from the help menu, at
 * lapinbim.com/updates/<product>/ and, second, ciuntucbimstudio.ro/downloads/<product>-update/
 * (the same files; one manifest per platform and kind of package). Each package is signed with Ciuntuc BIM Studio's
 * update key and the signature carries its version; the app refuses anything
 * else. Asked first, with the build's notes; unsaved work is saved (or let go)
 * before; the package downloaded, the engine stopped, the update installed and
 * the app restarted. Nothing happens in a browser or a development build.
 */

import { notify } from './store';
import { phrase } from '../shell/prompts';
import { useSession } from '../engine/session';
import { isDesktop, saveProject } from '../engine/files';

let busy = false;

/** The most the update window shows of a build's notes: its size follows its text, so a long list would outgrow a small screen, and the window cannot be moved to reach its buttons (report 195). */
export const NOTES_LINES = 10;
export const NOTES_CHARS = 700;

/** The notes as the update window shows them, after a blank line: the first lines, cut at a line's end, with an ellipsis when more follows; nothing when there are none. */
export function shortNotes(body: string | null | undefined): string {
  const text = (body ?? '').replace(/\r\n?/g, '\n').trim();
  if (!text) return '';
  const out: string[] = [];
  let used = 0;
  for (const raw of text.split('\n')) {
    const line = raw.length > 160 ? `${raw.slice(0, 159)}…` : raw;
    if (out.length >= NOTES_LINES || used + line.length > NOTES_CHARS) return `\n\n${out.join('\n').trimEnd()}\n…`;
    out.push(line);
    used += line.length + 1;
  }
  return `\n\n${out.join('\n')}`;
}

/** Look for an update; `manual` (the help menu) also says when there is none or the check failed. */
export async function checkForUpdates(manual = false): Promise<void> {
  if (!isDesktop() || busy) return;
  if (!import.meta.env.PROD) { if (manual) notify('Updates are checked in the built app, not in development.'); return; }
  busy = true;
  try {
    const { check } = await import('@tauri-apps/plugin-updater');
    const update = await check();
    if (!update) { if (manual) notify('Lapin is up to date.'); return; }
    const dialog = await import('@tauri-apps/plugin-dialog');
    const install = await dialog.ask(
      `${phrase('Lapin {version} is available (this is {current}).', { version: update.version, current: update.currentVersion })}${shortNotes(update.body)}\n\n${phrase('Install it now? Lapin restarts when it is done.')}`,
      { title: phrase('Lapin update'), kind: 'info', okLabel: phrase('Install and restart'), cancelLabel: phrase('Later') },
    );
    if (!install || !(await letWorkGo(dialog))) return;
    let total = 0;
    let got = 0;
    let shown = -1;
    await update.download((e) => {
      if (e.event === 'Started') total = e.data.contentLength ?? 0;
      else if (e.event === 'Progress') {
        got += e.data.chunkLength;
        const pct = total ? Math.floor((got / total) * 100) : -1;
        if (pct !== shown && pct % 5 === 0) { shown = pct; notify(`Downloading Lapin ${update.version}… ${pct}%`); }
      }
    });
    notify(`Installing Lapin ${update.version}…`);
    const { invoke } = await import('@tauri-apps/api/core');
    const { relaunch } = await import('@tauri-apps/plugin-process');
    await invoke('stop_engine');
    try {
      await update.install();
    } catch (e) {
      // The engine is stopped: start afresh, on the version still installed.
      await dialog.message(`${phrase('The update could not be installed:')} ${e instanceof Error ? e.message : String(e)}\n\n${phrase('Lapin restarts as it was.')}`, { title: phrase('Lapin update'), kind: 'error' });
    }
    await relaunch();
  } catch (e) {
    if (manual) notify(`Could not look for updates: ${e instanceof Error ? e.message : String(e)}`);
  } finally {
    busy = false;
  }
}

/** Unsaved work before the restart: saved, or let go when the user says so. */
async function letWorkGo(dialog: typeof import('@tauri-apps/plugin-dialog')): Promise<boolean> {
  if (!useSession.getState().doc?.dirty) return true;
  const save = await dialog.ask(phrase('Save the changes to the project before updating?'), { title: phrase('Lapin update'), kind: 'warning', okLabel: phrase('Save'), cancelLabel: phrase("Don't save") });
  if (save) return saveProject();
  return dialog.confirm(phrase('Update without saving? The changes will be lost.'), { title: phrase('Lapin update'), kind: 'warning', okLabel: phrase('Update'), cancelLabel: phrase('Cancel') });
}
