import { describe, expect, it } from 'vitest';
import { boxTest } from '../viewport/selectBox';
import { boxEdges, projectSegments, takes } from '../viewport/select3d';
import { dragTo, isWindow, regionTestOf, toggleLasso, type SelectDrag } from '../viewport/selectDrag';
import { DEFAULT_PREFS, readPrefs } from './prefs';
import {
  NAV_STYLES, NAV_STYLE_IDS, clickMode, clickSelection, dragAction, modsOf, navStyleOf, pansIn2d, picksLeaf, regionMode, regionSelection, wheelFactor, zoomsToCursor, type Mods,
} from './navStyle';

const none: Mods = { shift: false, ctrl: false, alt: false };
const m = (...k: ('shift' | 'ctrl' | 'alt')[]): Mods => ({ ...none, ...Object.fromEntries(k.map((x) => [x, true])) });
const [LEFT, MIDDLE, RIGHT] = [0, 1, 2];

describe('the navigation style table', () => {
  it('has the seven presets, Lapin the default, each with a label and a hint', () => {
    expect(NAV_STYLE_IDS).toEqual(['lapin', 'revit', 'autocad', 'archicad', 'sketchup', 'blender', 'rhino']);
    expect(DEFAULT_PREFS.navStyle).toBe('lapin');
    for (const id of NAV_STYLE_IDS) expect(NAV_STYLES[id]).toMatchObject({ id, label: expect.any(String), hint: expect.any(String) });
    expect(navStyleOf('nonsense').id).toBe('lapin');
    expect(readPrefs({ navStyle: 'revit' }).navStyle).toBe('revit');
    expect(readPrefs({ navStyle: 'maya' }).navStyle).toBe('lapin');
  });

  it('keeps Lapin as it was: the controls own the buttons, Shift toggles, Ctrl picks the part, the 2D views pan with any button but the left', () => {
    const s = NAV_STYLES.lapin;
    expect(s.native).toBe(true);
    // Nothing of the controls' own is in the table: left orbits, middle zooms and right pans by three's mapping.
    for (const b of [LEFT, MIDDLE, RIGHT]) expect(dragAction(s, b, none)).toBeNull();
    expect(clickMode(s, none)).toBe('replace');
    expect(clickMode(s, m('shift'))).toBe('toggle');
    expect(clickMode(s, m('shift', 'ctrl'))).toBe('toggle');
    expect(picksLeaf(s, m('ctrl'))).toBe(true);
    expect(picksLeaf(s, none)).toBe(false);
    expect(pansIn2d(s, LEFT, none)).toBe(false);
    expect(pansIn2d(s, MIDDLE, none)).toBe(true);
    expect(pansIn2d(s, RIGHT, m('shift'))).toBe(true);
    expect(zoomsToCursor(s, true)).toBe(true);
    expect(zoomsToCursor(s, false)).toBe(false);
    expect(s.region).toBe('direction');
  });

  it('gives Lapin a region on Alt + left drag, which nothing else uses', () => {
    expect(dragAction(NAV_STYLES.lapin, LEFT, m('alt'))).toBe('region');
    expect(dragAction(NAV_STYLES.lapin, LEFT, m('alt', 'shift'))).toBe('region');
    expect(dragAction(NAV_STYLES.lapin, LEFT, m('shift'))).toBeNull();
    expect(dragAction(NAV_STYLES.lapin, RIGHT, m('alt'))).toBeNull();
  });

  it('reads Revit and AutoCAD: middle pans, Shift + middle orbits, a left drag is a region, double middle fits', () => {
    for (const id of ['revit', 'autocad'] as const) {
      const s = NAV_STYLES[id];
      expect(dragAction(s, MIDDLE, none)).toBe('pan');
      expect(dragAction(s, MIDDLE, m('shift'))).toBe('orbit');
      expect(dragAction(s, LEFT, none)).toBe('region');
      expect(dragAction(s, RIGHT, none)).toBeNull();
      expect(s.fitDouble3d).toBe(true);
      expect(pansIn2d(s, MIDDLE, none)).toBe(true);
      expect(pansIn2d(s, RIGHT, none)).toBe(false);
      expect(pansIn2d(s, MIDDLE, m('shift'))).toBe(false);
      expect(zoomsToCursor(s, false)).toBe(true);
    }
    expect(clickMode(NAV_STYLES.revit, m('ctrl'))).toBe('add');
    expect(clickMode(NAV_STYLES.revit, m('shift'))).toBe('remove');
    expect(clickMode(NAV_STYLES.autocad, m('shift'))).toBe('remove');
  });

  it('reads Blender: middle orbits, Shift + middle pans, Ctrl + middle zooms, a left drag is a crossing box, Shift toggles', () => {
    const s = NAV_STYLES.blender;
    expect(dragAction(s, MIDDLE, none)).toBe('orbit');
    expect(dragAction(s, MIDDLE, m('shift'))).toBe('pan');
    expect(dragAction(s, MIDDLE, m('ctrl'))).toBe('zoom');
    expect(dragAction(s, MIDDLE, m('shift', 'ctrl'))).toBeNull();
    expect(dragAction(s, LEFT, none)).toBe('region');
    expect(s.region).toBe('crossing');
    expect(clickMode(s, m('shift'))).toBe('toggle');
    expect(zoomsToCursor(s, true)).toBe(false);
  });

  it('reads SketchUp: middle orbits, Shift + middle pans; Ctrl adds, Shift toggles, Ctrl + Shift removes', () => {
    const s = NAV_STYLES.sketchup;
    expect(dragAction(s, MIDDLE, none)).toBe('orbit');
    expect(dragAction(s, MIDDLE, m('shift'))).toBe('pan');
    expect(clickMode(s, m('ctrl'))).toBe('add');
    expect(clickMode(s, m('shift'))).toBe('toggle');
    expect(clickMode(s, m('ctrl', 'shift'))).toBe('remove');
    expect(pansIn2d(s, MIDDLE, m('shift'))).toBe(true);
  });

  it('reads Rhino: right drag orbits, Shift + right pans, Shift adds and Ctrl removes; and ArchiCAD: middle pans, Shift + middle orbits, Shift adds', () => {
    const r = NAV_STYLES.rhino;
    expect(dragAction(r, RIGHT, none)).toBe('orbit');
    expect(dragAction(r, RIGHT, m('shift'))).toBe('pan');
    expect(dragAction(r, MIDDLE, none)).toBeNull();
    expect(pansIn2d(r, RIGHT, none)).toBe(true);
    expect(clickMode(r, m('shift'))).toBe('add');
    expect(clickMode(r, m('ctrl'))).toBe('remove');
    const a = NAV_STYLES.archicad;
    expect(dragAction(a, MIDDLE, none)).toBe('pan');
    expect(dragAction(a, MIDDLE, m('shift'))).toBe('orbit');
    expect(clickMode(a, m('shift'))).toBe('toggle');
  });

  it('never gives one press two actions, nor a region on a modifier a binding of its own takes', () => {
    for (const id of NAV_STYLE_IDS) {
      const s = NAV_STYLES[id];
      for (const d of s.drags.filter((x) => x.action !== 'region')) {
        const same = s.drags.filter((x) => x.action !== 'region' && x.button === d.button && [...(x.mods ?? [])].sort().join() === [...(d.mods ?? [])].sort().join());
        expect(same.length).toBe(1);
      }
    }
  });

  it('turns the wheel over when a style says so, and leaves a normal one alone', () => {
    expect(wheelFactor(NAV_STYLES.revit, 1.2)).toBe(1.2);
    expect(wheelFactor({ ...NAV_STYLES.revit, wheel: { invert: true, toCursor: true } }, 1.25)).toBeCloseTo(0.8, 12);
  });

  it('reads the keys of an event, Cmd as Ctrl', () => {
    expect(modsOf({ shiftKey: true, ctrlKey: false, metaKey: true, altKey: false })).toEqual(m('shift', 'ctrl'));
  });
});

