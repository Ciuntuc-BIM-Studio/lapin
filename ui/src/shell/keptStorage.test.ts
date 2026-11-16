import { describe, expect, it } from 'vitest';
import { touchOrder } from './keptStorage';

describe('the capped keys', () => {
  it('keeps the newest and drops the oldest beyond the cap', () => {
    let order: string[] = [];
    const dropped: string[] = [];
    for (const k of ['a', 'b', 'c', 'd']) { const r = touchOrder(order, k, 3); order = r.keep; dropped.push(...r.drop); }
    expect(order).toEqual(['d', 'c', 'b']);
    expect(dropped).toEqual(['a']);
  });
  it('a key written again is the newest, not a second entry', () => {
    const r = touchOrder(['c', 'b', 'a'], 'a', 3);
    expect(r.keep).toEqual(['a', 'c', 'b']);
    expect(r.drop).toEqual([]);
  });
});
