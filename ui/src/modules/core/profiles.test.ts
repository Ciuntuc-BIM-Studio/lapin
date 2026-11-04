import { describe, expect, it } from 'vitest';
import { formatPoints, parsePoints, profileArea, profileOutline, profileSize, profileSvg } from './profiles';

describe('a profile (decision 297)', () => {
  it('is its corners anticlockwise from the insertion point, whichever way they were given', () => {
    const cw = { points: [[0, 0], [0, 0.1], [0.05, 0.1], [0.05, 0]] as [number, number][], insertion: [0.025, 0] as [number, number] };
    const o = profileOutline(cw);
    expect(o).toHaveLength(4);
    expect(Math.min(...o.map((p) => p[0]))).toBeCloseTo(-0.025, 12);
    expect(profileArea(cw)).toBeCloseTo(0.005, 12);
    expect(profileSize(cw)).toEqual({ width: 0.05, height: 0.1 });
  });

  it('makes a side of bulge 1 a half circle, its chords no more than a millimetre off', () => {
    const half = { points: [[0, 0, 1], [0, 0.1]] as [number, number, number][] };
    const o = profileOutline(half);
    expect(o.length).toBeGreaterThan(8);
    // Out to the right of the chord, as the engine's (bulge positive: anticlockwise).
    expect(Math.max(...o.map((p) => p[0]))).toBeCloseTo(0.05, 3);
    const ratio = profileArea(half) / ((Math.PI * 0.05 * 0.05) / 2);
    expect(ratio).toBeGreaterThan(0.98);
    expect(ratio).toBeLessThanOrEqual(1);
  });

  it('reads the corners typed in millimetres and writes them back the same', () => {
    const p = parsePoints('0 0; 300 0; 300 400 0.5; 0 400');
    expect(p).toEqual([[0, 0], [0.3, 0], [0.3, 0.4, 0.5], [0, 0.4]]);
    expect(formatPoints(p!)).toBe('0 0; 300 0; 300 400 0.5; 0 400');
    expect(parsePoints('0,5 0; 10 0; 10 10')).toEqual([[0.0005, 0], [0.01, 0], [0.01, 0.01]]);
    expect(parsePoints('0 0; 1 1')).toBeNull();
    expect(parsePoints('0 0; a 1; 2 2')).toBeNull();
  });

  it('is drawn in its box at one scale, v up, with the insertion point', () => {
    const { path, ix, iy } = profileSvg({ points: [[0, 0], [0.1, 0], [0.1, 0.1], [0, 0.1]] }, 100, 100, 10);
    expect(path).toBe('M10 90 L90 90 L90 10 L10 10 Z');
    expect([ix, iy]).toEqual([10, 90]);
  });
});
