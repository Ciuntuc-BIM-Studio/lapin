import { describe, expect, it } from 'vitest';
import type { FieldDescriptor, NodeDto } from '../../engine/protocol';
import { MATCH_GROUPS, matchSteps, readGroups, type MatchContext, type MatchGroup } from './matchProperties';

// Match properties (a forum user's wish, topic #29; decision 490): AutoCAD's MATCHPROP, Revit's Match Type.

const node = (id: string, type: string, props: Record<string, unknown> = {}, more: Partial<NodeDto> = {}): NodeDto =>
  ({ id, type, name: id, parentId: 'storey', px: 0, py: 0, props: props as NodeDto['props'], editable: true, module: null, storeyId: 'storey', ...more });

const fields: Record<string, FieldDescriptor[]> = {
  IfcWall: [
    { key: 'height', label: 'Height', kind: 'length' },
    { key: 'baseOffset', label: 'Base offset', kind: 'length' },
    { key: 'arc', label: 'Arc rise', kind: 'length' },
    { key: 'cutPriority', label: 'Cut priority', kind: 'number' },
    { key: 'startAngle', label: 'Start cut angle', kind: 'angle', showIf: [{ startAngle: ['!'] }] },
    { key: 'roofTop', label: 'Top under a roof', kind: 'choice', choices: ['level', 'roof'] },
  ],
  IfcColumn: [
    { key: 'height', label: 'Height (own)', kind: 'length' },
    { key: 'baseOffset', label: 'Base offset', kind: 'length' },
    { key: 'rotation', label: 'Rotation', kind: 'angle' },
  ],
  IfcDoor: [], IfcWindow: [],
  IfcAnnotation: [
    { key: 'text', label: 'Text', kind: 'text' },
    { key: 'textStyle', label: 'Text style', kind: 'text' },
    { key: 'textHeight', label: 'Text height', kind: 'number' },
    { key: 'angle', label: 'Angle', kind: 'angle' },
  ],
  IfcSpace: [{ key: 'number', label: 'Number', kind: 'text' }, { key: 'area', label: 'Area', kind: 'number', readOnly: true }],
};

const types: Record<string, NodeDto> = { brick: node('brick', 'IfcWallType'), block: node('block', 'IfcWallType'), wide: node('wide', 'IfcDoorType'), narrow: node('narrow', 'IfcDoorType'), win: node('win', 'IfcWindowType') };
const typeOfId: Record<string, string> = { w1: 'brick', w2: 'block', w3: 'brick', d1: 'wide', d2: 'narrow', win1: 'win' };
const sills: Record<string, number> = { d1: 0, d2: 0, win1: 0.9 };

const ctx = (phases = true): MatchContext => ({
  fieldsOf: (cls) => fields[cls] ?? [],
  typeOf: (id) => types[typeOfId[id]] ?? null,
  typeSteps: (cls, ids, typeId) => (cls === 'IfcWall' ? [{ command: 'architecture.setWallType', args: { ids, typeId } }]
    : cls === 'IfcDoor' || cls === 'IfcWindow' ? [{ command: 'architecture.setJoineryType', args: { ids, typeId } }] : null),
  sillOf: (id) => sills[id] ?? null,
  phases,
});

// What is chosen until the user says otherwise: all but the name.
const all = new Set<MatchGroup>(MATCH_GROUPS.filter((g) => g.on).map((g) => g.id));
const only = (...g: MatchGroup[]) => new Set<MatchGroup>(g);

