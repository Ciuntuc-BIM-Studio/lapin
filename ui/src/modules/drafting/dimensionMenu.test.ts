import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { alignSteps } from './dimensionMenu';

const dim = (id: string, pts: number[][], at: number[], extra: Record<string, unknown> = {}): NodeDto => ({
  id, type: 'IfcAnnotation', name: id, parentId: null, px: 0, py: 0, editable: true, module: null, storeyId: null,
  props: { predefinedType: 'DIMENSION', pts, at, ...extra },
});

describe('aligning dimension strings', () => {
  it('moves each parallel string onto the first one\'s line, leaving one already there', () => {
    const a = dim('a', [[0, 0], [4, 0]], [2, 1]);
    const b = dim('b', [[5, 0], [9, 0]], [7, 1.5]);
    const c = dim('c', [[0, 3], [4, 3]], [9, 1]);
    const { steps, skipped } = alignSteps([a, b, c]);
    expect(steps).toEqual([{ command: 'drafting.moveDimension', args: { id: 'b', at: [2, 1] } }]);
    expect(skipped).toBe(0);
  });
  it('skips what is not parallel to the first', () => {
    const a = dim('a', [[0, 0], [4, 0]], [2, 1]);
    const v = dim('v', [[0, 0], [0, 4]], [1, 2]);
    expect(alignSteps([a, v])).toEqual({ steps: [], skipped: 1 });
  });
  it('takes a stored direction over the points\' own', () => {
    const a = dim('a', [[0, 0], [4, 0]], [2, 1]);
    const d = dim('d', [[0, 0], [4, 3]], [1, 5], { dir: [1, 0] });
    expect(alignSteps([a, d]).steps).toEqual([{ command: 'drafting.moveDimension', args: { id: 'd', at: [2, 1] } }]);
  });
});
