import { describe, expect, it } from 'vitest';
import { arcLength, axisLine, middle, pointAt, project, riseThrough, tangentAt } from './arc';

describe('arc', () => {
  const c = { a: { x: 0, y: 0 }, b: { x: 4, y: 0 }, rise: 2 };
  it('measures and walks a half circle as the engine does', () => {
    expect(arcLength(c)).toBeCloseTo(2 * Math.PI, 9);
    expect(middle(c).x).toBeCloseTo(2, 9);
    expect(middle(c).y).toBeCloseTo(2, 9);
    expect(tangentAt(c, 0).y).toBeCloseTo(1, 9);
    expect(project(c, { x: 2, y: 5 })).toBeCloseTo(Math.PI, 9);
    expect(pointAt(c, arcLength(c)).x).toBeCloseTo(4, 9);
    expect(axisLine(c).length).toBe(37);
  });
  it('finds the rise through a third point', () => {
    expect(riseThrough({ x: 0, y: 0 }, { x: 4, y: 0 }, { x: 2, y: 2 })).toBeCloseTo(2, 9);
    expect(riseThrough({ x: 0, y: 0 }, { x: 4, y: 0 }, { x: 2, y: -0.5 })).toBeCloseTo(-0.5, 9);
    expect(riseThrough({ x: 0, y: 0 }, { x: 4, y: 0 }, { x: 9, y: 0 })).toBe(0);
  });
});
