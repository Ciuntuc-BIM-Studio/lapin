/**
 * Edit type… (forum topic 176, decision 735): a types dialog opened on one type, the selected element's, rather than on the
 * first of its list. Whoever opens a dialog for a type says which (`openTypeDialog`); the dialog asks `typeToEdit` as it
 * mounts. The type asked for holds only while that dialog stays open: another dialog, or the same one opened again from the
 * ribbon, starts as it always did.
 */

import { useUi } from '../app/store';

let pending: { dialog: string; id: string } | null = null;

useUi.subscribe((s) => { if (pending && s.dialog !== pending.dialog) pending = null; });

/** Opens `dialog` on the type `id` (none: as the ribbon opens it). */
export function openTypeDialog(dialog: string, id: string | null | undefined): void {
  pending = id ? { dialog, id } : null;
  useUi.setState({ dialog });
}

/** The type `dialog` was opened on by `openTypeDialog`, while it is the open dialog; null otherwise. */
export function typeToEdit(dialog: string): string | null {
  return pending && pending.dialog === dialog && useUi.getState().dialog === dialog ? pending.id : null;
}
