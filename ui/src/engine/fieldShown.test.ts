import { describe, expect, it } from 'vitest';
import { fieldShown, type FieldDescriptor } from './protocol';

describe('fieldShown', () => {
  const deck: Pick<FieldDescriptor, 'showIf'> = { showIf: [{ shape: ['flat', 'barrel'] }, { structure: ['none'] }] };
  const rafters: Pick<FieldDescriptor, 'showIf'> = { showIf: [{ shape: ['!flat', '!barrel'], structure: ['!none'] }] };

  it('shows a field with no condition always', () => {
    expect(fieldShown({}, {})).toBe(true);
    expect(fieldShown({ showIf: null }, { shape: 'flat' })).toBe(true);
  });

  it('shows the deck without framing and the rafters with it', () => {
    expect(fieldShown(deck, { shape: 'flat', structure: 'rafters' })).toBe(true);
    expect(fieldShown(deck, { shape: 'gable', structure: 'none' })).toBe(true);
    expect(fieldShown(deck, { shape: 'gable', structure: 'rafters' })).toBe(false);
    expect(fieldShown(rafters, { shape: 'gable', structure: 'trusses' })).toBe(true);
    expect(fieldShown(rafters, { shape: 'barrel', structure: 'trusses' })).toBe(false);
    expect(fieldShown(rafters, { shape: 'hip', structure: 'none' })).toBe(false);
    // A missing attribute reads as "": a roof without a structure attribute is framed, as the engine reads it.
    expect(fieldShown(rafters, { shape: 'hip' })).toBe(true);
    expect(fieldShown(deck, { shape: 'hip' })).toBe(false);
    // Booleans and numbers by their text.
    expect(fieldShown({ showIf: [{ shedFlip: ['true'] }] }, { shedFlip: true })).toBe(true);
    expect(fieldShown({ showIf: [{ purlins: ['0'] }] }, { purlins: 0 })).toBe(true);
  });
});
