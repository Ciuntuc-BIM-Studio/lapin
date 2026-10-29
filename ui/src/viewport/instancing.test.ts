/** The 3D view's instances (docs/plan-instancing.md): batches by shared mesh, kept incrementally, styled per instance. */

import { describe, expect, it } from 'vitest';
import * as THREE from 'three';
import { InstanceLayer } from './instancing';
import type { RenderItem, SharedMesh } from '../engine/protocol';

const mesh = (id: string, parts = 1): SharedMesh => ({
  id,
  box: new Float32Array([-0.5, -0.5, 0, 0.5, 0.5, 1]),
  parts: Array.from({ length: parts }, (_, k) => ({
    name: 'Body', color: k ? '#ff0000' : '#c9ced6',
    positions: new Float32Array([-0.5, -0.5, 0, 0.5, -0.5, 0, 0, 0.5, 1]), normals: new Float32Array(9), indices: new Int32Array([0, 1, 2]),
    edges: new Float32Array([-0.5, -0.5, 0, 0.5, -0.5, 0]), hiddenBy: k ? ['core' as const] : null,
  })),
});
const at = (x: number, y = 0) => new Float32Array([1, 0, 0, 0, 1, 0, 0, 0, 1, x, y, 0]);
const inst = (id: string, m: string, x: number): RenderItem => ({ nodeId: id, type: 'IfcBuildingElementProxy', parts: [], instance: { mesh: m, matrix: at(x) } } as unknown as RenderItem);
const render = (...items: RenderItem[]) => new Map(items.map((i) => [i.nodeId, i]));
const edges = new THREE.LineBasicMaterial();

/** The instance's matrix as drawn by the batch's first part (solid or faded), as x and the scale's sign. */
function drawn(layer: InstanceLayer, meshId: string, id: string, which: 'solid' | 'faded' = 'solid') {
  const b = layer.batches.get(meshId)!;
  const m = new THREE.Matrix4();
  b.parts[0][which].getMatrixAt(b.index.get(id)!, m);
  return { x: m.elements[12], shown: m.elements[0] !== 0 };
}

