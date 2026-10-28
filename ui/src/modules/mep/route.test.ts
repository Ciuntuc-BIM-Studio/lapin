import { describe, expect, it } from 'vitest';
import { bypass, legAt, onLeg, sideOf, squareOff } from './route';

describe('a detour aimed on a run', () => {
  const pts = [{ x: 0, y: 0 }, { x: 6, y: 0 }, { x: 6, y: 4 }];

  it('finds the leg under the click and keeps the second point on it', () => {
    const l = legAt(pts, { x: 2, y: 0.1 })!;
    expect(l.leg).toBe(0);
    expect(onLeg(l, { x: 3, y: 1.2 })).toEqual({ x: 3, y: 0 });
    expect(onLeg(l, { x: 9, y: 0 })).toEqual({ x: 6, y: 0 });
  });

  it('takes the side square off the leg, and the bypass out, along and back', () => {
    const l = legAt(pts, { x: 2, y: 0 })!;
    expect(sideOf(l, { x: 2, y: -0.5 })).toBeCloseTo(-0.5);
    expect(squareOff(l, { x: 3, y: 0 }, { x: 4, y: -0.5 })).toEqual({ x: 3, y: -0.5 });
    const b = bypass(l, { x: 1, y: 0 }, { x: 3, y: 0 }, { x: 3, y: -0.5 });
    expect(b.map((p) => [p.x, +p.y.toFixed(9)])).toEqual([[1, 0], [1, -0.5], [3, -0.5], [3, 0]]);
  });
});