describe('what a click and a region do to the selection', () => {
  it('replaces, adds, removes and toggles one element; empty space only clears on a plain click', () => {
    const sel = ['a', 'b'];
    expect(clickSelection(sel, 'c', 'replace')).toEqual(['c']);
    expect(clickSelection(sel, 'c', 'add')).toEqual(['a', 'b', 'c']);
    expect(clickSelection(sel, 'b', 'add')).toEqual(['a', 'b']);
    expect(clickSelection(sel, 'b', 'remove')).toEqual(['a']);
    expect(clickSelection(sel, 'c', 'remove')).toEqual(['a', 'b']);
    expect(clickSelection(sel, 'b', 'toggle')).toEqual(['a']);
    expect(clickSelection(sel, 'c', 'toggle')).toEqual(['a', 'b', 'c']);
    expect(clickSelection(sel, null, 'replace')).toEqual([]);
    expect(clickSelection(sel, null, 'add')).toEqual(['a', 'b']);
    expect(clickSelection(sel, null, 'remove')).toEqual(['a', 'b']);
  });

  it('takes a region over the selection, adds to it (a toggle adds, as Shift always did) or takes from it', () => {
    expect(regionSelection(['a'], ['b', 'c', 'b'], 'replace')).toEqual(['b', 'c']);
    expect(regionSelection(['a'], ['b', 'a'], 'add')).toEqual(['a', 'b']);
    expect(regionSelection(['a'], ['b'], 'toggle')).toEqual(['a', 'b']);
    expect(regionSelection(['a', 'b', 'c'], ['b', 'x'], 'remove')).toEqual(['a', 'c']);
  });
});

