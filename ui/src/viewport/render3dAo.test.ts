import { describe, expect, it } from 'vitest';
import { AO_MAX_PX, AO_RADIUS, aoRadius, devicePxPerMetre } from './render3d';

describe('the occlusion reach (forum topic 162)', () => {
  it('is 0.6 m at a building’s distance', () => {
    expect(aoRadius(devicePxPerMetre(1500, { fovDeg: 45, distance: 33 }))).toBe(AO_RADIUS);
  });

  it('never spans more than AO_MAX_PX on screen when zoomed in close', () => {
    for (const distance of [5, 1, 0.3, 0.01]) {
      const ppm = devicePxPerMetre(1500, { fovDeg: 45, distance });
      expect(aoRadius(ppm) * ppm).toBeLessThanOrEqual(AO_MAX_PX + 1e-9);
    }
  });

  it('works for an orthographic view, and falls back to 0.6 m when the scale is unknown', () => {
    const ppm = devicePxPerMetre(1000, { metresHigh: 2 });
    expect(aoRadius(ppm)).toBeCloseTo(AO_MAX_PX / 500, 9);
    expect(aoRadius(NaN)).toBe(AO_RADIUS);
  });
});
