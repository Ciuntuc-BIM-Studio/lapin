/** The interface's own state: which screen, tab, tool, view, storey and selection, the theme. (The model lives in the engine.) */

import { create } from 'zustand';

export type Theme = 'paper' | 'graphite';
export type ViewKind = '3d' | 'plan';
export type Dialog = 'wallTypes' | null;

export interface UiState {
  screen: 'start' | 'workspace';
  tab: string | null;
  /** The active tool's id ("architecture.wall"); null: select. */
  tool: string | null;
  view: ViewKind;
  /** The storey the plan shows and new elements go on; null: the lowest. */
  storeyId: string | null;
  /** The selected entity (the one the properties panel shows). */
  selection: string | null;
  /** Everything selected, the primary selection among it (Shift adds to it). */
  selected: string[];
  /** The wall type the wall tool draws with; null: the engine's default. */
  wallTypeId: string | null;
  dialog: Dialog;
  /** A message for the user (saved, recovered…), cleared by the next one. */
  notice: string | null;
  theme: Theme;
}

export const useUi = create<UiState>(() => ({
  screen: 'start', tab: null, tool: null, view: 'plan', storeyId: null, selection: null, selected: [], wallTypeId: null, dialog: null, notice: null, theme: 'paper',
}));

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
