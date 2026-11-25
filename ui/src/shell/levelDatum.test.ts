import { describe, expect, it } from 'vitest';
import { levelDatumPatch, spotDatumChoice, spotDatumPatch, viewDatumChoice, viewDatumPatch } from './levelDatum';

describe('level datums (decision 867)', () => {
  it('sends the project\'s mode, zero storey, tie and terrain', () => {
    expect(levelDatumPatch({ mode: 'absolute' })).toEqual({ mode: 'absolute' });
    // Relative is the default: nothing stored.
    expect(levelDatumPatch({ mode: 'relative' })).toEqual({ mode: null });
    expect(levelDatumPatch({ zeroStorey: '' })).toEqual({ zeroStorey: null });
    expect(levelDatumPatch({ zeroStorey: 's1' })).toEqual({ zeroStorey: 's1' });
    expect(levelDatumPatch({ tie: 650 })).toEqual({ tie: 650 });
    expect(levelDatumPatch({ fromTerrain: 't' })).toEqual({ fromTerrain: 't' });
  });

  it('reads a view\'s own datum and sends it, the project\'s when none', () => {
    expect(viewDatumChoice('both')).toBe('both');
    expect(viewDatumChoice(undefined)).toBe('');
    expect(viewDatumChoice('sideways')).toBe('');
    expect(viewDatumPatch({ id: 'v' }, 'absolute')).toEqual({ id: 'v', datum: 'absolute' });
    expect(viewDatumPatch({ id: 'v' }, '')).toEqual({ id: 'v', datum: 'project' });
    expect(viewDatumPatch({ sheet: 's' }, 'relative')).toEqual({ sheet: 's', datum: 'relative' });
  });

  it('reads the spot levels\' own datum, the view\'s when they differ or say none, and sets them all', () => {
    expect(spotDatumChoice([[1, 2, 'absolute'], [3, 4, 'absolute']])).toBe('absolute');
    expect(spotDatumChoice([[1, 2, 'absolute'], [3, 4]])).toBe('');
    expect(spotDatumChoice([[1, 2]])).toBe('');
    expect(spotDatumChoice(undefined)).toBe('');
    expect(spotDatumPatch('t', 'both')).toEqual({ id: 't', all: true, datum: 'both' });
    expect(spotDatumPatch('t', '')).toEqual({ id: 't', all: true, datum: null });
  });
});
