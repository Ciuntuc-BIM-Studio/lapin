/** Opening Drafting standards on a tab of choice (the project's units are on "Project"): from Ask Lapin, the status bar. */

import { useUi } from '../../app/store';

let next: 'project' | null = null;

/** Opens Drafting standards, on its Project tab when asked. */
export function openStandards(tab?: 'project'): void {
  next = tab ?? null;
  useUi.setState({ dialog: 'standards' });
}

/** The tab the dialog was opened on (read while it first renders, which may happen twice). */
export const standardsTab = (): 'project' | null => next;

/** Forgets it once the dialog is up, so the next opening starts on the first tab. */
export function clearStandardsTab(): void { next = null; }
