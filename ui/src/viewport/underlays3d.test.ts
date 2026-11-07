import { describe, expect, it } from 'vitest';
import * as THREE from 'three';
import type { NodeDto } from '../engine/protocol';
import { bytesOf, floatsOf, isUnderlayNode, LIFT, sameSources, themed, underlayGroup, underlaySegmentsInPlane, underlaySources, type UnderlayBatch } from './underlays3d';
import { sceneSegments } from './sketch/edges';
import { levelFrame } from './workPlane';

const node = (id: string, type: string, props: NodeDto['props'] = {}): NodeDto =>
  ({ id, type, name: id, parentId: null, px: 0, py: 0, props, editable: true, module: null, storeyId: null });

const b64 = (bytes: Uint8Array) => btoa(String.fromCharCode(...bytes));
const f32 = (v: number[]) => b64(new Uint8Array(new Float32Array(v).buffer));

function batch(lines: number[], colors: number[], fills: number[] = [], fillColors: number[] = []): UnderlayBatch {
  return {
    key: 'k', viewId: 'v', storeyId: 's', source: 'i', layer: 'WALLS', normal: [0, 0, 1],
    segments: lines.length / 6, triangles: fills.length / 9,
    lines: f32(lines), lineColors: b64(new Uint8Array(colors)), fills: f32(fills), fillColors: b64(new Uint8Array(fillColors)),
  };
}

describe('an imported DXF in 3D (decision 382)', () => {
  it('knows a DXF drawing from hand drafting and from 3D lines, and asks again only when its sources change', () => {
    const line = node('a', 'IfcAnnotation', { predefinedType: 'DRAFT', dxf: true, shape: { type: 'line' } });
    const hatch = node('h', 'IfcAnnotation', { predefinedType: 'DRAFT', dxfImport: 'imp', hatch: {} });
    const own = node('o', 'IfcAnnotation', { predefinedType: 'DRAFT', shape: { type: 'line' } });
    const lifted = node('z', 'IfcAnnotation', { predefinedType: 'DRAFT', dxf: true, heights: [0, 1] });
    expect([line, hatch, own, lifted].map(isUnderlayNode)).toEqual([true, true, false, false]);

    const wall = node('w', 'IfcWall');
    const project = node('p', 'IfcProject');
    const nodes = new Map([line, own, wall, project].map((n) => [n.id, n]));
    const first = underlaySources(nodes);
    expect(first.any).toBe(true);
    expect(first.refs).toEqual([line, project]);
    // A wall moved: the same sources, nothing asked.
    const moved = new Map(nodes); moved.set('w', node('w', 'IfcWall', { x: 1 }));
    expect(sameSources(underlaySources(moved).refs, first.refs)).toBe(true);
    // The project changed (a layer off, an import's switch): asked again.
    const changed = new Map(nodes); changed.set('p', node('p', 'IfcProject', { draftLayers: [] }));
    expect(sameSources(underlaySources(changed).refs, first.refs)).toBe(false);
    expect(underlaySources(new Map([[own.id, own]])).any).toBe(false);
  });

  it('reads the engine\'s buffers and shows a CAD drawing\'s white dark on paper and its black light on graphite', () => {
    expect([...floatsOf(f32([1.5, -2, 1e5]))]).toEqual([1.5, -2, 1e5]);
    expect([...bytesOf(b64(new Uint8Array([0, 128, 255])))]).toEqual([0, 128, 255]);
    expect([...themed(new Uint8Array([255, 255, 255, 200, 30, 30]), false)]).toEqual([0x27, 0x27, 0x27, 200, 30, 30]);
    expect([...themed(new Uint8Array([0, 0, 0, 200, 30, 30]), true)]).toEqual([0xd4, 0xd4, 0xd4, 200, 30, 30]);
  });

  it('makes one line object and one faint fill a batch, lifted off the plane, never picked, snapped to on its plane', () => {
    const g = underlayGroup([batch([0, 0, 3, 1, 0, 3, 0, 0, 3, 0, 1, 3.5], [255, 0, 0, 255, 0, 0, 0, 0, 255, 0, 0, 255], [0, 0, 3, 1, 0, 3, 0, 1, 3], [9, 9, 9, 9, 9, 9, 9, 9, 9])], false);
    expect(g.name).toBe('cad-underlays');
    const [lines, fill] = g.children as [THREE.LineSegments, THREE.Mesh];
    expect(lines.isLineSegments).toBe(true);
    expect(lines.position.z).toBeCloseTo(LIFT);
    expect(lines.geometry.getAttribute('position').count).toBe(4);
    expect(lines.geometry.getAttribute('color').normalized).toBe(true);
    expect((fill.material as THREE.MeshBasicMaterial).opacity).toBeLessThan(0.3);
    expect((fill.material as THREE.Material).depthWrite).toBe(false);
    const hits: THREE.Intersection[] = [];
    lines.raycast(new THREE.Raycaster(), hits);
    expect(hits).toEqual([]);

    const scene = new THREE.Scene();
    scene.add(g);
    // On the level at 3 m only the first segment lies (the second climbs to 3.5).
    const on = underlaySegmentsInPlane(scene, { origin: { x: 0, y: 0, z: 3 }, normal: { x: 0, y: 0, z: 1 } });
    expect(on).toEqual([[{ x: 0, y: 0, z: 3 }, { x: 1, y: 0, z: 3 }]]);
    expect(underlaySegmentsInPlane(scene, { origin: { x: 0, y: 0, z: 0 }, normal: { x: 0, y: 0, z: 1 } })).toEqual([]);
    // The sketch's inference finds it with the bodies' edges; hidden by View → DXF in 3D, it does not.
    expect(sceneSegments(scene, levelFrame(3))).toContainEqual([{ x: 0, y: 0, z: 3 }, { x: 1, y: 0, z: 3 }]);
    g.visible = false;
    expect(sceneSegments(scene, levelFrame(3))).toEqual([]);
  });
});
