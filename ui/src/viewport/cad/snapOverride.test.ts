/** Forum topic 154: Object Snap Override (Ctrl + right click, or a typed code): one snap, or none, for the next point only. */

import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { Vec2 } from '../../platform/module';

const engine = vi.hoisted(() => ({ execute: vi.fn(), undo: vi.fn(), useSession: { getState: vi.fn(() => ({ nodes: new Map(), error: null })), setState: vi.fn() } }));
vi.mock('../../engine/session', () => engine);

import { cancel, feedPoint, startCommand, submit, useCad } from './session';
import { ALL_MODES, findSnap, modesFor, snapsOn, SNAP_CODES } from './snaps';
import type { Shape } from './shapes';
import { SNAP_MODES, readPrefs } from '../../app/prefs';
import { SNAP_LABELS } from '../../shell/CursorCoords';
import { useContextMenu } from '../../shell/ContextMenu';
import { useUi } from '../../app/store';
import { armSnapOverride, askingForPoint, isOverrideGesture, openSnapOverrideMenu, overridePress, spendOverride } from './snapOverride';
import { resolveAim } from './aim';

const P = (x: number, y: number): Vec2 => ({ x, y });
// A line from (0,0) to (10,0): its end at (10,0), its middle at (5,0).
const line: Shape[] = [{ type: 'line', a: P(0, 0), b: P(10, 0) }];

beforeEach(() => {
  engine.execute.mockReset();
  engine.execute.mockImplementation(() => new Promise((r) => setTimeout(() => r({ ids: ['x'] }), 2)));
  cancel(true);
  useCad.setState({ host: { viewId: 'v', pickDraft: () => null, pickAny: () => null, shapeOf: () => null }, history: [], osnap: true, snapOverride: null });
  useUi.setState({ tool: null });
  useContextMenu.setState({ at: null, items: [] });
});

describe('the modes an override leaves', () => {
  it('is the one snap alone, none for "none", the running ones with no override', () => {
    expect([...modesFor('midpoint', SNAP_MODES)]).toEqual(['midpoint']);
    expect(modesFor('none', SNAP_MODES).size).toBe(0);
    expect([...modesFor(null, ['endpoint', 'nearest'])]).toEqual(['endpoint', 'nearest']);
  });

  it('snaps with SNAP off when a kind is armed, never for none', () => {
    expect(snapsOn(null, true)).toBe(true);
    expect(snapsOn(null, false)).toBe(false);
    expect(snapsOn('tangent', false)).toBe(true);
    expect(snapsOn('none', true)).toBe(false);
  });

  it('makes the finder take only that snap, even where another is nearer', () => {
    const p = P(9.9, 0.05);
    expect(findSnap(p, line, null, { tolerance: 6, modes: ALL_MODES })?.mode).toBe('endpoint');
    const mid = findSnap(p, line, null, { tolerance: 6, modes: modesFor('midpoint', SNAP_MODES) });
    expect(mid?.mode).toBe('midpoint');
    expect(mid?.point).toEqual(P(5, 0));
    expect(findSnap(p, line, null, { tolerance: 6, modes: modesFor('none', SNAP_MODES) })).toBeNull();
  });

  it('gives resolveAim the armed snap as the point', () => {
    const s = findSnap(P(9.9, 0.05), line, null, { tolerance: 6, modes: modesFor('midpoint', SNAP_MODES) })!;
    const a = resolveAim(P(9.9, 0.05), { base: null, ortho: false, polar: false, snap: { point: s.point, mode: s.mode }, acquired: [], tol: 6 });
    expect(a.mode).toBe('midpoint');
    expect(a.point).toEqual(P(5, 0));
  });
});

