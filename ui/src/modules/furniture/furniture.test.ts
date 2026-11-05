import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { furnitureGrips, planBox, planPaths, sizeOf, typesOfKind, type FurnitureKind, type PreviewDto } from './furniture';

const node = (id: string, type: string, props: Record<string, unknown>, name = id): NodeDto => ({ id, type, name, props } as unknown as NodeDto);

describe('furniture', () => {
  const kinds = [{ key: 'sofa', width: 2.2, depth: 0.95, height: 0.82 }, { key: 'kitchen', width: 3, depth: 0.6, height: 0.9 }] as FurnitureKind[];

  it('lists a kind\'s types by name and says their size', () => {
    const nodes = new Map([
      ['b', node('b', 'IfcFurnitureType', { spec: { kind: 'sofa', width: 1.8 } }, 'Sofa B')],
      ['a', node('a', 'IfcFurnitureType', { spec: { kind: 'sofa' } }, 'Sofa A')],
      ['k', node('k', 'IfcFurnitureType', { spec: { kind: 'kitchen' } }, 'Kitchen')],
    ]);
    expect(typesOfKind(nodes, 'sofa').map((t) => t.name)).toEqual(['Sofa A', 'Sofa B']);
    expect(sizeOf(nodes.get('b'), kinds)).toBe('1800 × 950 × 820');
    expect(sizeOf(nodes.get('k'), kinds)).toBe('depth 600 · worktop 900');
  });

  it('turns a piece so its front looks at the cursor', () => {
    const piece = node('p', 'IfcFurniture', { at: [2, 3], rotation: 0 });
    const [move, turn] = furnitureGrips(piece, 1);
    expect(move.at).toEqual({ x: 2, y: 3 });
    // Its front looks along −y: the handle is in front of it.
    expect(turn.at.x).toBeCloseTo(2);
    expect(turn.at.y).toBeLessThan(3);
    // The cursor to its east: its front turned to +x, a quarter turn anticlockwise.
    expect(turn.step!({ x: 5, y: 3 })).toEqual({ command: 'element.transform', args: { ids: ['p'], op: 'rotate', center: [2, 3], angle: 90 } });
    expect(move.step!({ x: 3, y: 3 })).toEqual({ command: 'element.transform', args: { ids: ['p'], op: 'move', by: [1, 0] } });
  });

  it('draws the preview\'s plan with y up', () => {
    const p: PreviewDto = { plan: [{ kind: 'symbol', closed: true, points: [0, 0, 1, 0, 1, 0.5] }], front: [], width: 1, depth: 0.5, height: 1 };
    expect(planPaths(p)[0].d).toBe('M0.0000 0.0000 L1.0000 0.0000 L1.0000 -0.5000 Z');
    const [x, y, w, h] = planBox(p, 0);
    expect([x, y, w, h]).toEqual([0, -0.5, 1, 0.5]);
  });
});
