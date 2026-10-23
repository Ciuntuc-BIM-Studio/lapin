/** The interface's own state: which screen, tab, tool, view and selection, the theme. (The model lives in the engine.) */

import { create } from 'zustand';

export type Theme = 'paper' | 'graphite';

export interface UiState {
  screen: 'start' | 'workspace';
  tab: string | null;
  tool: string | null;
  view: string;
  /** The selected element's kind (mock selection for now). */
  selectedKind: string | null;
  theme: Theme;
}

export const useUi = create<UiState>(() => ({
  screen: 'start', tab: null, tool: null, view: '3d', selectedKind: 'wall', theme: 'paper',
}));

export const setTool = (tool: string | null) => useUi.setState({ tool });
export const toggleTool = (tool: string) => useUi.setState((s) => ({ tool: s.tool === tool ? null : tool }));
