import { describe, expect, it } from 'vitest';
import { clipSegment, outside, screenRect } from './screenClip';

const r = screenRect(1000, 800, 0);

describe('clipSegment (forum topic 162)', () => {
  it('keeps a segment inside the screen as it is', () => {
    const a = { x: 10, y: 10 }, b = { x: 500, y: 300 };
    expect(clipSegment(a, b, r)).toEqual([a, b]);
  });

  it('leaves out a segment wholly off the screen', () => {
    expect(clipSegment({ x: -500, y: 10 }, { x: -10, y: 700 }, r)).toBeNull();
    expect(clipSegment({ x: 2000, y: -50 }, { x: 3000, y: -50 }, r)).toBeNull();
  });

  it('cuts a line crossing the screen to its width', () => {
    const c = clipSegment({ x: -1e6, y: 400 }, { x: 1e6, y: 400 }, r)!;
    expect(c[0].x).toBeCloseTo(0, 6);
    expect(c[1].x).toBeCloseTo(1000, 6);
  });

  it('keeps the dashes where they were: the start a whole number of periods from the first end', () => {
    const period = 24;
    const c = clipSegment({ x: -100_005, y: 400 }, { x: 100_000, y: 400 }, r, period)!;
    const fromStart = c[0].x - -100_005;
    expect(Math.abs(fromStart / period - Math.round(fromStart / period))).toBeLessThan(1e-6);
    expect(c[0].x).toBeLessThanOrEqual(0);
    expect(c[0].x).toBeGreaterThan(-period);
    expect(c[1].x).toBeGreaterThanOrEqual(1000);
    expect(c[1].x).toBeLessThan(1000 + period);
  });

  it('holds a zoomed-in drawing to the screen: the length drawn does not grow with the zoom', () => {
    const drawn = (zoom: number) => {
      const c = clipSegment({ x: 500 - 50 * zoom, y: 400 }, { x: 500 + 50 * zoom, y: 400 }, r, 24);
      return c ? Math.hypot(c[1].x - c[0].x, c[1].y - c[0].y) : 0;
    };
    expect(drawn(100)).toBeLessThan(1000 + 2 * 24);
    expect(drawn(10_000)).toBeLessThan(1000 + 2 * 24);
  });
});

describe('outside', () => {
  it('tells a loop off one side from one across the screen', () => {
    expect(outside([-50, 10, -20, 10, -20, 90], r)).toBe(true);
    expect(outside([-50, -50, 5000, -50, 5000, 5000, -50, 5000], r)).toBe(false);
    expect(outside([100, 100], r)).toBe(false);
  });
});
