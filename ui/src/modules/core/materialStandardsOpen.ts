/** Opening Material standards on a tab of choice (apart from the dialog, so a caller does not load it). */

import { useUi } from '../../app/store';

export type MaterialStandardsTab = 'project' | 'store';
let next: MaterialStandardsTab | null = null;

/** Opens the dialog, on the tab asked for. */
export function openMaterialStandards(tab?: MaterialStandardsTab): void {
  next = tab ?? null;
  useUi.setState({ dialog: 'materialStandards' });
}

/** The tab the dialog was opened on, read once while it first renders. */
export function takeMaterialStandardsTab(): MaterialStandardsTab | null {
  const t = next;
  next = null;
  return t;
}
