/** Opening Drafting standards on a tab of choice (the project's units are on "Project"): from Ask Lapin, the status bar. */

import { useUi } from '../../app/store';

/** A tab of the dialog: the project's options, or a list (grid axes, tags). */
export type StandardsTab = 'project' | 'axis' | 'tag';
let next: StandardsTab | null = null;

/** Opens Drafting standards, on the tab asked for (Project, Grid axes, Tags). */
export function openStandards(tab?: StandardsTab): void {
  next = tab ?? null;
  useUi.setState({ dialog: 'standards' });
}

/** The tab the dialog was opened on (read while it first renders, which may happen twice). */
export const standardsTab = (): StandardsTab | null => next;

/** Forgets it once the dialog is up, so the next opening starts on the first tab. */
export function clearStandardsTab(): void { next = null; }
