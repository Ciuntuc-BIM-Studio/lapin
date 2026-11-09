import { describe, expect, it } from 'vitest';
import { holeIn, insertPoint, measure, movePoint, nearestEdge, removePoint, signedArea, starter, toMm } from './sectionEditor';

describe('the section editor', () => {
  it('starts from a hollow box: its outside anticlockwise, its chamber clockwise, the wall as thick as asked', () => {
    const rings = starter('box', { width: 50, depth: 100, wall: 3 });
    expect(rings).toHaveLength(2);
    expect(signedArea(rings[0])).toBeGreaterThan(0);
    expect(signedArea(rings[1])).toBeLessThan(0);
    expect(measure(rings)).toEqual({ width: 50, depth: 100, area: 5000 - 44 * 94 });
  });

  it('starts from a T, an angle and a channel with their walls', () => {
    expect(measure(starter('T', { width: 60, depth: 80, wall: 5 })).area).toBe(60 * 5 + 75 * 5);
    expect(measure(starter('L', { width: 60, depth: 80, wall: 5 })).area).toBe(60 * 5 + 75 * 5);
    expect(measure(starter('U', { width: 60, depth: 80, wall: 5 })).area).toBe(60 * 5 + 2 * 75 * 5);
    for (const k of ['T', 'L', 'U'] as const) expect(signedArea(starter(k, { width: 60, depth: 80, wall: 5 })[0])).toBeGreaterThan(0);
    // A wall too thick for the section is held to what fits.
    expect(measure(starter('box', { width: 20, depth: 20, wall: 50 })).area).toBeGreaterThan(0);
  });

  it('moves, adds and takes out points, a ring keeping three', () => {
    const r = starter('rectangle', { width: 50, depth: 100, wall: 3 });
    expect(movePoint(r, 0, 2, [51.4, 99.6])[0][2]).toEqual([51, 100]);
    expect(movePoint(r, 0, 2, [52, 98], 5)[0][2]).toEqual([50, 100]);
    const four = insertPoint(r, 0, 0, [25, 0]);
    expect(four[0]).toHaveLength(5);
    expect(four[0][1]).toEqual([25, 0]);
    expect(removePoint(four, 0, 1)[0]).toHaveLength(4);
    const tri = [[[0, 0], [10, 0], [0, 10]] as [number, number][]];
    expect(removePoint(tri, 0, 0)[0]).toHaveLength(3);
  });

  it('finds the edge a click is nearest, and the foot on it', () => {
    const e = nearestEdge(starter('rectangle', { width: 50, depth: 100, wall: 3 })[0], [30, -2]);
    expect(e.i).toBe(0);
    expect(e.foot).toEqual([30, 0]);
    expect(e.dist).toBe(2);
  });

  it('puts a hole in the middle when there is room, none when there is not, and reads the engine\'s metres as millimetres', () => {
    const r = starter('rectangle', { width: 50, depth: 100, wall: 3 });
    const h = holeIn(r, 30, 60)!;
    expect(signedArea(h)).toBeLessThan(0);
    expect(measure([...r, h]).area).toBe(5000 - 1800);
    expect(holeIn(r, 50, 60)).toBeNull();
    expect(toMm([[[-0.025, -0.05], [0.025, 0.05], [0, 0.0123456]]])).toEqual([[[-25, -50], [25, 50], [0, 12.346]]]);
  });
});
