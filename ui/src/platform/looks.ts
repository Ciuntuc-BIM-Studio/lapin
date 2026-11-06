/**
 * How modules ask the views to show an element: hidden, or tinted (a phase's
 * override). Keyed by entity id; absent: as it is. Each source (phases, the
 * 3D view's visibility) sets its own; hidden wins over a tint.
 */

import { create } from 'zustand';

export interface Look {
  hidden?: boolean; color?: string; opacity?: number;
  /** Looks of an element's parts by their keys (layer:0…): a layer hidden or tinted apart (the 4D simulation); the rest take the element's. */
  parts?: Record<string, Look>;
}

interface LooksState { bySource: Record<string, Record<string, Look>>; looks: Record<string, Look> }

export const useLooks = create<LooksState>(() => ({ bySource: {}, looks: {} }));

/** One source's looks, merged with the others'. */
export function setLooks(source: string, looks: Record<string, Look>): void {
  const bySource = { ...useLooks.getState().bySource, [source]: looks };
  const merged: Record<string, Look> = {};
  for (const map of Object.values(bySource))
    // One source's look kept as it is (the same object while it does not change, so the views restyle only what changed).
    for (const [id, l] of Object.entries(map)) merged[id] = !merged[id] ? l : merged[id].hidden ? merged[id] : { ...merged[id], ...l };
  useLooks.setState({ bySource, looks: merged });
}
