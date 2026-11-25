import { describe, expect, it } from 'vitest';
import { levelDatumPatch, viewDatumChoice, viewDatumPatch } from './levelDatum';

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
});
