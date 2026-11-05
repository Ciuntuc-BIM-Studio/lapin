import { describe, expect, it } from 'vitest';
import { balanced, insertField, materialFields, orderLabels, parseWidth, wrapOptional } from './buildupTag';

describe('the build-up tag editor', () => {
  it('offers the materials\' parameters by their symbols where the engine reads one, else by name, each once', () => {
    expect(materialFields([
      { params: [{ pset: 'Pset_MaterialThermal', name: 'ThermalConductivity' }, { name: 'Reaction to fire' }] },
      { params: [{ pset: 'Pset_MaterialThermal', name: 'ThermalConductivity' }, { pset: 'Pset_MaterialHygroscopic', name: 'UpperVaporResistanceFactor' }, { name: ' ' }] },
      { params: 'nonsense' },
      {},
    ])).toEqual(['λ', 'Reaction to fire', 'μ']);
  });

  it('puts a field where the caret is, over what is selected', () => {
    expect(insertField('{description}:  mm', 15, 15, 'thickness')).toEqual({ text: '{description}: {thickness} mm', caret: 26 });
    expect(insertField('a X b', 2, 3, 'λ')).toEqual({ text: 'a {λ} b', caret: 5 });
    expect(insertField('', 9, 9, 'note').text).toBe('{note}');
  });

  it('makes the selection an optional part, or one for a field', () => {
    expect(wrapOptional('{description}, λ = {λ}', 13, 22)).toEqual({ text: '{description}[, λ = {λ}]', caret: 24 });
    expect(wrapOptional('{description}: {thickness}', 13, 13, 'μ').text).toBe('{description}[, μ = {μ}]: {thickness}');
    expect(wrapOptional('ab', 1, 1)).toEqual({ text: 'a[]b', caret: 2 });
  });

  it('tells a template whose brackets do not close', () => {
    expect(balanced('{description}[, λ = {λ} W/mK]: {thickness} mm')).toBe(true);
    expect(balanced('\\[{a}\\]')).toBe(true);
    expect(balanced('{a}[, b')).toBe(false);
    expect(balanced('{a{b}}')).toBe(false);
    expect(balanced('a]')).toBe(false);
  });

  it('names the order by what the sample is, and reads a width', () => {
    expect(orderLabels(false)[0][1]).toBe('exterior → interior');
    expect(orderLabels(true)[1]).toEqual(['in-out', 'bottom → top']);
    expect(parseWidth('62,5')).toBe(62.5);
    expect(parseWidth('5')).toBeNull();
    expect(parseWidth('wide')).toBeNull();
  });
});
