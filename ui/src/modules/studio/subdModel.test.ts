import { describe, expect, it } from 'vitest';
import {
  anchorOf, dragBy, edgeClasses, edgeKey, edgeOf, middleOf, moveArgs, none, opArgs, opProblem, pick, pickAt, pointsOf, previewPoints, selectionArgs,
  sharpnessOf, symmetryArgs, symmetryAxis, thicknessArgs, transformArgs, withMode, type Cage, type Lens,
} from './subdModel';

/** A unit square cage lying on the ground: points 0..3, one face, four edges. */
const square: Cage = {
  id: 's', points: [[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0]], faces: [[0, 1, 2, 3]], edges: [[0, 1], [1, 2], [2, 3], [3, 0]],
  creases: [[0, 1, 10], [1, 2, 0]], borders: [[0, 1], [1, 2], [2, 3], [3, 0]], closed: false, symmetry: null, partners: null, level: 3, deviation: 0.0004,
  levels: null, thickness: 0, side: 'out', problem: null,
};

/** A camera looking straight down: a point's place is its x and y times 100 px, its depth the height below the camera. */
const down: Lens = { project: (p) => ({ x: p[0]! * 100, y: p[1]! * 100 }), depth: (p) => 10 - p[2]! };

describe('the pick', () => {
  it('a click picks one thing, Shift adds and takes off, another kind starts again', () => {
    let p = pick(none('vertex'), 'vertex', 2, false);
    expect(p).toEqual({ mode: 'vertex', keys: ['2'] });
    p = pick(p, 'vertex', 3, true);
    expect(p.keys).toEqual(['2', '3']);
    p = pick(p, 'vertex', 2, true);
    expect(p.keys).toEqual(['3']);
    expect(pick(p, 'vertex', 1, false).keys).toEqual(['1']);
    expect(pick(p, 'vertex', null, false).keys).toEqual([]);
    expect(pick(p, 'vertex', null, true).keys).toEqual(['3']);
    expect(pick(p, 'face', 0, true)).toEqual({ mode: 'face', keys: ['0'] });
    expect(withMode(p, 'edge')).toEqual({ mode: 'edge', keys: [] });
    expect(withMode(p, 'vertex')).toBe(p);
  });

  it('an edge is the same whichever way it is named', () => {
    expect(edgeKey(3, 1)).toBe('1-3');
    expect(edgeOf('1-3')).toEqual([1, 3]);
    const p = pick(pick(none('edge'), 'edge', [2, 1], false), 'edge', [1, 2], true);
    expect(p.keys).toEqual([]);
  });

  it('names the points of a vertex, an edge or a face pick, each once', () => {
    expect(pointsOf(square, { mode: 'vertex', keys: ['3', '1'] })).toEqual([1, 3]);
    expect(pointsOf(square, { mode: 'edge', keys: ['0-1', '1-2'] })).toEqual([0, 1, 2]);
    expect(pointsOf(square, { mode: 'face', keys: ['0'] })).toEqual([0, 1, 2, 3]);
    expect(middleOf(square, { mode: 'face', keys: ['0'] })).toEqual({ x: 0.5, y: 0.5, z: 0 });
    expect(middleOf(square, none('vertex'))).toBeNull();
  });
});

describe('what is under the cursor', () => {
  it('a vertex within the reach, the nearer one', () => {
    expect(pickAt(square, 'vertex', down, { x: 103, y: 4 })).toBe(1);
    expect(pickAt(square, 'vertex', down, { x: 50, y: 50 })).toBeNull();
  });

  it('an edge within the reach of its segment', () => {
    expect(pickAt(square, 'edge', down, { x: 50, y: 4 })).toEqual([0, 1]);
    expect(pickAt(square, 'edge', down, { x: 104, y: 60 })).toEqual([1, 2]);
    expect(pickAt(square, 'edge', down, { x: 50, y: 50 })).toBeNull();
  });

  it('a face the cursor is inside of, the nearer of two on top of each other', () => {
    const two: Cage = { ...square, points: [...square.points, [0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1]], faces: [[0, 1, 2, 3], [4, 5, 6, 7]] };
    expect(pickAt(two, 'face', down, { x: 50, y: 50 })).toBe(1);
    expect(pickAt(two, 'face', down, { x: 150, y: 50 })).toBeNull();
  });

  it('nothing behind the camera', () => {
    const away: Lens = { project: () => null, depth: () => 1 };
    expect(pickAt(square, 'vertex', away, { x: 0, y: 0 })).toBeNull();
    expect(pickAt(square, 'face', away, { x: 0, y: 0 })).toBeNull();
  });

  it('where a thing is taken from', () => {
    expect(anchorOf(square, 'vertex', 2)).toEqual({ x: 1, y: 1, z: 0 });
    expect(anchorOf(square, 'edge', [0, 1])).toEqual({ x: 0.5, y: 0, z: 0 });
    expect(anchorOf(square, 'face', 0)).toEqual({ x: 0.5, y: 0.5, z: 0 });
    expect(anchorOf(square, 'face', 9)).toBeNull();
  });
});

