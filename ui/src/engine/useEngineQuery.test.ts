import { describe, expect, it } from 'vitest';
import { askWhileLive } from './useEngineQuery';

const later = <T,>(value: T, ms: number) => new Promise<T>((r) => setTimeout(() => r(value), ms));

describe('askWhileLive', () => {
  it('hands the answer over while the question is current', async () => {
    const got: (string | null)[] = [];
    await askWhileLive(() => later('A', 1), () => true, (v) => got.push(v));
    expect(got).toEqual(['A']);
  });
  it('drops an answer that lands after the deps changed', async () => {
    const got: (string | null)[] = [];
    let live = true;
    const slow = askWhileLive(() => later('room A', 20), () => live, (v) => got.push(v));
    live = false; // the selection moved to room B
    await askWhileLive(() => later('room B', 1), () => true, (v) => got.push(v));
    await slow;
    expect(got).toEqual(['room B']);
  });
  it('gives null when the engine cannot answer or the call throws', async () => {
    const got: (string | null)[] = [];
    await askWhileLive(async () => null, () => true, (v) => got.push(v));
    await askWhileLive(async () => { throw new Error('x'); }, () => true, (v) => got.push(v));
    expect(got).toEqual([null, null]);
  });
});
