/**
 * The interface's own state: which screen, tab, tool, view, storey and
 * selection, the theme. (The model lives in the engine.)
 *
 * The workspace shows one or two panes, each with its own view (a plan, the
 * 3D, a section, a sheet, a schedule). The active pane's view is mirrored in
 * `view`, `drawingViewId`, `sheetId`, `scheduleId`, `planViewId` and `view3dId`, so the
 * code that asks "what is in front" keeps asking the store; setting any of
 * those sets the active pane. A detached window (desktop) is a workspace with
 * one pane and no ribbon, sharing the storey, selection and tool with the
 * main window.
 */

import { create } from 'zustand';

export type Theme = 'paper' | 'graphite';
export type ViewKind = '3d' | 'plan' | 'drawing' | 'sheet' | 'schedule';
export type Dialog = string | null;

/** What one pane shows. */
export interface Pane {
  view: ViewKind;
  /** The section or elevation the drawing view shows (a LapinView id). */
  drawingViewId: string | null;
  /** The sheet the sheet view shows (a LapinSheet id). */
  sheetId: string | null;
  /** The schedule the schedule view shows (a LapinSchedule id). */
  scheduleId: string | null;
  /** The plan view (a LapinView id) the plan shows; null: the storey's own plan. */
  planViewId: string | null;
  /** The saved 3D view (a LapinView of kind 3d) the 3D shows; null: {3D}. */
  view3dId: string | null;
}

export const PANE_KEYS = ['view', 'drawingViewId', 'sheetId', 'scheduleId', 'planViewId', 'view3dId'] as const;
export const emptyPane = (view: ViewKind = 'plan'): Pane => ({ view, drawingViewId: null, sheetId: null, scheduleId: null, planViewId: null, view3dId: null });

/**
 * An open view, as a tab of the workspace: what a pane shows, plus the storey
 * a plan tab belongs to (plans are per storey; the other kinds carry their
 * own id). Every view brought in front gets a tab once; a tab is closed by
 * hand. The tabs are one list for the workspace; each pane marks its own.
 */
export interface ViewTab extends Pane { storeyId: string | null }

/** A view's identity: the key the views keep their zoom under (zoom.ts), so a tab and its zoom memory are one. */
export function tabKey(t: Pane & { storeyId?: string | null }): string {
  switch (t.view) {
    case 'plan': return t.planViewId ?? `plan:${t.storeyId ?? ''}`;
    case 'drawing': return t.drawingViewId ?? 'drawing:none';
    case 'sheet': return t.sheetId ?? 'sheet:none';
    case 'schedule': return t.scheduleId ?? 'schedule:none';
    default: return t.view3dId ?? '3d';
  }
}
const tabOf = (pane: Pane, storeyId: string | null): ViewTab => ({ ...pane, storeyId: pane.view === 'plan' ? storeyId : null });

export interface UiState extends Pane {
  screen: 'start' | 'workspace' | 'join';
  tab: string | null;
  /** The active tool's id ("architecture.wall"); null: select. */
  tool: string | null;
  /** The panes of the workspace (one or two), and which is active (its view is mirrored above). */
  panes: Pane[];
  pane: number;
  /** The views opened so far, in order, each a tab (see ViewTab). */
  viewTabs: ViewTab[];
  /** Two panes side by side, or one above the other. */
  split: 'row' | 'column';
  /** The main window, or a detached view window (desktop). */
  windowRole: 'main' | 'view';
  /** Whether plans show the generated drawing (hatches, pens) or the plain modelling lines. */
  planDrawing: boolean;
  /** The storey the plan shows and new elements go on; null: the lowest. */
  storeyId: string | null;
  /** The selected entity (the one the properties panel shows). */
  selection: string | null;
  /** Everything selected, the primary selection among it (Shift adds to it). */
  selected: string[];
  /** The wall type the wall tool draws with; null: the engine's default. */
  wallTypeId: string | null;
  /** The door and window types the door and window tools place; null: the project's first. */
  doorTypeId: string | null;
  windowTypeId: string | null;
  slabTypeId: string | null;
  /** The stair type the stair tool places; null: the project's first. */
  stairTypeId: string | null;
  /** The curtain wall type the curtain wall tool draws with; null: the project's first. */
  curtainTypeId: string | null;
  /** The style a new roof takes (the catalogue's first when null). */
  roofStyle: string | null;
  dialog: Dialog;
  /** A message for the user (saved, recovered…), cleared by the next one. */
  notice: string | null;
  theme: Theme;
  /** How the 3D draws: shaded, shaded with edges, or wireframe. */
  visualStyle: 'shaded' | 'edges' | 'wireframe';
  /** The 3D view fades with distance. */
  fog: boolean;
  /** The key plan over the views (the camera's place in 3D, the frame in 2D). */
  keyPlan: boolean;
}

const first = emptyPane('plan');
export const useUi = create<UiState>(() => ({
  screen: 'start', tab: null, tool: null, ...first, panes: [first], pane: 0, viewTabs: [], split: 'row', windowRole: 'main', planDrawing: true, storeyId: null, selection: null, selected: [], wallTypeId: null, doorTypeId: null, windowTypeId: null, stairTypeId: null, curtainTypeId: null, slabTypeId: null, roofStyle: null, dialog: null, notice: null, theme: 'paper', visualStyle: 'edges', fog: false, keyPlan: true,
}));

/**
 * The tabs after the active pane came to show `pane` on `storeyId`: its tab
 * added at the end when new. The plan tab of "the lowest storey" (storeyId
 * null, the state before a storey was chosen) becomes the tab of the storey
 * chosen first, instead of lingering beside it.
 */
