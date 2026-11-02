/**
 * What the 2D views draw for the user's eyes only (a tester's report, decision 247): the plan's background
 * grid (F7, as AutoCAD's), and thin lines (TL, as Revit's; AutoCAD's LWT off): every line one pixel, whatever
 * its pen. Kept per user; the drawings themselves, sheets, PDF and DXF keep their pens, and snapping to the
 * grid stays as it is.
 */

import { create } from 'zustand';

export interface Display { grid: boolean; thinLines: boolean }

const KEY = 'lapin.display';

/** What was kept, anything unreadable left at its default (the grid shown, the pens' widths). */
export function readDisplay(stored: unknown): Display {
  const o = stored as Partial<Display> | null;
  return { grid: !(o && typeof o === 'object' && o.grid === false), thinLines: !!(o && typeof o === 'object' && o.thinLines === true) };
}

function read(): Display {
  try { return readDisplay(JSON.parse(localStorage.getItem(KEY) ?? 'null')); } catch { return readDisplay(null); }
}

export const useDisplay = create<Display>(() => read());

export function setDisplay(patch: Partial<Display>): void {
  useDisplay.setState(patch);
  try { localStorage.setItem(KEY, JSON.stringify(useDisplay.getState())); } catch { /* kept for this session */ }
}

export const toggleGrid = () => setDisplay({ grid: !useDisplay.getState().grid });
export const toggleThinLines = () => setDisplay({ thinLines: !useDisplay.getState().thinLines });

/** A pen's width on screen (px): its paper width at the view's scale, within what a screen shows well; one pixel with thin lines. */
export const screenWidth = (paperMm: number, pxPerMm: number, thin: boolean): number => (thin ? 1 : Math.min(4, Math.max(0.5, paperMm * pxPerMm)));
