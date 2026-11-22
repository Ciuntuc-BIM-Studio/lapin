import { describe, expect, it } from 'vitest';
import { lastDay, parseYear, toggleDay } from './calendarRules';

describe('calendar rules', () => {
  it('takes only a whole year between 1900 and 2099', () => {
    expect(parseYear('2027')).toBe(2027);
    expect(parseYear(' 1900 ')).toBe(1900);
    for (const bad of ['2', '20', '202', '2100', '1899', '20x7', '', '2027.5']) expect(parseYear(bad)).toBeNull();
  });
  it('refuses to leave the calendar without a working day', () => {
    expect(toggleDay([1, 2], 2, false)).toEqual([1]);
    expect(toggleDay([1], 1, false)).toBeNull();
    expect(toggleDay([1, 5], 3, true)).toEqual([1, 3, 5]);
  });
  it('keeps an exception\'s last day from its first', () => {
    expect(lastDay('2027-03-10', '2027-03-01')).toBe('2027-03-10');
    expect(lastDay('2027-03-10', '2027-03-12')).toBe('2027-03-12');
  });
});
