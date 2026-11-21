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

/** A floating panel's place: window coordinates in pixels. */
export interface FloatRect { x: number; y: number; w: number; h: number }

export type PropsTab = 'properties' | 'tool' | 'assistant';

export interface PanelsState {
  browserSide: Side;
  propsSide: Side;
  /** With both panels on one side: the browser's share of its height. */
  stackRatio: number;
  /** With both panels on one side: the properties above the browser (dragging a panel's header, decision 755). */
  propsFirst?: boolean;
  /** A panel floating over the view (stage 2): its place and size; null or absent: docked on its side. */
  browserFloat?: FloatRect | null;
  propsFloat?: FloatRect | null;
  browserWidth: number;
  propsWidth: number;
  browserOpen: boolean;
  propsOpen: boolean;
  /** A folded panel shown over the view for a moment (not kept), on its tab when it has tabs. */
  overlay?: { side: 'browser' | 'props'; tab?: PanelTab } | null;
  /** Folded panels show no rail (Focus): Tab or the shortcuts bring them back. */
  railsHidden?: boolean;
  /** What the right panel shows: the properties, the active tool's properties, or the assistant (desktop only, docs/plan-assistant.md). */
  propsTab?: PropsTab;
  /** The user's last non-tool tab so ending a tool returns to it. */
  prevPropsTab?: 'properties' | 'assistant';
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
      propsFirst: p.propsFirst === true,
      browserFloat: loadRect(p.browserFloat),
      propsFloat: loadRect(p.propsFloat),
      browserWidth: clamp(Number(p.browserWidth) || DEFAULTS.browserWidth),
      propsWidth: clamp(Number(p.propsWidth) || DEFAULTS.propsWidth),
      browserOpen: p.browserOpen !== false,
      propsOpen: p.propsOpen !== false,
    };
  } catch { return DEFAULTS; }
}

function loadRect(r: unknown): FloatRect | null {
  const o = r as Partial<FloatRect> | null | undefined;
  if (!o || ![o.x, o.y, o.w, o.h].every((n) => typeof n === 'number' && Number.isFinite(n))) return null;
  return clampFloat(o as FloatRect, typeof window === 'undefined' ? { w: 1600, h: 900 } : { w: window.innerWidth, h: window.innerHeight });
}

export const clampRatio = (r: number) => Math.max(0.15, Math.min(0.85, r));
export const clamp = (w: number) => Math.max(MIN_WIDTH, Math.min(MAX_WIDTH, Math.round(w)));

export const usePanels = create<PanelsState>(() => load());

function save(): void {
  const { browserWidth, propsWidth, browserOpen, propsOpen, browserSide, propsSide, stackRatio, propsFirst, browserFloat, propsFloat } = usePanels.getState();
  try { localStorage.setItem(KEY, JSON.stringify({ browserWidth, propsWidth, browserOpen, propsOpen, browserSide, propsSide, stackRatio, propsFirst, browserFloat, propsFloat })); } catch { /* a convenience only */ }
}

/** The panel whose width is its side's: the first open one when both stand on one side (they share the width). */
export function governor(s: PanelsState, panel: PanelId): PanelId {
  const first = panelsOn(s, panel === 'browser' ? s.browserSide : s.propsSide).find((p) => (p === 'browser' ? s.browserOpen : s.propsOpen));
  return first ?? panel;
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
  usePanels.setState({ browserSide: l.browser, propsSide: l.props, propsFirst: false, browserFloat: null, propsFloat: null, overlay: null });
  save();
}

/** Which named arrangement the panels are in: 'custom' once one floats or a stacked pair is in reverse order (dragging). */
export function layoutName(s: Pick<PanelsState, 'browserSide' | 'propsSide' | 'propsFirst' | 'browserFloat' | 'propsFloat'>): LayoutName | 'custom' {
  if (s.browserFloat || s.propsFloat || (s.propsFirst && s.browserSide === s.propsSide)) return 'custom';
  return (Object.keys(LAYOUTS) as LayoutName[]).find((k) => LAYOUTS[k].browser === s.browserSide && LAYOUTS[k].props === s.propsSide) ?? 'default';
}

/** The panels docked on a side, top to bottom (the browser first unless `propsFirst`); a floating panel is on no side. */
export function panelsOn(s: Pick<PanelsState, 'browserSide' | 'propsSide' | 'propsFirst' | 'browserFloat' | 'propsFloat'>, side: Side): PanelId[] {
  const ps = (['browser', 'props'] as const).filter((p) => (p === 'browser' ? s.browserSide : s.propsSide) === side && !(p === 'browser' ? s.browserFloat : s.propsFloat));
  return s.propsFirst ? ps.reverse() : ps;
}

/** Where a dragged panel would land. `stack`: above or below a docked panel; `edge`: a side of the views; `float`: over the view. */
export type DropZone =
  | { kind: 'stack'; target: PanelId; where: 'above' | 'below' }
  | { kind: 'edge'; side: Side; where?: 'above' | 'below' }
  | { kind: 'float' };

export interface Box { left: number; top: number; right: number; bottom: number }
const inside = (b: Box, x: number, y: number) => x >= b.left && x < b.right && y >= b.top && y < b.bottom;

/**
 * The drop zone under the pointer. `panels` are the rectangles of the open docked panels (not the dragged one's own),
 * `views` the views area. Over another panel: its top half is above, its bottom half below. In the views' outer fifth
 * (at most 160 px): that side (above or below what already stands there, by the pointer's height). Elsewhere in the
 * views: float. Anywhere else (the ribbon, the dragged panel's own place): none.
 */
