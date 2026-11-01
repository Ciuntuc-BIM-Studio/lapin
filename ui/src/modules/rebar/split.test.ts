import { describe, expect, it } from 'vitest';
import { lapFor, overStock, pieceLengths, splitRefusal, stockStation } from './split';

describe('splitting a bar with a lap', () => {
  it('takes the lap from the project or 50Ø', () => {
    expect(lapFor(0.016, null)).toBeCloseTo(0.8);
    expect(lapFor(0.016, undefined)).toBeCloseTo(0.8);
    expect(lapFor(0.016, 0.6)).toBeCloseTo(0.6);
    expect(lapFor(0.016, 0)).toBeCloseTo(0.8);
  });

  it('cuts so the first piece is exactly the stock length, the lap centred on the station', () => {
    expect(stockStation(12, 0.8)).toBeCloseTo(11.6);
    expect(pieceLengths(14, stockStation(12, 0.8), 0.8)[0]).toBeCloseTo(12);
  });

  it('gives a 14 m bar cut at 7 m with 0.8 m two pieces of 7.4 m', () => {
    expect(pieceLengths(14, 7, 0.8).map((x) => +x.toFixed(6))).toEqual([7.4, 7.4]);
  });

  it('refuses a cut outside the bar, too near an end, or without a lap, as the engine does', () => {
    expect(splitRefusal(14, 7, 0.8)).toBeNull();
    expect(splitRefusal(14, 15, 0.8)).toBe('The cut is outside the bar.');
    expect(splitRefusal(14, 0.3, 0.8)).toBe('The cut is too close to the end.');
    expect(splitRefusal(14, 13.8, 0.8)).toBe('The cut is too close to the end.');
    expect(splitRefusal(14, 7, 0)).toBe('The lap is a length.');
  });

  it('knows a bar over the stock length', () => {
    expect(overStock(14, 12)).toBe(true);
    expect(overStock(12, 12)).toBe(false);
    expect(overStock(8, 6)).toBe(true);
  });
});
