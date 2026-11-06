import { describe, expect, it } from 'vitest';
import { partLook } from './looks';

describe('partLook', () => {
  const parts = { 'layer:0': { hidden: true }, deck: { color: '#e8a33d' }, 'timber:': { hidden: true }, 'timber:rafter:': { color: '#123456' } };

  it('finds a part by its key, else by the longest prefix ending in ":"', () => {
    expect(partLook(parts, 'layer:0')).toEqual({ hidden: true });
    expect(partLook(parts, 'deck')).toEqual({ color: '#e8a33d' });
    expect(partLook(parts, 'timber:purlin:2')).toEqual({ hidden: true });
    expect(partLook(parts, 'timber:rafter:7')).toEqual({ color: '#123456' });
    // A key that is not a prefix takes nothing of another's: the element's look.
    expect(partLook(parts, 'layer:01')).toBeUndefined();
    expect(partLook(parts, 'covering')).toBeUndefined();
    expect(partLook(undefined, 'deck')).toBeUndefined();
  });
});
