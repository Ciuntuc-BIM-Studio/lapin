import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { knownDisciplines } from './disciplines';

const node = (id: string, type: string, props: Record<string, unknown> = {}): NodeDto =>
  ({ id, type, name: id, parentId: null, x: 0, y: 0, props, editable: true, module: 'core', storeyId: null } as unknown as NodeDto);

describe('knownDisciplines', () => {
  it('starts with the defaults, then what the project names, once each, case-free', () => {
    const nodes = new Map<string, NodeDto>([
      ['p', node('p', 'IfcProject', { browserSets: [{ id: 's', name: 'Site', filter: { disciplines: ['Landscape'] } }] })],
      ['v', node('v', 'LapinView', { kind: 'section', discipline: 'structure' })],
      ['q', node('q', 'LapinSheet', { discipline: 'Fire safety' })],
    ]);
    expect(knownDisciplines(nodes, [{ discipline: 'Landscape' }, { discipline: null }])).toEqual(['Architecture', 'Structure', 'MEP', 'Fire safety', 'Landscape']);
  });
  it('is the three defaults for an empty project', () => {
    expect(knownDisciplines(new Map())).toEqual(['Architecture', 'Structure', 'MEP']);
  });
});
