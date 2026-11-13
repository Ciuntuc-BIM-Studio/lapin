/**
 * Mouse navigation styles (decision 516, a tester's report): which buttons and modifiers orbit, pan and zoom the 3D
 * view, pan the 2D views, select (replace, add, remove, toggle) and draw a selection region, as the programs people
 * come from do them. One table, data and not ifs: every place that reads the mouse asks it (the 3D view, plans,
 * sections, elevations, sheets). "Lapin" is the behaviour the app always had and stays the default: three's orbit
 * controls keep their mapping (left orbits, middle zooms, right pans), the table only adds what is free there
 * (Alt + left drag draws a region). The others take the buttons over; they assume a three-button mouse (a trackpad
 * has no middle button: it stays with Lapin's).
 *
 * Only control mappings, from each program's published defaults: Revit-like, AutoCAD-like and the others are plain
 * labels. Kept per user in the preferences (`navStyle`), chosen from the status bar or File → Preferences.
 */

export type NavStyleId = 'lapin' | 'revit' | 'autocad' | 'archicad' | 'sketchup' | 'blender' | 'rhino';
export const NAV_STYLE_IDS: readonly NavStyleId[] = ['lapin', 'revit', 'autocad', 'archicad', 'sketchup', 'blender', 'rhino'];

/** The modifier keys a binding reads: Ctrl also stands for Cmd (the Mac's). */
export type Mod = 'shift' | 'ctrl' | 'alt';
export interface Mods { shift: boolean; ctrl: boolean; alt: boolean }
export type Button = 'left' | 'middle' | 'right';

/** What a drag does in the 3D view. A region is the selection window or crossing, by the drag's direction. */
export type DragAction = 'orbit' | 'pan' | 'zoom' | 'region';
export interface DragBinding { button: Button; mods?: readonly Mod[]; action: DragAction }

/** What a click does to the selection: take it over, add to it, take from it, or flip. */
export type ClickMode = 'replace' | 'add' | 'remove' | 'toggle';
/** The first entry whose keys are all held decides (the most specific first). */
export interface ClickBinding { mods: readonly Mod[]; mode: ClickMode }

/** A region's mode: by the drag's direction (to the right a window, to the left a crossing), or always the same. */
export type RegionMode = 'direction' | 'window' | 'crossing';

export interface NavStyle {
  id: NavStyleId;
  /** The short name the status bar shows (a plain label, no program's mark). */
  label: string;
  /** One line of the main gestures, in English (translated where it is shown). */
  hint: string;
  /** Lapin's own: the 3D orbit controls keep their mapping and `drags` only add. */
  native: boolean;
  /** The 3D view's drags, an exact button and modifiers each (a region ignores Shift and Ctrl: they say how it joins the selection). */
  drags: readonly DragBinding[];
  /** The 2D views' pan drags; null: any button but the left (what the app always did). */
  pan2d: readonly DragBinding[] | null;
  wheel: {
    /** The wheel turned the other way: forward zooms out. */
    invert: boolean;
    /** Zoom to the point under the cursor: true or false, or null for the user's own choice (View → Zoom to cursor). */
    toCursor: boolean | null;
  };
  clicks: readonly ClickBinding[];
  /** The keys that pick a part, not the whole (drill select); null: none. */
  leaf: readonly Mod[] | null;
  region: RegionMode;
  /** The middle button pressed twice fits the 3D view (the 2D views always do). */
  fitDouble3d: boolean;
}

const L = (action: DragAction, mods?: readonly Mod[]): DragBinding => ({ button: 'left', mods, action });
const M = (action: DragAction, mods?: readonly Mod[]): DragBinding => ({ button: 'middle', mods, action });
const R = (action: DragAction, mods?: readonly Mod[]): DragBinding => ({ button: 'right', mods, action });

