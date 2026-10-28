import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../engine/protocol';
import { filterSets, importedSets } from './ifcProperties';

const node = (props: Record<string, unknown>) => ({ id: 'x', type: 'IfcSpace', name: 'Office', props } as unknown as NodeDto);

describe('the IFC properties an element brought', () => {
  it('reads a recognised element\'s sets and a reference\'s', () => {
    const room = node({ imported: { properties: { Pset_SpaceCommon: { Reference: '101' }, BaseQuantities: { NetFloorArea: '20' }, Empty: {} } } });
    expect(importedSets(room)).toEqual([
      { name: 'Pset_SpaceCommon', props: [['Reference', '101']] },
      { name: 'BaseQuantities', props: [['NetFloorArea', '20']] },
    ]);
    expect(importedSets(node({ properties: { Pset_WallCommon: { IsExternal: 'yes' } } }))).toEqual([{ name: 'Pset_WallCommon', props: [['IsExternal', 'yes']] }]);
    expect(importedSets(node({ number: '101' }))).toEqual([]);
  });

  it('filters by set, name or value', () => {
    const sets = importedSets(node({ imported: { properties: { 'PSet_Revit_Identity Data': { Department: 'Administration', Occupant: 'ACME' }, Pset_SpaceCommon: { Reference: '101' } } } }));
    expect(filterSets(sets, 'acme')).toEqual([{ name: 'PSet_Revit_Identity Data', props: [['Occupant', 'ACME']] }]);
    expect(filterSets(sets, 'spacecommon')).toEqual([{ name: 'Pset_SpaceCommon', props: [['Reference', '101']] }]);
    expect(filterSets(sets, ' ')).toBe(sets);
  });
});
