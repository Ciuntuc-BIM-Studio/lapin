import { describe, expect, it } from 'vitest';
import * as THREE from 'three';
import type { NodeDto } from '../engine/protocol';
import { disposeRebar, rebarGroup, rebarKey, REBAR_GROUP, removeRebar, type RebarBodies } from './rebar3d';

/** A tube of two triangles' worth: one quad on each of two colours, as the engine's payload comes (typed arrays over 64 numbers, plain ones under). */
const payload: RebarBodies = {
  groups: [
    { color: '#1d4ed8', positions: [0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0], indices: [0, 1, 2, 0, 2, 3] },
    { color: '#ca8a04', positions: new Float32Array([0, 0, 1, 1, 0, 1, 1, 1, 1]), indices: new Int32Array([0, 1, 2]) },
  ],
  triangles: 3,
  truncated: false,
};

const node = (id: string, type: string, props: Record<string, unknown>): NodeDto => ({ id, type, name: id, props } as unknown as NodeDto);

describe('the bars in 3D', () => {
  it('build one mesh per colour under the rebar-3d group, lit by normals, with nothing a click could land on', () => {
    const group = rebarGroup(payload);
    expect(group.name).toBe(REBAR_GROUP);
    expect(group.children).toHaveLength(2);
    const [blue, gold] = group.children as THREE.Mesh[];
    expect((blue.material as THREE.MeshStandardMaterial).color.getHexString()).toBe('1d4ed8');
    expect((gold.material as THREE.MeshStandardMaterial).color.getHexString()).toBe('ca8a04');
    expect(blue.geometry.getAttribute('position').count).toBe(4);
    expect(blue.geometry.getIndex()?.count).toBe(6);
    expect(gold.geometry.getAttribute('position').count).toBe(3);
    expect(gold.geometry.getIndex()?.array).toBeInstanceOf(Uint32Array);
    expect(blue.geometry.getAttribute('normal').count).toBe(4);
    expect(blue.userData.nodeId).toBeUndefined();
    // The pick asks every mesh; this one never answers.
    const hits: THREE.Intersection[] = [];
    blue.raycast(new THREE.Raycaster(new THREE.Vector3(0.5, 0.5, 5), new THREE.Vector3(0, 0, -1)), hits);
    expect(hits).toEqual([]);
  });

  it('leave out a group with fewer than three vertices', () => {
    const group = rebarGroup({ groups: [{ color: '#000000', positions: [0, 0, 0], indices: [] }], triangles: 0, truncated: false });
    expect(group.children).toHaveLength(0);
  });

  it('are taken out of the scene and freed, the next ones replacing them', () => {
    const scene = new THREE.Scene();
    const first = rebarGroup(payload);
    scene.add(first);
    const geometry = (first.children[0] as THREE.Mesh).geometry;
    let freed = false;
    geometry.addEventListener('dispose', () => { freed = true; });
    removeRebar(scene);
    expect(scene.getObjectByName(REBAR_GROUP)).toBeUndefined();
    expect(freed).toBe(true);
    const second = rebarGroup(payload);
    scene.add(second);
    expect(scene.getObjectByName(REBAR_GROUP)).toBe(second);
    disposeRebar(second);
  });

  it('are asked again when a beam, a column, a bar or a type changes, not when a wall does', () => {
    const nodes = new Map<string, NodeDto>([
      ['b1', node('b1', 'IfcBeam', { x1: 0, x2: 6 })],
      ['t1', node('t1', 'IfcBeamType', { stirrups: { enabled: true, step: 0.15 } })],
      ['r1', node('r1', 'LapinRebar', { at: [1, 2] })],
      ['w1', node('w1', 'IfcWall', { x1: 0 })],
    ]);
    const key = rebarKey(nodes);
    const moved = new Map(nodes);
    moved.set('w1', node('w1', 'IfcWall', { x1: 3 }));
    expect(rebarKey(moved)).toBe(key);
    moved.set('t1', node('t1', 'IfcBeamType', { stirrups: { enabled: true, step: 0.1 } }));
    expect(rebarKey(moved)).not.toBe(key);
    const gone = new Map(nodes);
    gone.delete('r1');
    expect(rebarKey(gone)).not.toBe(key);
    const column = new Map(nodes);
    column.set('c1', node('c1', 'IfcColumn', { x: 2, y: 3 }));
    expect(rebarKey(column)).not.toBe(key);
  });
});
