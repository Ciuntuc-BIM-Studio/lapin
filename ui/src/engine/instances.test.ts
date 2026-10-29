/** Instances of shared meshes placed in the world (docs/plan-instancing.md). */

import { describe, expect, it } from 'vitest';
import { elements, inWorld, place, placedBox } from './instances';
import type { RenderItem, SharedMesh } from './protocol';

// A quarter turn about Z, then 10 m along X: X ↦ Y, Y ↦ −X.
const turned = new Float32Array([0, 1, 0, -1, 0, 0, 0, 0, 1, 10, 0, 0]);
const cube: SharedMesh = {
  id: 'm',
  box: new Float32Array([-0.5, -0.5, 0, 0.5, 0.5, 1]),
  parts: [{ name: 'Body', color: '#c9ced6', positions: new Float32Array([1, 0, 0, 0, 1, 0, 0, 0, 1]), normals: new Float32Array([1, 0, 0, 1, 0, 0, 1, 0, 0]), indices: new Int32Array([0, 1, 2]), edges: new Float32Array([0, 0, 0, 2, 0, 0]) }],
};

describe('instances', () => {
  it('places a point by the 12 numbers', () => {
    expect(place(turned, 1, 0, 0)).toEqual([10, 1, 0]);
    expect(place(turned, 0, 1, 2)).toEqual([9, 0, 2]);
  });
  it('gives three.js the same placement, column by column', () => {
    expect(elements(turned)).toEqual([0, 1, 0, 0, -1, 0, 0, 0, 0, 0, 1, 0, 10, 0, 0, 1]);
  });
  it('a mesh box placed: its corners turned and moved', () => {
    expect(placedBox(new Float32Array([0, 0, 0, 2, 1, 1]), turned)).toEqual([9, 0, 0, 10, 2, 1]);
  });
  it('an instance in the world: its mesh placed, normals turned, edges moved; kept per item; any other item as it is', () => {
    const item = { nodeId: 'a', parts: [], instance: { mesh: 'm', matrix: turned } } as unknown as RenderItem;
    const w = inWorld(item, new Map([['m', cube]]));
    expect(Array.from(w.parts![0].positions)).toEqual([10, 1, 0, 9, 0, 0, 10, 0, 1]);
    expect(Array.from(w.parts![0].normals.slice(0, 3))).toEqual([0, 1, 0]);
    expect(Array.from(w.parts![0].edges)).toEqual([10, 0, 0, 10, 2, 0]);
    expect(w.instance).toBeNull();
    expect(inWorld(item, new Map([['m', cube]]))).toBe(w);
    const plain = { nodeId: 'b', parts: [] } as unknown as RenderItem;
    expect(inWorld(plain, new Map())).toBe(plain);
  });
});