describe('a region\'s keys', () => {
  it('takes the region out of the selection with Ctrl or Cmd in Lapin\'s style only; a click keeps Ctrl for the part', () => {
    expect(regionMode(NAV_STYLES.lapin, m('ctrl'))).toBe('remove');
    expect(regionMode(NAV_STYLES.lapin, m('shift'))).toBe('toggle');
    expect(regionMode(NAV_STYLES.lapin, m('ctrl', 'shift'))).toBe('toggle');
    expect(regionMode(NAV_STYLES.lapin, none)).toBe('replace');
    expect(clickMode(NAV_STYLES.lapin, m('ctrl'))).toBe('replace');
    expect(picksLeaf(NAV_STYLES.lapin, m('ctrl'))).toBe(true);
    expect(regionMode(NAV_STYLES.revit, m('ctrl'))).toBe('add');
    expect(regionMode(NAV_STYLES.rhino, m('ctrl'))).toBe('remove');
  });
});

describe('the 3D selection region of a style', () => {
  // A camera looking down −z from z = 10, 100 px a metre, the origin at (500, 500).
  const project = (x: number, y: number, z: number) => (z >= 10 ? null : { x: 500 + 100 * x, y: 500 - 100 * y });
  const unit = projectSegments(boxEdges([0, 0, 0, 1, 1, 1]), project);
  const half = projectSegments(boxEdges([0, 0, 0, 0.4, 1, 1]), project);

  const takesBy = (d: SelectDrag, seen: typeof unit) => takes(regionTestOf(d, (x, y) => ({ x, y })), isWindow(d), seen);

  it('is a window when dragged to the right, a crossing to the left: a window takes what lies wholly inside, a crossing what it touches', () => {
    const right: SelectDrag = { a: { x: 450, y: 350 }, b: { x: 560, y: 520 } }; // round the left part only
    const left: SelectDrag = { a: { x: 560, y: 520 }, b: { x: 450, y: 350 } };
    expect(isWindow(right)).toBe(true);
    expect(isWindow(left)).toBe(false);
    expect(takesBy(right, unit)).toBe(false);
    expect(takesBy(right, half)).toBe(true);
    expect(takesBy(left, unit)).toBe(true);
    expect(takesBy(left, half)).toBe(true);
    // A rectangle that misses it takes nothing either way.
    const away: SelectDrag = { a: { x: 700, y: 100 }, b: { x: 800, y: 200 } };
    expect(takesBy(away, unit)).toBe(false);
    expect(takesBy({ a: away.b, b: away.a }, unit)).toBe(false);
  });

  it('is always a crossing in a style that says so (Blender), whatever way it is dragged, and keeps it while it follows the pointer or becomes a lasso', () => {
    const right: SelectDrag = { a: { x: 450, y: 350 }, b: { x: 560, y: 520 }, mode: 'crossing' };
    expect(isWindow(right)).toBe(false);
    expect(takesBy(right, unit)).toBe(true);
    const moved = dragTo(right, { x: 800, y: 700 });
    expect(moved.mode).toBe('crossing');
    expect(isWindow(moved)).toBe(false);
    expect(isWindow(toggleLasso(moved))).toBe(false);
    expect(isWindow({ ...right, mode: 'window' })).toBe(true);
    // Without a mode the lasso keeps what the box was when it began.
    expect(isWindow(toggleLasso({ a: { x: 0, y: 0 }, b: { x: 10, y: 10 } }))).toBe(true);
  });

  it('refuses a window for what lies partly behind the eye, takes it by a crossing', () => {
    const tall = projectSegments(boxEdges([0, 0, 0, 1, 1, 20]), project);
    expect(takes(boxTest({ x: 0, y: 0 }, { x: 1000, y: 1000 }, true), true, tall)).toBe(false);
    expect(takes(boxTest({ x: 1000, y: 1000 }, { x: 0, y: 0 }, false), false, tall)).toBe(true);
  });
});
