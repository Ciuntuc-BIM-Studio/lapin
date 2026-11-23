import { describe, expect, it } from 'vitest';
import { DEFAULT_PREFS, gridSnapStep, readPrefs } from '../app/prefs';
import { ALL_MODES, edgePoints, findSnap, SnapIndex, type SnapMode } from './cad/snaps';
import type { Shape } from './cad/shapes';
import { livePoint, liveMeasure } from './liveInput';
import { measured } from '../modules/core/measure';

describe('the grid snap step shown is the step used', () => {
  it('follows the zoom in the adaptive grid, and is the one step in a fixed grid', () => {
    const adaptive = { gridMode: 'adaptive' as const, gridStep: 1, gridSnap: true };
    expect(gridSnapStep(60, adaptive)).toBe(0.05);
    expect(gridSnapStep(20, adaptive)).toBe(0.5); // zoomed out: the status line must say 500 mm, not 50
    expect(gridSnapStep(20, { gridMode: 'fixed', gridStep: 0.05, gridSnap: true })).toBe(0.05);
    expect(gridSnapStep(200, { gridMode: 'fixed', gridStep: 0.05, gridSnap: true })).toBe(0.05);
    expect(gridSnapStep(60, { ...adaptive, gridSnap: false })).toBe(0);
  });
});

describe('the new preferences', () => {
  it('keep the divisions, the distance and the crosshair, with safe defaults', () => {
    expect(DEFAULT_PREFS.crosshair).toBe(false);
    expect(DEFAULT_PREFS.snapDivisions).toEqual([3, 4]);
    expect(DEFAULT_PREFS.snapModes).not.toContain('division');
    expect(DEFAULT_PREFS.snapModes).not.toContain('distance');
    const p = readPrefs({ snapDivisions: [4, 3, 3, 1, 2.5, 'x', 100], snapDistance: -2, crosshair: true });
    expect(p.snapDivisions).toEqual([3, 4]);
    expect(p.snapDistance).toBe(0.5);
    expect(p.crosshair).toBe(true);
    // Settings kept before they existed: the new snaps stay off.
    expect(readPrefs({ snapModes: ['endpoint'], snapModesKnown: ['endpoint', 'midpoint'] }).snapModes).not.toContain('division');
  });
});

const line: Shape = { type: 'line', a: { x: 0, y: 0 }, b: { x: 6, y: 0 } };

describe('the edge points', () => {
  it('cuts an edge into thirds and quarters, leaving the ends and the middle to their own snaps', () => {
    const e = edgePoints(line, [3, 4], 0);
    const xs = e.division.map((p) => p.x).sort((a, b) => a - b);
    expect(xs).toEqual([1.5, 2, 4, 4.5]);
    expect(e.distance).toEqual([]);
  });

  it('puts a point a typed distance in from each end, none where the edge is too short', () => {
    expect(edgePoints(line, [], 0.5).distance.map((p) => p.x).sort((a, b) => a - b)).toEqual([0.5, 5.5]);
    expect(edgePoints(line, [], 7).distance).toEqual([]);
    expect(edgePoints({ type: 'polyline', pts: [{ x: 0, y: 0 }, { x: 2, y: 0 }, { x: 2, y: 3 }], closed: false }, [], 1).distance).toHaveLength(3); // the first edge's two meet in its middle
  });

  it('cuts an arc into equal angles', () => {
    const arc: Shape = { type: 'arc', c: { x: 0, y: 0 }, r: 2, start: 0, end: Math.PI };
    const d = edgePoints(arc, [3], 0).division;
    expect(d).toHaveLength(2);
    expect(Math.atan2(d[0].y, d[0].x)).toBeCloseTo(Math.PI / 3, 9);
  });

  it('is taken as a snap only when its mode is on', () => {
    const o = { tolerance: 0.1, divisions: [3], distance: 0.5 };
    const idx = new SnapIndex([line]);
    const modes = (...m: SnapMode[]) => new Set<SnapMode>(m);
    expect(findSnap({ x: 2.02, y: 0.03 }, [line], idx, { ...o, modes: modes('division') })?.mode).toBe('division');
    expect(findSnap({ x: 2.02, y: 0.03 }, [line], idx, { ...o, modes: modes('division') })?.point).toEqual({ x: 2, y: 0 });
    expect(findSnap({ x: 0.52, y: 0.03 }, [line], idx, { ...o, modes: modes('distance') })?.point).toEqual({ x: 0.5, y: 0 });
    expect(findSnap({ x: 2.02, y: 0.03 }, [line], idx, { ...o, modes: modes('endpoint', 'midpoint') })).toBeNull();
    // The middle belongs to the midpoint, which comes first when both are on.
    expect(findSnap({ x: 3.01, y: 0 }, [line], idx, { ...o, divisions: [2, 4], modes: modes('midpoint', 'division') })?.mode).toBe('midpoint');
    expect(ALL_MODES.has('division') && ALL_MODES.has('distance')).toBe(true);
  });

  it('offers a wall axis\' thirds as well', () => {
    const s = findSnap({ x: 4.02, y: 0.05 }, [], null, { tolerance: 0.1, modes: new Set<SnapMode>(['division']), wallAxes: [line], divisions: [3] });
    expect(s?.point).toEqual({ x: 4, y: 0 });
  });
});

describe('a point from a temporary reference point', () => {
  const datum = { x: 12, y: 3 };
  it('is the typed ΔX and ΔY from it, signed', () => {
    expect(livePoint({ base: datum, cursor: { x: 13, y: 3.5 }, len: '2000', ang: '1500', mode: 'rect' })).toEqual({ x: 14, y: 4.5 });
    expect(livePoint({ base: datum, cursor: { x: 13, y: 3.5 }, len: '-500', ang: '-250', mode: 'rect' })).toEqual({ x: 11.5, y: 2.75 });
    // ΔX only: the height follows the cursor
    expect(livePoint({ base: datum, cursor: { x: 13, y: 3.5 }, len: '1000', ang: '', mode: 'rect' })).toEqual({ x: 13, y: 3.5 });
    const m = liveMeasure({ base: datum, cursor: { x: 13, y: 3.5 }, len: '2000', ang: '1500', mode: 'rect' })!;
    expect(m.dx).toBe(2);
    expect(m.dy).toBe(1.5);
  });
});

describe('measuring in a section', () => {
  it('reads the drawing\'s x along the view and its y up, as the plan\'s x and y', () => {
    // A window 1.2 wide whose sill is 0.9 above the floor, 3.0 along the section: its top-left to bottom-right corner.
    const r = measured([{ x: 3, y: 2.1 }, { x: 4.2, y: 0.9 }]);
    expect(r.total).toBeCloseTo(Math.hypot(1.2, 1.2), 9);
    // Vertical: floor to ceiling
    expect(measured([{ x: 5, y: 0 }, { x: 5, y: 2.7 }]).total).toBeCloseTo(2.7, 9);
  });
});
