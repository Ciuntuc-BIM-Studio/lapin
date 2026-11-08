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
 * finishHatch: plans hatch the rooms' floor finishes (decision 299; a reflected ceiling plan their ceilings).
 * surfaceHatch: elevations and sections hatch the faces they see with their materials' elevation hatch (decision 325).
 * entourage: how people, cars and plants draw in 2D (decision 341): outlines (the default), abstract, detailed or none;
 * entourageFill: the colour their silhouettes are filled with in elevations and sections, null for none.
 */
export type EntourageMode = 'outline' | 'abstract' | 'detailed' | 'none';
export interface ViewStyle { id: string; name: string; discipline: string | null; layers: LayerMode; joins: boolean; overhead: boolean; ifc2d: boolean; voids?: boolean; voidSizes?: boolean; finishHatch?: boolean; surfaceHatch?: boolean; entourage?: EntourageMode; entourageFill?: string | null; /** The graphics set its views take when they have none of their own (decision 391). */ graphicsSet?: string | null; /** Its views' level of development (decision 404). */ detail?: number }

/** The entourage modes as the editors offer them. */
export const ENTOURAGE_MODES: [EntourageMode, string][] = [['outline', 'outlines'], ['abstract', 'abstract'], ['detailed', 'detailed'], ['none', 'not drawn']];
/** The silhouettes' fills offered: none, or a pale colour that prints light. */
export const ENTOURAGE_FILLS: [string, string][] = [['', 'no fill'], ['#e8e8e8', 'light grey'], ['#e2ecd8', 'pale green']];

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

/**
 * The levels of development a view draws at (decision 404), after the LOD Specification: never taken from the scale. Each
 * with what it adds over the one before, as the views' settings and Visibility / Graphics name them.
 */
export const LODS: [number, string, string][] = [
  [100, 'LOD 100 · massing', 'Walls, floors and roofs one outline hatched as their core; steel members as their axes; doors and windows as their opening and swing'],
  [200, 'LOD 200 · layers', 'The layers of walls, floors and roofs; doors\' and windows\' frames and sashes, simplified'],
  [300, 'LOD 300 · details', 'The details of doors and windows (reveals, sills, surrounds, thresholds, boards, hardware, shutters) and the openings\' linings'],
  [350, 'LOD 350 · connections', 'Steel connections: plates, bolts, anchors'],
  [400, 'LOD 400 · fabrication', 'A linked structural model\'s reinforcing bars'],
];
export const lodLabel = (n: number | undefined) => LODS.find(([k]) => k === n)?.[1] ?? (n ? `LOD ${n}` : '');
/** What a level shows, each level's addition in turn: the title of its choice. */
export const LOD_HELP = 'How much of the model the view draws, never by its scale: ' + LODS.map(([, l, d]) => `${l}: ${d}`).join('. ') + '.';
