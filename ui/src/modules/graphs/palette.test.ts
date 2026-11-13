import { describe, expect, it } from 'vitest';
import { changedValues, fieldOf, paletteOf, shown, stateOf, toolOf, valueOf, GRAPHS_GROUP } from './palette';
import type { GraphJson, NodeSpec } from './graph';

const specs: Record<string, NodeSpec> = {
  number: { kind: 'number', label: 'Number', group: 'Inputs', inputs: [{ name: 'value', label: 'Value', type: 'number', setting: true, default: 0 }, { name: 'unit', label: 'Unit', type: 'text', setting: true, default: 'none' }], outputs: [{ name: 'value', label: 'Value', type: 'number' }] },
  integer: { kind: 'integer', label: 'Whole number', group: 'Inputs', inputs: [{ name: 'value', label: 'Value', type: 'integer', setting: true, default: 1 }], outputs: [{ name: 'value', label: 'Value', type: 'integer' }] },
  choice: { kind: 'choice', label: 'Choice', group: 'Inputs', inputs: [{ name: 'options', label: 'Options', type: 'text', setting: true, default: 'A, B' }, { name: 'value', label: 'Value', type: 'text', setting: true, default: 'A' }], outputs: [{ name: 'value', label: 'Chosen', type: 'text' }] },
  watch: { kind: 'watch', label: 'Watch', group: 'Outputs', inputs: [{ name: 'value', label: 'Value', type: 'any', whole: true }], outputs: [{ name: 'value', label: 'Value', type: 'any' }] },
  sum: { kind: 'sum', label: 'Sum', group: 'Lists', inputs: [{ name: 'list', label: 'Numbers', type: 'number', whole: true }], outputs: [{ name: 'sum', label: 'Sum', type: 'number' }] },
  storey: { kind: 'storey', label: 'Storey', group: 'Inputs', inputs: [{ name: 'value', label: 'Storey', type: 'storey', setting: true, default: '' }], outputs: [{ name: 'value', label: 'Storey', type: 'storey' }] },
};
const specOf = (k: string) => specs[k];

const graph: GraphJson = {
  format: 2,
  nodes: [
    { id: 'len', kind: 'number', x: 0, y: 100, values: { value: 4.5, unit: 'length' }, label: 'Length', palette: { min: 1, max: 10, step: 0.5 } },
    { id: 'n', kind: 'integer', x: 0, y: 0, values: { value: 3 }, palette: { label: 'How many', order: 2 } },
    { id: 'hidden', kind: 'number', x: 0, y: 200, palette: { hide: true } },
    { id: 'pick', kind: 'choice', x: 0, y: 50, values: { options: 'Brick, Concrete', value: 'Concrete' }, palette: { order: 1 } },
    { id: 'total', kind: 'sum', x: 300, y: 0, palette: { show: true } },
    { id: 'w', kind: 'watch', x: 300, y: 50 },
    { id: 'quiet', kind: 'watch', x: 300, y: 80, palette: { hide: true } },
    { id: 'g1', kind: 'group', x: 0, y: 300, graph: { nodes: [{ id: 's', kind: 'storey', x: 0, y: 0 }], wires: [] }, inputs: [], outputs: [] },
  ],
  wires: [],
  tool: { show: true, icon: 'wall' },
};

describe('a graph\'s palette', () => {
  it('makes its input nodes fields, in the order set, then by their place, and its watches and shown nodes outputs', () => {
    const p = paletteOf(graph, specOf);
    expect(p.inputs.map((x) => x.path)).toEqual(['pick', 'n', 'len', 'g1/s']);
    expect(p.inputs.map((x) => x.field.label)).toEqual(['Choice', 'How many', 'Length', 'Storey']);
    expect(p.outputs.map((x) => [x.path, x.label])).toEqual([['total', 'Sum'], ['w', 'Watch']]);
  });

  it('gives each kind its field: a length in metres with its slider limits, a whole number, a choice, the view\'s storey', () => {
    const p = paletteOf(graph, specOf);
    expect(p.inputs[2]!.field).toMatchObject({ kind: 'number', unit: 'm', min: 1, max: 10, step: 0.5, value: 4.5 });
    expect(p.inputs[1]!.field).toMatchObject({ kind: 'number', integer: true, value: 3 });
    expect(p.inputs[0]!.field.choices!.map((c) => c.value)).toEqual(['Brick', 'Concrete']);
    expect(stateOf(p.inputs[0]!.field)).toBe(1);
    expect(p.inputs[3]!.field).toMatchObject({ kind: 'storey', required: false, value: '' });
    expect(fieldOf('x', { id: 'x', kind: 'pick', x: 0, y: 0, values: { ids: ['a', 'b'] } }, undefined)).toMatchObject({ kind: 'element', multiple: true, value: ['a', 'b'] });
    expect(fieldOf('x', { id: 'x', kind: 'type', x: 0, y: 0, values: { class: 'IfcSlabType' } }, undefined)).toMatchObject({ kind: 'type', cls: 'IfcSlabType', required: false });
    expect(fieldOf('x', { id: 'x', kind: 'range', x: 0, y: 0 }, undefined)).toBeNull();
  });

  it('reads a field back as the graph\'s value, within its limits, and sends only what changed', () => {
    const p = paletteOf(graph, specOf);
    const len = p.inputs[2]!.field;
    expect(valueOf(len, '6,5')).toEqual({ value: 6.5 });
    expect(valueOf(len, '12')).toEqual({ error: 'At most 10 m.' });
    expect(valueOf(p.inputs[1]!.field, '2.5')).toEqual({ error: 'Type a whole number.' });
    const r = changedValues(p, { len: '4.5', n: '5', pick: 0, 'g1/s': '' });
    expect(r.values).toEqual({ n: 5, pick: 'Brick' });
    expect(r.wrong).toBeNull();
    expect(changedValues(p, { len: 'x' }).wrong).toEqual({ key: 'len', error: 'Type a number.' });
  });

  it('shows an output as a value, a short list or a small table, with how many in all', () => {
    expect(shown(12.3456)).toEqual({ kind: 'value', text: '12.346' });
    expect(shown([1, 2, { $more: 98 }])).toEqual({ kind: 'list', items: ['1', '2'], total: 100 });
    expect(shown([[1, 'a'], [2, 'b']])).toEqual({ kind: 'table', rows: [['1', 'a'], ['2', 'b']], total: 2 });
    expect(shown(Array.from({ length: 20 }, (_, i) => i)).kind === 'list' && (shown(Array.from({ length: 20 }, (_, i) => i)) as { items: string[] }).items.length).toBe(8);
  });

  it('is a tool of My tools when the graph says so, named after the graph unless it is given a name, in Graphs by default', () => {
    expect(toolOf(graph, 'Row of walls')).toEqual({ name: 'Row of walls', icon: 'wall', group: GRAPHS_GROUP });
    expect(toolOf({ ...graph, tool: { show: true, name: 'Walls', group: 'Mine' } }, 'x')).toEqual({ name: 'Walls', group: 'Mine' });
    expect(toolOf({ ...graph, tool: { name: 'Walls' } }, 'x')).toBeNull();
  });
});