export const NAV_STYLES: Record<NavStyleId, NavStyle> = {
  lapin: {
    id: 'lapin', label: 'Lapin', native: true,
    hint: 'Left drag orbits, right drag pans, wheel zooms; Alt + left drag selects a region',
    drags: [L('region', ['alt'])], pan2d: null,
    wheel: { invert: false, toCursor: null },
    clicks: [{ mods: ['shift'], mode: 'toggle' }], leaf: ['ctrl'], region: 'direction', fitDouble3d: false,
  },
  revit: {
    id: 'revit', label: 'Revit-like', native: false,
    hint: 'Middle drag pans, Shift + middle drag orbits; left drag selects a window or crossing; Ctrl adds, Shift removes',
    drags: [M('pan'), M('orbit', ['shift']), L('region')], pan2d: [M('pan')],
    wheel: { invert: false, toCursor: true },
    clicks: [{ mods: ['ctrl'], mode: 'add' }, { mods: ['shift'], mode: 'remove' }], leaf: ['alt'], region: 'direction', fitDouble3d: true,
  },
  autocad: {
    id: 'autocad', label: 'AutoCAD-like', native: false,
    hint: 'Middle drag pans, Shift + middle drag orbits; left drag selects a window or crossing; Shift removes, Ctrl adds',
    drags: [M('pan'), M('orbit', ['shift']), L('region')], pan2d: [M('pan')],
    wheel: { invert: false, toCursor: true },
    clicks: [{ mods: ['shift'], mode: 'remove' }, { mods: ['ctrl'], mode: 'add' }], leaf: ['alt'], region: 'direction', fitDouble3d: true,
  },
  archicad: {
    id: 'archicad', label: 'ArchiCAD-like', native: false,
    hint: 'Middle drag pans, Shift + middle drag orbits; left drag draws a marquee; Shift adds or removes',
    drags: [M('pan'), M('orbit', ['shift']), L('region')], pan2d: [M('pan')],
    wheel: { invert: false, toCursor: true },
    clicks: [{ mods: ['shift'], mode: 'toggle' }], leaf: ['alt'], region: 'direction', fitDouble3d: false,
  },
  sketchup: {
    id: 'sketchup', label: 'SketchUp-like', native: false,
    hint: 'Middle drag orbits, Shift + middle drag pans; left drag selects a window or crossing; Ctrl adds, Shift toggles, Ctrl + Shift removes',
    drags: [M('orbit'), M('pan', ['shift']), L('region')], pan2d: [M('pan', ['shift'])],
    wheel: { invert: false, toCursor: true },
    clicks: [{ mods: ['ctrl', 'shift'], mode: 'remove' }, { mods: ['ctrl'], mode: 'add' }, { mods: ['shift'], mode: 'toggle' }], leaf: ['alt'], region: 'direction', fitDouble3d: false,
  },
  blender: {
    id: 'blender', label: 'Blender-like', native: false,
    hint: 'Middle drag orbits, Shift + middle drag pans, Ctrl + middle drag zooms; left drag selects a box; Shift toggles',
    drags: [M('orbit'), M('pan', ['shift']), M('zoom', ['ctrl']), L('region')], pan2d: [M('pan', ['shift'])],
    wheel: { invert: false, toCursor: false },
    clicks: [{ mods: ['shift'], mode: 'toggle' }], leaf: ['alt'], region: 'crossing', fitDouble3d: false,
  },
  rhino: {
    id: 'rhino', label: 'Rhino-like', native: false,
    hint: 'Right drag orbits, Shift + right drag pans; left drag selects a window or crossing; Shift adds, Ctrl removes',
    drags: [R('orbit'), R('pan', ['shift']), L('region')], pan2d: [R('pan'), R('pan', ['shift'])],
    wheel: { invert: false, toCursor: true },
    clicks: [{ mods: ['shift'], mode: 'add' }, { mods: ['ctrl'], mode: 'remove' }], leaf: ['alt'], region: 'direction', fitDouble3d: false,
  },
};

