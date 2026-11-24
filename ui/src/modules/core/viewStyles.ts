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
 * floorHatch: plans hatch the top faces of the floors they see below their cut with their top layer's surface pattern (forum topic 259);
 * on in a new project's Architectural style, off in the others and in a project saved before.
 * surfaceHatch: elevations and sections hatch the faces they see with their materials' elevation hatch (decision 325).
 * entourage: how people, cars and plants draw in 2D (decision 341): outlines (the default), abstract, detailed or none;
 * entourageFill: the colour their silhouettes are filled with in elevations and sections, null for none.
 * vegetation: how plants' bodies look (decision 818): coloured (each species in its colour, the default) or maquette
 * (neutral faceted bodies, an architectural model's), in the 3D, its exports and the drawings alike.
 */
export type EntourageMode = 'outline' | 'abstract' | 'detailed' | 'none';
export type VegetationMode = 'coloured' | 'maquette';
export interface ViewStyle { id: string; name: string; discipline: string | null; layers: LayerMode; joins: boolean; overhead: boolean; ifc2d: boolean; voids?: boolean; voidSizes?: boolean; finishHatch?: boolean; floorHatch?: boolean; surfaceHatch?: boolean; entourage?: EntourageMode; entourageFill?: string | null; /** How its plants' bodies look (decision 818). */ vegetation?: VegetationMode; /** The graphics set its views take when they have none of their own (decision 391). */ graphicsSet?: string | null; /** Its views' level of development (decision 404). */ detail?: number;
  /** The finish layers by the kind of view (the forum's topic #30): plans, and sections with elevations, drawing the walls' and the floors' finishes; on unless false. */
  wallFinishesPlan?: boolean; wallFinishesSection?: boolean; floorFinishesPlan?: boolean; floorFinishesSection?: boolean;
  /** Its plans' and its sections' and elevations' partial structure display (decision 500); unset, its layers. */
  layersPlan?: LayerMode | null; layersSection?: LayerMode | null;
  /** Whether its views draw what is not of their discipline halftone (an MEP plan's architecture): its own choice (null: the discipline's, on for MEP), and as it resolves. */
  halftoneOthers?: boolean | null; halftonesOthers?: boolean;
  /** Whether the partial structure display leaves out the separate finish elements (rooms' finishes, skirtings, finish coverings) with the finish layers (decision 502); on unless false (a project made before). */
  finishElements?: boolean;
  /** Hatch lines closer than this on paper (mm, default 0.7) are not drawn; 0 draws all (docs/plan-material-graphics.md, stage 1). */
  minHatchSpacing?: number;
  /** What cut elements draw at small scales (docs/plan-material-graphics.md, stage 2); unset: as their materials say. */
  smallScaleCut?: SmallScaleCut | null;
  /** The material graphics set its views draw by (docs/plan-material-graphics.md, stage 4); unset: the materials' own graphics. */
  materialGraphicsSet?: string | null;
  /** What sections and elevations draw beyond their cut (issue #93, decision 721); unset: full. */
  beyond?: BeyondMode;
  /** Where `near` puts the rest in the background, metres from the cut (default 1). */
  nearDepth?: number }

/** What a section or an elevation draws beyond its cut (decision 721): everything, the near part with the rest in the background, or only the cut. */
export type BeyondMode = 'full' | 'near' | 'cut';
/** The engine's default near depth, metres. */
export const DEFAULT_NEAR_DEPTH = 1;
/** The presets as the editors offer them, with what each does. */
export const BEYOND_MODES: [BeyondMode, string, string][] = [
  ['full', 'Full depth', 'Everything the view sees up to its depth, in the seen pen'],
  ['near', 'Cut + near', 'What lies within the near depth of the cut in the seen pen; what lies wholly farther, up to the depth, in a thin grey background pen'],
  ['cut', 'Cut only', 'Only what the section plane cuts: nothing seen beyond it (for details)'],
];

/** The engine's default of the minimum hatch spacing, paper millimetres. */
export const DEFAULT_MIN_HATCH_SPACING = 0.7;

/** A minimum hatch spacing typed (a comma too): 0 or more, or null when the text is not one. */
export function parseMinSpacing(text: string): number | null {
  const v = Number(text.trim().replace(',', '.'));
  return text.trim() !== '' && Number.isFinite(v) && v >= 0 && v <= 100 ? v : null;
}

/** The four finish choices, by kind of view and category, with their labels and what they do, as View settings and View styles offer them. */
export const FINISH_FIELDS: { key: 'wallFinishesPlan' | 'wallFinishesSection' | 'floorFinishesPlan' | 'floorFinishesSection'; plan: boolean; label: string; column: string; title: string }[] = [
  { key: 'wallFinishesPlan', plan: true, label: 'Wall finishes', column: 'Wall finishes in plans', title: 'Plans draw the finish layers of walls (a layer\'s own category in its type, else its material\'s)' },
  { key: 'floorFinishesPlan', plan: true, label: 'Floor finishes', column: 'Floor finishes in plans', title: 'Plans draw the finish layers of floors (a layer\'s own category in its type, else its material\'s)' },
  { key: 'wallFinishesSection', plan: false, label: 'Wall finishes', column: 'Wall finishes in sections', title: 'Sections and elevations draw the finish layers of walls (a layer\'s own category in its type, else its material\'s)' },
  { key: 'floorFinishesSection', plan: false, label: 'Floor finishes', column: 'Floor finishes in sections', title: 'Sections and elevations draw the finish layers of floors (a layer\'s own category in its type, else its material\'s)' },
];

/** The entourage modes as the editors offer them. */
export const ENTOURAGE_MODES: [EntourageMode, string][] = [['outline', 'outlines'], ['abstract', 'abstract'], ['detailed', 'detailed'], ['none', 'not drawn']];
/** The vegetation's styles as the editors offer them (decision 818). */
export const VEGETATION_MODES: [VegetationMode, string][] = [['coloured', 'coloured'], ['maquette', 'maquette']];
/** What the vegetation's style does, as its control's tooltip. */
export const VEGETATION_HELP = "How trees, shrubs and hedges look in 3D, in the view's exports and where drawings draw their bodies: each species in its colour, or as an architectural model's, neutral and faceted";
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

/** How cut elements draw from a small scale on: as their materials say, plain solid, an outline only, or the core layer alone. */
export type SmallScaleCutMode = 'asMaterial' | 'solid' | 'outline' | 'coreOnly';
export type SmallScaleCut = { mode?: SmallScaleCutMode; from?: number; color?: string | null };
export const SMALL_SCALE_MODES: [SmallScaleCutMode, string][] = [['asMaterial', 'As material'], ['solid', 'Solid'], ['outline', 'Outline only'], ['coreOnly', 'Core only']];
export const DEFAULT_CUT_FROM = 200;

/** A threshold typed ("200", "1:200"): a whole scale denominator from 1 to 100000, or null. */
export function parseCutFrom(text: string): number | null {
  const s = text.trim().replace(/^1\s*:\s*/, '');
  const v = Number(s);
  return /^\d+$/.test(s) && v >= 1 && v <= 100000 ? v : null;
}

/** The patch for one part of the small-scale cut changed (the engine lays it field by field; a colour of null takes it away). */
export function smallScaleCutPatch(part: SmallScaleCut): { smallScaleCut: SmallScaleCut } { return { smallScaleCut: part }; }

/** The patch for a view's own material graphics choice: 'none' draws by the materials alone, an id is a set (the way back to the style's is a null, by Reset). */
export function materialSetPatch(choice: string): { materialGraphicsSet: string } { return { materialGraphicsSet: choice }; }

/** What a view's select shows: the set it resolves to ('none' when it draws by the materials). */
export const materialSetChoice = (resolved: string | null | undefined): string => resolved || 'none';
