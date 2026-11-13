import { describe, expect, it } from 'vitest';
import {
  addPair, dashSegments, defaultLineChoices, fromPairs, lineTypeNames, parseDashes, patternChoices, removePair, setPairValue, toPairs, validDashes, withCurrent,
} from './lineTypeModel';

describe('line types', () => {
  it('cuts a pattern into the dashes along a line', () => {
    expect(dashSegments([3, 1.5], 10)).toEqual([[0, 3], [4.5, 7.5], [9, 10]]);
    expect(dashSegments([], 5)).toEqual([[0, 5]]);
  });
  it('edits dash and gap pairs, an odd list getting a last gap', () => {
    expect(toPairs([4, 1, 0.3])).toEqual([[4, 1], [0.3, 1]]);
    expect(fromPairs(setPairValue([[3, 1]], 0, 1, 2))).toEqual([3, 2]);
    expect(addPair([[3, 1.5]])).toEqual([[3, 1.5], [3, 1.5]]);
    expect(removePair([[1, 1]], 0)).toEqual([[1, 1]]);
    expect(addPair(Array.from({ length: 12 }, () => [1, 1] as [number, number]))).toHaveLength(12);
  });
  it('checks what the engine checks and reads typed dashes', () => {
    expect(validDashes([3, 1])).toBe(true);
    expect(validDashes([3, 0])).toBe(false);
    expect(validDashes([100, 1])).toBe(false);
    expect(parseDashes('6 2, 1;2')).toEqual([6, 2, 1, 2]);
    expect(parseDashes('0,5 1')).toEqual([0.5, 1]);
    expect(parseDashes('3')).toBeNull();
  });
  it('offers Lapin\'s names then the project\'s, keeping a removed one', () => {
    const lt = { builtin: [{ name: 'dashed', dashes: [3, 1.5] }], own: [{ id: 'x', name: 'Gas', dashes: [6, 2] }] };
    expect(lineTypeNames(lt)).toEqual(['continuous', 'dashed', 'Gas']);
    expect(withCurrent(['continuous'], 'Gone')).toEqual(['Gone', 'continuous']);
    expect(patternChoices([['', 'as styled']], lt)).toEqual([['', 'as styled'], ['Gas', 'Gas']]);
  });
  it('asks only about the types that differ', () => {
    const r = [
      { name: 'a', complex: false, builtin: false, dashes: [1, 1], exists: true, same: false },
      { name: 'b', complex: false, builtin: false, dashes: [1, 1], exists: true, same: true },
    ];
    expect(defaultLineChoices(r)).toEqual({ a: 'update' });
  });
});
