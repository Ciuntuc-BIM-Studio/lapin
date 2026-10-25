/**
 * The interface's own state: which screen, tab, tool, view, storey and
 * selection, the theme. (The model lives in the engine.)
 *
 * The workspace shows one or two panes, each with its own view (a plan, the
 * 3D, a section, a sheet, a schedule). The active pane's view is mirrored in
 * `view`, `drawingViewId`, `sheetId`, `scheduleId` and `planViewId`, so the
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
}

export const PANE_KEYS = ['view', 'drawingViewId', 'sheetId', 'scheduleId', 'planViewId'] as const;
export const emptyPane = (view: ViewKind = 'plan'): Pane => ({ view, drawingViewId: null, sheetId: null, scheduleId: null, planViewId: null });

export interface UiState extends Pane {
  screen: 'start' | 'workspace';
  tab: string | null;
  /** The active tool's id ("architecture.wall"); null: select. */
  tool: string | null;
  /** The panes of the workspace (one or two), and which is active (its view is mirrored above). */
  panes: Pane[];
  pane: number;
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
  /** The style a new roof takes (the catalogue's first when null). */
  roofStyle: string | null;
  dialog: Dialog;
  /** A message for the user (saved, recovered…), cleared by the next one. */
  notice: string | null;
  theme: Theme;
  /** How the 3D draws: shaded, shaded with edges, or wireframe. */
  visualStyle: 'shaded' | 'edges' | 'wireframe';
  /** The key plan over the views (the camera's place in 3D, the frame in 2D). */
  keyPlan: boolean;
}

const first = emptyPane('plan');
export const useUi = create<UiState>(() => ({
  screen: 'start', tab: null, tool: null, ...first, panes: [first], pane: 0, split: 'row', windowRole: 'main', planDrawing: true, storeyId: null, selection: null, selected: [], wallTypeId: null, doorTypeId: null, windowTypeId: null, stairTypeId: null, slabTypeId: null, roofStyle: null, dialog: null, notice: null, theme: 'paper', visualStyle: 'edges', keyPlan: true,
}));

// Setting the view in front sets the active pane too.
const rawSet = useUi.setState;
useUi.setState = ((partial: Partial<UiState> | ((s: UiState) => Partial<UiState>), replace?: boolean) => {
  const p = typeof partial === 'function' ? partial(useUi.getState()) : partial;
  if (!p) return;
  if (replace || !PANE_KEYS.some((k) => k in p)) { rawSet(p as UiState, replace as false | undefined); return; }
  const s = useUi.getState();
  const panes = s.panes.slice();
  const cur = { ...panes[s.pane] };
  for (const k of PANE_KEYS) if (k in p) (cur as Record<string, unknown>)[k] = p[k];
  panes[s.pane] = cur;
  rawSet({ ...p, panes } as UiState);
}) as typeof useUi.setState;

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
