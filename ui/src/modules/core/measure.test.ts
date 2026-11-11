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

describe('measure in 3D', () => {
  it('takes the true length of a leg between points at different heights', async () => {
    const { measured3d } = await import('./measure');
    const r = measured3d([{ x: 0, y: 0 }, { x: 3, y: 0 }], [0, 4]);
    expect(r.legs[0]).toBe(5);
    expect(r.rise).toBe(4);
    expect(measured3d([{ x: 0, y: 0 }, { x: 3, y: 4 }, { x: 3, y: 4 }], [0, 0, 12]).total).toBe(17);
  });
});
