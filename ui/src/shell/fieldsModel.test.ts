import { describe, expect, it } from 'vitest';
import { filterGroups, insertAt, modeOf, pieceFor, type FieldGroup } from './fieldsModel';

const groups: FieldGroup[] = [
  { id: 'element', label: 'This element', scope: null, entityId: 'w', entityName: 'Wall', entityClass: 'IfcWall', fields: [
    { token: 'name', label: 'name', value: 'Wall 1', description: 'Its name' },
    { token: 'Fire rating', label: 'Fire rating', value: 'EI 60', description: 'Your parameter' },
  ] },
  { id: 'quantities', label: 'Its quantities', scope: null, entityId: 'w', entityName: 'Wall', entityClass: 'IfcWall', fields: [
    { token: 'NetArea', label: 'Net area', value: '12 m²', description: 'A quantity', formulaOnly: true },
  ] },
  { id: 'storey', label: 'Its storey', scope: 'storey', entityId: 's', entityName: 'Ground floor', entityClass: 'IfcBuildingStorey', fields: [
    { token: 'storey.code', label: 'code', value: 'K1', description: 'Its storey: a parameter' },
  ] },
];

describe('the Parameters inspector', () => {
  it('keeps the fields matching every word typed, in their groups', () => {
    expect(filterGroups(groups, '')).toBe(groups);
    expect(filterGroups(groups, 'K1').map((g) => g.id)).toEqual(['storey']);
    expect(filterGroups(groups, 'fire 60')[0].fields.map((f) => f.token)).toEqual(['Fire rating']);
    expect(filterGroups(groups, 'storey code')[0].fields[0].token).toBe('storey.code');
    expect(filterGroups(groups, 'nothing like it')).toEqual([]);
  });

  it('writes {token} into a text, a quantity as a formula, and the bare name into an expression', () => {
    expect(pieceFor({ token: 'storey.code' }, 'text')).toBe('{storey.code}');
    expect(pieceFor({ token: 'NetArea', formulaOnly: true }, 'text')).toBe('{= NetArea}');
    expect(pieceFor({ token: 'Fire rating' }, 'formula')).toBe('FireRating');
    expect(pieceFor({ token: 'storey.Floor height' }, 'formula')).toBe('storey.FloorHeight');
    expect(modeOf('{storey.code}.')).toBe('text');
    expect(modeOf('', 'text')).toBe('text');
    expect(modeOf('Height * 2', 'number')).toBe('formula');
  });

  it('puts the piece where the caret is, over what is selected', () => {
    expect(insertAt('A-', 2, 2, '{number}')).toEqual({ text: 'A-{number}', caret: 10 });
    expect(insertAt('x * 2', 0, 1, 'Height')).toEqual({ text: 'Height * 2', caret: 6 });
    expect(insertAt('', 5, 9, '{name}')).toEqual({ text: '{name}', caret: 6 });
  });
});
