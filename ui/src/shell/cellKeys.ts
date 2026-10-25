/** The properties panel's keys: Enter commits and moves down, the arrows move between cells, focusing a cell selects its text. */

import type React from 'react';
import { parseAngle } from '../viewport/liveInput';

/** The panel's editable cells, in order (inputs that take text, and selects). */
const cells = (from: Element) => [...(from.closest('.props')?.querySelectorAll<HTMLElement>('.fields input:not([type=checkbox]):not([type=color]):not([disabled]), .fields select:not([disabled])') ?? [])];

/** Focus the cell `step` places after (or before) this one. */
export function moveFocus(from: HTMLElement, step: number): void {
  const all = cells(from);
  const i = all.indexOf(from);
  const next = all[i + step];
  if (next) { next.focus(); if (next instanceof HTMLInputElement) next.select(); }
}

/** Keys for the whole panel: Enter commits and moves down, arrows move, focus selects. */
export function panelKeys(e: React.KeyboardEvent): void {
  const t = e.target as HTMLElement;
  if (!t.closest('.fields') || e.defaultPrevented) return;
  const text = t instanceof HTMLInputElement && t.type !== 'checkbox' && t.type !== 'color';
  if (e.key === 'Enter' && (text || t instanceof HTMLSelectElement)) { e.preventDefault(); t.blur(); moveFocus(t, e.shiftKey ? -1 : 1); }
  else if (text && (e.key === 'ArrowDown' || e.key === 'ArrowUp')) { e.preventDefault(); t.blur(); moveFocus(t, e.key === 'ArrowDown' ? 1 : -1); }
}
export function panelFocus(e: React.FocusEvent): void {
  const t = e.target;
  if (t instanceof HTMLInputElement && t.closest('.fields') && t.type !== 'checkbox' && t.type !== 'color') requestAnimationFrame(() => t.select());
}

/** A number typed in a cell: a sum or difference allowed. */
export const parseNumber = (text: string): number | null => parseAngle(text);

