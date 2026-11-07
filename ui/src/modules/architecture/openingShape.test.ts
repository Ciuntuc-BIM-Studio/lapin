import { describe, expect, it } from 'vitest';
import { area, outline, riseOf } from './openingShape';

describe('a wall opening’s outline (report #114)', () => {
  it('is its rectangle, or its head, taken in by the reveal’s finish', () => {
    expect(area(outline('rect', 1, 2.1, null))).toBeCloseTo(2.1, 9);
    expect(area(outline('rect', 1, 2.1, null, 0.015, 0))).toBeCloseTo((1 - 0.03) * (2.1 - 0.015), 9);
    // A semicircle: the jambs to the spring line, a half circle over them.
    expect(area(outline('round', 1, 2.1, null))).toBeCloseTo(1 * 1.6 + Math.PI * 0.25 / 2, 2);
    expect(riseOf('segmental', 1.2, 2, null)).toBeCloseTo(0.2, 9);
    expect(riseOf('radiused', 1, 2, 0.8)).toBe(0.5);
    const seg = outline('segmental', 1.2, 2, null);
    expect(Math.max(...seg.map((p) => p.y))).toBeCloseTo(2, 6);
    expect(area(outline('circle', 1, 1, null))).toBeCloseTo(Math.PI / 4, 2);
  });
});
