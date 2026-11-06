/**
 * How modules ask the views to show an element: hidden, or tinted (a phase's
 * override). Keyed by entity id; absent: as it is. Each source (phases, the
 * 3D view's visibility) sets its own; hidden wins over a tint.
 */

import { create } from 'zustand';

export interface Look {
  hidden?: boolean; color?: string; opacity?: number;
  /** Looks of an element's parts by their keys (layer:0…, a roof's deck; one ending in ":" every part whose key starts so, timber:): a layer hidden or tinted apart (the 4D simulation); the rest take the element's. */
  parts?: Record<string, Look>;
}

/** A part's own look among an element's part looks: by its key, else by the longest prefix (a key ending in ":") it starts with. */
export function partLook(parts: Record<string, Look> | undefined, key: string): Look | undefined {
  if (!parts) return undefined;
  if (parts[key]) return parts[key];
  let best: string | undefined;
  for (const k of Object.keys(parts)) if (k.endsWith(':') && key.startsWith(k) && (!best || k.length > best.length)) best = k;
  return best ? parts[best] : undefined;
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