export function dropZone(x: number, y: number, views: Box, panels: Partial<Record<PanelId, Box>>, dragged: PanelId): DropZone | null {
  for (const id of ['browser', 'props'] as const) {
    const b = panels[id];
    if (id !== dragged && b && inside(b, x, y)) return { kind: 'stack', target: id, where: y < (b.top + b.bottom) / 2 ? 'above' : 'below' };
  }
  if (!inside(views, x, y)) return null;
  const edge = Math.min(160, (views.right - views.left) / 5);
  const where = y < (views.top + views.bottom) / 2 ? 'above' : 'below';
  if (x < views.left + edge) return { kind: 'edge', side: 'left', where };
  if (x >= views.right - edge) return { kind: 'edge', side: 'right', where };
  return { kind: 'float' };
}

/** The state change of dropping `panel` in `zone`: docked on a side (above or below the other panel there), or floating at `at`. */
export function dropChange(s: PanelsState, panel: PanelId, zone: DropZone, at: FloatRect): Partial<PanelsState> {
  const other: PanelId = panel === 'browser' ? 'props' : 'browser';
  const mine = (v: unknown) => (panel === 'browser' ? { browserFloat: v as FloatRect | null } : { propsFloat: v as FloatRect | null });
  const open = panel === 'browser' ? { browserOpen: true } : { propsOpen: true };
  if (zone.kind === 'float') return { ...mine(at), ...open, overlay: null };
  const side = zone.kind === 'stack' ? (zone.target === 'browser' ? s.browserSide : s.propsSide) : zone.side;
  const dockedOther = panelsOn(s, side).includes(other);
  const where = zone.where ?? 'below';
  // Above the other: the browser is first when it is the dragged one; below, when it is the other.
  const propsFirst = dockedOther ? (panel === 'props') === (where === 'above') : (s.propsFirst ?? false);
  return { ...(panel === 'browser' ? { browserSide: side } : { propsSide: side }), propsFirst, ...mine(null), ...open, overlay: null };
}

/** Keep a floating panel inside the window: at least its header (and 60 px of width) can always be reached. */
export function clampFloat(r: FloatRect, win: { w: number; h: number }): FloatRect {
  const w = Math.max(220, Math.min(r.w, win.w));
  const h = Math.max(160, Math.min(r.h, win.h - 40));
  return { w: Math.round(w), h: Math.round(h), x: Math.round(Math.max(0, Math.min(r.x, win.w - w))), y: Math.round(Math.max(0, Math.min(r.y, win.h - h))) };
}

/** A panel being dragged by its header (not kept): the pointer, and the zone under it. */
export interface DragState { panel: PanelId; x: number; y: number; zone: DropZone | null }
export const useDrag = create<{ drag: DragState | null }>(() => ({ drag: null }));

export function dockPanel(panel: PanelId, zone: DropZone, at: FloatRect): void {
  usePanels.setState(dropChange(usePanels.getState(), panel, zone, at));
  save();
}
export function setFloatRect(panel: PanelId, r: FloatRect, win: { w: number; h: number }): void {
  const c = clampFloat(r, win);
  usePanels.setState(panel === 'browser' ? { browserFloat: c } : { propsFloat: c });
  save();
}
/** Float a docked panel (at a default place), or dock a floating one back on its side. */
export function toggleFloat(panel: PanelId, win: { w: number; h: number }): void {
  const s = usePanels.getState();
  const cur = panel === 'browser' ? s.browserFloat : s.propsFloat;
  const w = panel === 'browser' ? s.browserWidth : s.propsWidth;
  const r = cur ? null : clampFloat({ x: win.w / 2 - w / 2, y: 120, w, h: Math.round(win.h * 0.6) }, win);
  usePanels.setState({ ...(panel === 'browser' ? { browserFloat: r } : { propsFloat: r }), ...(panel === 'browser' ? { browserOpen: true } : { propsOpen: true }), overlay: null });
  save();
}

export function togglePanel(side: 'browser' | 'props'): void {
  const s = usePanels.getState();
  // A floating panel is never folded (it would be lost, with no rail): it stays until docked back.
  if (side === 'browser' ? s.browserFloat : s.propsFloat) return;
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

/** The right panel on the assistant, tool properties, or back on properties. */
export function showPropsTab(tab: PropsTab): void {
  usePanels.setState({ propsTab: tab, propsOpen: true, overlay: null });
  save();
}

/** Pure transition for the Tool Properties tab when a tool starts or finishes (forum #122). */
export function nextPropsTabForTool(
  curTab: PropsTab,
  prevTab: 'properties' | 'assistant' | undefined,
  hasTool: boolean,
): { propsTab: PropsTab; prevPropsTab?: 'properties' | 'assistant' } {
  if (hasTool) {
    if (curTab === 'tool') return { propsTab: 'tool', prevPropsTab: prevTab ?? 'properties' };
    return { propsTab: 'tool', prevPropsTab: curTab === 'assistant' ? 'assistant' : 'properties' };
  }
  if (curTab === 'tool') return { propsTab: prevTab ?? 'properties', prevPropsTab: undefined };
  return { propsTab: curTab, prevPropsTab: undefined };
}

/** Switch to Tool Properties when a tool with settings starts, and return to the previous tab when it ends. */
export function syncToolPropsTab(hasTool: boolean): void {
  const s = usePanels.getState();
  const next = nextPropsTabForTool(s.propsTab ?? 'properties', s.prevPropsTab, hasTool);
  if (next.propsTab !== (s.propsTab ?? 'properties') || next.prevPropsTab !== s.prevPropsTab) {
    usePanels.setState(next);
  }
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
