import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fits, fittingOwn, keepOwn, ownKey, paletteValuesOf, readOwn, withOwn } from './ownValues';
import { changedValues, paletteOf, stateOf } from './palette';
import type { GraphJson, NodeSpec } from './graph';

const specs: Record<string, NodeSpec> = {
  number: { kind: 'number', label: 'Number', group: 'Inputs', inputs: [{ name: 'value', label: 'Value', type: 'number', setting: true, default: 0 }, { name: 'unit', label: 'Unit', type: 'text', setting: true, default: 'none' }], outputs: [{ name: 'value', label: 'Value', type: 'number' }] },
  integer: { kind: 'integer', label: 'Whole number', group: 'Inputs', inputs: [{ name: 'value', label: 'Value', type: 'integer', setting: true, default: 1 }], outputs: [{ name: 'value', label: 'Value', type: 'integer' }] },
  choice: { kind: 'choice', label: 'Choice', group: 'Inputs', inputs: [{ name: 'options', label: 'Options', type: 'text', setting: true, default: 'A, B' }, { name: 'value', label: 'Value', type: 'text', setting: true, default: 'A' }], outputs: [{ name: 'value', label: 'Chosen', type: 'text' }] },
  bool: { kind: 'bool', label: 'Yes or no', group: 'Inputs', inputs: [{ name: 'value', label: 'Value', type: 'bool', setting: true, default: false }], outputs: [{ name: 'value', label: 'Value', type: 'bool' }] },
  storey: { kind: 'storey', label: 'Storey', group: 'Inputs', inputs: [{ name: 'value', label: 'Storey', type: 'storey', setting: true, default: '' }], outputs: [{ name: 'value', label: 'Storey', type: 'storey' }] },
  pick: { kind: 'pick', label: 'Elements picked', group: 'Inputs', inputs: [{ name: 'ids', label: 'Elements', type: 'element', setting: true }], outputs: [{ name: 'ids', label: 'Elements', type: 'element' }] },
};
const specOf = (k: string) => specs[k];

const graph: GraphJson = {
  format: 2,
  nodes: [
    { id: 'len', kind: 'number', x: 0, y: 0, values: { value: 4.5, unit: 'length' }, palette: { min: 1, max: 10 } },
    { id: 'n', kind: 'integer', x: 0, y: 50, values: { value: 3 } },
    { id: 'mat', kind: 'choice', x: 0, y: 100, values: { options: 'Brick, Concrete', value: 'Brick' } },
    { id: 'yes', kind: 'bool', x: 0, y: 150 },
    { id: 'st', kind: 'storey', x: 0, y: 200 },
    { id: 'els', kind: 'pick', x: 0, y: 250, values: { ids: ['w1'] } },
    { id: 'g1', kind: 'group', x: 0, y: 300, graph: { nodes: [{ id: 'inner', kind: 'number', x: 0, y: 0, values: { value: 1 } }], wires: [] }, inputs: [], outputs: [] },
  ],
  wires: [],
  tool: { show: true, values: 'own' },
};

describe('a graph\'s Palette values setting', () => {
  it('is each user\'s own only when the tool says so; shared otherwise, also with no tool', () => {
    expect(paletteValuesOf(graph)).toBe('own');
    expect(paletteValuesOf({ ...graph, tool: { show: true } })).toBe('shared');
    expect(paletteValuesOf({ ...graph, tool: { show: true, values: 'everyone' as never } })).toBe('shared');
    expect(paletteValuesOf({ format: 2, nodes: [], wires: [] })).toBe('shared');
  });
});

describe('each user\'s own values merged with the graph\'s', () => {
  const p = paletteOf(graph, specOf);
  const field = (path: string) => p.inputs.find((i) => i.path === path)!.field;
  const exists = (id: string) => ['w1', 'w2', 's1'].includes(id);

  it('says which values fit their field: kind, whole numbers, least and most, choices offered, storeys still there', () => {
    expect(fits(field('len'), 6)).toBe(true);
    expect(fits(field('len'), 12)).toBe(false);
    expect(fits(field('len'), 0.5)).toBe(false);
    expect(fits(field('len'), '6')).toBe(false);
    expect(fits(field('len'), Number.NaN)).toBe(false);
    expect(fits(field('n'), 2)).toBe(true);
    expect(fits(field('n'), 2.5)).toBe(false);
    expect(fits(field('mat'), 'Concrete')).toBe(true);
    expect(fits(field('mat'), 'Wood')).toBe(false);
    expect(fits(field('yes'), true)).toBe(true);
    expect(fits(field('yes'), 'yes')).toBe(false);
    expect(fits(field('st'), 's1', exists)).toBe(true);
    expect(fits(field('st'), 'gone', exists)).toBe(false);
    expect(fits(field('st'), '', exists)).toBe(true);
    expect(fits(field('els'), ['w2'])).toBe(true);
    expect(fits(field('els'), [1])).toBe(false);
  });

  it('keeps the stored values that still fit and differ from the graph\'s; the others fall back to the graph\'s', () => {
    const stored = {
      len: 6, // fits
      n: 3, // the graph's own: no need to keep
      mat: 'Wood', // a choice no longer offered
      yes: 'no', // the node's kind changed (a text kept for a yes or no)
      st: 'gone', // the storey deleted
      els: ['w1', 'gone', 'w2'], // an element deleted: dropped from the pick
      removed: 7, // the node is gone
      'g1/inner': 2, // inside a group
    };
    expect(fittingOwn(p, stored, exists)).toEqual({ len: 6, els: ['w1', 'w2'], 'g1/inner': 2 });
    expect(fittingOwn(p, { len: 20 }, exists)).toEqual({});
  });

  it('puts the user\'s values in the fields, so Preview and Bake send them as what differs from the graph', () => {
    const own = fittingOwn(p, { len: 6, mat: 'Concrete' }, exists);
    const q = withOwn(p, own);
    expect(q.inputs.find((i) => i.path === 'len')!.field.value).toBe(6);
    expect(q.inputs.find((i) => i.path === 'n')!.field.value).toBe(3);
    const state = Object.fromEntries(q.inputs.map((i) => [i.path, stateOf(i.field)]));
    expect(changedValues(p, state).values).toEqual({ len: 6, mat: 'Concrete' });
    // No own values: the palette as it was.
    expect(withOwn(p, {})).toBe(p);
  });
});

describe('each user\'s own values kept on this computer', () => {
  beforeEach(() => {
    const m = new Map<string, string>();
    vi.stubGlobal('localStorage', {
      getItem: (k: string) => m.get(k) ?? null, setItem: (k: string, v: string) => void m.set(k, String(v)),
      removeItem: (k: string) => void m.delete(k), clear: () => m.clear(),
    });
  });

  it('are kept per project and graph, forgotten when none, and read as none when unreadable', () => {
    keepOwn('P1', 'g', { len: 6 });
    expect(readOwn('P1', 'g')).toEqual({ len: 6 });
    expect(readOwn('P2', 'g')).toEqual({});
    expect(readOwn('P1', 'h')).toEqual({});
    keepOwn('P1', 'g', {});
    expect(localStorage.getItem(ownKey('P1', 'g'))).toBeNull();
    localStorage.setItem(ownKey('P1', 'g'), '[1,2]');
    expect(readOwn('P1', 'g')).toEqual({});
    localStorage.setItem(ownKey('P1', 'g'), '{oops');
    expect(readOwn('P1', 'g')).toEqual({});
  });
});
