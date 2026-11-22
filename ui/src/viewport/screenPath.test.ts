import { describe, expect, it } from 'vitest';
import { screenPath } from './screenPath';

describe('screenPath', () => {
  it('draws an open and a closed path', () => {
    expect(screenPath([{ x: 1, y: 2 }, { x: 3.04, y: 4 }], false)).toBe('M1.0 2.0L3.0 4.0');
    expect(screenPath([{ x: 1, y: 2 }, { x: 3, y: 4 }, { x: 5, y: 6 }], true)).toBe('M1.0 2.0L3.0 4.0L5.0 6.0Z');
  });
  it('gives null for too few points or one not projected', () => {
    expect(screenPath([{ x: 1, y: 2 }], true)).toBeNull();
    expect(screenPath([{ x: 1, y: 2 }, null], false)).toBeNull();
  });
});