describe('InstanceLayer', () => {
  it('one batch per shared mesh, an instance per element, placed and pickable by its id', () => {
    const layer = new InstanceLayer(edges);
    const meshes = new Map([['m', mesh('m')], ['n', mesh('n')]]);
    const r = render(inst('a', 'm', 0), inst('b', 'm', 2), inst('c', 'n', 4), { nodeId: 'w', parts: [] } as unknown as RenderItem);
    expect(layer.sync(r, meshes)).toBe(true);
    layer.restyle({}, [], 'shaded+edges', 'all');
    expect([...layer.batches.keys()].sort()).toEqual(['m', 'n']);
    expect(layer.count()).toBe(3);
    expect(layer.batches.get('m')!.parts[0].solid.count).toBe(2);
    expect(drawn(layer, 'm', 'b')).toEqual({ x: 2, shown: true });
    const b = layer.batches.get('m')!;
    expect(layer.nodeOf({ object: b.parts[0].solid, instanceId: b.index.get('b') } as unknown as THREE.Intersection)).toBe('b');
    expect(layer.nodeOf({ object: new THREE.Mesh(), instanceId: 0 } as unknown as THREE.Intersection)).toBeUndefined();
    // Nothing changed: nothing to do.
    expect(layer.sync(r, meshes)).toBe(false);
  });

  it('grows past its first capacity, keeping what it holds; an instance taken out leaves the last one in its place', () => {
    const layer = new InstanceLayer(edges);
    const meshes = new Map([['m', mesh('m')]]);
    const many = Array.from({ length: 200 }, (_, i) => inst(`e${i}`, 'm', i));
    layer.sync(render(...many), meshes);
    layer.restyle({}, [], 'shaded', 'all');
    const b = layer.batches.get('m')!;
    expect(b.capacity).toBeGreaterThanOrEqual(200);
    expect(drawn(layer, 'm', 'e7').x).toBe(7);
    expect(drawn(layer, 'm', 'e199').x).toBe(199);
    layer.sync(render(...many.filter((i) => i.nodeId !== 'e7')), meshes);
    layer.restyle({}, [], 'shaded', 'all');
    expect(b.ids.length).toBe(199);
    expect(b.index.has('e7')).toBe(false);
    expect(drawn(layer, 'm', 'e199').x).toBe(199);
    expect(b.ids[b.index.get('e199')!]).toBe('e199');
  });

  it('a moved instance is placed again; one now drawn on its own leaves the batch; the last one gone takes the batch', () => {
    const layer = new InstanceLayer(edges);
    const meshes = new Map([['m', mesh('m')]]);
    layer.sync(render(inst('a', 'm', 0), inst('b', 'm', 2)), meshes);
    layer.restyle({}, [], 'shaded', 'all');
    layer.sync(render(inst('a', 'm', 5), inst('b', 'm', 2)), meshes);
    layer.restyle({}, [], 'shaded', 'all');
    expect(drawn(layer, 'm', 'a').x).toBe(5);
    layer.sync(render(inst('a', 'm', 5), { nodeId: 'b', parts: [] } as unknown as RenderItem), meshes);
    expect(layer.count()).toBe(1);
    layer.sync(render(), meshes);
    expect(layer.batches.size).toBe(0);
  });

  it('looks: hidden instances drawn as nothing, faded ones by the see-through mesh, a tint and the selection as colours', () => {
    const layer = new InstanceLayer(edges);
    const meshes = new Map([['m', mesh('m')]]);
    layer.sync(render(inst('a', 'm', 0), inst('b', 'm', 2), inst('c', 'm', 4)), meshes);
    layer.restyle({ a: { hidden: true }, b: { opacity: 0.18 }, c: { color: '#00ff00' } }, ['c'], 'shaded', 'all');
    expect(drawn(layer, 'm', 'a').shown).toBe(false);
    expect(drawn(layer, 'm', 'a', 'faded').shown).toBe(false);
    expect(drawn(layer, 'm', 'b').shown).toBe(false);
    expect(drawn(layer, 'm', 'b', 'faded')).toEqual({ x: 2, shown: true });
    const p = layer.batches.get('m')!.parts[0];
    expect((p.faded.material as THREE.MeshStandardMaterial).opacity).toBeCloseTo(0.18);
    expect(p.solid.castShadow).toBe(true);
    expect(p.faded.castShadow).toBe(false);
    const c = new THREE.Color();
    p.solid.getColorAt(layer.batches.get('m')!.index.get('c')!, c);
    // Green, lightened towards the selection's colour.
    expect(c.g).toBeGreaterThan(c.b);
    expect(c.r).toBeGreaterThan(0.2);
    // Back to plain: drawn solid again.
    layer.restyle({}, [], 'shaded', 'all');
    expect(drawn(layer, 'm', 'b')).toEqual({ x: 2, shown: true });
  });

  it('the visual style and the layers mode show or hide whole parts', () => {
    const layer = new InstanceLayer(edges);
    layer.sync(render(inst('a', 'm', 0)), new Map([['m', mesh('m', 2)]]));
    layer.restyle({}, [], 'wireframe', 'core');
    const [body, finish] = layer.batches.get('m')!.parts;
    expect(body.solid.visible).toBe(false);
    expect(body.edges!.visible).toBe(true);
    expect(finish.edges!.visible).toBe(false);
    layer.restyle({}, [], 'shaded', 'all');
    expect(body.solid.visible).toBe(true);
    expect(body.edges!.visible).toBe(false);
    expect(finish.solid.visible).toBe(true);
  });

  it('bounds of what is shown; a mesh sent again rebuilds its batch, keeping the instances', () => {
    const layer = new InstanceLayer(edges);
    const r = render(inst('a', 'm', 0), inst('b', 'm', 10));
    layer.sync(r, new Map([['m', mesh('m')]]));
    const box = layer.expand(new THREE.Box3(), { b: { hidden: true } });
    expect(box.min.toArray()).toEqual([-0.5, -0.5, 0]);
    expect(box.max.toArray()).toEqual([0.5, 0.5, 1]);
    expect(layer.expand(new THREE.Box3(), {}).max.x).toBe(10.5);
    layer.sync(r, new Map([['m', mesh('m', 2)]]));
    layer.restyle({}, [], 'shaded', 'all');
    expect(layer.batches.get('m')!.parts.length).toBe(2);
    expect(drawn(layer, 'm', 'b').x).toBe(10);
  });
});
