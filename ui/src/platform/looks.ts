/**
 * How modules ask the views to show an element: hidden, or tinted (a phase's
 * override). Keyed by entity id; absent: as it is. Each source (phases, the
 * 3D view's visibility) sets its own; hidden wins over a tint.
 */

import { create } from 'zustand';

export interface Look { hidden?: boolean; color?: string; opacity?: number }

interface LooksState { bySource: Record<string, Record<string, Look>>; looks: Record<string, Look> }

export const useLooks = create<LooksState>(() => ({ bySource: {}, looks: {} }));

/** One source's looks, merged with the others'. */
export function setLooks(source: string, looks: Record<string, Look>): void {
  const bySource = { ...useLooks.getState().bySource, [source]: looks };
  const merged: Record<string, Look> = {};
  for (const map of Object.values(bySource))
    for (const [id, l] of Object.entries(map)) merged[id] = merged[id]?.hidden ? merged[id] : { ...merged[id], ...l };
  useLooks.setState({ bySource, looks: merged });
}
