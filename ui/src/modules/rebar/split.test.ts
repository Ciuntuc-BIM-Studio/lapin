import { describe, expect, it } from 'vitest';
import { lapFor, lapFromTo, overStock, pieceLengths, splitRefusal, stockLap } from './split';

describe('splitting a bar with a lap', () => {
  it('takes the lap from the project or 50Ø', () => {
    expect(lapFor(0.016, null)).toBeCloseTo(0.8);
    expect(lapFor(0.016, undefined)).toBeCloseTo(0.8);
    expect(lapFor(0.016, 0.6)).toBeCloseTo(0.6);
    expect(lapFor(0.016, 0)).toBeCloseTo(0.8);
  });

  it('ends the lap at a station by default, or starts it there', () => {
    expect(lapFromTo(7, 0.8)).toEqual({ from: 6.2, to: 7 });
    expect(lapFromTo(7, 0.8, 'before')).toEqual({ from: 6.2, to: 7 });
    expect(lapFromTo(7, 0.8, 'after')).toEqual({ from: 7, to: 7.8 });
  });

  it('cuts so the first piece is exactly the stock length: the lap ends at the stock length', () => {
    expect(stockLap(12, 0.8)).toEqual({ from: 11.2, to: 12 });
    const { from, to } = stockLap(12, 0.8);
    expect(pieceLengths(14, from, to)[0]).toBeCloseTo(12);
    expect(pieceLengths(14, from, to)[1]).toBeCloseTo(2.8);
  });

  it('gives a 14 m bar with the lap from 6.6 to 7.4 m two pieces of 7.4 m, and from 6.2 to 7 m pieces of 7 and 7.8 m', () => {
    expect(pieceLengths(14, 6.6, 7.4).map((x) => +x.toFixed(6))).toEqual([7.4, 7.4]);
    expect(pieceLengths(14, 6.2, 7).map((x) => +x.toFixed(6))).toEqual([7, 7.8]);
  });

  it('refuses a lap without length, outside the bar, or leaving a piece all lap, as the engine does', () => {
    expect(splitRefusal(14, 6.6, 7.4)).toBeNull();
    expect(splitRefusal(14, 7, 7.005)).toBe('The lap needs a length.');
    expect(splitRefusal(14, 7.4, 6.6)).toBe('The lap needs a length.');
    expect(splitRefusal(14, 14.2, 15)).toBe('The cut is outside the bar.');
    expect(splitRefusal(14, -0.5, 0.3)).toBe('The cut is outside the bar.');
    expect(splitRefusal(14, 0, 0.8)).toBe('The cut is too close to the end.');
    expect(splitRefusal(14, 13.2, 14)).toBe('The cut is too close to the end.');
  });

  it('knows a bar over the stock length', () => {
    expect(overStock(14, 12)).toBe(true);
    expect(overStock(12, 12)).toBe(false);
    expect(overStock(8, 6)).toBe(true);
  });
});
