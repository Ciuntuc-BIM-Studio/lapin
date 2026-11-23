import { describe, expect, it } from 'vitest';
import { heightArgs, heightOf } from './arrayHeight';

describe('an array along a path: its height', () => {
  it('sends the level and the rise only when the copies stand on something', () => {
    expect(heightArgs('ground', 0.5)).toEqual({ level: 'ground', rise: 0.5 });
    expect(heightArgs('line', 0)).toEqual({ level: 'line', rise: 0 });
    expect(heightArgs('none', 0.5)).toEqual({ level: null, rise: 0 });
    expect(heightArgs('ground', Number.NaN)).toEqual({ level: 'ground', rise: 0 });
  });
  it('reads the level a rule has', () => {
    expect(heightOf('ground')).toBe('ground');
    expect(heightOf('line')).toBe('line');
    expect(heightOf(undefined)).toBe('none');
    expect(heightOf('none')).toBe('none');
  });
});
