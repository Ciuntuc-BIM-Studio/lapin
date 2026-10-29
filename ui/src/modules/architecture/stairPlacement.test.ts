import { describe, expect, it } from 'vitest';
import { dirOf, footFromTop, levelText } from './stairPlacement';

describe('a stair’s levels', () => {
  it('are written as drawings write them', () => {
    expect(levelText(-0.45)).toBe('−0.45');
    expect(levelText(0)).toBe('±0.00');
    expect(levelText(-0.001)).toBe('±0.00');
    expect(levelText(3)).toBe('+3.00');
  });
});

describe('placing entrance steps by their top', () => {
  it('puts the foot one run down from the porch edge, climbing back towards it', () => {
    // The porch edge at (3, -1.7); the way down is −Y; three risers have a run of 0.70 m.
    const { foot, up } = footFromTop({ x: 3, y: -1.7 }, { x: 3, y: -5 }, 0.7);
    expect(foot.x).toBeCloseTo(3, 9);
    expect(foot.y).toBeCloseTo(-2.4, 9);
    expect(up.x).toBeCloseTo(0, 9);
    expect(up.y).toBeCloseTo(1, 9);
  });

  it('works at any angle, and a run of nothing (a single riser) leaves the foot on the edge', () => {
    const { foot, up } = footFromTop({ x: 0, y: 0 }, { x: 1, y: 1 }, Math.SQRT2);
    expect(foot.x).toBeCloseTo(1, 9);
    expect(foot.y).toBeCloseTo(1, 9);
    expect(Math.hypot(up.x, up.y)).toBeCloseTo(1, 9);
    expect(footFromTop({ x: 2, y: 5 }, { x: 4, y: 5 }, 0).foot).toEqual({ x: 2, y: 5 });
    expect(dirOf({ x: 1, y: 1 }, { x: 1, y: 1 })).toEqual({ x: 1, y: 0 });
  });
});
