import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { addToPartRequest, driveChange, driveRows, freeAttrs, memberIdsOf, type PartMember } from './partMembersModel';

const node = (id: string, props: Record<string, unknown> = {}): NodeDto => ({ id, type: 'IfcWall', name: id, props } as unknown as NodeDto);
const nodes = (...ns: NodeDto[]) => new Map(ns.map((n) => [n.id, n]));

describe('a part\'s members', () => {
  it('reads the held elements from memberIds, each once, nested slots too', () => {
    expect(memberIdsOf({ memberIds: { a: 'w1', 'a/b': 'w2', c: 'w1' } })).toEqual(['w1', 'w2']);
    expect(memberIdsOf({})).toEqual([]);
    expect(memberIdsOf(null)).toEqual([]);
    expect(memberIdsOf({ memberIds: [1] })).toEqual([]);
  });

  const m: PartMember = { key: 'k1', class: 'IfcWall', name: 'Wall', id: 'w1', drives: { length: 'W' }, drivable: ['length', 'height', 'thickness'] };
  it('lists the drives and the attributes still free', () => {
    expect(driveRows(m)).toEqual([{ attr: 'length', formula: 'W' }]);
    expect(freeAttrs(m)).toEqual(['height', 'thickness']);
    expect(freeAttrs({ ...m, drives: undefined })).toEqual(['length', 'height', 'thickness']);
  });

  it('turns what is typed into the drives the engine takes, a blank one clearing', () => {
    expect(driveChange('height', 'H')).toEqual({ height: 'H' });
    expect(driveChange('height', '= H / 2')).toEqual({ height: 'H / 2' });
    expect(driveChange('height', '  ')).toEqual({ height: null });
    expect(driveChange('height', null)).toEqual({ height: null });
  });
});

describe('Add to part', () => {
  const part = node('p', { operation: 'part' });
  const ns = nodes(part, node('w1'), node('w2'), node('q', { operation: 'part' }));
  it('takes the placed part of the selection and the others as members', () => {
    expect(addToPartRequest(['w1', 'p', 'w2'], ns)).toEqual({ partId: 'p', ids: ['w1', 'w2'] });
  });
  it('says what to select otherwise', () => {
    expect('notice' in addToPartRequest(['w1', 'w2'], ns)).toBe(true);
    expect('notice' in addToPartRequest(['p', 'q', 'w1'], ns)).toBe(true);
    expect('notice' in addToPartRequest(['p'], ns)).toBe(true);
  });
});
