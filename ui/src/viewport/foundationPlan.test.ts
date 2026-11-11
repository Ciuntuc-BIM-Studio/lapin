import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../engine/protocol';
import { levelUnder, placingStorey } from './foundationPlan';

const node = (id: string, type: string, props: Record<string, unknown> = {}): NodeDto => ({ id, type, name: id, props, editable: true } as unknown as NodeDto);

function project(view: Record<string, unknown>) {
  const nodes = new Map<string, NodeDto>();
  for (const n of [
    node('found', 'IfcBuildingStorey', { elevation: -1.5 }), node('ground', 'IfcBuildingStorey', { elevation: 0 }), node('first', 'IfcBuildingStorey', { elevation: 3 }),
    node('fp', 'LapinView', { kind: 'plan', storeyId: 'ground', foundation: true, ...view }),
  ]) nodes.set(n.id, n);
  return nodes;
}

describe('a foundation plan (report #117)', () => {
  it('finds the level under a storey', () => {
    const nodes = project({});
    expect(levelUnder(nodes, 'ground')?.id).toBe('found');
    expect(levelUnder(nodes, 'first')?.id).toBe('ground');
    expect(levelUnder(nodes, 'found')).toBeNull();
  });

  it('puts floors and walls on the level under it, footings on its own', () => {
    const nodes = project({ storeys: { mode: 'some', ids: ['found'], halftone: false } });
    expect(placingStorey(nodes, 'ground', 'fp', 'architecture.slab')).toBe('found');
    expect(placingStorey(nodes, 'ground', 'fp', 'architecture.wall')).toBe('found');
    expect(placingStorey(nodes, 'ground', 'fp', 'structure.strip')).toBe('ground');
    expect(placingStorey(nodes, 'ground', 'fp', 'structure.pad')).toBe('ground');
    expect(placingStorey(nodes, 'ground', 'fp', 'drafting.line')).toBe('ground');
  });

  it('leaves a foundation plan that does not show the level under it, and any other plan, as they are', () => {
    expect(placingStorey(project({}), 'ground', 'fp', 'architecture.slab')).toBe('ground');
    const nodes = project({ storeys: { mode: 'some', ids: ['found'] } });
    nodes.set('plain', node('plain', 'LapinView', { kind: 'plan', storeyId: 'ground' }));
    expect(placingStorey(nodes, 'ground', 'plain', 'architecture.slab')).toBe('ground');
    expect(placingStorey(nodes, 'ground', null, 'architecture.slab')).toBe('ground');
    expect(placingStorey(nodes, 'found', 'fp', 'architecture.slab')).toBe('found');
  });
});
