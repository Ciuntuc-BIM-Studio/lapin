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
import { DEFAULT_NAV_STYLE, NAV_STYLE_IDS, type NavStyleId } from './navStyle';

export type ThemeChoice = 'paper' | 'graphite' | 'system';
export type GridMode = 'adaptive' | 'fixed';
export type CoordSystem = 'internal' | 'project' | 'survey' | 'ucs';

export interface Prefs {
  theme: ThemeChoice;
  gridMode: GridMode;
  /** The fixed grid's step, metres. */
  gridStep: number;
  /** Every how many steps a line is drawn heavier. */
  gridMajor: number;
  /** Points snap to the grid where no object snap takes them. */
  gridSnap: boolean;
  snapModes: SnapMode[];
  /** Polar tracking's increment, degrees. */
  polarStep: number;
  coords: CoordSystem;
  /** The profile standards the pickers leave out for this user (decision 457): a standard added later shows. */
  profileStandardsHidden: string[];
  /** The profile standards a new project offers (null: all); the project keeps only the profiles its types use. */
  newProjectProfileStandards: string[] | null;
  /** The project template File → New starts from (decision 470): a template's id on this computer; null, Lapin's default. */
  defaultTemplate: string | null;
  /** Developer extras (decision 489): the Script panel (Python) on View → Developer. Off: nothing of it shows. */
  developerExtras: boolean;
  /** The Python the Script panel runs (a path or a command); null: the one found on this computer. */
  pythonPath: string | null;
  /** The editor "Open in external editor" uses (a command: code, subl…); null: the system's text editor. */
  externalEditor: string | null;
  /** The mouse navigation style (decision 516): Lapin's own, or as Revit, AutoCAD, ArchiCAD, SketchUp, Blender or Rhino do it (app/navStyle.ts). */
  navStyle: NavStyleId;
}

export const SNAP_MODES: readonly SnapMode[] = ['endpoint', 'midpoint', 'intersection', 'center', 'quadrant', 'perpendicular', 'nearest'];
export const POLAR_STEPS: readonly number[] = [5, 10, 15, 22.5, 30, 45, 90];
export const DEFAULT_PREFS: Prefs = {
  theme: 'paper', gridMode: 'adaptive', gridStep: 1, gridMajor: 5, gridSnap: true,
  snapModes: [...SNAP_MODES], polarStep: 45, coords: 'internal',
  profileStandardsHidden: [], newProjectProfileStandards: null, defaultTemplate: null,
  developerExtras: false, pythonPath: null, externalEditor: null, navStyle: DEFAULT_NAV_STYLE,
};

const names = (v: unknown): string[] | null => (Array.isArray(v) ? [...new Set(v.filter((x): x is string => typeof x === 'string' && !!x.trim()).map((x) => x.trim()))] : null);

const KEY = 'lapin.prefs';
const pick = <T,>(v: unknown, allowed: readonly T[], fallback: T): T => (allowed.includes(v as T) ? (v as T) : fallback);

/** What was kept, anything unreadable at its default. */
export function readPrefs(stored: unknown): Prefs {
  const o = (stored && typeof stored === 'object' ? stored : {}) as Partial<Record<keyof Prefs, unknown>>;
  const d = DEFAULT_PREFS;
  const step = typeof o.gridStep === 'number' && Number.isFinite(o.gridStep) && o.gridStep > 0 ? Math.min(1000, Math.max(0.001, o.gridStep)) : d.gridStep;
  const major = typeof o.gridMajor === 'number' && Number.isInteger(o.gridMajor) && o.gridMajor >= 1 && o.gridMajor <= 100 ? o.gridMajor : d.gridMajor;
  const modes = Array.isArray(o.snapModes) ? SNAP_MODES.filter((m) => (o.snapModes as unknown[]).includes(m)) : d.snapModes;
  return {
    theme: pick(o.theme, ['paper', 'graphite', 'system'] as const, d.theme),
    gridMode: pick(o.gridMode, ['adaptive', 'fixed'] as const, d.gridMode),
    gridStep: step, gridMajor: major,
    gridSnap: o.gridSnap !== false,
    snapModes: modes,
    polarStep: pick(o.polarStep, POLAR_STEPS, d.polarStep),
    coords: pick(o.coords, ['internal', 'project', 'survey', 'ucs'] as const, d.coords),
    profileStandardsHidden: names(o.profileStandardsHidden) ?? [],
    newProjectProfileStandards: names(o.newProjectProfileStandards),
    defaultTemplate: typeof o.defaultTemplate === 'string' && o.defaultTemplate.trim() ? o.defaultTemplate : null,
    developerExtras: o.developerExtras === true,
    pythonPath: typeof o.pythonPath === 'string' && o.pythonPath.trim() ? o.pythonPath.trim() : null,
    externalEditor: typeof o.externalEditor === 'string' && o.externalEditor.trim() ? o.externalEditor.trim() : null,
    navStyle: pick(o.navStyle, NAV_STYLE_IDS, d.navStyle),
  };
}

function read(): Prefs {
  try { return readPrefs(JSON.parse(localStorage.getItem(KEY) ?? 'null')); } catch { return readPrefs(null); }
}

export const usePrefs = create<Prefs>(() => read());

export function setPrefs(patch: Partial<Prefs>): void {
  usePrefs.setState(readPrefs({ ...usePrefs.getState(), ...patch }));
  try { localStorage.setItem(KEY, JSON.stringify(usePrefs.getState())); } catch { /* kept for this session */ }
}

/** The theme a choice shows now (the system's: dark when it asks for dark). */
export function resolveTheme(choice: ThemeChoice): 'paper' | 'graphite' {
  if (choice !== 'system') return choice;
  try { return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'graphite' : 'paper'; } catch { return 'paper'; }
}

/** The theme the app starts in: the one kept (before decision 445 nothing was kept and every start was light). */
export const startTheme = (): 'paper' | 'graphite' => resolveTheme(usePrefs.getState().theme);

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
