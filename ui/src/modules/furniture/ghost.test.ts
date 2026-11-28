import { describe, expect, it } from 'vitest';
import { ghostLines, outlineOf } from './ghost';

const rect = { closed: true, points: [-0.3, -0.2, 0.3, -0.2, 0.3, 0.2, -0.3, 0.2] };
const near = (a: { x: number; y: number }, x: number, y: number) => { expect(a.x).toBeCloseTo(x, 9); expect(a.y).toBeCloseTo(y, 9); };

describe('furniture ghost', () => {
  it('places a 0.6 x 0.4 rectangle at (1, 2)', () => {
    const [l] = ghostLines([rect], { x: 1, y: 2 }, 0);
    expect(l).toHaveLength(5);
    near(l[0], 0.7, 1.8); near(l[1], 1.3, 1.8); near(l[2], 1.3, 2.2); near(l[3], 0.7, 2.2); near(l[4], 0.7, 1.8);
  });
  it('turns it 90 degrees counter-clockwise (the front -y goes to +x)', () => {
    const [l] = ghostLines([rect], { x: 1, y: 2 }, 90);
    near(l[0], 1.2, 1.7); near(l[1], 1.2, 2.3); near(l[2], 0.8, 2.3); near(l[3], 0.8, 1.7);
  });
  it('keeps an open item open', () => {
    const [l] = ghostLines([{ closed: false, points: [0, 0, 1, 0] }], { x: 0, y: 0 }, 0);
    expect(l).toHaveLength(2);
  });
  it('gives nothing for no input', () => {
    expect(ghostLines([], { x: 0, y: 0 })).toEqual([]);
  });
  it('falls back to a width x depth rectangle', () => {
    const items = outlineOf({ plan: [], width: 0.6, depth: 0.4 });
    expect(ghostLines(items, { x: 1, y: 2 })[0]).toHaveLength(5);
    expect(outlineOf({ plan: [], width: 0, depth: 0 })).toEqual([]);
  });
});
