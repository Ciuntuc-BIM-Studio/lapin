import { describe, expect, it } from 'vitest';
import { batterText, cleanPath, segmentRows } from './retainingWall';

describe("a retaining wall's segments", () => {
  it('lists each length and turn from the one before, the first as a direction from east', () => {
    const rows = segmentRows([[0, 0], [10, 0], [10, 6], [10, 6], [14, 2]], [{ frontBatter: 10 }, null, { warp: true, frontBatterEnd: 0.667, junk: 'x' }]);
    expect(rows.map((r) => Math.round(r.length * 1000) / 1000)).toEqual([10, 6, 5.657]);
    expect(rows.map((r) => Math.round(r.turn))).toEqual([0, 90, -135]);
    expect(rows[0].own).toEqual({ frontBatter: 10 });
    expect(rows[1].own).toEqual({});
    expect(rows[2].own).toEqual({ warp: true, frontBatterEnd: 0.667 });
  });

  it('drops repeated points as the engine does, and reads batters as n:1', () => {
    expect(cleanPath([[0, 0], [0, 0.0000001], [1, 0], 'x'])).toEqual([[0, 0], [1, 0]]);
    expect(batterText(10)).toBe('10:1');
    expect(batterText(0)).toBe('upright');
    expect(batterText(undefined)).toBe('upright');
  });
});
