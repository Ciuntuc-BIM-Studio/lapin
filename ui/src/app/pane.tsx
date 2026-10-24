/** Which pane a view sits in: the views read their ids from it, so two panes can show two sections. */

import { createContext, useContext } from 'react';
import { useUi, type Pane } from './store';

export const PaneContext = createContext(0);

/** The index of the pane this component is in (0 outside the workspace's panes). */
export const usePaneIndex = () => useContext(PaneContext);

/** One of the pane's fields, live. */
export function usePaneField<K extends keyof Pane>(key: K): Pane[K] {
  const i = usePaneIndex();
  return useUi((s) => (s.panes[i] ?? s.panes[s.pane])[key]);
}

/** Whether this pane is the active one (its view is in front). */
export function usePaneActive(): boolean {
  const i = usePaneIndex();
  return useUi((s) => s.pane === i);
}
