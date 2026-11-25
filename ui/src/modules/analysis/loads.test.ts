import { describe, expect, it } from 'vitest';
import { factorSetOptions } from './loads';
import { addCombinationArgs, boundedOrNull, factorsPayload, factorTexts, groupCombinations, isUltimate, areaLoadArgs, combinationName, formatFactor, formatKn, imposedArgs, lineLoadArgs, parsePositive, pointLoadArgs, psiFrom, snowArgs, windArgs } from './loads';

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
  it('reads the site\'s snow and wind as typed, s0,k and qb needed, the factors 1 when left empty', () => {
    expect(snowArgs('2,0', '1.2', '', '1.1')).toEqual({ s0k: 2, ce: 1.2, ct: 1, importance: 1.1 });
    expect(snowArgs('', '1', '1', '1')).toBeUndefined();
    expect(snowArgs('2', '3', '1', '1')).toBeUndefined();
    expect(windArgs('0,5', 'III', '')).toEqual({ qb: 0.5, terrain: 'III', importance: 1 });
    expect(windArgs('0.5', 'V', '1')).toBeUndefined();
    expect(windArgs('x', 'II', '1')).toBeUndefined();
  });
});

describe('combination helpers', () => {
  it('builds the factors from the form: empty and zero fields left out, a bad one refused', () => {
    expect(factorsPayload({ a: '1,35', b: '', c: ' 1.5 ', d: '0' })).toEqual([{ caseId: 'a', factor: 1.35 }, { caseId: 'c', factor: 1.5 }]);
    expect(factorsPayload({ a: 'x' })).toBeUndefined();
    expect(factorsPayload({})).toEqual([]);
  });
  it('writes the texts of factors for editing', () => {
    expect(factorTexts([{ caseId: 'a', factor: 1.35 }, { caseId: 'b', factor: 1 }])).toEqual({ a: '1.35', b: '1' });
  });
  it('reads a factor within its range, empty meaning the set\'s', () => {
    expect(boundedOrNull('', 1, 2)).toBeNull();
    expect(boundedOrNull('1,4', 1, 2)).toBe(1.4);
    expect(boundedOrNull('2.5', 1, 2)).toBeUndefined();
    expect(boundedOrNull('x', 1, 2)).toBeUndefined();
  });
  it('makes the add arguments only with a name and readable factors', () => {
    expect(addCombinationArgs(' Maintenance ', 'uls', { g: '1', q: '1.5' })).toEqual({ name: 'Maintenance', kind: 'uls', factors: [{ caseId: 'g', factor: 1 }, { caseId: 'q', factor: 1.5 }] });
    expect(addCombinationArgs('', 'uls', { g: '1' })).toBeUndefined();
    expect(addCombinationArgs('x', 'uls', { g: 'z' })).toBeUndefined();
  });
  it('groups ultimate, accidental and seismic combinations together, the serviceability ones apart', () => {
    const mk = (key: string, kind: 'uls' | 'sls-quasi' | 'seismic' | 'accidental') => ({ key, kind, name: key, factors: [] });
    const g = groupCombinations([mk('a', 'uls'), mk('b', 'sls-quasi'), mk('c', 'seismic'), mk('d', 'accidental')]);
    expect(g.ultimate.map((c) => c.key)).toEqual(['a', 'c', 'd']);
    expect(g.serviceability.map((c) => c.key)).toEqual(['b']);
    expect(isUltimate('sls-frequent')).toBe(false);
  });
});

describe('the sets of factors offered (N1)', () => {
  const set = (label: string, builtin?: boolean) => ({ name: label, label, gammaGSup: 1.35, gammaGInf: 1, gammaQ: 1.5, psi: {}, builtin });
  it('lists Lapin\'s first, translated, then the rule pack\'s by name with their own labels', () => {
    const o = factorSetOptions({ 'se-example': set('Sweden, example'), en: set('EN', true), cr0: set('CR 0-2012', true) });
    expect(o).toEqual([
      { key: 'cr0', label: 'CR 0-2012 (Romania)', ours: true },
      { key: 'en', label: 'EN 1990 Annex A1, recommended', ours: true },
      { key: 'se-example', label: 'Sweden, example', ours: false },
    ]);
    // A pack that changed Lapin's set: its own label, not translated.
    expect(factorSetOptions({ en: set('EN, our values', false) })[0]).toEqual({ key: 'en', label: 'EN, our values', ours: false });
  });
});
