import { describe, expect, it } from 'vitest';
import { cleanOptions, conditionValue, moved, searchGroups, type FieldGroupDef } from './fieldSchedule';

describe('field schedule options', () => {
  it('reads a number as a number and keeps text', () => {
    expect(conditionValue('2,5')).toBe(2.5);
    expect(conditionValue(' L1 ')).toBe('L1');
    expect(conditionValue('')).toBe('');
  });

  it('leaves out what is empty, and the value of an operator that takes none', () => {
    const o = cleanOptions({
      filter: { classes: ['IfcWall'], types: [], conditions: [{ field: 'Pset_X.Etapa', op: 'missing', value: 'x' }, { field: '', op: '=' }, { field: 'Length', op: '>', value: 2.5 }] },
      groupBy: ['storey.name', ' '],
      columns: [{ field: 'Length', aggregate: 'sum', unit: 'm', decimals: 12 }, { formula: 'NetSideArea * 1.1', heading: 'Area' }, { field: '' }],
    });
    expect(o.filter).toEqual({ classes: ['IfcWall'], conditions: [{ field: 'Pset_X.Etapa', op: 'missing' }, { field: 'Length', op: '>', value: 2.5 }] });
    expect(o.groupBy).toEqual(['storey.name']);
    expect(o.columns).toEqual([{ field: 'Length', aggregate: 'sum', unit: 'm', decimals: 9 }, { formula: 'NetSideArea * 1.1', heading: 'Area' }]);
    expect(o.totals).toBe(true);
  });

  it('keeps a column\'s level format (decision 867), and nothing it does not know', () => {
    const o = cleanOptions({ columns: [{ field: 'storey.elevation', level: 'absolute' }, { field: 'storey.elevation', level: 'project' }, { field: 'storey.elevation', level: 'up' as never }, { field: 'Length' }] });
    expect(o.columns).toEqual([{ field: 'storey.elevation', level: 'absolute' }, { field: 'storey.elevation', level: 'project' }, { field: 'storey.elevation' }, { field: 'Length' }]);
  });

  it('moves an item one place, and stays at the ends', () => {
    expect(moved([1, 2, 3], 1, -1)).toEqual([2, 1, 3]);
    expect(moved([1, 2, 3], 0, -1)).toEqual([1, 2, 3]);
    expect(moved([1, 2, 3], 2, 1)).toEqual([1, 2, 3]);
  });

  it('searches the fields by every word', () => {
    const g: FieldGroupDef[] = [{ id: 'pset:Pset_Firma', label: 'Pset_Firma', fields: [
      { token: 'Pset_Firma.CodLucrare', label: 'CodLucrare', kind: 'text', count: 3, examples: ['L1'] },
      { token: 'Pset_Firma.Etapa', label: 'Etapa', kind: 'text', count: 3, examples: ['Faza 2'] }] }];
    expect(searchGroups(g, 'firma faza')[0].fields.map((f) => f.token)).toEqual(['Pset_Firma.Etapa']);
    expect(searchGroups(g, 'nothing')).toEqual([]);
  });
});
