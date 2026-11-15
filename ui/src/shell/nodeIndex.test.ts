import { describe, expect, it } from 'vitest';
import { nodesOfType, projectOf } from './nodeIndex';
import type { NodeDto } from '../engine/protocol';

const node = (id: string, type: string) => ({ id, type, name: id, props: {} }) as unknown as NodeDto;

describe('the by-type index', () => {
  it('lists the nodes of a type in order, the same array each time for one map', () => {
    const nodes = new Map([['a', node('a', 'IfcWall')], ['p', node('p', 'IfcProject')], ['b', node('b', 'IfcWall')]]);
    expect(nodesOfType(nodes, 'IfcWall').map((n) => n.id)).toEqual(['a', 'b']);
    expect(nodesOfType(nodes, 'IfcWall')).toBe(nodesOfType(nodes, 'IfcWall'));
    expect(projectOf(nodes)?.id).toBe('p');
    expect(nodesOfType(nodes, 'IfcSlab')).toEqual([]);
  });
  it('is a new index for a new map', () => {
    const one = new Map([['a', node('a', 'IfcWall')]]);
    const two = new Map([...one, ['b', node('b', 'IfcWall')]]);
    expect(nodesOfType(one, 'IfcWall')).toHaveLength(1);
    expect(nodesOfType(two, 'IfcWall')).toHaveLength(2);
  });
});
