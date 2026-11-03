import { describe, expect, it } from 'vitest';
import { PROFILES, profileArea, profilePath, profilePoints } from './skirtingProfile';

describe('a skirting board’s section', () => {
  it('fills its type’s height and thickness, anticlockwise, in every profile', () => {
    for (const { id } of PROFILES) {
      const spec = { profile: id, height: 0.08, thickness: 0.012 };
      const p = profilePoints(spec);
      expect(Math.max(...p.map((q) => q[0]))).toBeCloseTo(0.012, 9);
      expect(Math.max(...p.map((q) => q[1]))).toBeCloseTo(0.08, 9);
      expect(Math.min(...p.map((q) => q[0]))).toBeCloseTo(0, 9);
      expect(profileArea(spec)).toBeLessThanOrEqual(0.08 * 0.012 + 1e-12);
    }
  });

  it('is a full rectangle when flush, less where its top is shaped', () => {
    expect(profileArea({ profile: 'flush', height: 0.08, thickness: 0.01 })).toBeCloseTo(0.0008, 12);
    expect(profileArea({ profile: 'bevelled', height: 0.08, thickness: 0.01 })).toBeLessThan(0.0008);
    expect(profileArea({ profile: 'cove', height: 0.06, thickness: 0.01 })).toBeLessThan(0.06 * 0.01 * 0.4);
  });

  it('is drawn in its box at one scale, the floor at the bottom and the wall on the left', () => {
    const path = profilePath({ profile: 'flush', height: 0.08, thickness: 0.01 }, 100, 100, 10);
    // 80 px for 80 mm high: the board 10 px thick.
    expect(path).toBe('M10 90 L20 90 L20 10 L10 10 Z');
  });
});
