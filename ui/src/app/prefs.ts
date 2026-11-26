/**
 * The user's preferences (File → Preferences…, Ctrl+,): kept on this computer, the same for every project, read
 * when the app starts. The theme (light, dark, or as the system is), the 2D views' grid (adaptive: its step follows
 * the zoom; or fixed: one step, drawn and snapped to, whatever the zoom), the object snaps a cursor takes (as
 * AutoCAD's OSNAP settings), polar tracking's angle, and which coordinates the status line reads at the cursor
 * (the plan's own, the project base point's, the survey point's map coordinates, or the UCS's), and the project template a
 * new project starts from (decision 470), and Developer extras (decision 489: the Script panel, the Python it runs, the editor
 * a script opens in; off by default, as in Blender). On the desktop they are also kept in the user's folder (app/userSettings.ts). The display's own
 * switches (thin lines, the 3D view's navigation) stay in app/display.ts; the dialog shows them too.
 */

import { create } from 'zustand';
import type { SnapMode } from '../viewport/cad/snaps';
import { DIM_REFS, OPENING_DIM_REFS, type DimRef, type OpeningDimRef } from '../viewport/lineDims';
import { DEFAULT_NAV_STYLE, NAV_STYLE_IDS, type NavStyleId } from './navStyle';

/** The looks the interface wears: paper and graphite (Lapin's own), classic (a neutral grey desktop look), contrast (high contrast). */
export type ThemeName = 'paper' | 'graphite' | 'classic' | 'contrast';
export type LightTheme = Exclude<ThemeName, 'graphite'>;
export const LIGHT_THEMES: readonly LightTheme[] = ['paper', 'classic', 'contrast'];
export const THEME_NAMES: readonly ThemeName[] = ['paper', 'graphite', 'classic', 'contrast'];
export type ThemeChoice = ThemeName | 'system';
/** The look of the drawing areas (3D, plans, sections, sheets) apart from the panels: as the panels (the default), or paper or graphite whatever they wear. */
export type ViewportTheme = 'same' | 'paper' | 'graphite';
export const VIEWPORT_THEMES: readonly ViewportTheme[] = ['same', 'paper', 'graphite'];
export type GridMode = 'adaptive' | 'fixed';
export type CoordSystem = 'internal' | 'project' | 'survey' | 'ucs';

