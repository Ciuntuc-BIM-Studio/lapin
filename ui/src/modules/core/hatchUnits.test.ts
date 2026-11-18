import { describe, expect, it } from 'vitest';
import { effectiveUnits, groupByUnits, scaleLabel, unitsOf } from './hatchUnits';
import { parseMinSpacing } from './viewStyles';

describe('hatch units', () => {
  it('groups drafting before real size, leaving empty groups out', () => {
    const g = groupByUnits([{ name: 'A', units: 'model' as const }, { name: 'B', units: 'paper' as const }, { name: 'C' }]);
    expect(g.map((x) => [x.units, x.items.map((i) => i.name)])).toEqual([['paper', ['B', 'C']], ['model', ['A']]]);
    expect(groupByUnits([{ name: 'A', units: 'model' as const }]).map((x) => x.units)).toEqual(['model']);
  });
  it('takes the material\'s choice over the pattern\'s', () => {
    expect(effectiveUnits(undefined, { name: 'X', units: 'model' })).toBe('model');
    expect(effectiveUnits('paper', { name: 'X', units: 'model' })).toBe('paper');
    expect(unitsOf(undefined)).toBe('paper');
  });
  it('words the scale by units', () => {
    expect(scaleLabel('model')).toContain('real size');
    expect(scaleLabel('paper')).toContain('×');
  });
  it('parses the minimum spacing', () => {
    expect(parseMinSpacing('0,7')).toBe(0.7);
    expect(parseMinSpacing('0')).toBe(0);
    expect(parseMinSpacing('-1')).toBeNull();
    expect(parseMinSpacing('')).toBeNull();
  });
});
