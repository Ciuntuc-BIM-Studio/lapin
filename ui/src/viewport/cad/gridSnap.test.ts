/** Forum topic 151 / issue 109: grid axes snap (ends, crossings, along, perpendicular) and the "Grid only" mode takes nothing else. */

import { describe, expect, it } from 'vitest';
import type { Vec2 } from '../../platform/module';
import { ALL_MODES, findSnap, gridCrossings, isLineSnap, modesFor, snapsOn, SNAP_CODES, type SnapMode } from './snaps';
import { gridAxisShapes } from './elementSnaps';
import type { Shape } from './shapes';

const P = (x: number, y: number): Vec2 => ({ x, y });
// Axes 1, 2 (vertical, x = 0 and 6) and A, B (horizontal, y = 0 and 4), 10 m by 8 m.
const axes = gridAxisShapes([
  { tag: '1', a: [0, -1], b: [0, 5] }, { tag: '2', a: [6, -1], b: [6, 5] },
  { tag: 'A', a: [-1, 0], b: [7, 0] }, { tag: 'B', a: [-1, 4], b: [7, 4] },
]);
// A drawn line crossing the grid, and a point on it, that "Grid only" must ignore.
const drawn: Shape[] = [{ type: 'line', a: P(2, 2), b: P(3, 2) }, { type: 'line', a: P(3.5, 1), b: P(3.5, 3) }];
const only = modesFor(null, [...ALL_MODES], true);

describe('grid axes', () => {
  it('reads an IfcGrid\'s axes as lines, skipping empty ones', () => {
    expect(axes).toHaveLength(4);
    expect(gridAxisShapes([{ tag: 'X', a: [1, 1], b: [1, 1] }, { tag: 'Y' }, 'junk'])).toEqual([]);
  });

  it('finds the crossing of two axes, with the cursor near it', () => {
    const s = findSnap(P(6.05, 3.95), [], null, { tolerance: 0.2, modes: new Set<SnapMode>(['gridAxis']), gridAxes: axes });
    expect(s).toMatchObject({ mode: 'gridAxis', point: P(6, 4) });
    expect(s?.along).toBeUndefined();
  });

  it('crossings of axes that stop a little short still count; far apart ones do not', () => {
    const short: Shape[] = [{ type: 'line', a: P(0, 0), b: P(5, 0) }, { type: 'line', a: P(5.3, -2), b: P(5.3, 2) }];
    const [q] = gridCrossings(short, P(5.3, 0), 0.2);
    expect(q.x).toBeCloseTo(5.3);
    expect(q.y).toBeCloseTo(0);
    const far: Shape[] = [{ type: 'line', a: P(0, 0), b: P(5, 0) }, { type: 'line', a: P(8, -2), b: P(8, 2) }];
    expect(gridCrossings(far, P(8, 0), 0.2)).toEqual([]);
    expect(gridCrossings([{ type: 'line', a: P(0, 0), b: P(5, 0) }, { type: 'line', a: P(0, 1), b: P(5, 1) }], P(2, 0), 5)).toEqual([]);
  });

  it('snaps to an axis\' end, ahead of the crossing\'s neighbours, and along an axis elsewhere', () => {
    const end = findSnap(P(0.05, 5.05), [], null, { tolerance: 0.2, modes: only, gridAxes: axes });
    expect(end).toMatchObject({ mode: 'gridAxis', point: P(0, 5) });
    const along = findSnap(P(6.1, 2), [], null, { tolerance: 0.2, modes: only, gridAxes: axes });
    expect(along?.point.x).toBeCloseTo(6);
    expect(along?.point.y).toBeCloseTo(2);
    expect(along && isLineSnap(along)).toBe(true);
  });

  it('offers the foot of the perpendicular from the previous point', () => {
    const s = findSnap(P(6.05, 2.5), [], null, { tolerance: 0.3, modes: only, gridAxes: axes, from: P(10, 2.5) });
    expect(s?.point).toEqual(P(6, 2.5));
    expect(s && isLineSnap(s)).toBe(false);
  });
});

describe('Grid only', () => {
  it('leaves the grid axis as the one snap, whatever the SNAP menu has on', () => {
    expect([...only]).toEqual(['gridAxis']);
    expect([...modesFor(null, ['endpoint'], false)]).toEqual(['endpoint']);
  });

  it('ignores drawn lines\' ends, middles and nearest points, even right on them', () => {
    const shapes = drawn;
    expect(findSnap(P(3, 2), shapes, null, { tolerance: 0.2, modes: only, gridAxes: axes })).toBeNull();
    expect(findSnap(P(2.5, 2), shapes, null, { tolerance: 0.2, modes: only, gridAxes: axes })).toBeNull();
    // With the ordinary snaps the same cursor does snap to the line.
    expect(findSnap(P(3, 2), shapes, null, { tolerance: 0.2, modes: new Set<SnapMode>(['endpoint']), gridAxes: axes })?.mode).toBe('endpoint');
  });

  it('takes the grid\'s crossing where a drawn line is also under the cursor', () => {
    const shapes: Shape[] = [{ type: 'line', a: P(5, 4), b: P(6, 4.02) }];
    const s = findSnap(P(6, 4), shapes, null, { tolerance: 0.2, modes: only, gridAxes: axes });
    expect(s).toMatchObject({ mode: 'gridAxis', point: P(6, 4) });
  });

  it('is off for the Grid axis snap alone when the menu leaves it out, and the typed code arms it for one point', () => {
    const without = ALL_MODES.size ? [...ALL_MODES].filter((m) => m !== 'gridAxis') : [];
    expect(findSnap(P(6, 4), [], null, { tolerance: 0.2, modes: modesFor(null, without), gridAxes: axes })).toBeNull();
    expect(SNAP_CODES.GRID).toBe('gridAxis');
    expect([...modesFor('gridAxis', without)]).toEqual(['gridAxis']);
  });

  it('snaps even with SNAP off, but a "no snap" override still wins', () => {
    expect(snapsOn(null, false, true)).toBe(true);
    expect(snapsOn(null, false, false)).toBe(false);
    expect(snapsOn('none', true, true)).toBe(false);
    expect(modesFor('none', [], true).size).toBe(0);
    expect([...modesFor('midpoint', [], true)]).toEqual(['midpoint']);
  });
});
