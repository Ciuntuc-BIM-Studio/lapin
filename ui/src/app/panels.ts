/**
 * The side panels of the workspace: the project browser on the left, the
 * properties on the right. Each has a width the user drags, and can be
 * docked away to a narrow rail and brought back. Kept in the browser's
 * storage, a convenience for this computer.
 */

import { create } from 'zustand';

export interface PanelsState {
  browserWidth: number;
  propsWidth: number;
  browserOpen: boolean;
  propsOpen: boolean;
}

const KEY = 'lapin.panels';
export const MIN_WIDTH = 180, MAX_WIDTH = 640, RAIL = 30;
const DEFAULTS: PanelsState = { browserWidth: 250, propsWidth: 300, browserOpen: true, propsOpen: true };

function load(): PanelsState {
  try {
    const raw = localStorage.getItem(KEY);
    if (!raw) return DEFAULTS;
    const p = JSON.parse(raw) as Partial<PanelsState>;
    return {
      browserWidth: clamp(Number(p.browserWidth) || DEFAULTS.browserWidth),
      propsWidth: clamp(Number(p.propsWidth) || DEFAULTS.propsWidth),
      browserOpen: p.browserOpen !== false,
      propsOpen: p.propsOpen !== false,
    };
  } catch { return DEFAULTS; }
}

export const clamp = (w: number) => Math.max(MIN_WIDTH, Math.min(MAX_WIDTH, Math.round(w)));

export const usePanels = create<PanelsState>(() => load());

function save(): void {
  try { localStorage.setItem(KEY, JSON.stringify(usePanels.getState())); } catch { /* a convenience only */ }
}

export function setPanelWidth(side: 'browser' | 'props', width: number): void {
  usePanels.setState(side === 'browser' ? { browserWidth: clamp(width) } : { propsWidth: clamp(width) });
  save();
}

export function togglePanel(side: 'browser' | 'props'): void {
  const s = usePanels.getState();
  usePanels.setState(side === 'browser' ? { browserOpen: !s.browserOpen } : { propsOpen: !s.propsOpen });
  save();
}

/** The grid's columns: each panel's width (or its rail), a separator beside each open panel, the views between. */
export function columns(s: PanelsState): string {
  const left = s.browserOpen ? `${s.browserWidth}px 6px` : `${RAIL}px 0px`;
  const right = s.propsOpen ? `6px ${s.propsWidth}px` : `0px ${RAIL}px`;
  return `${left} minmax(0, 1fr) ${right}`;
}
