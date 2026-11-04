import { describe, expect, it } from 'vitest';
import { angleOf, axesOf, lengthOf, setSpacing, spacingOf } from './gridAxes';

const axes = axesOf([
  { tag: '1', a: [0, -1], b: [0, 11] }, { tag: '2', a: [6, -1], b: [6, 11] }, { tag: '3', a: [12, -1], b: [12, 11] },
  { tag: 'A', a: [-1, 0], b: [13, 0] }, { tag: 'B', a: [-1, 5], b: [13, 5] },
]);

describe('a grid axis in the properties panel (report #68)', () => {
  it('measures its length, angle and spacing from the parallel axis before it', () => {
    expect(lengthOf(axes[0])).toBe(12);
    expect(angleOf(axes[0])).toBe(90);
    expect(angleOf(axes[3])).toBe(0);
    expect(spacingOf(axes, '1')).toBeNull();
    expect(spacingOf(axes, '2')).toEqual({ prev: '1', spacing: 6 });
    expect(spacingOf(axes, 'B')).toEqual({ prev: 'A', spacing: 5 });
  });
  it('a new spacing moves the axis, and with it those beyond', () => {
    const alone = setSpacing('g', axes, '2', 7.5, false);
    expect(alone.map((s) => s.args.tag)).toEqual(['2']);
    expect(alone[0].args.x1).toBeCloseTo(7.5);
    const all = setSpacing('g', axes, '2', 7.5, true);
    expect(all.map((s) => s.args.tag)).toEqual(['2', '3']);
    expect(all[1].args.x2).toBeCloseTo(13.5);
    expect(setSpacing('g', axes, '1', 3, true)).toEqual([]);
  });
});
