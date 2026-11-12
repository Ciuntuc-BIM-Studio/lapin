import { describe, expect, it } from 'vitest';
import { areaLoadArgs, combinationName, formatFactor, formatKn, imposedArgs, lineLoadArgs, parsePositive, pointLoadArgs, psiFrom } from './loads';

describe('structural load helpers', () => {
  it('reads typed values', () => {
    expect(parsePositive('2,5')).toBe(2.5);
    expect(parsePositive('0')).toBeNull();
    expect(parsePositive('abc')).toBeNull();
  });
  it('formats factors and totals', () => {
    expect(formatFactor(1.35)).toBe('1.35');
    expect(formatFactor(1.5)).toBe('1.5');
    expect(formatFactor(0.7 * 1.5)).toBe('1.05');
    expect(formatKn(40)).toBe('40');
    expect(formatKn(1.0789)).toBe('1.08');
  });
  it('writes a combination name from its factors when it has none', () => {
    const f = [{ caseId: 'a', code: 'G1', factor: 1.35 }, { caseId: 'b', code: 'Q-A', factor: 1.5 }];
    expect(combinationName({ key: 'k', kind: 'uls', name: '', factors: f })).toBe('1.35 G1 + 1.5 Q-A');
    expect(combinationName({ key: 'k', kind: 'uls', name: 'x', factors: f })).toBe('x');
  });
  it('builds the load commands arguments, with the case only when chosen', () => {
    expect(areaLoadArgs('s', [{ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 1, y: 1 }], 2, null)).toEqual({ storeyId: 's', points: [[0, 0], [1, 0], [1, 1]], value: 2 });
    expect(lineLoadArgs('s', { x: 0, y: 0 }, { x: 3, y: 0 }, 5, 'c')).toEqual({ storeyId: 's', a: [0, 0], b: [3, 0], value: 5, caseId: 'c' });
    expect(pointLoadArgs('s', { x: 1, y: 2 }, 10, null)).toEqual({ storeyId: 's', at: [1, 2], value: 10 });
  });
  it('reads psi factors', () => {
    expect(psiFrom('', '', '')).toBeNull();
    expect(psiFrom('0,7', '0.5', '0.3')).toEqual({ '0': 0.7, '1': 0.5, '2': 0.3 });
    expect(psiFrom('0.7', '', '0.3')).toBeUndefined();
    expect(psiFrom('2', '0', '0')).toBeUndefined();
  });
  it('reads an imposed load, empty meaning the default', () => {
    expect(imposedArgs('A', '')).toEqual({ category: 'A', qk: null });
    expect(imposedArgs('A', '1,5')).toEqual({ category: 'A', qk: 1.5 });
    expect(imposedArgs('A', 'x')).toBeUndefined();
  });
});
