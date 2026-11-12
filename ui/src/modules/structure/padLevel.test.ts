import { describe, expect, it } from 'vitest';
import type { NodeDto, RelationDto } from '../../engine/protocol';
import { columnSnapPoints, underColumnsArgs } from './padLevel';

const node = (id: string, type: string, props: Record<string, unknown> = {}): NodeDto => ({ id, type, name: id, props } as unknown as NodeDto);
const rel = (id: string, relating: string, related: string[]): RelationDto => ({ id, class: 'IfcRelContainedInSpatialStructure', relating, related } as unknown as RelationDto);

function model() {
  const nodes = new Map<string, NodeDto>([
    ['fdn', node('fdn', 'IfcBuildingStorey', { elevation: -1.5 })],
    ['gf', node('gf', 'IfcBuildingStorey', { elevation: 0 })],
    ['ff', node('ff', 'IfcBuildingStorey', { elevation: 3 })],
    ['c1', node('c1', 'IfcColumn', { at: [0, 0] })],
    ['c2', node('c2', 'IfcColumn', { at: [6, 0] })],
    ['c3', node('c3', 'IfcColumn', { at: [9, 9] })],
    ['w', node('w', 'IfcWall', { start: [0, 0], end: [1, 0] })],
  ]);
  const relations = new Map<string, RelationDto>([
    ['r1', rel('r1', 'gf', ['c1', 'c2', 'w'])],
    ['r2', rel('r2', 'ff', ['c3'])],
  ]);
  return { nodes, relations };
}

describe('the Pad footing tool snaps on columns', () => {
  it('of the foundation plan\'s storey and of the one above it, not higher', () => {
    expect(columnSnapPoints(model(), 'fdn')).toEqual([{ x: 0, y: 0 }, { x: 6, y: 0 }]);
    expect(columnSnapPoints(model(), 'gf')).toEqual([{ x: 0, y: 0 }, { x: 6, y: 0 }, { x: 9, y: 9 }]);
    expect(columnSnapPoints(model(), 'ff')).toEqual([{ x: 9, y: 9 }]);
    expect(columnSnapPoints(model(), null)).toEqual([]);
  });
});

describe('Pad footings under columns', () => {
  it('takes the selected columns, else the plan\'s storey', () => {
    expect(underColumnsArgs(model(), ['c2', 'w'], 'gf')).toEqual({ ids: ['c2'] });
    expect(underColumnsArgs(model(), ['w'], 'fdn')).toEqual({ storeyId: 'fdn' });
    expect(underColumnsArgs(model(), [], null)).toBeNull();
  });
});
