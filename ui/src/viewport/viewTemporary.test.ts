import { describe, expect, it } from 'vitest';
import { tempArgs, tempOn } from './viewTemporary';

describe('a view\'s temporary hide and isolate (forum topic 73)', () => {
  it('asks the drawing with only what is on', () => {
    expect(tempArgs({ hidden: [], isolated: null, reveal: false })).toEqual({});
    expect(tempArgs({ hidden: ['a'], isolated: null, reveal: false })).toEqual({ temp: { hide: ['a'] } });
    expect(tempArgs({ hidden: [], isolated: ['b'], reveal: true })).toEqual({ temp: { isolate: ['b'] }, reveal: true });
  });

  it('is on while it hides, isolates or reveals anything', () => {
    expect(tempOn({ hidden: [], isolated: null, reveal: false })).toBe(false);
    expect(tempOn({ hidden: [], isolated: [], reveal: false })).toBe(true);
    expect(tempOn({ hidden: [], isolated: null, reveal: true })).toBe(true);
  });
});
