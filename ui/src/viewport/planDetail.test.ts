import { describe, expect, it } from 'vitest';
import { COARSE, FINE, MEDIUM, planDetail, shownAt } from './planDetail';

describe('how much a plan draws, by its scale', () => {
  it('coarse at 1:1000 and smaller, fine at 1:200 and larger, medium between', () => {
    expect([planDetail(5000), planDetail(1000), planDetail(500), planDetail(201), planDetail(200), planDetail(50)]).toEqual([COARSE, COARSE, MEDIUM, MEDIUM, FINE, FINE]);
  });
  it('an item shows at the levels it says, at all of them when it says none', () => {
    const sleeper = { kind: 'symbol', closed: true, points: [], minDetail: FINE, maxDetail: FINE };
    const axis = { kind: 'heavy', closed: false, points: [], maxDetail: COARSE };
    const plain = { kind: 'symbol', closed: false, points: [] };
    expect([shownAt(sleeper, FINE), shownAt(sleeper, MEDIUM)]).toEqual([true, false]);
    expect([shownAt(axis, COARSE), shownAt(axis, MEDIUM)]).toEqual([true, false]);
    expect([COARSE, MEDIUM, FINE].every((d) => shownAt(plain, d))).toBe(true);
  });
});
