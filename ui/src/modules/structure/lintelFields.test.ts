import { describe, expect, it } from 'vitest';
import { formatLengths, parseLengths } from './lintelFields';

describe("a beam type's stock lengths", () => {
  it('reads millimetres in any order, each once, and writes them back', () => {
    expect(parseLengths('1500 | 1250 1250;1750')).toEqual([1.25, 1.5, 1.75]);
    expect(parseLengths('')).toEqual([]);
    expect(parseLengths('1000 abc')).toBeNull();
    expect(formatLengths([1.25, 1.5])).toBe('1250 | 1500');
  });
});
