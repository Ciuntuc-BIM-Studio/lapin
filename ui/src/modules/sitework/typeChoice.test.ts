import { describe, expect, it } from 'vitest';
import type { NodeDto, RelationDto } from '../../engine/protocol';
import { openingType, rememberedType } from './typeChoice';

const node = (id: string, type: string): NodeDto => ({ id, type, name: id, props: {} } as unknown as NodeDto);
const rel = (id: string, relating: string, related: string[]): RelationDto => ({ id, class: 'IfcRelDefinesByType', relating, related });

/** Two scaffold types; the scaffold drawn with the second. */
const model = {
  nodes: new Map([node('T1', 'LapinScaffoldType'), node('T2', 'LapinScaffoldType'), node('S', 'LapinScaffold'), node('W', 'IfcWall')].map((n) => [n.id, n])),
  relations: new Map([rel('r', 'T2', ['S'])].map((r) => [r.id, r])),
};
const types = [model.nodes.get('T1')!, model.nodes.get('T2')!];

describe('the type a site works configurator opens on', () => {
  it('is the selected scaffold\'s type, whatever was chosen before', () => {
    expect(openingType(model, ['W', 'S'], 'LapinScaffold', 'T1', types)).toBe('T2');
  });
  it('is the type chosen last when nothing of the kind is selected, if the project still has it', () => {
    expect(openingType(model, ['W'], 'LapinScaffold', 'T1', types)).toBe('T1');
    expect(openingType(model, [], 'LapinScaffold', 'gone', types)).toBe('T1');
    expect(openingType(model, [], 'LapinScaffold', null, [])).toBeNull();
  });
});

describe('a type remembered for the site works tools', () => {
  const formwork = (id: string, kind?: string): NodeDto => ({ id, type: 'LapinFormworkType', name: id, props: kind ? { kind } : {} } as unknown as NodeDto);
  const nodes = new Map([formwork('F1'), formwork('F2', 'beam'), node('T1', 'LapinScaffoldType')].map((n) => [n.id, n]));
  it('is sent while the project has it, of its class', () => {
    expect(rememberedType(nodes, 'T1', 'LapinScaffoldType')).toBe('T1');
    expect(rememberedType(nodes, 'F1', 'LapinScaffoldType')).toBeUndefined();
  });
  it('a deleted type is never sent, not even for a floor (the kind a type without one has)', () => {
    expect(rememberedType(nodes, 'gone', 'LapinFormworkType', 'slab')).toBeUndefined();
    expect(rememberedType(nodes, null, 'LapinFormworkType', 'slab')).toBeUndefined();
  });
  it('a formwork type only for its own kind', () => {
    expect(rememberedType(nodes, 'F1', 'LapinFormworkType', 'slab')).toBe('F1');
    expect(rememberedType(nodes, 'F2', 'LapinFormworkType', 'slab')).toBeUndefined();
    expect(rememberedType(nodes, 'F2', 'LapinFormworkType', 'beam')).toBe('F2');
  });
});