export interface Prefs {
  theme: ThemeChoice;
  /** The drawing areas' own look (forum request): `same` follows the panels' theme. */
  viewportTheme: ViewportTheme;
  /** The light look the sun/moon button goes back to from dark: the last light theme chosen. */
  lastLight: LightTheme;
  gridMode: GridMode;
  /** The fixed grid's step, metres. */
  gridStep: number;
  /** Every how many steps a line is drawn heavier. */
  gridMajor: number;
  /** Points snap to the grid where no object snap takes them. */
  gridSnap: boolean;
  snapModes: SnapMode[];
  /**
   * The object snaps there were when these settings were kept (forum topic 151): one added since is on when it is on by default,
   * whatever the snaps kept, and one turned off since stays off.
   */
  snapModesKnown: SnapMode[];
  /** Polar tracking's increment, degrees. */
  polarStep: number;
  coords: CoordSystem;
  /** The profile standards the pickers leave out for this user (decision 457): a standard added later shows. */
  profileStandardsHidden: string[];
  /** The profile standards a new project offers (null: all); the project keeps only the profiles its types use. */
  newProjectProfileStandards: string[] | null;
  /** The project template File → New starts from (decision 470): a template's id on this computer; null, Lapin's default. */
  defaultTemplate: string | null;
  /** The material standard a new project applies after it is made (stage 3 of the material graphics): a built-in standard's id; null, Lapin's own. Never taken from the language or the country. */
  newProjectMaterialStandard: string | null;
  /** Developer extras (decision 489): the Script panel (Python) on View → Developer. Off: nothing of it shows. */
  developerExtras: boolean;
  /** The Python the Script panel runs (a path or a command); null: the one found on this computer. */
  pythonPath: string | null;
  /** The editor "Open in external editor" uses (a command: code, subl…); null: the system's text editor. */
  externalEditor: string | null;
  /** The mouse navigation style (decision 516): Lapin's own, or as Revit, AutoCAD, ArchiCAD, SketchUp, Blender or Rhino do it (app/navStyle.ts). */
  navStyle: NavStyleId;
  /** Drawing in a sketch records what the inference found as constraints (decision 520, C2); Ctrl on a click places that point free. */
  constrainWhileDrawing: boolean;
  /** What an opening's "Along the wall" measures (Properties): from the wall's start, the meeting wall's face or its core face, to the opening's start, centre or end. */
  openingAlong: { from: 'start' | 'face' | 'core'; point: 'start' | 'centre' | 'end' };
  /** A click outside the input box takes what is typed in it, as Enter does (forum 155); off: it stays pending, as before. Esc and a right click throw it away. */
  clickOutAccepts: boolean;
  /**
   * Where temporary dimensions in plan measure from and to (decision 733, the old Lapin's "Wall dimension snap"): core faces,
   * faces (finish), axis / centre, the centre of the core (decision 738), or the reference line (a wall's kept line, else its
   * axis, to the other's near face: as before).
   * For walls, columns and beams, selected or being drawn; a dimension's dot still cycles that one dimension.
   */
  tempDimRef: DimRef;
  /**
   * Where a door's, a window's or an opening's temporary dimensions measure from (decision 738, the Temporary Dimension
   * Properties' "Doors and windows"): its edges (openings) or its centre (centrelines); a dot still switches one dimension.
   */
  tempDimOpeningRef: OpeningDimRef;
  /** The Division snap's cuts (forum topic 226): each edge into this many equal parts, one number per cut (3: thirds, 4: quarters). */
  snapDivisions: number[];
  /** The Distance snap's distance (m): a point this far in from each end of an edge. */
  snapDistance: number;
  /** A crosshair across the whole drawing area, as AutoCAD's cursor (forum topic 226); off by default. */
  crosshair: boolean;
  /** The arrow keys' nudge step, metres (forum topic 212): 0 follows the zoom (about four pixels), else this one; Shift is ten times as far. */
  nudgeStep: number;
  /** Subtle synthetic tactile sounds on snap, placement, undo and mission completion (docs/plan-ux-game-like.md, P9); off by default. */
  tactileSounds: boolean;
  /**
   * The Flow extras (docs/plan-ux-game-like.md): each off by default, so the interface reads as it did; picking the Flow
   * preset (View → Interface…) turns them on (FLOW_EXTRAS), and Preferences → Flow extras switches each.
   */
  viewCube: boolean;
  selectionToolbar: boolean;
  healthRadar: boolean;
  microTips: boolean;
  quickWalkSplit: boolean;
  keycapHints: boolean;
  ghostPreview: boolean;
  xrayPeek: boolean;
  scrubFields: boolean;
  roleCards: boolean;
  /** The 3D cursor pill, equal-spacing magnets, animated ribbon tooltips, the hold ring and right-click + WASD flight (docs/plan-ux-game-like.md, phase 4). */
  precisionTools: boolean;
  /** The quick tools chosen for the workspace (architecture, structure, MEP, site, drafting): on only once a role card was picked, or here. */
  roleHotbars: boolean;
}

/** The extras the Flow preset turns on, in the order Preferences shows them (tactile sounds and the role's quick tools stay their own choice). */
export const FLOW_EXTRAS = ['viewCube', 'selectionToolbar', 'healthRadar', 'microTips', 'quickWalkSplit', 'keycapHints', 'ghostPreview', 'xrayPeek', 'scrubFields', 'roleCards', 'precisionTools'] as const;
export type FlowExtra = (typeof FLOW_EXTRAS)[number];

export const SNAP_MODES: readonly SnapMode[] = ['endpoint', 'midpoint', 'intersection', 'center', 'quadrant', 'perpendicular', 'tangent', 'nearest', 'wallAxis', 'gridAxis', 'division', 'distance'];
/** The object snaps before forum topic 151 added Tangent and Wall axis: what settings kept with no list of known snaps knew. */
const FIRST_SNAP_MODES: readonly SnapMode[] = ['endpoint', 'midpoint', 'intersection', 'center', 'quadrant', 'perpendicular', 'nearest'];
/** The object snaps on by default (all of them but the Division and Distance, which add points along every edge: none takes a point ahead of the drawn lines' ends and crossings). */
const SNAP_MODES_ON: readonly SnapMode[] = SNAP_MODES.filter((m) => m !== 'division' && m !== 'distance');
export const POLAR_STEPS: readonly number[] = [5, 10, 15, 22.5, 30, 45, 90];
export const DEFAULT_PREFS: Prefs = {
  theme: 'paper', viewportTheme: 'same', lastLight: 'paper', gridMode: 'adaptive', gridStep: 1, gridMajor: 5, gridSnap: true,
  snapModes: [...SNAP_MODES_ON], snapModesKnown: [...SNAP_MODES], polarStep: 45, coords: 'internal',
  profileStandardsHidden: [], newProjectProfileStandards: null, defaultTemplate: null, newProjectMaterialStandard: null,
  developerExtras: false, pythonPath: null, externalEditor: null, navStyle: DEFAULT_NAV_STYLE, constrainWhileDrawing: true,
  openingAlong: { from: 'start', point: 'centre' },
  clickOutAccepts: false,
  tempDimRef: 'core',
  tempDimOpeningRef: 'openings',
  nudgeStep: 0,
  snapDivisions: [3, 4],
  snapDistance: 0.5,
  crosshair: false,
  tactileSounds: false,
  viewCube: false, selectionToolbar: false, healthRadar: false, microTips: false, quickWalkSplit: false,
  keycapHints: false, ghostPreview: false, xrayPeek: false, scrubFields: false, roleCards: false, precisionTools: false, roleHotbars: false,
};

