import { describe, expect, it } from 'vitest';
import { sideLengths, withSideLength } from './sides';

const R = [{ x: 0, y: 0 }, { x: 4, y: 0 }, { x: 4, y: 2 }, { x: 0, y: 2 }];

describe('polyline sides', () => {
  it('measures each side, the closing one too', () => {
    expect(sideLengths(R, true)).toEqual([4, 2, 4, 2]);
    expect(sideLengths(R, false)).toEqual([4, 2, 4]);
  });
  it('keeps a rectangle a rectangle', () => {
    expect(withSideLength(R, true, 0, 5)).toEqual([{ x: 0, y: 0 }, { x: 5, y: 0 }, { x: 5, y: 2 }, { x: 0, y: 2 }]);
    expect(withSideLength(R, true, 1, 3)).toEqual([{ x: 0, y: 0 }, { x: 4, y: 0 }, { x: 4, y: 3 }, { x: 0, y: 3 }]);
    // The closing side: its end is the first vertex, the second follows it.
    expect(withSideLength(R, true, 3, 1)).toEqual([{ x: 0, y: 1 }, { x: 4, y: 1 }, { x: 4, y: 2 }, { x: 0, y: 2 }]);
  });
  it('keeps an L an L', () => {
    const L = [{ x: 0, y: 0 }, { x: 4, y: 0 }, { x: 4, y: 1 }, { x: 1, y: 1 }, { x: 1, y: 3 }, { x: 0, y: 3 }];
    expect(withSideLength(L, true, 0, 5)!.slice(0, 4)).toEqual([{ x: 0, y: 0 }, { x: 5, y: 0 }, { x: 5, y: 1 }, { x: 1, y: 1 }]);
  });
  it('moves an open polyline on past the side when nothing takes the change', () => {
    const P = [{ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 1, y: 1 }];
    expect(withSideLength(P, false, 0, 2)).toEqual([{ x: 0, y: 0 }, { x: 2, y: 0 }, { x: 2, y: 1 }]);
    expect(withSideLength(P, false, 5, 2)).toBeNull();
    expect(withSideLength(P, false, 0, 0)).toBeNull();
  });
});
