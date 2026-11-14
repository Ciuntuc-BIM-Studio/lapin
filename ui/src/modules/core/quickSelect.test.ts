import { describe, expect, it } from 'vitest';
import { nextSelection, QS_OPS, queryRules } from './quickSelect';

describe('Quick select (forum topic 73)', () => {
  it('sends a range as min..max, a parameter by its name, and leaves out rules with nothing to compare', () => {
    expect(queryRules([
      { field: 'type', op: 'equals', value: ' Block 30 ' },
      { field: 'param', op: 'between', value: '2.5', to: '', name: 'height' },
      { field: 'param', op: 'equals', value: '3', name: '  ' },
      { field: 'class', op: 'equals', value: '' },
      { field: 'material', op: 'hasValue', value: '' },
    ])).toEqual([
      { field: 'type', op: 'equals', value: 'Block 30' },
      { field: 'param', op: 'between', value: '2.5..', name: 'height' },
      { field: 'material', op: 'hasValue' },
    ]);
  });

  it('offers a range among the view filters\' operators', () => {
    expect(QS_OPS.map(([k]) => k)).toContain('between');
    expect(QS_OPS.map(([k]) => k)).toContain('contains');
  });

  it('replaces the selection, or adds to it once each', () => {
    expect(nextSelection(['b', 'c'], ['a', 'b'], false)).toEqual(['b', 'c']);
    expect(nextSelection(['b', 'c'], ['a', 'b'], true)).toEqual(['a', 'b', 'c']);
  });
});
