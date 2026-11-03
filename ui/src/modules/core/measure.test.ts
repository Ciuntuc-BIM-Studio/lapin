import { describe, expect, it } from 'vitest';
import { measured } from './measure';

describe('measure', () => {
  it('adds the legs and gives the area the points enclose', () => {
    const r = measured([{ x: 0, y: 0 }, { x: 8, y: 0 }, { x: 8, y: 6 }]);
    expect(r.legs).toEqual([8, 6]);
    expect(r.total).toBe(14);
    expect(r.area).toBe(24);
    expect(measured([{ x: 0, y: 0 }, { x: 3, y: 4 }]).area).toBeNull();
  });
});
