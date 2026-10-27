/**
 * Updates of the desktop app (Tauri's updater): a newer build of this edition
 * is looked for at start and from the help menu, at
 * ciuntucbimstudio.ro/downloads/<product>-update/ (one manifest per platform
 * and kind of package). Each package is signed with Ciuntuc BIM Studio's
 * update key and the signature carries its version; the app refuses anything
 * else. Asked first, with the build's notes; unsaved work is saved (or let go)
 * before; the package downloaded, the engine stopped, the update installed and
 * the app restarted. Nothing happens in a browser or a development build.
 */

import { notify } from './store';
import { useSession } from '../engine/session';
import { isDesktop, saveProject } from '../engine/files';

let busy = false;

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
    const notes = update.body?.trim();
    const install = await dialog.ask(
      `Lapin ${update.version} is available (this is ${update.currentVersion}).${notes ? `\n\n${notes}` : ''}\n\nInstall it now? Lapin restarts when it is done.`,
      { title: 'Lapin update', kind: 'info', okLabel: 'Install and restart', cancelLabel: 'Later' },
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
      await dialog.message(`The update could not be installed: ${e instanceof Error ? e.message : String(e)}\n\nLapin restarts as it was.`, { title: 'Lapin update', kind: 'error' });
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
  const save = await dialog.ask('Save the changes to the project before updating?', { title: 'Lapin update', kind: 'warning', okLabel: 'Save', cancelLabel: "Don't save" });
  if (save) return saveProject();
  return dialog.confirm('Update without saving? The changes will be lost.', { title: 'Lapin update', kind: 'warning', okLabel: 'Update', cancelLabel: 'Cancel' });
}
