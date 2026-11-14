import { describe, expect, it } from 'vitest';
import { gridOffsets, itemCount, stepArgs, stepBetween, type StepJob } from './arrayGrid';

const job = (o: Partial<StepJob>): StepJob => ({ ids: ['w'], kind: 'row', count: 3, rows: 1, levels: 1, dz: 0, levelStep: [0, 0, 3], ...o });

describe('an array in a row or a grid (forum 90)', () => {
  it('puts the items in the engine\'s order: along a row, row by row, then level by level', () => {
    expect(gridOffsets(3, 1, 1, [2, 0, 0])).toEqual([[0, 0, 0], [2, 0, 0], [4, 0, 0]]);
    const g = gridOffsets(2, 2, 2, [2, 0, 0], [0, 3, 0], [0, 0, 3]);
    expect(g).toHaveLength(8);
    expect(g[1]).toEqual([2, 0, 0]);
    expect(g[2]).toEqual([0, 3, 0]);
    expect(g[3]).toEqual([2, 3, 0]);
    expect(g[7]).toEqual([2, 3, 3]);
  });

  it('takes a clicked step: a row\'s from two points, a grid\'s rows from the first to a third', () => {
    const a = { x: 1, y: 1 };
    const step = stepBetween(a, { x: 1.1, y: 1 });
    expect(step.x).toBeCloseTo(0.1, 12);
    expect(stepArgs(job({ dz: 0.5 }), step)).toEqual({ ids: ['w'], count: 3, step: [step.x, 0, 0.5] });
    const g = stepArgs(job({ kind: 'grid', count: 4, rows: 2 }), { x: 2, y: 0 }, stepBetween(a, { x: 1, y: 4 }));
    expect(g).toEqual({ ids: ['w'], kind: 'grid', count: 4, step: [2, 0, 0], rows: 2, rowStep: [0, 3, 0] });
    expect(stepArgs(job({ kind: 'grid', count: 2, rows: 2, levels: 3 }), { x: 1, y: 0 }, { x: 0, y: 1 })).toMatchObject({ levels: 3, levelStep: [0, 0, 3] });
  });

  it('counts a grid\'s items as columns × rows × levels', () => {
    expect(itemCount(job({}))).toBe(3);
    expect(itemCount(job({ kind: 'grid', count: 4, rows: 3, levels: 2 }))).toBe(24);
    expect(itemCount(job({ kind: 'grid', count: 1, rows: 1 }))).toBe(1);
  });
});
