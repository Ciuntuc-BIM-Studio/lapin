import { describe, expect, it } from 'vitest';
import { arraySkipNotes, arrayTargets, gridOffsets, itemCount, stepArgs, stepBetween, type StepJob } from './arrayGrid';

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

describe('what an array takes where it is made (forum 90)', () => {
  const node = (type: string, o: { storeyId?: string | null; predefinedType?: string; viewId?: string } = {}) =>
    ({ type, storeyId: o.storeyId ?? null, props: { predefinedType: o.predefinedType, viewId: o.viewId } });
  const nodes = new Map([
    ['wall', node('IfcWall', { storeyId: 's' })],
    ['group', node('IfcGroup')],
    ['rect', node('IfcAnnotation', { predefinedType: 'DRAFT', viewId: 'plan:s', storeyId: 's' })],
    ['text', node('IfcAnnotation', { predefinedType: 'TEXT', viewId: 'plan:s' })],
    ['symbol', node('IfcAnnotation', { predefinedType: 'SYMBOL', viewId: 'sec' })],
    ['dim', node('IfcAnnotation', { predefinedType: 'DIMENSION', viewId: 'plan:s' })],
    ['tag', node('IfcAnnotation', { predefinedType: 'TAG', viewId: 'plan:s' })],
    ['storey', node('IfcBuildingStorey')],
  ]);
  const all = ['wall', 'group', 'rect', 'text', 'symbol', 'dim', 'tag', 'storey', 'gone'];

  it('takes elements and drafting together in a plan, never a dimension or a tag', () => {
    const t = arrayTargets(all, nodes, 'plan');
    expect(t.ids).toEqual(['wall', 'group', 'rect', 'text', 'symbol']);
    expect(t.skippedNotes).toBe(2);
    expect(arraySkipNotes(t)).toEqual(['Dimensions and tags measure or name their elements: they were left out of the array.']);
  });

  it('takes only the drafting in a section, an elevation or a drafting view, and says the elements were left out', () => {
    const t = arrayTargets(['wall', 'rect', 'symbol'], nodes, 'view');
    expect(t.ids).toEqual(['rect', 'symbol']);
    expect(t.skippedElements).toBe(1);
    expect(arraySkipNotes(t)[0]).toMatch(/^Elements are arrayed in a plan or in 3D/);
  });

  it('takes only the elements in 3D, and says the drafting was left out', () => {
    const t = arrayTargets(['wall', 'rect'], nodes, '3d');
    expect(t.ids).toEqual(['wall']);
    expect(t.skippedDrafts).toBe(1);
    expect(arraySkipNotes(arrayTargets(['rect'], nodes, '3d'))[0]).toMatch(/^Drafted shapes are arrayed in their own view/);
    expect(arraySkipNotes(arrayTargets(['wall'], nodes, '3d'))).toEqual([]);
  });
});
