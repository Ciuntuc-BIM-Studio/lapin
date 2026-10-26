import { describe, expect, it } from 'vitest';
import type { NodeDto, RelationDto } from '../engine/protocol';
import { describeItem, ifcTree, ownParameters, shapeOf } from './ifcTreeModel';

const node = (id: string, type: string, name: string, parentId: string | null = null, props: Record<string, unknown> = {}): NodeDto =>
  ({ id, type, name, parentId, px: 0, py: 0, props, editable: true }) as NodeDto;
const rel = (id: string, cls: string, relating: string, related: string[]): RelationDto => ({ id, class: cls, relating, related });

describe('the IFC tree', () => {
  const nodes = new Map<string, NodeDto>([
    ['p', node('p', 'IfcBuildingStorey', 'Parter', 'b', { elevation: 0 })],
    ['e1', node('e1', 'IfcBuildingStorey', 'Etaj 1', 'b', { elevation: 2.8 })],
    ['t1', node('t1', 'IfcWallType', 'Brick 300')],
    ['w1', node('w1', 'IfcWall', 'W1', 'p', { height: 2.8, imported: { shape: [{ type: 'IfcExtrudedAreaSolid', profile: { type: 'IfcRectangleProfileDef', x: 5, y: 0.3 }, depth: 2.8, direction: [0, 0, 1] }] } })],
    ['w2', node('w2', 'IfcWall', 'W2', 'p', { height: 3 })],
    ['o1', node('o1', 'IfcOpeningElement', 'Opening', null, { along: 2 })],
    ['d1', node('d1', 'IfcDoor', 'D1', 'p')],
    ['c1', node('c1', 'IfcColumn', 'C1', 'e1')],
    ['r1', node('r1', 'IfcRailing', 'Rail', null, { reference: true, shape: [{ type: 'IfcFacetedBrep', faces: 5 }] })],
    ['v', node('v', 'LapinView', 'Plan', null)],
  ]);
  const relations = new Map<string, RelationDto>([
    ['x1', rel('x1', 'IfcRelDefinesByType', 't1', ['w1'])],
    ['x2', rel('x2', 'IfcRelVoidsElement', 'w1', ['o1'])],
    ['x3', rel('x3', 'IfcRelFillsElement', 'o1', ['d1'])],
  ]);
  const tree = ifcTree({ nodes, relations });

  it('groups storey → class → type → element, the highest storey first, doors under their wall, views left out', () => {
    expect(tree.map((s) => s.name)).toEqual(['Etaj 1 · +2.80', 'Parter · +0.00', 'Not on a level']);
    const parter = tree[1];
    expect(parter.classes.map((c) => c.cls)).toEqual(['IfcWall']);
    expect(parter.classes[0].types.map((t) => [t.name, t.elements.length])).toEqual([['(no type)', 1], ['Brick 300', 1]]);
    const w1 = parter.classes[0].types[1].elements[0];
    expect(w1.openings).toHaveLength(1);
    expect(w1.openings[0].fill?.id).toBe('d1');
    expect(tree[2].classes[0].cls).toBe('IfcRailing');
    expect(JSON.stringify(tree)).not.toContain('"LapinView"');
  });

  it('describes the construction of an imported element, and the parameters of a native one', () => {
    const w1 = nodes.get('w1')!;
    expect(describeItem(shapeOf(w1)[0])).toBe('IfcExtrudedAreaSolid · IfcRectangleProfileDef 5000 × 300 · depth 2800 · ↑');
    expect(describeItem(shapeOf(nodes.get('r1')!)[0])).toBe('IfcFacetedBrep · 5 faces');
    expect(shapeOf(nodes.get('w2')!)).toEqual([]);
    expect(ownParameters(nodes.get('w2')!)).toEqual([['height', '3']]);
  });
});
