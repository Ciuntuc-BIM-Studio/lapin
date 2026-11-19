/**
 * Opening Drafting standards on a tab of choice (the project's units are on "Project"): from Ask Lapin, the status bar, and
 * a dimension's, text's or tag's properties (forum topic 173: "Edit style…" opens its tab with its style picked out).
 */

import { useUi } from '../../app/store';

/** A tab of the dialog: the project's options, or a list (text styles, dimension styles, grid axes, tags). */
export type StandardsTab = 'project' | 'text' | 'dim' | 'axis' | 'tag';
let next: StandardsTab | null = null;
let focus: string | null = null;

/** Opens Drafting standards, on the tab asked for (Project, Text, Dimensions, Grid axes, Tags), with one of its styles picked out. */
export function openStandards(tab?: StandardsTab, styleId?: string | null): void {
  next = tab ?? null;
  focus = tab && tab !== 'project' ? styleId ?? null : null;
  useUi.setState({ dialog: 'standards' });
}

/** The tab the dialog was opened on (read while it first renders, which may happen twice). */
export const standardsTab = (): StandardsTab | null => next;

/** The style the dialog was opened on, to pick it out in its tab; null: none. */
export const standardsFocus = (): string | null => focus;

/** Forgets them once the dialog is up, so the next opening starts on the first tab. */
export function clearStandardsTab(): void { next = null; focus = null; }
