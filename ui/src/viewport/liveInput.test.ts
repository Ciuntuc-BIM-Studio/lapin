import { describe, expect, it } from 'vitest';
import { liveMeasure, livePoint, parseAngle, parseLength } from './liveInput';

describe('live input', () => {
  it('reads lengths in mm unless they say, with sums', () => {
    expect(parseLength('2400')).toBeCloseTo(2.4, 12);
    expect(parseLength('240cm')).toBeCloseTo(2.4, 12);
    expect(parseLength('2.4m')).toBeCloseTo(2.4, 12);
    expect(parseLength('3000+250')).toBeCloseTo(3.25, 12);
    expect(parseLength('3m-25cm')).toBeCloseTo(2.75, 12);
    expect(parseLength('2,5m')).toBeCloseTo(2.5, 12);
    expect(parseLength('abc')).toBeNull();
    expect(parseLength('')).toBeNull();
  });

  it('reads angles in degrees', () => {
    expect(parseAngle('30')).toBe(30);
    expect(parseAngle('90°')).toBe(90);
    expect(parseAngle('45+15')).toBe(60);
    expect(parseAngle('x')).toBeNull();
  });

  it('takes the typed length along the cursor, the typed angle with the cursor\'s reach, or both', () => {
    const base = { x: 1, y: 1 }, cursor = { x: 4, y: 5 };              // 5 m away at 53.13°
    expect(livePoint({ base, cursor, len: '', ang: '' })).toEqual(cursor);
    const l = livePoint({ base, cursor, len: '10000', ang: '' })!;
    expect(l.x).toBeCloseTo(7, 9); expect(l.y).toBeCloseTo(9, 9);
    const a = livePoint({ base, cursor, len: '', ang: '0' })!;
    expect(a.x).toBeCloseTo(4, 9); expect(a.y).toBeCloseTo(1, 9);
    const both = livePoint({ base, cursor, len: '2000', ang: '90' })!;
    expect(both.x).toBeCloseTo(1, 9); expect(both.y).toBeCloseTo(3, 9);
    const m = liveMeasure({ base, cursor, len: '', ang: '' })!;
    expect(m.len).toBeCloseTo(5, 9);
    expect(m.ang).toBeCloseTo(53.13, 2);
  });
});
