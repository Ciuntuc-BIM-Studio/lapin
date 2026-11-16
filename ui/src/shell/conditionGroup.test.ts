import { describe, expect, it } from 'vitest';
import {
  edited, finishedGroup, fromClashGroup, fromConditions, fromNativeGroup, fromViewFilter, splitList, toClashGroup, toConditions, toNativeGroup, toViewFilter, withOp,
  type ClashGroup, type ViewRule,
} from './conditionGroup';

describe('the one condition editor\'s model (audit S10)', () => {
  it('gives a view filter\'s stored rules back unchanged when they are not edited', () => {
    const rules: ViewRule[] = [
      { field: 'type', op: 'equals', value: 'Brick 300' },
      { field: 'param', op: 'between', value: '3..3.5', name: 'height' },
      { field: 'field', op: 'oneOf', values: ['EI 60', 'REI 90'], name: 'Fire rating' },
      { field: 'phase', op: 'hasNoValue' },
    ];
    const g = fromViewFilter('any', rules);
    expect(g.conditions.map((c) => [c.field, c.op])).toEqual([['@type', '='], ['@text:height', 'between'], ['Fire rating', 'one of'], ['@phase', 'missing']]);
    expect(g.conditions[1].values).toEqual(['3', '3.5']);
    expect(toViewFilter(g)).toEqual({ rules, match: 'any' });
  });

  it('writes an edited condition in each place\'s own words', () => {
    const v = fromViewFilter('all', [{ field: 'type', op: 'equals', value: 'A' }]);
    v.conditions[0] = edited(v.conditions[0], { field: '@text:Fire rating', op: 'starts with', value: 'REI' });
    expect(toViewFilter(v).rules).toEqual([{ field: 'param', op: 'beginsWith', value: 'REI', name: 'Fire rating' }]);
    const c = fromClashGroup({ matchAll: false, rules: [{ field: 'class', op: 'is', values: ['IfcWall'] }], groups: [] });
    c.conditions[0] = edited(withOp(c.conditions[0], 'none of'), { values: ['IfcDoor', 'IfcWindow'] });
    expect(toClashGroup(c)).toEqual({ matchAll: false, rules: [{ field: 'class', op: 'isNotAnyOf', values: ['IfcDoor', 'IfcWindow'] }], groups: [] });
    const n = fromConditions([{ field: 'Pset_Firma.Etapa', op: '=', value: 'E2' }]);
    n.conditions.push({ field: 'height', op: '≥', value: '2,5' }, { field: '@storey', op: 'between', values: [' 1', ''] });
    expect(toConditions(n)).toEqual([{ field: 'Pset_Firma.Etapa', op: '=', value: 'E2' }, { field: 'height', op: '≥', value: 2.5 }, { field: '@storey', op: 'between', values: ['1', ''] }]);
  });

  it('keeps a clash search set\'s groups, and a field schedule\'s, to any depth', () => {
    const set: ClashGroup = { matchAll: false, rules: [{ field: 'parameter', op: 'contains', values: ['REI'], parameter: 'Fire rating' }], groups: [{ matchAll: true, rules: [{ field: 'link', op: 'is', values: [''] }], groups: [] }] };
    expect(toClashGroup(fromClashGroup(set))).toEqual(set);
    const f = { match: 'any' as const, conditions: [{ field: '@type', op: '=', value: 'X' }], nested: [{ match: 'all' as const, conditions: [{ field: 'Length', op: '>', value: 2 }] }] };
    expect(toNativeGroup(fromNativeGroup(f))).toEqual(f);
    // A script's operator is read by its name.
    expect(fromConditions([{ field: 'x', op: '<=', value: 1 }]).conditions[0].op).toBe('≤');
  });

  it('sends the finished conditions while the others are typed, a stored one as it is', () => {
    const g = fromViewFilter('all', [{ field: 'type', op: 'contains', value: '' }]);
    g.conditions.push({ field: '@class', op: '=', value: '' }, { field: '@text:', op: 'exists' }, { field: '@name', op: 'exists' });
    g.groups.push({ matchAll: true, conditions: [{ field: '@class', op: '=', value: '' }], groups: [] });
    const done = finishedGroup(g);
    expect(done.conditions.map((c) => c.field)).toEqual(['@type', '@name']);
    expect(done.groups).toEqual([]);
  });

  it('carries a value over to another operator\'s shape, and splits a list but not a thousands', () => {
    expect(withOp({ field: 'x', op: '=', value: '3' }, 'between')).toEqual({ field: 'x', op: 'between', values: ['3', ''] });
    expect(withOp({ field: 'x', op: 'one of', values: ['a', 'b'] }, '=')).toEqual({ field: 'x', op: '=', value: 'a' });
    expect(withOp({ field: 'x', op: '=', value: '3' }, 'exists')).toEqual({ field: 'x', op: 'exists' });
    expect(splitList('1,200, 800; 5\nWall')).toEqual(['1,200', '800', '5', 'Wall']);
  });
});