describe('Match properties', () => {
  const source = node('w1', 'IfcWall', {
    start: [0, 0], end: [4, 0], arc: 0.5, height: 3.1, baseOffset: -0.2, cutPriority: 40,
    params: [{ name: 'Fire rating', kind: 'text', value: 'EI 60' }, { name: 'FireRating', kind: 'text', value: 'EI 60', pset: 'Pset_WallCommon' }],
    phaseCreated: 'existing',
  }, { name: 'Facade wall' });

  it('gives a wall its type, its height and offsets, its parameters and its phases, in one batch per target', () => {
    const target = node('w2', 'IfcWall', { start: [0, 3], end: [4, 3], height: 2.8, pinned: true });
    expect(matchSteps(source, [target], all, ctx())).toEqual([
      { command: 'architecture.setWallType', args: { ids: ['w2'], typeId: 'brick' } },
      { command: 'element.update', args: { ids: ['w2'], attrs: { height: 3.1, baseOffset: -0.2, cutPriority: 40 } } },
      { command: 'element.setParam', args: { ids: ['w2'], name: 'Fire rating', kind: 'text', value: 'EI 60' } },
      { command: 'element.setParam', args: { ids: ['w2'], name: 'FireRating', kind: 'text', value: 'EI 60', pset: 'Pset_WallCommon' } },
      { command: 'phases.assign', args: { ids: ['w2'], created: 'existing', demolished: null } },
    ]);
  });

  it('never carries where an element stands, its shape, its identity, nor its name unless asked', () => {
    const target = node('w2', 'IfcWall', { start: [0, 3], end: [4, 3] });
    const steps = matchSteps(source, [target], all, ctx());
    const text = JSON.stringify(steps);
    for (const k of ['start', 'end', '"arc"', 'GlobalId', 'Facade wall']) expect(text).not.toContain(k);
    expect(matchSteps(source, [target], only('name'), ctx())).toEqual([{ command: 'element.rename', args: { id: 'w2', name: 'Facade wall' } }]);
  });

  it('leaves out what the target already has, a group not chosen, the source itself, and what cannot be edited', () => {
    const same = node('w3', 'IfcWall', { height: 3.1, baseOffset: -0.2, cutPriority: 40, params: source.props.params, phaseCreated: 'existing' });
    expect(matchSteps(source, [same], all, ctx())).toEqual([]);
    const target = node('w2', 'IfcWall', {});
    expect(matchSteps(source, [target], only('type'), ctx())).toEqual([{ command: 'architecture.setWallType', args: { ids: ['w2'], typeId: 'brick' } }]);
    expect(matchSteps(source, [source, node('x', 'IfcWall', {}, { editable: false })], all, ctx())).toEqual([]);
    // Without the phases module, no phases.
    expect(matchSteps(source, [target], only('phase'), ctx(false))).toEqual([]);
  });

  it('gives another class the fields both have and its own parameters, not the type nor a standard set of another class', () => {
    const column = node('c1', 'IfcColumn', { at: [1, 1], rotation: 30 });
    expect(matchSteps(source, [column], all, ctx())).toEqual([
      { command: 'element.update', args: { ids: ['c1'], attrs: { height: 3.1, baseOffset: -0.2 } } },
      { command: 'element.setParam', args: { ids: ['c1'], name: 'Fire rating', kind: 'text', value: 'EI 60' } },
      { command: 'phases.assign', args: { ids: ['c1'], created: 'existing', demolished: null } },
    ]);
  });

  it('gives a door its type and its frame\'s place in the wall, and a window the door\'s sill but not the door\'s type', () => {
    const door = node('d1', 'IfcDoor', { position: { from: 'exterior', distance: 0.05 } });
    const other = node('d2', 'IfcDoor', {});
    const window = node('win1', 'IfcWindow', { position: { from: 'exterior', distance: 0.05 } });
    expect(matchSteps(door, [other], all, ctx())).toEqual([
      { command: 'architecture.setJoineryType', args: { ids: ['d2'], typeId: 'wide' } },
      { command: 'architecture.setJoineryPosition', args: { ids: ['d2'], from: 'exterior', distance: 0.05 } },
    ]);
    expect(matchSteps(door, [window], all, ctx())).toEqual([{ command: 'architecture.moveOpening', args: { ids: ['win1'], sill: 0 } }]);
    // A frame set back on the target, none on the source: back to its type's.
    expect(matchSteps(other, [window], only('fields'), ctx())).toEqual([
      { command: 'architecture.setJoineryPosition', args: { ids: ['win1'], clear: true } },
      { command: 'architecture.moveOpening', args: { ids: ['win1'], sill: 0 } },
    ]);
  });

  it('gives a drafted shape the layer and colour, a hatch its pattern, scale and angle, a text its style, never one to the other', () => {
    const line = node('l1', 'IfcAnnotation', { shape: { type: 'line', a: [0, 0], b: [1, 0] }, layer: 'Walls', color: '#ff0000' });
    const line2 = node('l2', 'IfcAnnotation', { shape: { type: 'line', a: [5, 5], b: [6, 5] }, layer: '0' });
    expect(matchSteps(line, [line2], all, ctx())).toEqual([{ command: 'drafting.updateDraft', args: { ids: ['l2'], layer: 'Walls', color: '#ff0000' } }]);
    const hatch = node('h1', 'IfcAnnotation', { hatch: { pattern: 'ANSI31', scale: 2, angle: 45, loops: [[[0, 0], [1, 0], [1, 1]]] }, layer: '0' });
    const hatch2 = node('h2', 'IfcAnnotation', { hatch: { pattern: 'SOLID', scale: 1, angle: 0, loops: [[[5, 5], [6, 5], [6, 6]]] }, layer: '0', color: '#00ff00' });
    expect(matchSteps(hatch, [hatch2], all, ctx())).toEqual([{ command: 'drafting.updateDraft', args: { ids: ['h2'], color: null, hatch: { pattern: 'ANSI31', scale: 2, angle: 45 } } }]);
    const text = node('t1', 'IfcAnnotation', { text: 'A', textStyle: 'Titles', textHeight: 5, angle: 30 });
    const text2 = node('t2', 'IfcAnnotation', { text: 'B', textHeight: 2.5 });
    expect(matchSteps(text, [text2], all, ctx())).toEqual([{ command: 'element.update', args: { ids: ['t2'], attrs: { textStyle: 'Titles', textHeight: 5 } } }]);
    expect(matchSteps(text, [line2], all, ctx())).toEqual([]);
    expect(matchSteps(line, [text2], all, ctx())).toEqual([]);
  });

  it('never writes a read-only field nor a number of its own (a room\'s)', () => {
    const room = node('r1', 'IfcSpace', { number: '101', area: 12 });
    expect(matchSteps(room, [node('r2', 'IfcSpace', { number: '102', area: 9 })], all, ctx())).toEqual([]);
  });

  it('remembers the groups chosen, every group but the name by default', () => {
    expect([...readGroups(null)].sort()).toEqual(MATCH_GROUPS.filter((g) => g.on).map((g) => g.id).sort());
    expect(readGroups(null).has('name')).toBe(false);
    expect([...readGroups('["type","phase","nonsense"]')].sort()).toEqual(['phase', 'type']);
    expect([...readGroups('not json')].sort()).toEqual([...readGroups(null)].sort());
  });
});