export const DEFAULT_NAV_STYLE: NavStyleId = 'lapin';
export const navStyleOf = (id: string | null | undefined): NavStyle => NAV_STYLES[(id as NavStyleId) in NAV_STYLES ? (id as NavStyleId) : DEFAULT_NAV_STYLE];

/** The modifiers an event holds (Cmd counts as Ctrl). */
export const modsOf = (e: { shiftKey: boolean; ctrlKey: boolean; metaKey: boolean; altKey: boolean }): Mods => ({ shift: e.shiftKey, ctrl: e.ctrlKey || e.metaKey, alt: e.altKey });

export const buttonOf = (n: number): Button | null => (n === 0 ? 'left' : n === 1 ? 'middle' : n === 2 ? 'right' : null);

const held = (need: readonly Mod[] | undefined, m: Mods) => (need ?? []).every((k) => m[k]);
const exact = (need: readonly Mod[] | undefined, m: Mods) => (['shift', 'ctrl', 'alt'] as const).every((k) => m[k] === !!need?.includes(k));

/**
 * What a press of `button` with `mods` begins in the 3D view; null: nothing (in a style that is not Lapin's the
 * controls then stay still for the press). A region is taken by the keys it names, whatever Shift and Ctrl say.
 */
export function dragAction(style: NavStyle, button: number, mods: Mods): DragAction | null {
  const b = buttonOf(button);
  if (!b) return null;
  const own = style.drags.filter((d) => d.button === b && d.action !== 'region').find((d) => exact(d.mods, mods));
  if (own) return own.action;
  const region = style.drags.find((d) => d.button === b && d.action === 'region' && held(d.mods, mods) && (d.mods?.includes('alt') ? true : !mods.alt));
  return region ? 'region' : null;
}

/** Whether a press pans a 2D view (a plan, a section, an elevation, a sheet). */
export function pansIn2d(style: NavStyle, button: number, mods: Mods): boolean {
  if (!style.pan2d) return button !== 0;
  const b = buttonOf(button);
  return !!b && style.pan2d.some((d) => d.button === b && exact(d.mods, mods));
}

/** What a click with these keys does to the selection. */
export function clickMode(style: NavStyle, mods: Mods): ClickMode {
  return style.clicks.find((c) => held(c.mods, mods))?.mode ?? 'replace';
}

/** Whether these keys pick a part, not the whole. */
export const picksLeaf = (style: NavStyle, mods: Mods): boolean => !!style.leaf && held(style.leaf, mods) && style.leaf.length > 0;

/** The selection a click on `id` (null: empty space) gives: from `before`, in `mode`. Empty space with add, remove or toggle changes nothing. */
export function clickSelection(before: readonly string[], id: string | null, mode: ClickMode): string[] {
  if (!id) return mode === 'replace' ? [] : [...before];
  const has = before.includes(id);
  switch (mode) {
    case 'replace': return [id];
    case 'add': return has ? [...before] : [...before, id];
    case 'remove': return before.filter((x) => x !== id);
    case 'toggle': return has ? before.filter((x) => x !== id) : [...before, id];
  }
}

/** The selection a region gives: `ids` taken over the old one, added to it (a toggle adds too), or taken from it. */
export function regionSelection(before: readonly string[], ids: readonly string[], mode: ClickMode): string[] {
  if (mode === 'replace') return [...new Set(ids)];
  if (mode === 'remove') { const gone = new Set(ids); return before.filter((x) => !gone.has(x)); }
  return [...new Set([...before, ...ids])];
}

/** Whether the wheel zooms to the point under the cursor, given the user's own choice (View → Zoom to cursor). */
export const zoomsToCursor = (style: NavStyle, userChoice: boolean): boolean => style.wheel.toCursor ?? userChoice;

/** A wheel's zoom factor, turned over when the style's wheel is. */
export const wheelFactor = (style: NavStyle, k: number): number => (style.wheel.invert ? 1 / k : k);
