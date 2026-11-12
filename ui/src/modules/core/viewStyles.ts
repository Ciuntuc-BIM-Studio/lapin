/**
 * The project's view styles (docs/view-styles.md), as the engine lists them:
 * one shared copy, read again once per change of the model however many
 * editors show it (the properties panel, View settings, View styles).
 */

import { useEffect } from 'react';
import { create } from 'zustand';
import { query, useSession } from '../../engine/session';

/** The partial structure display (decision 500, as ArchiCAD's): the entire model, without the finishes, the cores only, the load-bearing elements' cores only. */
export type LayerMode = 'all' | 'noFinishes' | 'core' | 'coreLoadBearing';

/** The partial structure display's presets, as the view settings, the view styles and the view's corner offer them. */
export const STRUCTURE_MODES: [LayerMode, string, string][] = [
  ['all', 'Entire model', 'Every layer of walls, floors and roofs'],
  ['noFinishes', 'Without finishes', "The finish layers (plasters, renders, floor finishes: category Finish) left out, with the rooms' finishes and the skirtings; the insulation, membranes and structure drawn"],
  ['core', 'Core only', 'The cores alone, between their boundaries; the finishes, insulation and membranes left out'],
  ['coreLoadBearing', 'Core of load-bearing elements only', 'The cores of the load-bearing elements alone (LoadBearing in their properties, as Pset_WallCommon; a wall that says nothing bears load); partitions with their doors and windows, coverings and room finishes not drawn'],
];
export const structureLabel = (m: LayerMode | null | undefined) => STRUCTURE_MODES.find(([k]) => k === m)?.[1] ?? STRUCTURE_MODES[0][1];

/** What a style says for a view of a kind: its plans' or its sections' and elevations' own, else its layers (ViewStyles.Style.LayersFor). */
export function styleStructure(style: Pick<ViewStyle, 'layers' | 'layersPlan' | 'layersSection'>, kind: string | null | undefined): LayerMode {
  if (kind === 'plan' || kind == null) return style.layersPlan ?? style.layers;
  if (kind === 'section' || kind === 'elevation') return style.layersSection ?? style.layers;
  return style.layers;
}
/**
 * ifc2d: plans draw the 2D an IFC file gave its elements (footprints, symbols) instead of their cut.
 * voids: the openings' voids drawn, crossed and marked (decision 290); voidSizes: with their sizes and bottom levels.
 * finishHatch: plans hatch the rooms' floor finishes (decision 299; a reflected ceiling plan their ceilings).
 * surfaceHatch: elevations and sections hatch the faces they see with their materials' elevation hatch (decision 325).
 * entourage: how people, cars and plants draw in 2D (decision 341): outlines (the default), abstract, detailed or none;
 * entourageFill: the colour their silhouettes are filled with in elevations and sections, null for none.
 */
export type EntourageMode = 'outline' | 'abstract' | 'detailed' | 'none';
export interface ViewStyle { id: string; name: string; discipline: string | null; layers: LayerMode; joins: boolean; overhead: boolean; ifc2d: boolean; voids?: boolean; voidSizes?: boolean; finishHatch?: boolean; surfaceHatch?: boolean; entourage?: EntourageMode; entourageFill?: string | null; /** The graphics set its views take when they have none of their own (decision 391). */ graphicsSet?: string | null; /** Its views' level of development (decision 404). */ detail?: number;
  /** The finish layers by the kind of view (the forum's topic #30): plans, and sections with elevations, drawing the walls' and the floors' finishes; on unless false. */
  wallFinishesPlan?: boolean; wallFinishesSection?: boolean; floorFinishesPlan?: boolean; floorFinishesSection?: boolean;
  /** Its plans' and its sections' and elevations' partial structure display (decision 500); unset, its layers. */
  layersPlan?: LayerMode | null; layersSection?: LayerMode | null;
  /** Whether the partial structure display leaves out the separate finish elements (rooms' finishes, skirtings, finish coverings) with the finish layers (decision 502); on unless false (a project made before). */
  finishElements?: boolean }

/** The four finish choices, by kind of view and category, with their labels and what they do, as View settings and View styles offer them. */
export const FINISH_FIELDS: { key: 'wallFinishesPlan' | 'wallFinishesSection' | 'floorFinishesPlan' | 'floorFinishesSection'; plan: boolean; label: string; column: string; title: string }[] = [
  { key: 'wallFinishesPlan', plan: true, label: 'Wall finishes', column: 'Wall finishes in plans', title: 'Plans draw the finish layers of walls (a layer\'s own category in its type, else its material\'s)' },
  { key: 'floorFinishesPlan', plan: true, label: 'Floor finishes', column: 'Floor finishes in plans', title: 'Plans draw the finish layers of floors (a layer\'s own category in its type, else its material\'s)' },
  { key: 'wallFinishesSection', plan: false, label: 'Wall finishes', column: 'Wall finishes in sections', title: 'Sections and elevations draw the finish layers of walls (a layer\'s own category in its type, else its material\'s)' },
  { key: 'floorFinishesSection', plan: false, label: 'Floor finishes', column: 'Floor finishes in sections', title: 'Sections and elevations draw the finish layers of floors (a layer\'s own category in its type, else its material\'s)' },
];

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
