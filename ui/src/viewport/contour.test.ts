import { describe, expect, it } from 'vitest';
import { edgeMiddles, insertVertex, moveVertex, offsetEdge, removeVertex } from './contour';

const square = { pts: [{ x: 0, y: 0 }, { x: 4, y: 0 }, { x: 4, y: 3 }, { x: 0, y: 3 }], edges: ['s', 'e', 'n', 'w'] };

describe('outline editing', () => {
  it('moves one vertex and leaves the edges alone', () => {
    const o = moveVertex(square, 2, { x: 5, y: 4 });
    expect(o.pts[2]).toEqual({ x: 5, y: 4 });
    expect(o.edges).toEqual(['s', 'e', 'n', 'w']);
  });

  it('inserts a vertex in the middle of an edge, both halves keeping the edge data', () => {
    const o = insertVertex(square, 1);
    expect(o.pts).toHaveLength(5);
    expect(o.pts[2]).toEqual({ x: 4, y: 1.5 });
    expect(o.edges).toEqual(['s', 'e', 'e', 'n', 'w']);
    // At a given point too.
    expect(insertVertex(square, 0, { x: 1, y: -1 }).pts[1]).toEqual({ x: 1, y: -1 });
  });

  it('removes a vertex: the edge that ended there now runs on and keeps its data; a triangle stays a triangle', () => {
    const o = removeVertex(square, 2)!;
    expect(o.pts).toEqual([{ x: 0, y: 0 }, { x: 4, y: 0 }, { x: 0, y: 3 }]);
    expect(o.edges).toEqual(['s', 'e', 'w']);
    expect(removeVertex(o, 0)).toBeNull();
  });

  it('moves an edge parallel to itself, its ends sliding along the neighbours', () => {
    // The east edge pushed 1 m out.
    const o = offsetEdge(square, 1, { x: 5, y: 1 })!;
    expect(o.pts[1]).toEqual({ x: 5, y: 0 });
    expect(o.pts[2]).toEqual({ x: 5, y: 3 });
    // A slanted neighbour: the end slides along it.
    const trap = { pts: [{ x: 0, y: 0 }, { x: 4, y: 0 }, { x: 3, y: 3 }, { x: 0, y: 3 }] };
    const t = offsetEdge(trap, 2, { x: 1, y: 2 })!;
    expect(t.pts[2].y).toBeCloseTo(2, 9);
    expect(t.pts[2].x).toBeCloseTo(4 - 2 / 3, 9);
    expect(t.pts[3]).toEqual({ x: 0, y: 2 });
    expect(edgeMiddles(square.pts)[0]).toEqual({ x: 2, y: 0 });
  });
});
