import { describe, expect, it } from 'vitest';
import { inTypeOrder, ofClass, type ModelState } from './model';
import type { NodeDto } from './protocol';

const node = (id: string, type: string, props: Record<string, unknown> = {}): NodeDto => ({ id, type, name: id, parentId: null, px: 0, py: 0, props } as unknown as NodeDto);

describe("types in the project's order (forum #71)", () => {
  it('lists the types the project orders first, in its order, then the others as they were made', () => {
    const types = [node('a', 'IfcWallType'), node('b', 'IfcWallType'), node('c', 'IfcWallType'), node('d', 'IfcWallType')];
    expect(inTypeOrder(types, { IfcWallType: ['c', 'a', 'gone'] }, 'IfcWallType').map((n) => n.id)).toEqual(['c', 'a', 'b', 'd']);
    expect(inTypeOrder(types, undefined, 'IfcWallType').map((n) => n.id)).toEqual(['a', 'b', 'c', 'd']);
    expect(inTypeOrder(types, { IfcSlabType: ['d'] }, 'IfcWallType').map((n) => n.id)).toEqual(['a', 'b', 'c', 'd']);
  });

  it('is what every type list reads (ofClass), other classes as they come', () => {
    const nodes = new Map([node('w1', 'IfcWall'), node('t1', 'IfcWallType'), node('t2', 'IfcWallType'), node('p', 'IfcProject', { typeOrder: { IfcWallType: ['t2'] } }), node('w2', 'IfcWall')].map((n) => [n.id, n]));
    const s: ModelState = { nodes, relations: new Map() };
    expect(ofClass(s, 'IfcWallType').map((n) => n.id)).toEqual(['t2', 't1']);
    expect(ofClass(s, 'IfcWall').map((n) => n.id)).toEqual(['w1', 'w2']);
  });
});
