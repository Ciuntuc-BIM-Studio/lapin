/**
 * The side panels of the workspace: the project browser on the left, the
 * properties on the right. Each has a width the user drags, and can be
 * folded to a narrow rail and unfolded (its header's button, the rail's, a
 * double-click on its separator, Ctrl+B and Ctrl+Shift+B), in every
 * interface mode. A folded panel opens for a moment over the view from its
 * rail (`overlay`), without unfolding. Focus hides the rails too. Kept in
 * the browser's storage, a convenience for this computer.
 *
 * Each panel stands on the left or the right (View → Panels; decision 427, a tester's report #123): the
 * browser left and the properties right (the default), swapped, or both on one side, stacked with a
 * divider the user drags (`stackRatio`: the browser's share of the height). A side is as wide as its
 * first panel (the browser's width when the browser is there).
 */

import { create } from 'zustand';

export type PanelId = 'browser' | 'props';
export type Side = 'left' | 'right';

export interface PanelsState {
  browserSide: Side;
  propsSide: Side;
  /** With both panels on one side: the browser's share of its height. */
  stackRatio: number;
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
const DEFAULTS: PanelsState = { browserSide: 'left', propsSide: 'right', stackRatio: 0.5, browserWidth: 250, propsWidth: 300, browserOpen: true, propsOpen: true };

function load(): PanelsState {
  try {
    const raw = localStorage.getItem(KEY);
    if (!raw) return DEFAULTS;
    const p = JSON.parse(raw) as Partial<PanelsState>;
    return {
      browserSide: p.browserSide === 'right' ? 'right' : 'left',
      propsSide: p.propsSide === 'left' ? 'left' : 'right',
      stackRatio: clampRatio(Number(p.stackRatio) || DEFAULTS.stackRatio),
      browserWidth: clamp(Number(p.browserWidth) || DEFAULTS.browserWidth),
      propsWidth: clamp(Number(p.propsWidth) || DEFAULTS.propsWidth),
      browserOpen: p.browserOpen !== false,
      propsOpen: p.propsOpen !== false,
    };
  } catch { return DEFAULTS; }
}

export const clampRatio = (r: number) => Math.max(0.15, Math.min(0.85, r));
export const clamp = (w: number) => Math.max(MIN_WIDTH, Math.min(MAX_WIDTH, Math.round(w)));

export const usePanels = create<PanelsState>(() => load());

function save(): void {
  const { browserWidth, propsWidth, browserOpen, propsOpen, browserSide, propsSide, stackRatio } = usePanels.getState();
  try { localStorage.setItem(KEY, JSON.stringify({ browserWidth, propsWidth, browserOpen, propsOpen, browserSide, propsSide, stackRatio })); } catch { /* a convenience only */ }
}

/** The panel whose width is its side's: the browser when both are open on one side (they share the width). */
export function governor(s: PanelsState, panel: PanelId): PanelId {
  return panel === 'props' && s.browserSide === s.propsSide && s.browserOpen ? 'browser' : panel;
}

export function setPanelWidth(panel: PanelId, width: number): void {
  const g = governor(usePanels.getState(), panel);
  usePanels.setState(g === 'browser' ? { browserWidth: clamp(width) } : { propsWidth: clamp(width) });
  save();
}

export function setStackRatio(r: number): void { usePanels.setState({ stackRatio: clampRatio(r) }); save(); }

/** Named arrangements, offered in View → Panels. */
export const LAYOUTS = {
  default: { browser: 'left', props: 'right' },
  swapped: { browser: 'right', props: 'left' },
  bothLeft: { browser: 'left', props: 'left' },
  bothRight: { browser: 'right', props: 'right' },
} as const satisfies Record<string, Record<PanelId, Side>>;
export type LayoutName = keyof typeof LAYOUTS;

export function setLayout(name: LayoutName): void {
  const l = LAYOUTS[name];
  usePanels.setState({ browserSide: l.browser, propsSide: l.props, overlay: null });
  save();
}

/** Which named arrangement the panels are in. */
export function layoutName(s: Pick<PanelsState, 'browserSide' | 'propsSide'>): LayoutName {
  return (Object.keys(LAYOUTS) as LayoutName[]).find((k) => LAYOUTS[k].browser === s.browserSide && LAYOUTS[k].props === s.propsSide) ?? 'default';
}

/** The panels standing on a side, top to bottom (the browser first). */
export function panelsOn(s: Pick<PanelsState, 'browserSide' | 'propsSide'>, side: Side): PanelId[] {
  return (['browser', 'props'] as const).filter((p) => (p === 'browser' ? s.browserSide : s.propsSide) === side);
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
/** A side's width and separator: its first open panel's width, else a rail for the folded ones, else nothing. */
function sideCols(s: PanelsState, side: Side): { width: string; sep: string } {
  const rail = s.railsHidden ? 0 : RAIL;
  const ps = panelsOn(s, side);
  if (!ps.length) return { width: '0px', sep: '0px' };
  const open = ps.filter((p) => (p === 'browser' ? s.browserOpen : s.propsOpen));
  if (!open.length) return { width: `${rail}px`, sep: '0px' };
  return { width: `${open[0] === 'browser' ? s.browserWidth : s.propsWidth}px`, sep: '6px' };
}

/** The grid's columns: each side's width (or its rail), a separator beside each open side, the views between. */
export function columns(s: PanelsState): string {
  const l = sideCols(s, 'left'), r = sideCols(s, 'right');
  return `${l.width} ${l.sep} minmax(0, 1fr) ${r.sep} ${r.width}`;
}
