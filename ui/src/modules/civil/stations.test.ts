import { describe, expect, it } from 'vitest';
import { parseStationList } from './stations';

describe('a corridor’s joints typed as stations', () => {
  it('reads the stations, commas or semicolons between', () => {
    expect(parseStationList('12.5, 40; 80')).toEqual([12.5, 40, 80]);
  });
  it('an empty field or a trailing comma gives no joint at 0', () => {
    expect(parseStationList('')).toEqual([]);
    expect(parseStationList('  ')).toEqual([]);
    expect(parseStationList('20, ')).toEqual([20]);
    expect(parseStationList(', 20,,')).toEqual([20]);
  });
  it('a piece that is not a number is left out; a typed 0 stays', () => {
    expect(parseStationList('abc, 0, 15')).toEqual([0, 15]);
  });
});
