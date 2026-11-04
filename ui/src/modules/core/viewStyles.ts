/**
 * The project's view styles (docs/view-styles.md), as the engine lists them:
 * one shared copy, read again once per change of the model however many
 * editors show it (the properties panel, View settings, View styles).
 */

import { useEffect } from 'react';
import { create } from 'zustand';
import { query, useSession } from '../../engine/session';

export type LayerMode = 'all' | 'noFinishes' | 'core';
/**
 * ifc2d: plans draw the 2D an IFC file gave its elements (footprints, symbols) instead of their cut.
 * voids: the openings' voids drawn, crossed and marked (decision 290); voidSizes: with their sizes and bottom levels.
 */
export interface ViewStyle { id: string; name: string; discipline: string | null; layers: LayerMode; joins: boolean; overhead: boolean; ifc2d: boolean; voids?: boolean; voidSizes?: boolean }

/** The styles, and the model they were asked for (the session's nodes map, new with every diff). */
const useStyles = create<{ styles: ViewStyle[]; asked: unknown }>(() => ({ styles: [], asked: null }));

export function useViewStyles(): ViewStyle[] {
  const nodes = useSession((s) => s.nodes);
  useEffect(() => {
    // The first editor to see this model asks; the others share the answer. A late answer for an older model is dropped.
    if (useStyles.getState().asked === nodes) return;
    useStyles.setState({ asked: nodes });
    void query<ViewStyle[]>('viewStyle.list').then((r) => { if (useStyles.getState().asked === nodes) useStyles.setState({ styles: r ?? [] }); });
  }, [nodes]);
  return useStyles((s) => s.styles);
}