describe('the codes and the menu', () => {
  it('has a code for every snap in SNAP_MODES, and NON for none', () => {
    expect(new Set(Object.values(SNAP_CODES))).toEqual(new Set([...SNAP_MODES, 'none']));
    expect(SNAP_CODES.MID).toBe('midpoint');
    expect(SNAP_CODES.TAN).toBe('tangent');
  });

  it('lists the same snaps and labels as SNAP_MODES, then None and Snap settings…', () => {
    openSnapOverrideMenu(100, 100);
    const items = useContextMenu.getState().items;
    expect(items.map((i) => i.label)).toEqual([...SNAP_MODES.map((m) => SNAP_LABELS[m]), 'No snap (for this point)', 'Snap settings…']);
    expect(items.map((i) => i.label)).toContain('Wall axis');
    expect(items.map((i) => i.label)).toContain('Tangent');
    items.find((i) => i.label === 'Midpoint')!.onSelect();
    expect(useCad.getState().snapOverride).toBe('midpoint');
    items.find((i) => i.label === 'No snap (for this point)')!.onSelect();
    expect(useCad.getState().snapOverride).toBe('none');
  });

  it('leaves the running snaps in the preferences as they were', () => {
    expect(readPrefs(null).snapModes).toEqual(SNAP_MODES.filter((m) => m !== 'division' && m !== 'distance')); // Divisions and Distance are off until ticked
  });
});

describe('the gesture', () => {
  const press = (o: Partial<{ button: number; ctrlKey: boolean; metaKey: boolean }>) => ({ button: 0, ctrlKey: false, metaKey: false, ...o });
  it('is Ctrl or Cmd with the right button; on macOS also Ctrl with the left (its right click)', () => {
    expect(isOverrideGesture(press({ button: 2, ctrlKey: true }), false)).toBe(true);
    expect(isOverrideGesture(press({ button: 2, metaKey: true }), false)).toBe(true);
    expect(isOverrideGesture(press({ button: 2 }), false)).toBe(false);
    expect(isOverrideGesture(press({ button: 0, ctrlKey: true }), false)).toBe(false);
    expect(isOverrideGesture(press({ button: 0, ctrlKey: true }), true)).toBe(true);
    expect(isOverrideGesture(press({ button: 0 }), true)).toBe(false);
  });

  it('opens the menu only while a point is asked; a plain right click is left alone', async () => {
    const ev = (o: object) => ({ button: 2, ctrlKey: true, metaKey: false, clientX: 5, clientY: 50, preventDefault: vi.fn(), ...o });
    expect(askingForPoint()).toBe(false);
    expect(overridePress(ev({}))).toBe(false);
    await startCommand('LINE');
    expect(askingForPoint()).toBe(true);
    const plain = ev({ ctrlKey: false });
    expect(overridePress(plain)).toBe(false);
    expect(plain.preventDefault).not.toHaveBeenCalled();
    const e = ev({});
    expect(overridePress(e)).toBe(true);
    expect(e.preventDefault).toHaveBeenCalled();
    expect(useContextMenu.getState().items.length).toBeGreaterThan(9);
  });
});

describe('the one-shot', () => {
  it('is armed by a typed code at a point step and spent by the next point', async () => {
    await startCommand('LINE');
    await submit('mid');
    expect(useCad.getState().snapOverride).toBe('midpoint');
    expect(useCad.getState().step?.want).toBe('point');
    await feedPoint(P(1, 1));
    expect(useCad.getState().snapOverride).toBeNull();
    await submit('NON');
    expect(useCad.getState().snapOverride).toBe('none');
  });

  it("is spent by a tool's left click, dropped when the command is cancelled", async () => {
    armSnapOverride('center');
    spendOverride({ button: 2 });
    await new Promise((r) => setTimeout(r, 5));
    expect(useCad.getState().snapOverride).toBe('center');
    spendOverride({ button: 0 });
    await new Promise((r) => setTimeout(r, 5));
    expect(useCad.getState().snapOverride).toBeNull();
    await startCommand('LINE');
    armSnapOverride('center');
    cancel();
    expect(useCad.getState().snapOverride).toBeNull();
  });

  it('is not armed by a code when no point is asked', async () => {
    await submit('MID');
    expect(useCad.getState().snapOverride).toBeNull();
  });
});
