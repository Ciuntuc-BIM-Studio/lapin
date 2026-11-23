import { describe, expect, it } from 'vitest';
import { COARSE, FINE, MEDIUM, onPaper, planDetail, shownAt } from './planDetail';

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
  it('an item sized on paper is drawn at the plan\'s scale round its anchor; one of the model\'s size stays as it is', () => {
    const ring = { kind: 'symbol', closed: true, points: [1.06, 2, 1, 2.06], paper: [1, 2, 50] };
    const at100 = onPaper(ring, 100);
    expect(at100.points.map((v) => Math.round(v * 1000) / 1000)).toEqual([1.12, 2, 1, 2.12]);
    expect(at100.paper).toBeNull();
    expect(onPaper(ring, 50).points).toEqual(ring.points);
    const box = { kind: 'symbol', closed: true, points: [0, 0, 1, 1] };
    expect(onPaper(box, 100)).toBe(box);
  });
});
