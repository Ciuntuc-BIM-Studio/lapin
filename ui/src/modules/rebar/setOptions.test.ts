import { describe, expect, it } from 'vitest';
import { directionChoice, setArgs } from './setOptions';

describe('the rebar set tools\' options', () => {
  it('send only what was chosen', () => {
    expect(setArgs({ layer: '', direction: '', diameter: 0, step: 0 })).toEqual({});
    expect(setArgs({ layer: 'T1', direction: 'y', diameter: 0.012, step: 0.15 })).toEqual({ layer: 'T1', direction: 'y', diameter: 0.012, step: 0.15 });
  });
  it('name a set\'s direction as a slab or a wall does', () => {
    expect(directionChoice(0, false)).toBe('x');
    expect(directionChoice(90, false)).toBe('y');
    expect(directionChoice(180, false)).toBe('x');
    expect(directionChoice(30, false)).toBe('angle');
    expect(directionChoice(0, true)).toBe('horizontal');
    expect(directionChoice(90, true)).toBe('vertical');
  });
});
