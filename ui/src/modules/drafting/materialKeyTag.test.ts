import { describe, expect, it } from 'vitest';
import { isMaterialField, MATERIAL_FIELDS, TAG_SOURCES, tagContent } from './tag';
import { legendKeyedByDefault } from './sheetAnnotations';

const prefs = { field: 'mark', tagType: '', roomField: 'name', parameter: 'FireRating', buildup: '', keynote: 'code' as const };

describe('material key tags (forum topic 255)', () => {
  it('are a source of the one Tag tool, reading the material key', () => {
    expect(TAG_SOURCES.map((s) => s.source)).toContain('materialKey');
    expect(tagContent({ ...prefs, source: 'materialKey' })).toEqual({ field: 'materialKey' });
    // A tag type chosen for another source does not change what a material key tag reads.
    expect(tagContent({ ...prefs, source: 'materialKey', tagType: 'door-marker' })).toEqual({ field: 'materialKey' });
  });

  it('know the fields that read the material seen where the leader points', () => {
    expect(MATERIAL_FIELDS.map(([k]) => k)).toEqual(['materialKey', 'materialName', 'materialDescription']);
    expect(isMaterialField('materialKey')).toBe(true);
    expect(isMaterialField('MaterialName')).toBe(true);
    expect(isMaterialField('mark')).toBe(false);
  });
});

describe('a material legend', () => {
  it('keys its rows by itself when it lists what the views show or what their tags read', () => {
    expect(legendKeyedByDefault('cut')).toBe(false);
    expect(legendKeyedByDefault('seen')).toBe(true);
    expect(legendKeyedByDefault('tagged')).toBe(true);
  });
});
