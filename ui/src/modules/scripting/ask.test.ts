import { describe, expect, it } from 'vitest';
import { collect, elementsOf, initialState, readNumber, readQuestion, storeysOf, type ModelFacts } from './ask';

const node = (id: string, type: string, name: string, props: Record<string, unknown> = {}) => [id, { id, type, name, props }] as const;

const model: ModelFacts = {
  nodes: new Map([
    node('s0', 'IfcBuildingStorey', 'Ground floor', { elevation: 0 }),
    node('s1', 'IfcBuildingStorey', 'First floor', { elevation: 3 }),
    node('w1', 'IfcWall', 'South'),
    node('w2', 'IfcWall', 'North'),
    node('d1', 'IfcDoor', 'Entrance'),
    node('t1', 'IfcWallType', 'Brick 250'),
    node('t2', 'IfcWallType', 'Aerated 300'),
  ]),
  selection: ['d1', 'w2', 'w1'],
  storey: 's1',
};

// What lapinbim sends (python/lapinbim/_ui.py, the host's script.ask) as the window gets it.
const event = {
  type: 'ask', ask: 'a1', title: 'Perimeter walls', text: 'Four walls.',
  fields: [
    { key: 'width', kind: 'number', label: 'Width', value: 8, unit: 'm', min: 0.1 },
    { key: 'count', kind: 'number', label: 'Count', value: 4, integer: true },
    { key: 'name', kind: 'text', label: 'Name', value: 'Room' },
    { key: 'closed', kind: 'bool', label: 'Closed', value: true },
    { key: 'kind', kind: 'choice', label: 'Kind', value: 'c', choices: [{ value: 'b', label: 'Brick' }, { value: 'c', label: 'Concrete' }] },
    { key: 'wall', kind: 'element', label: 'Wall', classes: ['IfcWall'], selection: true },
    { key: 'walls', kind: 'element', label: 'Walls', classes: ['IfcWall'], multiple: true },
    { key: 'storey', kind: 'storey', label: 'Storey' },
    { key: 'type', kind: 'type', label: 'Type', cls: 'IfcWallType' },
    { key: 'script', kind: 'html', label: 'Never', value: '<script>' },
    { key: 'width', kind: 'text', label: 'Twice' },
  ],
};

describe('lapin.ui.ask\'s dialog (decision 489, stage 4)', () => {
  it('keeps only the fields it knows how to draw, each key once', () => {
    const q = readQuestion(event)!;
    expect(q.title).toBe('Perimeter walls');
    expect(q.fields.map((f) => f.key)).toEqual(['width', 'count', 'name', 'closed', 'kind', 'wall', 'walls', 'storey', 'type']);
    expect(q.fields[0]).toMatchObject({ kind: 'number', unit: 'm', min: 0.1, value: 8 });
    expect(readQuestion({ ask: 'x', fields: [{ key: 'k', kind: 'choice' }] })).toBeNull();
    expect(readQuestion({ fields: [] })).toBeNull();
  });

  it('starts each field from the script\'s value, the selection, the active storey or the first type', () => {
    const q = readQuestion(event)!;
    const s = initialState(q.fields, model);
    expect(s).toEqual({ width: '8', count: '4', name: 'Room', closed: true, kind: 1, wall: ['w2'], walls: ['w2', 'w1'], storey: 's1', type: 't2' });
    expect(storeysOf(model).map((x) => x.name)).toEqual(['Ground floor', 'First floor']);
    // No active storey: the one at ±0.00.
    expect(initialState(q.fields, { ...model, storey: null }).storey).toBe('s0');
    expect(elementsOf(q.fields[5], model).map((x) => x.name)).toEqual(['North', 'South']);
  });

  it('answers the values typed, numbers with a comma too, and says the first that is wrong', () => {
    const q = readQuestion(event)!;
    const s = { ...initialState(q.fields, model), width: '10,5' };
    expect(collect(q.fields, s)).toEqual({
      values: { width: 10.5, count: 4, name: 'Room', closed: true, kind: 'c', wall: 'w2', walls: ['w2', 'w1'], storey: 's1', type: 't2' },
    });
    expect(collect(q.fields, { ...s, width: '0' })).toEqual({ key: 'width', error: 'At least 0.1 m.' });
    expect(collect(q.fields, { ...s, width: 'eight' })).toEqual({ key: 'width', error: 'Type a number.' });
    expect(collect(q.fields, { ...s, count: '4.5' })).toEqual({ key: 'count', error: 'Type a whole number.' });
    expect(collect(q.fields, { ...s, wall: [] })).toEqual({ key: 'wall', error: 'Choose an element.' });
    expect(readNumber(' 1 250,5 ')).toBe(1250.5);
    expect(readNumber('1e3')).toBe(1000);
    expect(readNumber('')).toBeNull();
  });
});
