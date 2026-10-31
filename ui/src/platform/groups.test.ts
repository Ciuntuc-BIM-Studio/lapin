import { beforeEach, describe, expect, it } from 'vitest';
import type { NodeDto, RelationDto } from '../engine/protocol';
import { useSession } from '../engine/session';
import { useScope } from './scope';
import { groupOf, membersOf, openGroup, pickTarget, pickTargets, shownSelection } from './groups';

const node = (id: string, type: string): NodeDto => ({ id, type, name: id, parentId: null, px: 0, py: 0, props: {}, editable: true, module: null, storeyId: null });

beforeEach(() => {
  const nodes = new Map([['g', node('g', 'IfcGroup')], ['a', node('a', 'IfcWall')], ['b', node('b', 'IfcWall')], ['c', node('c', 'IfcWall')]]);
  const relations = new Map<string, RelationDto>([['r', { id: 'r', class: 'IfcRelAssignsToGroup', relating: 'g', related: ['a', 'b'] }]]);
  useSession.setState({ nodes, relations });
  useScope.setState({ scope: null });
});

describe('groups in the interface', () => {
  it('a member picks its group; an element on its own picks itself', () => {
    expect(groupOf('a')).toBe('g');
    expect(membersOf('g')).toEqual(['a', 'b']);
    expect(pickTarget('a')).toBe('g');
    expect(pickTarget('c')).toBe('c');
    expect(pickTargets(['a', 'b', 'c'])).toEqual(['g', 'c']);
    expect([...shownSelection(['g'])].sort()).toEqual(['a', 'b', 'g']);
  });

  it('opened, its members are picked one by one and nothing outside it is', () => {
    openGroup('g');
    const scope = useScope.getState().scope!;
    expect(scope.label).toBe('g');
    expect(pickTarget('a')).toBe('a');
    expect(scope.includes(node('a', 'IfcWall'))).toBe(true);
    expect(scope.includes(node('c', 'IfcWall'))).toBe(false);
  });
});
