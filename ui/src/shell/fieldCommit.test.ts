import { afterEach, describe, expect, it } from 'vitest';
import { commitLength, commitNumber, NO_COMMIT } from './fieldCommit';
import { useStandards } from '../engine/unitsStore';

afterEach(() => useStandards.setState({ units: 'mm' }));

describe('the shared fields commit rules', () => {
  it('reads a length in the project unit or its own', () => {
    expect(commitLength('2400', 1)).toBeCloseTo(2.4, 9);
    expect(commitLength('240cm', 1)).toBeCloseTo(2.4, 9);
    expect(commitLength('3000+250', 1)).toBeCloseTo(3.25, 9);
    useStandards.setState({ units: 'ft-in' });
    expect(commitLength("10'", 1)).toBeCloseTo(3.048, 9);
    expect(commitLength('10', 1)).toBeCloseTo(3.048, 9);
  });
  it('ignores empty text, never a 0', () => {
    expect(commitLength('', 2.4)).toBe(NO_COMMIT);
    expect(commitLength('   ', 2.4)).toBe(NO_COMMIT);
    expect(commitNumber('', 3)).toBe(NO_COMMIT);
  });
  it('commits null for empty text when blank means the default', () => {
    expect(commitLength('', 2.4, { blank: true })).toBeNull();
    // Already the default: nothing to do.
    expect(commitLength('', null, { blank: true })).toBe(NO_COMMIT);
  });
  it('commits nothing when the text does not read or the value is the same', () => {
    expect(commitLength('abc', 2.4)).toBe(NO_COMMIT);
    expect(commitLength('2400', 2.4)).toBe(NO_COMMIT);
    expect(commitNumber('3', 3)).toBe(NO_COMMIT);
  });
  it('keeps a value within min and max, and whole when asked', () => {
    expect(commitLength('5', 0.05, { min: 0.01 })).toBeCloseTo(0.01, 9);
    expect(commitNumber('0', 4, { min: 1, integer: true })).toBe(1);
    expect(commitNumber('2.6', 4, { integer: true })).toBe(3);
    expect(commitNumber('500', 4, { max: 100 })).toBe(100);
    // Raised to a minimum it already has: nothing to do.
    expect(commitNumber('0', 1, { min: 1 })).toBe(NO_COMMIT);
  });
  it('reads sums and decimal commas in numbers', () => {
    expect(commitNumber('2,5', 1)).toBeCloseTo(2.5, 9);
    expect(commitNumber('10+5', 1)).toBe(15);
  });
});
