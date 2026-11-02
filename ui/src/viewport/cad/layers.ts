/**
 * The drafting layers, asked of the engine once per change of the model for
 * every component that shows them (the command lines, a shape's properties,
 * the Layers dialog), the latest answer kept.
 */

import { useEffect } from 'react';
import { create } from 'zustand';
import { query, useSession } from '../../engine/session';

export interface DraftLayer { name: string; color: string; weight: number; lineType: string; visible: boolean; locked: boolean }

const useDraftLayers = create<{ layers: DraftLayer[] }>(() => ({ layers: [] }));

let users = 0;
let unsubscribe: (() => void) | null = null;
/** The model the last question was asked for: an answer for an older one is dropped. */
let askedFor: unknown = null;

function refresh(nodes: unknown): void {
  if (askedFor === nodes) return;
  askedFor = nodes;
  void query<DraftLayer[]>('drafting.layers').then((l) => { if (l && askedFor === nodes) useDraftLayers.setState({ layers: l }); });
}

/** The project's drafting layers, kept up to date while any component uses them. */
export function useLayers(): DraftLayer[] {
  useEffect(() => {
    if (users++ === 0) {
      refresh(useSession.getState().nodes);
      unsubscribe = useSession.subscribe((s, prev) => { if (s.nodes !== prev.nodes) refresh(s.nodes); });
    }
    return () => {
      if (--users === 0) { unsubscribe?.(); unsubscribe = null; askedFor = null; }
    };
  }, []);
  return useDraftLayers((s) => s.layers);
}
