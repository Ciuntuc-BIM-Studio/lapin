import { describe, expect, it } from 'vitest';
import { formatLengths, formatSize, label, level, parseLengths, toggle, when } from './format';

describe('the core dialogs’ formatters', () => {
  it('a file size in B, kB or MB', () => {
    expect(formatSize(512)).toBe('512 B');
    expect(formatSize(2048)).toBe('2 kB');
    expect(formatSize(3 * 1_048_576)).toBe('3.0 MB');
  });
  it('a level with its sign', () => {
    expect(level(3)).toBe('+3.00');
    expect(level(-0.5)).toBe('−0.50');
  });
  it('a label capitalised; an empty one stays empty', () => {
    expect(label('wall')).toBe('Wall');
    expect(label('')).toBe('');
  });
  it('a date that does not read is shown as given; none is empty', () => {
    expect(when(null)).toBe('');
    expect(when('not a date')).toBe('not a date');
    expect(when('2026-11-02T10:00:00Z')).not.toBe('');
  });
  it('toggle takes keys out of the set when on, puts them in when off', () => {
    const off = new Set(['a', 'b']);
    expect([...toggle(off, ['a'], true)]).toEqual(['b']);
    expect([...toggle(off, ['c'], false)].sort()).toEqual(['a', 'b', 'c']);
    expect([...off]).toEqual(['a', 'b']);
  });
});

describe('a list of lengths in one field', () => {
  it('reads spaces or semicolons in the project’s unit', () => {
    expect(parseLengths('1200 2400; 3000', 'mm')).toEqual([1.2, 2.4, 3]);
    expect(parseLengths('120 240', 'cm')).toEqual([1.2, 2.4]);
  });
  it('an empty field or a trailing separator adds no length (never a 0)', () => {
    expect(parseLengths('', 'mm')).toEqual([]);
    expect(parseLengths('1200; ', 'mm')).toEqual([1.2]);
    expect(parseLengths('1200 , ', 'mm')).toBeNull();
  });
  it('a piece that does not read refuses the whole', () => {
    expect(parseLengths('1200 abc', 'mm')).toBeNull();
  });
  it('imperial lengths keep their own spaces and are joined by semicolons', () => {
    const text = formatLengths([0.3048, 1.0668], 'ft-in');
    expect(text).toContain('; ');
    const back = parseLengths(text, 'ft-in')!;
    expect(back).toHaveLength(2);
    expect(back[0]).toBeCloseTo(0.3048, 4);
    expect(back[1]).toBeCloseTo(1.0668, 4);
  });
});
