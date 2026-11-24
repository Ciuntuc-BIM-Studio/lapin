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

describe('measure an area in space (G2 of docs/plan-gaussian-splats.md)', () => {
  it('gives a closed outline its area and perimeter in the plane that best fits it, and that plane\'s tilt', async () => {
    const { outlineArea } = await import('./measure');
    // A wall's patch, 4 × 3 m, standing in the plane y = 2.
    const wall = outlineArea([{ x: 0, y: 2, z: 0 }, { x: 4, y: 2, z: 0 }, { x: 4, y: 2, z: 3 }, { x: 0, y: 2, z: 3 }]);
    expect(wall.area).toBeCloseTo(12, 9);
    expect(wall.perimeter).toBeCloseTo(14, 9);
    expect(wall.tilt).toBeCloseTo(90, 9);
    // A roof's slope at 45°: 4 m along x, √2 · 2 m up the slope.
    const roof = outlineArea([{ x: 0, y: 0, z: 0 }, { x: 4, y: 0, z: 0 }, { x: 4, y: 2, z: 2 }, { x: 0, y: 2, z: 2 }]);
    expect(roof.area).toBeCloseTo(4 * 2 * Math.SQRT2, 9);
    expect(roof.tilt).toBeCloseTo(45, 9);
    // A floor's, whichever way round it is clicked.
    expect(outlineArea([{ x: 0, y: 0, z: 1 }, { x: 0, y: 5, z: 1 }, { x: 2, y: 5, z: 1 }, { x: 2, y: 0, z: 1 }]).area).toBeCloseTo(10, 9);
    // Points picked on a scan are never quite flat: the plane that fits them carries nearly the whole area.
    const rough = outlineArea([{ x: 0, y: 0, z: 0.01 }, { x: 4, y: 0, z: -0.01 }, { x: 4, y: 3, z: 0.01 }, { x: 0, y: 3, z: -0.01 }]);
    expect(rough.area).toBeCloseTo(12, 3);
  });

  it('says the running total while a chain is drawn, and the area once there are three points', async () => {
    const { measureInfo } = await import('./measure');
    expect(measureInfo([{ x: 0, y: 0, z: 0 }], 'distance')).toBeNull();
    expect(measureInfo([{ x: 0, y: 0, z: 0 }, { x: 3, y: 0, z: 4 }, { x: 3, y: 2, z: 4 }], 'distance')).toMatch(/^Total 7/);
    expect(measureInfo([{ x: 0, y: 0, z: 0 }, { x: 2, y: 0, z: 0 }], 'area')).toBeNull();
    expect(measureInfo([{ x: 0, y: 0, z: 0 }, { x: 2, y: 0, z: 0 }, { x: 2, y: 2, z: 0 }], 'area')).toMatch(/^Area 2\.00 m² · perimeter /);
  });
});

describe('calibrate a cloud\'s scale', () => {
  it('sends the two points with their heights (a plan\'s at the level) and the true distance', async () => {
    const { calibrateArgs } = await import('./cloudCalibrate');
    expect(calibrateArgs('c', { x: 1, y: 2 }, { x: 4, y: 6 }, [0.5, 1.5], 0, 10)).toEqual({ id: 'c', a: [1, 2, 0.5], b: [4, 6, 1.5], length: 10 });
    expect(calibrateArgs('c', { x: 1, y: 2 }, { x: 4, y: 6 }, undefined, 3, 10).a).toEqual([1, 2, 3]);
  });
});
