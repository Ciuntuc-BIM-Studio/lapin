import { describe, expect, it } from 'vitest';
import { distToSegment, pointInPolygon, snap } from './snap';

describe('plan snapping', () => {
  it('prefers a model point, then the grid', () => {
    expect(snap({ x: 1.02, y: 0.01 }, { grid: 0.05, points: [{ x: 1, y: 0 }], pickRadius: 0.1 })).toEqual({ point: { x: 1, y: 0 }, kind: 'point' });
    expect(snap({ x: 1.32, y: 0.01 }, { grid: 0.05, points: [{ x: 1, y: 0 }], pickRadius: 0.1 })).toEqual({ point: { x: 1.3, y: 0 }, kind: 'grid' });
  });

  it('squares to the previous point with ortho', () => {
    expect(snap({ x: 3.04, y: 0.4 }, { grid: 0.05, points: [], pickRadius: 0.1, from: { x: 0, y: 0.2 }, ortho: true }).point).toEqual({ x: 3.05, y: 0.2 });
  });

  it('measures and contains', () => {
    expect(distToSegment({ x: 1, y: 1 }, { x: 0, y: 0 }, { x: 2, y: 0 })).toBe(1);
    expect(pointInPolygon({ x: 1, y: 1 }, [{ x: 0, y: 0 }, { x: 2, y: 0 }, { x: 2, y: 2 }, { x: 0, y: 2 }])).toBe(true);
  });
});