describe('a drag is ONE subdEdit', () => {
  it('becomes the translate of the picked points', () => {
    const by = dragBy({ x: 1, y: 1, z: 0 }, { x: 1.25, y: 1, z: 0.5 });
    expect(by).toEqual({ x: 0.25, y: 0, z: 0.5 });
    expect(moveArgs('s', { mode: 'vertex', keys: ['2'] }, by)).toEqual({ id: 's', vertices: [2], translate: [0.25, 0, 0.5] });
    expect(moveArgs('s', { mode: 'edge', keys: ['0-1'] }, by)).toEqual({ id: 's', edges: [[0, 1]], translate: [0.25, 0, 0.5] });
    expect(moveArgs('s', { mode: 'face', keys: ['0'] }, by)).toEqual({ id: 's', faces: [0], translate: [0.25, 0, 0.5] });
  });

  it('is nothing when it moves nothing or nothing is picked', () => {
    expect(moveArgs('s', { mode: 'vertex', keys: ['2'] }, { x: 0, y: 0, z: 0.00001 })).toBeNull();
    expect(moveArgs('s', none('vertex'), { x: 1, y: 0, z: 0 })).toBeNull();
    expect(selectionArgs({ mode: 'vertex', keys: [] })).toEqual({ vertices: [] });
  });

  it('shows the picked points moved, and the partners of a symmetric cage by the mirrored move', () => {
    const moved = previewPoints(square, { mode: 'vertex', keys: ['2'] }, { x: 0, y: 0, z: 1 });
    expect(moved[2]).toEqual([1, 1, 1]);
    expect(moved[1]).toEqual([1, 0, 0]);
    expect(square.points[2]).toEqual([1, 1, 0]);
    const sym: Cage = { ...square, symmetry: { point: [0.5, 0, 0], normal: [1, 0, 0] }, partners: [1, 0, 3, 2] };
    const m = previewPoints(sym, { mode: 'vertex', keys: ['2'] }, { x: 0.25, y: 0, z: 1 });
    expect(m[2]).toEqual([1.25, 1, 1]);
    expect(m[3]).toEqual([-0.25, 1, 1]);
  });

  it('a typed scale and turn go with the picked points, what changes nothing is dropped', () => {
    const p = { mode: 'face' as const, keys: ['0'] };
    expect(transformArgs('s', p, { scale: 2 })).toEqual({ id: 's', faces: [0], scale: 2 });
    expect(transformArgs('s', p, { rotate: 45, scale: 1 })).toEqual({ id: 's', faces: [0], rotate: 45 });
    expect(transformArgs('s', p, { scale: 1, rotate: 0 })).toBeNull();
    expect(transformArgs('s', none('face'), { scale: 2 })).toBeNull();
  });
});

describe('the ops', () => {
  const faces = { mode: 'face' as const, keys: ['0', '2'] };
  const edge = { mode: 'edge' as const, keys: ['1-2'] };

  it('extrude and delete want faces', () => {
    expect(opArgs('s', faces, { op: 'extrude', distance: 0.3 })).toEqual({ id: 's', op: 'extrude', faces: [0, 2], distance: 0.3 });
    expect(opArgs('s', faces, { op: 'delete' })).toEqual({ id: 's', op: 'delete', faces: [0, 2] });
    expect(opArgs('s', edge, { op: 'extrude', distance: 1 })).toBeNull();
    expect(opProblem(edge, 'delete')).toMatch(/faces/);
    expect(opArgs('s', faces, { op: 'extrude', distance: 0 })).toBeNull();
  });

  it('a loop wants exactly one edge', () => {
    expect(opArgs('s', edge, { op: 'loop' })).toEqual({ id: 's', op: 'loop', edge: [1, 2], fraction: 0.5 });
    expect(opArgs('s', { mode: 'edge', keys: ['0-1', '1-2'] }, { op: 'loop' })).toBeNull();
    expect(opProblem(faces, 'loop')).toMatch(/one edge/);
  });

  it('a crease takes edges and a sharpness: 0 smooth, absent sharp', () => {
    expect(opArgs('s', edge, { op: 'crease', sharpness: 2 })).toEqual({ id: 's', op: 'crease', edges: [[1, 2]], sharpness: 2 });
    expect(opArgs('s', edge, { op: 'crease', sharpness: 0 })).toEqual({ id: 's', op: 'crease', edges: [[1, 2]], sharpness: 0 });
    expect(opArgs('s', edge, { op: 'crease', sharpness: null })).toEqual({ id: 's', op: 'crease', edges: [[1, 2]] });
    expect(opArgs('s', faces, { op: 'crease', sharpness: 1 })).toBeNull();
  });
});

describe('the cage as it is drawn and told', () => {
  it('sorts the edges into creases, borders and plain ones; a crease of 0 is none', () => {
    const open: Cage = { ...square, borders: [[2, 3], [3, 0]] };
    const c = edgeClasses(open);
    expect(c.crease).toEqual([[0, 1, 10]]);
    expect(c.border).toEqual([[2, 3], [3, 0]]);
    expect(c.plain).toEqual([[1, 2]]);
    expect(sharpnessOf(square, 1, 0)).toBe(10);
    expect(sharpnessOf(square, 2, 3)).toBe(0);
  });

});

describe('the settings', () => {
  it('names the symmetry plane by its normal', () => {
    expect(symmetryAxis(square)).toBe('none');
    expect(symmetryAxis({ symmetry: { point: [0, 0, 0], normal: [-1, 0, 0] } })).toBe('x');
    expect(symmetryAxis({ symmetry: { point: [0, 0, 0], normal: [0, 1, 0] } })).toBe('y');
    expect(symmetryAxis({ symmetry: { point: [0, 0, 0], normal: [0, 0, 1] } })).toBe('z');
    expect(symmetryAxis({ symmetry: { point: [0, 0, 0], normal: [0.7071, 0.7071, 0] } })).toBe('plane');
  });

  it('makes the arguments of subdSet', () => {
    expect(thicknessArgs(0.2)).toEqual({ thickness: 0.2 });
    expect(thicknessArgs(0)).toEqual({ thickness: null });
    expect(symmetryArgs('x')).toEqual({ symmetry: 'x' });
    expect(symmetryArgs('none')).toEqual({ symmetry: null });
  });
});
