/**
 * The side panels of the workspace: the project browser on the left, the
 * properties on the right. Each has a width the user drags, and can be
 * folded to a narrow rail and unfolded (its header's button, the rail's, a
 * double-click on its separator, Ctrl+B and Ctrl+Shift+B), in every
 * interface mode. A folded panel opens for a moment over the view from its
 * rail (`overlay`), without unfolding. Focus hides the rails too. Kept in
 * the browser's storage, a convenience for this computer.
 */

import { create } from 'zustand';

export interface PanelsState {
  browserWidth: number;
  propsWidth: number;
  browserOpen: boolean;
  propsOpen: boolean;
  /** A folded panel shown over the view for a moment (not kept), on its tab when it has tabs. */
  overlay?: { side: 'browser' | 'props'; tab?: PanelTab } | null;
  /** Folded panels show no rail (Focus): Tab or the shortcuts bring them back. */
  railsHidden?: boolean;
  /** What the right panel shows: the properties, or the assistant (desktop only, docs/plan-assistant.md). */
  propsTab?: 'properties' | 'assistant';
}

/** A panel's tab: the browser's project, levels and IFC tree; the right panel's assistant. */
export type PanelTab = 'project' | 'levels' | 'ifc' | 'assistant';

const KEY = 'lapin.panels';
export const MIN_WIDTH = 180, MAX_WIDTH = 640, RAIL = 36;
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
  const { browserWidth, propsWidth, browserOpen, propsOpen } = usePanels.getState();
  try { localStorage.setItem(KEY, JSON.stringify({ browserWidth, propsWidth, browserOpen, propsOpen })); } catch { /* a convenience only */ }
}

export function setPanelWidth(side: 'browser' | 'props', width: number): void {
  usePanels.setState(side === 'browser' ? { browserWidth: clamp(width) } : { propsWidth: clamp(width) });
  save();
}

export function togglePanel(side: 'browser' | 'props'): void {
  const s = usePanels.getState();
  usePanels.setState({ ...(side === 'browser' ? { browserOpen: !s.browserOpen } : { propsOpen: !s.propsOpen }), overlay: null });
  save();
}

/** Both panels folded when either is open, else both open (Tab). */
export function toggleBothPanels(): void {
  const s = usePanels.getState();
  const open = !(s.browserOpen || s.propsOpen);
  usePanels.setState({ browserOpen: open, propsOpen: open, overlay: null });
  save();
}

/** A folded panel over the view for a moment; again: closed. */
export function showOverlay(side: 'browser' | 'props', tab?: PanelTab): void {
  const cur = usePanels.getState().overlay;
  usePanels.setState({ overlay: cur && cur.side === side && cur.tab === tab ? null : { side, tab } });
}

/** The right panel on the assistant (unfolded if it was folded), or back on the properties. */
export function showPropsTab(tab: 'properties' | 'assistant'): void {
  usePanels.setState({ propsTab: tab, propsOpen: true, overlay: null });
  save();
}
export const closeOverlay = () => { if (usePanels.getState().overlay) usePanels.setState({ overlay: null }); };

/** The interface's panels part: what is open, and whether the folded ones keep a rail. */
export function applyPanelsPart(part: 'docked' | 'browserRail' | 'bothRail' | 'hidden', propsWidth?: number): void {
  usePanels.setState({
    browserOpen: part === 'docked', propsOpen: part === 'docked' || part === 'browserRail',
    railsHidden: part === 'hidden', overlay: null,
    ...(propsWidth ? { propsWidth: clamp(propsWidth) } : {}),
  });
  save();
}
/** The grid's columns: each panel's width (or its rail), a separator beside each open panel, the views between. */
export function columns(s: PanelsState): string {
  const rail = s.railsHidden ? 0 : RAIL;
  const left = s.browserOpen ? `${s.browserWidth}px 6px` : `${rail}px 0px`;
  const right = s.propsOpen ? `6px ${s.propsWidth}px` : `0px ${rail}px`;
  return `${left} minmax(0, 1fr) ${right}`;
}
