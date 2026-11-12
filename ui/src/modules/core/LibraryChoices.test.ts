import { describe, expect, it } from 'vitest';
import { defaultChoices, MATERIAL_CHOICES, TYPE_CHOICES } from './LibraryChoices';

describe('a type library\'s choices (a tester\'s report: nothing lost in silence)', () => {
  it('start with every material kept as the project\'s and every type as the import does by default', () => {
    const c = defaultChoices([{ name: 'Brick', differs: ['section hatch'] }], [{ key: 'ref-1', name: 'WT1', differs: ['layers'] }], 'both');
    expect(c).toEqual({ materials: { Brick: 'keep' }, types: { 'ref-1': 'both' } });
    expect(defaultChoices([], [{ key: 'WT1', name: 'WT1', differs: ['parameters'] }], 'update').types).toEqual({ WT1: 'update' });
  });

  it('offer what the engine takes', () => {
    expect(MATERIAL_CHOICES.map(([v]) => v)).toEqual(['keep', 'update', 'both']);
    expect(TYPE_CHOICES.map(([v]) => v)).toEqual(['update', 'both', 'skip']);
  });
});
