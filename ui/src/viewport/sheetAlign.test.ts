import { describe, expect, it } from 'vitest';
import type { PenDto } from './drawing';
import { alignDelta, featureAt } from './sheetAlign';

const pen = (viewport: string, segments: number[], layer = 'cut'): PenDto => ({ layer, stroke: '#000', width: 0.25, fill: null, segments, viewport });

describe('featureAt', () => {
  const pens = [pen('a', [10, 10, 50, 10, 50, 10, 50, 40]), pen('b', [100, 0, 120, 30])];
  it('takes a corner before a line', () => {
    expect(featureAt(pens, { x: 50.5, y: 10.4 }, 1)?.feature).toEqual({ kind: 'point', x: 50, y: 10 });
  });
  it('takes a horizontal and a vertical line away from the corners', () => {
    expect(featureAt(pens, { x: 30, y: 10.5 }, 1)).toEqual({ viewport: 'a', feature: { kind: 'h', y: 10, x0: 10, x1: 50 } });
    expect(featureAt(pens, { x: 50.4, y: 25 }, 1)).toEqual({ viewport: 'a', feature: { kind: 'v', x: 50, y0: 10, y1: 40 } });
  });
  it('ignores an oblique line, a point out of reach, hatches and the frame (no viewport)', () => {
    expect(featureAt(pens, { x: 110, y: 15 }, 1)).toBeNull();
    expect(featureAt(pens, { x: 30, y: 20 }, 1)).toBeNull();
    expect(featureAt([pen('a', [0, 0, 20, 0], 'hatch')], { x: 10, y: 0 }, 1)).toBeNull();
    expect(featureAt([{ ...pen('a', [0, 0, 20, 0]), viewport: undefined }], { x: 10, y: 0 }, 1)).toBeNull();
  });
  it('reads closed loops', () => {
    const loop: PenDto = { layer: 'cut', stroke: '#000', width: 0.25, fill: null, loops: [[0, 0, 20, 0, 20, 10, 0, 10]], viewport: 'a' };
    expect(featureAt([loop], { x: 10, y: 10.3 }, 1)?.feature).toEqual({ kind: 'h', y: 10, x0: 0, x1: 20 });
  });
});

describe('alignDelta', () => {
  const p = (x: number, y: number) => ({ kind: 'point' as const, x, y });
  const h = (y: number) => ({ kind: 'h' as const, y, x0: 0, x1: 1 });
  const v = (x: number) => ({ kind: 'v' as const, x, y0: 0, y1: 1 });
  it('two corners line up on both axes', () => expect(alignDelta(p(100, 40), p(30, 70))).toEqual({ dx: 70, dy: -30 }));
  it('two horizontal lines line up on y alone, two vertical ones on x alone', () => {
    expect(alignDelta(h(40), h(55))).toEqual({ dx: 0, dy: -15 });
    expect(alignDelta(v(40), v(55))).toEqual({ dx: -15, dy: 0 });
  });
  it('a corner against a line lines up the line\'s axis, either way round', () => {
    expect(alignDelta(h(40), p(5, 55))).toEqual({ dx: 0, dy: -15 });
    expect(alignDelta(p(5, 55), v(8))).toEqual({ dx: -3, dy: 0 });
  });
  it('a horizontal line cannot line up with a vertical one', () => {
    expect(alignDelta(h(1), v(2))).toBeNull();
    expect(alignDelta(v(1), h(2))).toBeNull();
  });
});
