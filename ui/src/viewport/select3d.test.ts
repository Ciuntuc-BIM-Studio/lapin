import { describe, expect, it } from 'vitest';
import { boxEdges, boxOf, projectSegments, takes } from './select3d';
import { boxTest } from './selectBox';

// A camera looking down −z from z = 10, 100 px a metre, the origin at (500, 500); behind the eye past z = 10.
const project = (x: number, y: number, z: number) => (z >= 10 ? null : { x: 500 + 100 * x, y: 500 - 100 * y });

describe('selecting by a region in 3D', () => {
  it('projects edges, and leaves out those behind the eye', () => {
    const seen = projectSegments([0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 12], project);
    expect(seen.segs).toEqual([[{ x: 500, y: 500 }, { x: 600, y: 500 }]]);
    expect(seen.behind).toBe(true);
  });

  it('takes by a window wholly inside, by crossing what it touches; never wholly what lies behind the eye', () => {
    const box = boxEdges([0, 0, 0, 1, 1, 1]);
    expect(box.length).toBe(12 * 6);
    const seen = projectSegments(box, project);
    // A window round the unit box (500..600 across, 400..500 down) takes it; one round half of it does not.
    expect(takes(boxTest({ x: 490, y: 390 }, { x: 610, y: 510 }, true), true, seen)).toBe(true);
    expect(takes(boxTest({ x: 490, y: 390 }, { x: 550, y: 510 }, true), true, seen)).toBe(false);
    // Crossing half of it does.
    expect(takes(boxTest({ x: 550, y: 510 }, { x: 490, y: 390 }, false), false, seen)).toBe(true);
    // Partly behind the eye: a window refuses it, crossing still takes what is seen.
    const tall = projectSegments(boxEdges([0, 0, 0, 1, 1, 20]), project);
    expect(takes(boxTest({ x: 0, y: 0 }, { x: 1000, y: 1000 }, true), true, tall)).toBe(false);
    expect(takes(boxTest({ x: 1000, y: 1000 }, { x: 0, y: 0 }, false), false, tall)).toBe(true);
  });

  it('places an instance box and boxes points', () => {
    const moved = boxEdges([0, 0, 0, 1, 1, 1], (x, y, z) => [x + 5, y, z]);
    expect(Math.min(...moved.filter((_, i) => i % 3 === 0))).toBe(5);
    expect(boxOf([1, 2, 3, -1, 5, 0])).toEqual([-1, 2, 0, 1, 5, 3]);
    expect(boxOf([])).toBeNull();
  });
});
