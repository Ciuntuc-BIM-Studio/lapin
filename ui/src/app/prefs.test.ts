import { describe, expect, it } from 'vitest';
import { DEFAULT_PREFS, gridSnapStep, gridStepAt, readPrefs } from './prefs';
import { fromFrame, toFrame } from './frame';
import { dropAt, EDGE } from './tabDrag';
import { parseCoordinateInput, applyOrtho } from '../viewport/cad/input';
import { resolveAim } from '../viewport/cad/aim';

const near = (a: { x: number; y: number }, b: { x: number; y: number }) => { expect(a.x).toBeCloseTo(b.x, 9); expect(a.y).toBeCloseTo(b.y, 9); };

describe('preferences (decision 445)', () => {
  it('reads what was kept, anything unreadable at its default', () => {
    expect(readPrefs(null)).toEqual(DEFAULT_PREFS);
    const p = readPrefs({ theme: 'graphite', gridMode: 'fixed', gridStep: 0.25, gridMajor: 4, snapModes: ['endpoint', 'bogus'], polarStep: 30, coords: 'survey', gridSnap: false });
    expect(p).toMatchObject({ theme: 'graphite', gridMode: 'fixed', gridStep: 0.25, gridMajor: 4, snapModes: ['endpoint'], polarStep: 30, coords: 'survey', gridSnap: false });
    expect(readPrefs({ theme: 'neon', gridStep: -1, polarStep: 7, gridMajor: 0.5 })).toMatchObject({ theme: 'paper', gridStep: 1, polarStep: 45, gridMajor: 5 });
  });

  it('keeps the adaptive grid the plan always had, and a fixed one at every zoom', () => {
    expect(gridStepAt(20, { gridMode: 'adaptive', gridStep: 0.1 })).toBe(1);
    expect(gridStepAt(1, { gridMode: 'adaptive', gridStep: 0.1 })).toBe(25);
    expect(gridStepAt(1, { gridMode: 'fixed', gridStep: 0.1 })).toBe(0.1);
    expect(gridSnapStep(50, { gridMode: 'adaptive', gridStep: 1, gridSnap: true })).toBe(0.05);
    expect(gridSnapStep(50, { gridMode: 'fixed', gridStep: 0.3, gridSnap: true })).toBe(0.3);
    expect(gridSnapStep(50, { gridMode: 'fixed', gridStep: 0.3, gridSnap: false })).toBe(0);
  });
});

describe('the UCS (decision 445)', () => {
  const f = { origin: { x: 10, y: 5 }, angle: 90 };

  it('moves points in and out of its axes', () => {
    near(toFrame(f, { x: 10, y: 7 }), { x: 2, y: 0 });
    near(fromFrame(f, { x: 2, y: 0 }), { x: 10, y: 7 });
    near(fromFrame(f, toFrame(f, { x: -3.2, y: 8.1 })), { x: -3.2, y: 8.1 });
  });

  it('reads typed coordinates in it: absolute from its origin, relative and polar turned with it', () => {
    const abs = parseCoordinateInput('2000,0', { last: null, frame: f });
    expect(abs?.kind).toBe('point');
    if (abs?.kind === 'point') near(abs.pt, { x: 10, y: 7 });
    const rel = parseCoordinateInput('@1000,0', { last: { x: 0, y: 0 }, frame: f });
    if (rel?.kind === 'point') near(rel.pt, { x: 0, y: 1 });
    const polar = parseCoordinateInput('@1000<90', { last: { x: 0, y: 0 }, frame: f });
    if (polar?.kind === 'point') near(polar.pt, { x: -1, y: 0 });
    const plain = parseCoordinateInput('2000,0', { last: null }); // the project's unit: millimetres
    if (plain?.kind === 'point') near(plain.pt, { x: 2, y: 0 });
  });

  it('squares ortho and rounds the grid along its axes', () => {
    const g = { origin: { x: 0, y: 0 }, angle: 45 };
    const p = applyOrtho({ x: 0, y: 0 }, { x: 1, y: 0.9 }, g);
    near(toFrame(g, p), { x: toFrame(g, { x: 1, y: 0.9 }).x, y: 0 });
    const a = resolveAim({ x: 1.02, y: 1.01 }, { base: null, ortho: false, polar: false, snap: null, acquired: [], tol: 0.001, grid: Math.SQRT2, frame: g });
    expect(a.kind).toBe('grid');
    near(a.point, { x: 1, y: 1 });
  });

  it('takes polar tracking at the angle chosen', () => {
    const a = resolveAim({ x: Math.cos(Math.PI / 6) * 2, y: Math.sin(Math.PI / 6) * 2 + 0.01 }, { base: { x: 0, y: 0 }, ortho: false, polar: true, polarStep: 30, snap: null, acquired: [], tol: 0.05 });
    expect(a.lock).toBe('polar');
    const b = resolveAim({ x: Math.cos(Math.PI / 6) * 2, y: Math.sin(Math.PI / 6) * 2 + 0.01 }, { base: { x: 0, y: 0 }, ortho: false, polar: true, snap: null, acquired: [], tol: 0.05 });
    expect(b.lock).not.toBe('polar');
  });
});

describe('dragging a tab (decision 445)', () => {
  const size = { w: 1000, h: 800 };
  const one = [{ index: 0, rect: { left: 100, top: 100, width: 800, height: 600 } }];
  it('opens a window at the edge, splits at a view’s right or bottom quarter, else opens there', () => {
    expect(dropAt(EDGE - 1, 400, size, one, 1)?.kind).toBe('window');
    expect(dropAt(-50, 400, size, one, 1)?.kind).toBe('window');
    expect(dropAt(850, 300, size, one, 1)?.kind).toBe('right');
    expect(dropAt(300, 650, size, one, 1)?.kind).toBe('below');
    expect(dropAt(300, 300, size, one, 1)?.kind).toBe('here');
    expect(dropAt(850, 300, size, one, 2)?.kind).toBe('here');
    expect(dropAt(50, 50, size, one, 1)).toBeNull();
  });
});

describe('framing the model (decision 446)', () => {
  it('leaves out what stands far from the model’s main body', async () => {
    const { coreBounds } = await import('../viewport/zoom');
    const house: [number, number, number, number][] = [[0, 0, 12, 0.3], [0, 7.7, 12, 8], [0, 0, 0.3, 8], [11.7, 0, 12, 8], [6.9, 0, 7.1, 8], [2, 1, 3, 2]];
    const r = coreBounds([...house, [2000, 1500, 2001, 1501]]);
    expect(r?.left).toBe(1);
    expect(r?.bounds).toEqual([0, 0, 12, 8]);
    // A site 300 m across keeps its far parcel.
    const site: [number, number, number, number][] = [[0, 0, 10, 10], [150, 0, 160, 10], [290, 200, 300, 210], [100, 100, 110, 110]];
    expect(coreBounds(site)?.left).toBe(0);
    expect(coreBounds([])).toBeNull();
  });
});
