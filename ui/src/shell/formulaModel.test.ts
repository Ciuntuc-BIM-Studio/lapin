import { describe, expect, it } from 'vitest';
import { canFormula, formulaArgs, initialState, resultText, toggled, usableNames, valueArgs, worthTrying } from './formulaModel';

describe('parameter formulas', () => {
  it('lets numbers, lengths, areas, angles and yes/no have one, not text', () => {
    expect(['number', 'length', 'area', 'angle', 'bool'].every(canFormula)).toBe(true);
    expect(canFormula('text')).toBe(false);
  });
  it('switches from a value to a formula starting from the number, and back', () => {
    const p = { kind: 'length', value: 2.5 };
    const f = toggled(initialState(p), p);
    expect(f).toEqual({ mode: 'formula', text: '2.5' });
    expect(toggled(f, p).mode).toBe('value');
    expect(initialState({ kind: 'number', value: null, formula: 'Height * 2' })).toEqual({ mode: 'formula', text: 'Height * 2' });
  });
  it('writes a formula and a value back as the engine takes them', () => {
    expect(formulaArgs(['a'], { name: 'W', kind: 'length', pset: 'My_Set' }, ' Height*2 ')).toEqual({ ids: ['a'], name: 'W', formula: 'Height*2', kind: 'length', pset: 'My_Set' });
    expect(valueArgs(['a'], { name: 'W', kind: 'bool' }, 1)).toMatchObject({ value: true });
    expect(valueArgs(['a'], { name: 'W', kind: 'number' }, null)).toMatchObject({ value: 0 });
  });
  it('shows a result by its kind and lists usable names', () => {
    expect(resultText(2, 'length', (m) => `${m * 1000} mm`)).toBe('2000 mm');
    expect(resultText(0, 'bool', String)).toBe('no');
    expect(resultText(45, 'angle', String)).toBe('45°');
    expect(usableNames([{ name: 'A' }, { name: 'B' }], { height: 3, name: 'x' }, 'A')).toEqual(['B', 'height']);
    expect(worthTrying('1+1', '1+1')).toBe(false);
    expect(worthTrying('', null)).toBe(false);
  });
});
