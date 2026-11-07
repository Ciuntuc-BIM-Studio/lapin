import { describe, expect, it } from 'vitest';
import * as THREE from 'three';
import type { NodeDto } from '../../engine/protocol';
import { buildModel, type Preview3DPart } from './JoineryPreview3D';

const tri = (over: Partial<Preview3DPart>): Preview3DPart => ({ element: 'fill', name: 'frame', color: '#ffffff', positions: [0, 0, 0, 1, 0, 0, 0, 0, 1], indices: [0, 1, 2], edges: [0, 0, 0, 1, 0, 0], ...over });

describe('the joinery configurator\'s 3D preview (report #108)', () => {
  it('makes a mesh per part with its edges, in its material\'s colour, and glass without a material see-through', () => {
    const brick = { id: 'm1', props: { color: '#aa3300' } } as unknown as NodeDto;
    const g = buildModel([tri({ element: 'wall', name: 'Core', material: 'm1' }), tri({ name: 'glass', edges: [] })], (id) => (id === 'm1' ? brick : undefined));
    const meshes = g.children.filter((c): c is THREE.Mesh => c instanceof THREE.Mesh);
    expect(meshes).toHaveLength(2);
    expect(g.children.filter((c) => c instanceof THREE.LineSegments)).toHaveLength(1);
    expect((meshes[0].material as THREE.MeshStandardMaterial).color.getHexString()).toBe('aa3300');
    const glass = meshes[1].material as THREE.MeshStandardMaterial;
    expect(glass.transparent).toBe(true);
    expect(glass.opacity).toBeLessThan(0.5);
  });
});