const names = (v: unknown): string[] | null => (Array.isArray(v) ? [...new Set(v.filter((x): x is string => typeof x === 'string' && !!x.trim()).map((x) => x.trim()))] : null);

/** The cuts a list holds: whole numbers from 2 to 64, each once, in order. */
const divisionsOf = (v: unknown): number[] | null => (Array.isArray(v) ? [...new Set(v.filter((n): n is number => Number.isInteger(n) && n >= 2 && n <= 64))].sort((a, b) => a - b) : null);

const KEY = 'lapin.prefs';
const pick = <T,>(v: unknown, allowed: readonly T[], fallback: T): T => (allowed.includes(v as T) ? (v as T) : fallback);

/** What was kept, anything unreadable at its default. */
export function readPrefs(stored: unknown): Prefs {
  const o = (stored && typeof stored === 'object' ? stored : {}) as Partial<Record<keyof Prefs, unknown>>;
  const d = DEFAULT_PREFS;
  const step = typeof o.gridStep === 'number' && Number.isFinite(o.gridStep) && o.gridStep > 0 ? Math.min(1000, Math.max(0.001, o.gridStep)) : d.gridStep;
  const major = typeof o.gridMajor === 'number' && Number.isInteger(o.gridMajor) && o.gridMajor >= 1 && o.gridMajor <= 100 ? o.gridMajor : d.gridMajor;
  const known: readonly unknown[] = Array.isArray(o.snapModesKnown) ? o.snapModesKnown : FIRST_SNAP_MODES;
  const modes = Array.isArray(o.snapModes)
    ? SNAP_MODES.filter((m) => (o.snapModes as unknown[]).includes(m) || (!known.includes(m) && SNAP_MODES_ON.includes(m)))
    : d.snapModes;
  return {
    theme: pick(o.theme, [...THEME_NAMES, 'system'] as const, d.theme),
    viewportTheme: pick(o.viewportTheme, VIEWPORT_THEMES, d.viewportTheme),
    lastLight: pick(o.lastLight ?? (LIGHT_THEMES.includes(o.theme as LightTheme) ? o.theme : undefined), LIGHT_THEMES, d.lastLight),
    gridMode: pick(o.gridMode, ['adaptive', 'fixed'] as const, d.gridMode),
    gridStep: step, gridMajor: major,
    gridSnap: o.gridSnap !== false,
    snapModes: modes,
    snapModesKnown: [...SNAP_MODES],
    polarStep: pick(o.polarStep, POLAR_STEPS, d.polarStep),
    coords: pick(o.coords, ['internal', 'project', 'survey', 'ucs'] as const, d.coords),
    profileStandardsHidden: names(o.profileStandardsHidden) ?? [],
    newProjectProfileStandards: names(o.newProjectProfileStandards),
    defaultTemplate: typeof o.defaultTemplate === 'string' && o.defaultTemplate.trim() ? o.defaultTemplate : null,
    newProjectMaterialStandard: typeof o.newProjectMaterialStandard === 'string' && /^[a-z0-9][a-z0-9._-]*$/i.test(o.newProjectMaterialStandard) && o.newProjectMaterialStandard !== 'lapin' ? o.newProjectMaterialStandard : null,
    developerExtras: o.developerExtras === true,
    pythonPath: typeof o.pythonPath === 'string' && o.pythonPath.trim() ? o.pythonPath.trim() : null,
    externalEditor: typeof o.externalEditor === 'string' && o.externalEditor.trim() ? o.externalEditor.trim() : null,
    navStyle: pick(o.navStyle, NAV_STYLE_IDS, d.navStyle),
    constrainWhileDrawing: o.constrainWhileDrawing !== false,
    clickOutAccepts: o.clickOutAccepts === true,
    tempDimRef: pick(o.tempDimRef, DIM_REFS, d.tempDimRef),
    tempDimOpeningRef: pick(o.tempDimOpeningRef, OPENING_DIM_REFS, d.tempDimOpeningRef),
    nudgeStep: typeof o.nudgeStep === 'number' && Number.isFinite(o.nudgeStep) && o.nudgeStep > 0 ? Math.min(1, Math.max(0.001, o.nudgeStep)) : 0,
    snapDivisions: divisionsOf(o.snapDivisions) ?? d.snapDivisions,
    snapDistance: typeof o.snapDistance === 'number' && Number.isFinite(o.snapDistance) && o.snapDistance > 0 ? Math.min(1000, o.snapDistance) : d.snapDistance,
    crosshair: o.crosshair === true,
    tactileSounds: o.tactileSounds === true,
    viewCube: o.viewCube === true, selectionToolbar: o.selectionToolbar === true, healthRadar: o.healthRadar === true,
    microTips: o.microTips === true, quickWalkSplit: o.quickWalkSplit === true, keycapHints: o.keycapHints === true,
    ghostPreview: o.ghostPreview === true, xrayPeek: o.xrayPeek === true, scrubFields: o.scrubFields === true,
    roleCards: o.roleCards === true, precisionTools: o.precisionTools === true, roleHotbars: o.roleHotbars === true,
    openingAlong: {
      from: pick((o.openingAlong as { from?: unknown } | undefined)?.from, ['start', 'face', 'core'] as const, d.openingAlong.from),
      point: pick((o.openingAlong as { point?: unknown } | undefined)?.point, ['start', 'centre', 'end'] as const, d.openingAlong.point),
    },
  };
}

