import { describe, expect, it } from 'vitest';
import { arrowLock, axesInPlane, holdLock, infer, segsOf } from './inference';

const square = segsOf([{ x: 0, y: 0 }, { x: 4, y: 0 }, { x: 4, y: 3 }, { x: 0, y: 3 }], true);
const level = axesInPlane({ x: 1, y: 0, z: 0 }, { x: 0, y: 1, z: 0 });

describe('the 3D sketch\'s inference (decision 374)', () => {
  it('finds the model\'s axes lying in a plane', () => {
    expect(level.map((a) => a.name)).toEqual(['red', 'green']);
    // A wall's face looking −y: x along it, z up.
    expect(axesInPlane({ x: 1, y: 0, z: 0 }, { x: 0, y: 0, z: 1 }).map((a) => a.name)).toEqual(['red', 'blue']);
    // A roof sloping 30° toward −y: only the eaves' way (red) lies in it.
    const c = Math.cos(Math.PI / 6), s = Math.sin(Math.PI / 6);
    expect(axesInPlane({ x: 1, y: 0, z: 0 }, { x: 0, y: c, z: s }).map((a) => a.name)).toEqual(['red']);
  });

  it('snaps to endpoints, then midpoints, then edges, else the face', () => {
    expect(infer({ raw: { x: 4.05, y: 2.96 }, base: null, edges: square, axes: level, tol: 0.1 })).toMatchObject({ kind: 'Endpoint', point: { x: 4, y: 3 } });
    expect(infer({ raw: { x: 2.03, y: 0.04 }, base: null, edges: square, axes: level, tol: 0.1 })).toMatchObject({ kind: 'Midpoint', point: { x: 2, y: 0 } });
    const e = infer({ raw: { x: 1, y: 0.05 }, base: null, edges: square, axes: level, tol: 0.1 });
    expect(e.kind).toBe('On edge');
    expect(e.point.y).toBeCloseTo(0, 9);
    expect(infer({ raw: { x: 1, y: 1 }, base: null, edges: square, axes: level, tol: 0.1 }).kind).toBe('On face');
  });

  it('infers the red and green axes from the last point, and an edge met along them', () => {
    const r = infer({ raw: { x: 2, y: 1.02 }, base: { x: 1, y: 1 }, edges: square, axes: level, tol: 0.1 });
    expect(r).toMatchObject({ kind: 'On red axis', color: 'red', point: { x: 2, y: 1 } });
    expect(r.guide?.color).toBe('red');
    const g = infer({ raw: { x: 1.02, y: 2.5 }, base: { x: 1, y: 1 }, edges: square, axes: level, tol: 0.1 });
    expect(g).toMatchObject({ kind: 'On green axis', point: { x: 1, y: 2.5 } });
    // Along red onto the right edge: where the axis meets it.
    const m = infer({ raw: { x: 3.97, y: 1.03 }, base: { x: 1, y: 1 }, edges: square, axes: level, tol: 0.1 });
    expect(m.point.x).toBeCloseTo(4, 9);
    expect(m.point.y).toBeCloseTo(1, 9);
  });

  it('infers parallel and perpendicular to a slanted edge in magenta', () => {
    const slant = segsOf([{ x: 0, y: 0 }, { x: 3, y: 3 }], false);
    const p = infer({ raw: { x: 6.02, y: 5 }, base: { x: 5, y: 4 }, edges: slant, axes: level, tol: 0.05 });
    expect(p).toMatchObject({ kind: 'Parallel to edge', color: 'magenta' });
    expect(p.point.x - 5).toBeCloseTo(p.point.y - 4, 9);
    const q = infer({ raw: { x: 6, y: 3.02 }, base: { x: 5, y: 4 }, edges: slant, axes: level, tol: 0.05 });
    expect(q.kind).toBe('Perpendicular to edge');
    expect(q.point.x - 5).toBeCloseTo(-(q.point.y - 4), 9);
  });

  it('holds a locked axis, still taking endpoints on its line', () => {
    const l = { axis: 'red' as const };
    const r = infer({ raw: { x: 2.5, y: 2 }, base: { x: 1, y: 1 }, edges: square, axes: level, lock: l, tol: 0.1 });
    expect(r).toMatchObject({ kind: 'On red axis', point: { x: 2.5, y: 1 } });
    // The square's corner (4, 0) is not on the line y = 1, (0, 1)… is not an endpoint either: nothing but the line.
    const e = infer({ raw: { x: 4.02, y: 0.05 }, base: { x: 1, y: 0 }, edges: square, axes: level, lock: l, tol: 0.1 });
    expect(e).toMatchObject({ kind: 'Endpoint', point: { x: 4, y: 0 } });
  });

  it('locks with the arrow keys and Shift as SketchUp does', () => {
    expect(arrowLock('ArrowRight', null, level, null)).toEqual({ axis: 'red' });
    expect(arrowLock('ArrowRight', { axis: 'red' }, level, null)).toBeNull();
    expect(arrowLock('ArrowLeft', null, level, null)).toEqual({ axis: 'green' });
    // Blue does not lie in a level plane: nothing locked.
    expect(arrowLock('ArrowUp', null, level, null)).toBeUndefined();
    const down = arrowLock('ArrowDown', null, level, { a: { x: 0, y: 0 }, b: { x: 3, y: 4 } });
    expect(down && 'dir' in down ? down.dir : null).toEqual({ x: 0.6, y: 0.8 });
    expect(holdLock({ point: { x: 0, y: 0 }, kind: 'On green axis', color: 'green', dir: { x: 0, y: 1 } })).toEqual({ axis: 'green' });
    expect(holdLock({ point: { x: 0, y: 0 }, kind: 'On face' })).toBeNull();
  });
});
