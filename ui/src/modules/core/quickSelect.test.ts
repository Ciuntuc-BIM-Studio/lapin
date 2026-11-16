import { describe, expect, it } from 'vitest';
import { UNIFIED_OPS } from '../../shell/conditionGroup';
import { initialGroup, nextSelection, queryRules } from './quickSelect';

describe('Quick select (forum topic 73)', () => {
  it('sends a range as min..max, a parameter by its name, and leaves out rules with nothing to compare', () => {
    expect(queryRules({
      matchAll: true, groups: [], conditions: [
        { field: '@type', op: '=', value: ' Block 30 ' },
        { field: '@text:height', op: 'between', values: ['2.5', ''] },
        { field: '@text:  ', op: '=', value: '3' },
        { field: '@class', op: '=', value: '' },
        { field: '@material', op: 'exists' },
      ],
    })).toEqual([
      { field: 'type', op: 'equals', value: 'Block 30' },
      { field: 'param', op: 'between', value: '2.5..', name: 'height' },
      { field: 'material', op: 'hasValue' },
    ]);
  });

  it('offers a range and a list among the operators, and any field of the project', () => {
    expect(UNIFIED_OPS).toContain('between');
    expect(UNIFIED_OPS).toContain('one of');
    expect(queryRules({ matchAll: true, groups: [], conditions: [{ field: 'height', op: '>', value: '3.2' }, { field: 'Fire rating', op: 'one of', values: ['EI 60', ' REI 90'] }] }))
      .toEqual([{ field: 'field', op: 'greater', value: '3.2', name: 'height' }, { field: 'field', op: 'oneOf', values: ['EI 60', 'REI 90'], name: 'Fire rating' }]);
  });

  it('starts with a rule on the type', () => {
    expect(initialGroup().conditions).toEqual([{ field: '@type', op: '=', value: '' }]);
  });

  it('replaces the selection, or adds to it once each', () => {
    expect(nextSelection(['b', 'c'], ['a', 'b'], false)).toEqual(['b', 'c']);
    expect(nextSelection(['b', 'c'], ['a', 'b'], true)).toEqual(['a', 'b', 'c']);
  });
});
