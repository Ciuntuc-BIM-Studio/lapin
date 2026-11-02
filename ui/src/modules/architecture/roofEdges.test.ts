import { describe, expect, it } from 'vitest';
import { mergeEdge } from './roofEdges';

describe('mergeEdge', () => {
  it('gives one entry per side, the others as the roof', () => {
    expect(mergeEdge([], 3, 1, { kind: 'gable' })).toEqual([null, { kind: 'gable' }, null]);
  });
  it('keeps the edge’s other fields and drops the ones set to undefined', () => {
    expect(mergeEdge([{ kind: 'eave', pitch: 30 }], 1, 0, { overhang: 0.4 })).toEqual([{ kind: 'eave', pitch: 30, overhang: 0.4 }]);
    expect(mergeEdge([{ kind: 'eave', pitch: 30 }], 1, 0, { pitch: undefined })).toEqual([{ kind: 'eave' }]);
  });
  it('an edge with nothing of its own left, or reset, is null', () => {
    expect(mergeEdge([{ pitch: 30 }], 1, 0, { pitch: undefined })).toEqual([null]);
    expect(mergeEdge([{ pitch: 30 }, { kind: 'gable' }], 2, 1, null)).toEqual([{ pitch: 30 }, null]);
  });
  it('drops entries past the outline’s sides', () => {
    expect(mergeEdge([null, null, { kind: 'gable' }], 2, 0, { kind: 'eave' })).toEqual([{ kind: 'eave' }, null]);
  });
});