function read(): Prefs {
  try { return readPrefs(JSON.parse(localStorage.getItem(KEY) ?? 'null')); } catch { return readPrefs(null); }
}

export const usePrefs = create<Prefs>(() => read());

export function setPrefs(patch: Partial<Prefs>): void {
  const lastLight = LIGHT_THEMES.includes(patch.theme as LightTheme) ? { lastLight: patch.theme as LightTheme } : {};
  usePrefs.setState(readPrefs({ ...usePrefs.getState(), ...patch, ...lastLight }));
  try { localStorage.setItem(KEY, JSON.stringify(usePrefs.getState())); } catch { /* kept for this session */ }
}

/** The Flow preset's extras switched on together (picking the preset; each stays switchable in Preferences). */
export const turnOnFlowExtras = (): void => setPrefs(Object.fromEntries(FLOW_EXTRAS.map((k) => [k, true])) as Partial<Prefs>);

/** What the sun/moon button switches to: a light theme goes to dark (the choice is kept as the last light one), dark returns to it. */
export function quickToggleTheme(current: ThemeName, lastLight: LightTheme): ThemeName {
  return current === 'graphite' ? lastLight : 'graphite';
}

/** The theme a choice shows now (the system's: dark when it asks for dark). */
export function resolveTheme(choice: ThemeChoice): ThemeName {
  if (choice !== 'system') return choice;
  try { return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'graphite' : 'paper'; } catch { return 'paper'; }
}

/** The look the drawing areas wear: the panels' theme, or the viewport's own choice over it. */
export function resolveViewTheme(panel: ThemeName, choice: ViewportTheme): ThemeName {
  return choice === 'same' ? panel : choice;
}

/** The theme the app starts in: the one kept (before decision 445 nothing was kept and every start was light). */
export const startTheme = (): ThemeName => resolveTheme(usePrefs.getState().theme);

/** The 2D grid's step at a zoom (pixels per metre): the fixed one, or the adaptive series' first that is 12 px or more. */
export function gridStepAt(scale: number, p: Pick<Prefs, 'gridMode' | 'gridStep'> = usePrefs.getState()): number {
  if (p.gridMode === 'fixed') return p.gridStep;
  return [1, 5, 25, 100, 500, 2500, 10000, 50000].find((st) => st * scale >= 12) ?? 50000;
}

/** The step the cursor snaps to on the grid (0: none): the fixed grid's own, else the adaptive rule the plan always had. */
export function gridSnapStep(scale: number, p: Pick<Prefs, 'gridMode' | 'gridStep' | 'gridSnap'> = usePrefs.getState()): number {
  if (!p.gridSnap) return 0;
  return p.gridMode === 'fixed' ? p.gridStep : scale > 30 ? 0.05 : 0.5;
}

/** The zoom (pixels per metre) of the plan being drawn in: what the adaptive grid's snap step follows; the plan writes it, the status line reads it. */
export const useGridScale = create<{ scale: number }>(() => ({ scale: 50 }));

/** The steps the snap menu offers for a fixed grid (m); Preferences takes any. */
export const GRID_STEPS: readonly number[] = [0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1];

/** Snapping to a fixed step (the step chosen, snapping on), or back to the adaptive grid that follows the zoom. */
export function chooseGridStep(step: number | 'adaptive'): void {
  setPrefs(step === 'adaptive' ? { gridMode: 'adaptive', gridSnap: true } : { gridMode: 'fixed', gridStep: step, gridSnap: true });
}
