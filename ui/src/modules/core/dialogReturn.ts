/**
 * A way out of a type dialog and back (forum #45): there is one dialog slot, so "Open in Materials…" in Wall types
 * (or Floor types) would close the dialog and lose what was typed in it. The dialog hands its editor state over here,
 * Materials opens on the material, and its "Back to …" button opens the dialog again, which takes the state back.
 * A state nobody came back for (Materials closed instead) is dropped when Materials unmounts.
 */

import { useUi } from '../../app/store';

const stash = new Map<string, unknown>();

/** Opens Manage → Materials on `material`'s 2D graphics tab, keeping `snapshot` of the editor for `kind` to come back to. */
export function openMaterialsFrom(kind: string, material: string, snapshot: unknown): void {
  stash.set(kind, snapshot);
  useUi.setState({ materialFocus: { name: material, tab: 'graphics' }, returnTo: kind, dialog: 'materials' });
}

/** The editor state a dialog left on its way to Materials, once (null when it was opened on its own). */
export function takeStash<T>(kind: string): T | null {
  const v = stash.get(kind);
  stash.delete(kind);
  return (v as T | undefined) ?? null;
}

/** Materials goes back to the dialog it came from. */
export function returnToDialog(): void {
  const to = useUi.getState().returnTo;
  if (to) useUi.setState({ returnTo: null, materialFocus: null, dialog: to });
}

/** Materials left (not back to the dialog): what was kept is dropped. */
export function dropStash(): void {
  const to = useUi.getState().returnTo;
  const dialog = useUi.getState().dialog;
  if (dialog === 'materials') return; // a remount of the dialog itself, not a way out
  if (to && dialog !== to) stash.delete(to);
  useUi.setState({ returnTo: null, materialFocus: null });
}