function tabsWith(tabs: ViewTab[], pane: Pane, storeyId: string | null): ViewTab[] {
  const tab = tabOf(pane, storeyId);
  const key = tabKey(tab);
  if (tabs.some((t) => tabKey(t) === key)) return tabs;
  if (tab.view === 'plan' && tab.storeyId && !tab.planViewId) {
    const k = tabs.findIndex((t) => t.view === 'plan' && !t.planViewId && t.storeyId == null);
    if (k >= 0) { const next = tabs.slice(); next[k] = tab; return next; }
  }
  return [...tabs, tab];
}

// Setting the view in front sets the active pane too, and opens its tab when it has none yet.
const rawSet = useUi.setState;
useUi.setState = ((partial: Partial<UiState> | ((s: UiState) => Partial<UiState>), replace?: boolean) => {
  const p = typeof partial === 'function' ? partial(useUi.getState()) : partial;
  if (!p) return;
  const s = useUi.getState();
  const paneChange = PANE_KEYS.some((k) => k in p);
  const entering = 'screen' in p && p.screen === 'workspace';
  if (replace || (!paneChange && !('storeyId' in p) && !entering)) { rawSet(p as UiState, replace as false | undefined); return; }
  const panes = s.panes.slice();
  const cur = { ...panes[s.pane] };
  if (paneChange) { for (const k of PANE_KEYS) if (k in p) (cur as Record<string, unknown>)[k] = p[k]; panes[s.pane] = cur; }
  const storeyId = 'storeyId' in p ? (p.storeyId ?? null) : s.storeyId;
  const viewTabs = tabsWith(entering ? [] : s.viewTabs, cur, storeyId);
  rawSet({ ...p, ...(paneChange ? { panes } : {}), viewTabs } as UiState);
}) as typeof useUi.setState;

/** Show a tab in a pane: the pane takes the tab's view (and the storey, for a plan) and becomes active. */
export function openTab(i: number, tab: ViewTab): void {
  const s = useUi.getState();
  if (!s.panes[i]) return;
  const { storeyId, ...pane } = tab;
  const panes = s.panes.slice();
  panes[i] = { ...pane };
  rawSet({ panes, pane: i, ...pane, ...(tab.view === 'plan' && storeyId ? { storeyId } : {}) });
}

/** Close a tab: a pane that showed it shows the tab before it (else the one after, else a plan). */
export function closeTab(key: string): void {
  const s = useUi.getState();
  const k = s.viewTabs.findIndex((t) => tabKey(t) === key);
  if (k < 0) return;
  const rest = s.viewTabs.filter((_, j) => j !== k);
  const fallback: ViewTab = rest[k - 1] ?? rest[k] ?? { ...emptyPane('plan'), storeyId: s.storeyId };
  const { storeyId: fbStorey, ...fbPane } = fallback;
  const showed = (pane: Pane) => tabKey(tabOf(pane, s.storeyId)) === key;
  const panes = s.panes.map((pane) => (showed(pane) ? { ...fbPane } : pane));
  const activeShowed = showed(s.panes[s.pane]);
  rawSet({ viewTabs: rest.length ? rest : [fallback], panes, ...panes[s.pane], ...(activeShowed && fallback.view === 'plan' && fbStorey ? { storeyId: fbStorey } : {}) });
}

/** The pane at an index, as it is now. */
export const paneAt = (s: UiState, i: number): Pane => s.panes[i] ?? s.panes[s.pane];

/** Make a pane the active one: its view comes in front. */
export function focusPane(i: number): void {
  const s = useUi.getState();
  if (i === s.pane || !s.panes[i]) return;
  rawSet({ pane: i, ...s.panes[i] });
}

/** Set what a pane shows (the active one's view comes in front as well). */
export function setPane(i: number, patch: Partial<Pane>): void {
  const s = useUi.getState();
  if (!s.panes[i]) return;
  const panes = s.panes.slice();
  panes[i] = { ...panes[i], ...patch };
  rawSet(i === s.pane ? { panes, ...panes[i] } : { panes });
}

/** A second pane next to (or under) the first, showing another view of the same project; the new pane is active. */
export function splitPane(split: 'row' | 'column' = 'row'): void {
  const s = useUi.getState();
  if (s.panes.length >= 2) { rawSet({ split }); return; }
  const other: Pane = { ...s.panes[0], view: s.panes[0].view === '3d' ? 'plan' : '3d' };
  rawSet({ panes: [s.panes[0], other], split, pane: 1, ...other });
}

/** Close a pane: the other stays, active. */
export function closePane(i: number): void {
  const s = useUi.getState();
  if (s.panes.length < 2) return;
  const panes = s.panes.filter((_, k) => k !== i);
  rawSet({ panes, pane: 0, ...panes[0] });
}

export const setTool = (tool: string | null) => useUi.setState({ tool });
export const toggleTool = (tool: string) => useUi.setState((s) => ({ tool: s.tool === tool ? null : tool }));
/** Select one entity (none: clear), or with `add` toggle it in the selection. */
export function select(id: string | null, add = false): void {
  if (!add || !id) { useUi.setState({ selection: id, selected: id ? [id] : [] }); return; }
  const { selected } = useUi.getState();
  const next = selected.includes(id) ? selected.filter((x) => x !== id) : [...selected, id];
  useUi.setState({ selected: next, selection: next[next.length - 1] ?? null });
}
export const notify = (notice: string | null) => useUi.setState({ notice });
