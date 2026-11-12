/** Snaps take only what the view shows (forum #31, decision 491). */

import { describe, expect, it } from 'vitest';
import type { DrawingDto } from '../drawing';
import { ALL_MODES, SnapIndex, findSnap } from './snaps';
import { snapShapes } from './aim';
import type { Shape } from './shapes';
import { drawnElements, shownItems } from './shown';

/** A plan item's points as the plan view turns them into snap lines. */
const linesOf = (items: readonly { nodeId: string; pts: number[] }[]): Shape[] =>
  items.flatMap((r) => [{ type: 'line' as const, a: { x: r.pts[0], y: r.pts[1] }, b: { x: r.pts[2], y: r.pts[3] } }]);

describe('the snaps of a plan', () => {
  // Two walls on the storey; the view's Visibility / Graphics hides B, so its drawing draws and marks only A.
  const items = [{ nodeId: 'A', pts: [0, 0, 5, 0] }, { nodeId: 'B', pts: [0, 3, 5, 3] }];
  const drawing: DrawingDto = {
    kind: 'plan', scale: 100,
    pens: [{ layer: 'CUT', stroke: '#000', width: 0.5, fill: null, segments: [0, 0, 5, 0] }],
    marks: [{ id: 'A', segments: [0, 0, 5, 0] }, { id: 'grid1#B2', segments: [] }, { id: 'room7:stamp', segments: [] }],
  };

  it('know what the drawing shows: every mark names its element', () => {
    expect([...drawnElements(drawing)!].sort()).toEqual(['A', 'grid1', 'grid1#B2', 'room7', 'room7:stamp']);
    expect(drawnElements(null)).toBeNull();
    expect(drawnElements({ pens: [], problem: 'no storey' })).toBeNull();
  });

  it('offer nothing of a hidden element', () => {
    const shown = shownItems(items, drawing);
    expect(shown.map((r) => r.nodeId)).toEqual(['A']);
    const { shapes } = snapShapes(drawing, linesOf(shown));
    const index = new SnapIndex(shapes);
    // B's end, where the cursor rests: nothing to snap to.
    expect(findSnap({ x: 5.02, y: 3.01 }, shapes, index, { tolerance: 0.1, modes: ALL_MODES })).toBeNull();
    // A's end snaps as before.
    expect(findSnap({ x: 5.02, y: 0.01 }, shapes, index, { tolerance: 0.1, modes: ALL_MODES })?.mode).toBe('endpoint');
  });

  it('take everything drawn while there is no drawing (modelling lines)', () => {
    expect(shownItems(items, null).map((r) => r.nodeId)).toEqual(['A', 'B']);
  });
});

describe('the snaps of a plan without the walls\' finishes (forum #30)', () => {
  // A wall of plaster 15, brick 250 (the core), plaster 15 along x: its cut pieces, each with its layer's role.
  const band = (y0: number, y1: number, sub: string) => ({ kind: 'cut', closed: true, points: [0, y0, 5, y0, 5, y1, 0, y1], sub });
  const wall = { nodeId: 'W', type: 'IfcWall', plan: [band(-0.14, -0.125, 'finishes'), band(-0.125, 0.125, 'core'), band(0.125, 0.14, 'finishes')] };
  const drawingOf = (hiddenFinishes: string[]): DrawingDto => ({ kind: 'plan', scale: 100, pens: [], marks: [{ id: 'W', segments: [] }], hiddenFinishes });
  const linesFrom = (d: DrawingDto): Shape[] => shownItems([wall], d).flatMap((r) => r.plan.flatMap((it) => {
    const ps = Array.from({ length: it.points.length / 2 }, (_, i) => ({ x: it.points[2 * i], y: it.points[2 * i + 1] }));
    return ps.map((a, i) => ({ type: 'line' as const, a, b: ps[(i + 1) % ps.length] }));
  }));

  it('take only the core\'s outline, the plaster\'s faces left out', () => {
    const d = drawingOf(['walls']);
    expect(shownItems([wall], d)[0].plan.map((it) => it.sub)).toEqual(['core']);
    const { shapes } = snapShapes(d, linesFrom(d));
    // The plaster's outer corner, where the cursor rests: the core's corner is what it meets.
    const hit = findSnap({ x: 5.005, y: 0.138 }, shapes, new SnapIndex(shapes), { tolerance: 0.05, modes: ALL_MODES });
    expect(hit?.mode).toBe('endpoint');
    expect(hit?.point.y).toBeCloseTo(0.125, 6);
  });

  it('keep every layer where the view draws the finishes, or leaves out another category\'s', () => {
    expect(shownItems([wall], drawingOf([]))[0].plan).toHaveLength(3);
    expect(shownItems([wall], drawingOf(['slabs']))[0].plan).toHaveLength(3);
  });
});
