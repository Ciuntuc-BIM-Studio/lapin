import { describe, expect, it } from 'vitest';
import { clampDecimal, parseDecimal } from './parseDecimal';

describe('parseDecimal', () => {
  it('reads a comma or a point', () => {
    expect(parseDecimal('1,5')).toBe(1.5);
    expect(parseDecimal('1.5')).toBe(1.5);
    expect(parseDecimal(' 12 ')).toBe(12);
    expect(parseDecimal('.5')).toBe(0.5);
  });
  it('reads a hyphen or a true minus', () => {
    expect(parseDecimal('-3')).toBe(-3);
    expect(parseDecimal('−3,25')).toBe(-3.25);
    expect(parseDecimal('-0')).toBe(0);
  });
  it('rejects what is not one finite number', () => {
    for (const bad of ['', ' ', 'abc', '1,2,3', '12abc', 'Infinity', 'NaN', '1e999', '--1', '1-2', '1+2', '1.234,5']) expect(parseDecimal(bad)).toBeNull();
  });
  it('holds to its rules', () => {
    expect(parseDecimal('5', { min: 0, max: 4 })).toBeNull();
    expect(parseDecimal('4', { min: 0, max: 4 })).toBe(4);
    expect(parseDecimal('2,5', { integer: true })).toBeNull();
    expect(parseDecimal('3', { integer: true })).toBe(3);
  });
  it('clamps', () => {
    expect(clampDecimal(9, { max: 4 })).toBe(4);
    expect(clampDecimal(-9, { min: 0 })).toBe(0);
  });
});
