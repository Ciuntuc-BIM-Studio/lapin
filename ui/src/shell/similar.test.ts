import { describe, expect, it } from 'vitest';
import type { NodeDto, RelationDto } from '../engine/protocol';
import { HISTORY_SIZE, pruned, remember } from './selectionHistory';
import { criteriaOf, defaultKeys, similarIds } from './similar';

const node = (id: string, type: string, o: Partial<NodeDto> = {}): NodeDto => ({ id, type, name: id, parentId: null, px: 0, py: 0, props: {}, editable: true, module: null, storeyId: 's1', ...o });
const rel = (id: string, cls: string, relating: string, related: string[]): RelationDto => ({ id, class: cls, relating, related });

function model() {
  const nodes = new Map<string, NodeDto>();
  for (const n of [
    node('s1', 'IfcBuildingStorey', { name: 'Ground', storeyId: null }), node('s2', 'IfcBuildingStorey', { name: 'First', storeyId: null }),
    node('t1', 'IfcWallType', { name: 'Brick 300' }), node('t2', 'IfcWallType', { name: 'Brick 250' }),
    node('m1', 'IfcMaterial', { name: 'Brick' }), node('m2', 'IfcMaterial', { name: 'Concrete' }),
    node('ls1', 'IfcMaterialLayerSet', { props: { layers: [{ material: 'm1', thickness: 0.3, core: true }] } }),
    node('ls2', 'IfcMaterialLayerSet', { props: { layers: [{ material: 'm2', thickness: 0.25, core: true }] } }),
    node('w1', 'IfcWall'), node('w2', 'IfcWall'), node('w3', 'IfcWall', { storeyId: 's2' }), node('w4', 'IfcWall'), node('d1', 'IfcDoor'),
  ]) nodes.set(n.id, n);
  const relations = new Map<string, RelationDto>();
  for (const r of [
    rel('r1', 'IfcRelDefinesByType', 't1', ['w1', 'w3']), rel('r2', 'IfcRelDefinesByType', 't2', ['w2']),
    rel('r3', 'IfcRelAssociatesMaterial', 'ls1', ['t1']), rel('r4', 'IfcRelAssociatesMaterial', 'ls2', ['t2']),
  ]) relations.set(r.id, r);
  return { nodes, relations };
}

describe('select similar', () => {
  it('matches the class and the type, or the class alone for an untyped element', () => {
    const m = model();
    expect(defaultKeys(m, ['w1'])).toEqual(['type']);
    expect(similarIds(m, ['w1'], ['type']).sort()).toEqual(['w1', 'w3']);
    expect(defaultKeys(m, ['w4'])).toEqual([]);
    expect(similarIds(m, ['w4'], []).sort()).toEqual(['w1', 'w2', 'w3', 'w4']);
  });
  it('is limited by the scope (the view), the selection itself kept', () => {
    const m = model();
    expect(similarIds(m, ['w1'], ['type'], (n) => n.storeyId === 's1')).toEqual(['w1']);
  });
  it('offers what the element is told apart by: its type, width, core material and storey', () => {
    const m = model();
    expect(criteriaOf(m, 'w1').map((c) => [c.key, c.value])).toEqual([['type', 'Brick 300'], ['width', '300 mm'], ['coreMaterial', 'Brick'], ['storey', 'Ground']]);
    expect(criteriaOf(m, 'w4').map((c) => c.key)).toEqual(['storey']);
  });
  it('selects by one criterion across types', () => {
    const m = model();
    expect(similarIds(m, ['w1'], ['storey']).sort()).toEqual(['w1', 'w2', 'w4']);
    expect(similarIds(m, ['w1'], ['coreMaterial']).sort()).toEqual(['w1', 'w3']);
    // an element with no such thing never matches
    expect(similarIds(m, ['w1'], ['width']).includes('w4')).toBe(false);
  });
});

describe('the selection history', () => {
  it('keeps the newest first, none twice, and at most ten', () => {
    let h: string[][] = [];
    h = remember(h, ['a']);
    h = remember(h, ['b', 'c']);
    h = remember(h, ['a']);
    expect(h).toEqual([['a'], ['b', 'c']]);
    for (let i = 0; i < 15; i++) h = remember(h, [`x${i}`]);
    expect(h.length).toBe(HISTORY_SIZE);
    expect(h[0]).toEqual(['x14']);
  });
  it('does not keep an empty selection, and treats the same ids in another order as the same selection', () => {
    expect(remember([['a']], [])).toEqual([['a']]);
    expect(remember([['a', 'b']], ['b', 'a'])).toEqual([['b', 'a']]);
  });
  it('drops ids that are gone, and what that leaves empty or doubled', () => {
    expect(pruned([['a', 'gone'], ['gone'], ['a'], ['b']], (id) => id !== 'gone')).toEqual([['a'], ['b']]);
  });
});
