import { describe, expect, it } from 'vitest';
import { sortTypes, type ImportedType } from './mapTypes';

const t = (id: string, cls: string, name: string): ImportedType => ({ id, name, class: cls, sourceName: name, count: 1, candidates: [] });

describe('the imported types', () => {
  it('are listed by kind (walls first, then floors, doors, windows…), then by name, other kinds last', () => {
    const rows = sortTypes([t('a', 'IfcDoorType', 'D90'), t('b', 'IfcWallType', 'W300'), t('c', 'IfcPlateType', 'P'), t('d', 'IfcWallType', 'W250'), t('e', 'IfcSlabType', 'S200')]);
    expect(rows.map((r) => r.id)).toEqual(['d', 'b', 'e', 'a', 'c']);
  });
});
