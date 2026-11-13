import { describe, expect, it } from 'vitest';
import {
  FLEX_OFF, cellsOf, defaultChanges, flexChanged, flexKeepPlan, flexPreviewValues, flexSet, freeKey, groupParams, moveTarget, newParam, parseCell,
  previewVolume, publishedRows, type ParamDto,
} from './partParamModel';

const p = (key: string, over: Partial<ParamDto> = {}): ParamDto => ({ key, label: key, kind: 'length', default: 1, value: 1, instance: true, published: true, ...over });
const len = (t: string) => { const n = Number(t); return Number.isFinite(n) ? n / 1000 : null; };
const table = [p('W', { value: 1.6, default: 1.2, group: 'Size' }), p('D', { group: 'Size' }), p('H', { instance: false }), p('Half', { formula: 'W / 2', derived: true, value: 0.8, published: false }), p('Shelf', { kind: 'bool', default: 1, value: 0, group: 'Options' })];

describe('the parameter table', () => {
  it('groups the rows, the ungrouped first, each group in the engine\'s order', () => {
    expect(groupParams(table).map((g) => [g.group, g.rows.map((r) => r.key)])).toEqual([['', ['H', 'Half']], ['Size', ['W', 'D']], ['Options', ['Shelf']]]);
  });

  it('lets an instance value be edited per object, a type parameter only by its default, a derived one by its formula', () => {
    expect(cellsOf(table[0]!)).toEqual({ defaultCell: 'value', ownCell: 'edit', showsOwn: true });
    expect(cellsOf(table[2]!)).toEqual({ defaultCell: 'value', ownCell: 'none', showsOwn: false });
    expect(cellsOf(table[3]!)).toEqual({ defaultCell: 'formula', ownCell: 'none', showsOwn: false });
  });

  it('reads a cell as a formula with =, else a value of its kind', () => {
    expect(parseCell('= W - 2*L', 'length', len)).toEqual({ formula: 'W - 2*L' });
    expect(parseCell('=', 'length', len)).toBe('bad');
    expect(parseCell('1600', 'length', len)).toEqual({ value: 1.6 });
    expect(parseCell('x', 'length', len)).toBe('bad');
    expect(parseCell('45,5', 'angle', len)).toEqual({ value: 45.5 });
    expect(parseCell('2.6', 'integer', len)).toEqual({ value: 3 });
    expect(parseCell('yes', 'bool', len)).toEqual({ value: 1 });
    expect(parseCell('No', 'bool', len)).toEqual({ value: 0 });
    expect(parseCell('maybe', 'bool', len)).toBe('bad');
    expect(parseCell(' oak ', 'text', len)).toEqual({ value: ' oak ' });
    expect(parseCell('', 'number', len)).toEqual({ clear: true });
  });

  it('turns a typed default into the changes of updatePartParam', () => {
    expect(defaultChanges({ value: 2 }, false)).toEqual({ default: 2 });
    expect(defaultChanges({ value: 2 }, true)).toEqual({ default: 2, formula: null });
    expect(defaultChanges({ formula: 'W/2' }, false)).toEqual({ formula: 'W/2' });
    expect(defaultChanges('bad', false)).toBeNull();
    expect(defaultChanges({ clear: true }, true)).toBeNull();
  });

  it('moves a row by one place, never past the ends', () => {
    expect(moveTarget(table, 'W', -1)).toBeNull();
    expect(moveTarget(table, 'W', 1)).toBe(1);
    expect(moveTarget(table, 'Shelf', 1)).toBeNull();
    expect(moveTarget(table, 'none', 1)).toBeNull();
  });

  it('names a new parameter freely, by its kind', () => {
    expect(freeKey([p('P1'), p('P2')])).toBe('P3');
    expect(newParam([p('L1')], 'length')).toMatchObject({ key: 'L2', kind: 'length', instance: true });
    expect(newParam([], 'bool')).toMatchObject({ key: 'B1', default: 0 });
  });
});

describe('flex', () => {
  it('changes only what differs, and drops a value put back', () => {
    let s = flexSet(table, FLEX_OFF, 'W', 2);
    s = flexSet(table, { ...s, on: true }, 'H', 1.5);
    expect(flexChanged(table, s)).toEqual(['W', 'H']);
    expect(flexSet(table, s, 'W', 1.6).values).toEqual({ H: 1.5 });
    expect(flexPreviewValues(table, FLEX_OFF)).toBeNull();
    expect(flexPreviewValues(table, s)).toEqual({ W: 2, H: 1.5 });
  });

  it('never flexes a derived parameter', () => {
    expect(flexChanged(table, { on: true, values: { Half: 3 } })).toEqual([]);
  });

  it('keeps instance values on the object and type values as the part\'s default', () => {
    const s = { on: true, values: { W: 2, H: 0.9 } };
    expect(flexKeepPlan(table, s)).toEqual({ instance: { W: 2 }, defaults: [{ key: 'H', value: 0.9 }] });
  });

  it('adds the bodies\' volumes, and none for a refusal', () => {
    expect(previewVolume({ ok: true, bodies: [{ name: 'a', edges: [], volume: 0.25 }, { name: 'b', edges: [], volume: 0.5 }] })).toBe(0.75);
    expect(previewVolume({ ok: false, reason: 'depth is not positive' })).toBeNull();
    expect(previewVolume(null)).toBeNull();
  });
});

describe('the published parameters in Properties', () => {
  it('shows only the published, an instance one editable, a type one and a derived one read-only', () => {
    const rows = publishedRows([...table, p('Hid', { published: false }), p('Dx', { formula: 'W*2', derived: true, published: true, value: 3.2 })], [
      { key: 'W', value: 1.6, source: 'instance' }, { key: 'Dx', value: 3.2, source: 'formula', problem: 'cycle' },
    ]);
    expect(rows.map((r) => [r.key, r.mode])).toEqual([['W', 'own'], ['D', 'own'], ['H', 'type'], ['Shelf', 'own'], ['Dx', 'derived']]);
    expect(rows.find((r) => r.key === 'Dx')).toMatchObject({ formula: 'W*2', problem: 'cycle' });
  });
});
