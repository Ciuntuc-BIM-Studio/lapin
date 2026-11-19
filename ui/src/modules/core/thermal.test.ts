import { describe, expect, it } from 'vitest';
import { caseChoice, fmtLambda, fmtMm, fmtR, fmtU, isIndicative, parseResistance, type Thermal, type ThermalLayer } from './thermal';

describe('thermal formatting', () => {
  it('shows U and R to three decimals', () => {
    expect(fmtU(0.2134)).toBe('0.213');
    expect(fmtU(1)).toBe('1.000');
    expect(fmtR(4.6875)).toBe('4.688');
  });
  it('drops the zeros of λ and of millimetres', () => {
    expect(fmtLambda(0.04)).toBe('0.04');
    expect(fmtLambda(2.3)).toBe('2.3');
    expect(fmtLambda(0.1354)).toBe('0.135');
    expect(fmtMm(0.15)).toBe('150');
    expect(fmtMm(0.015)).toBe('15');
    expect(fmtMm(0.0075)).toBe('7.5');
  });
});

describe('parseResistance', () => {
  it('reads a number of 0 or more, with a comma or a point', () => {
    expect(parseResistance('0.13')).toBe(0.13);
    expect(parseResistance(' 0,04 ')).toBe(0.04);
    expect(parseResistance('0')).toBe(0);
    expect(parseResistance('.5')).toBe(0.5);
  });
  it('refuses what is not one', () => {
    for (const bad of ['', 'abc', '-0.1', '1.2.3', '0.1m', '10', '99']) expect(parseResistance(bad)).toBeNull();
  });
});

describe('the case and the layers', () => {
  it('chooses by kind when no own case is set', () => {
    expect(caseChoice({ caseOwn: null } as Thermal)).toBe('');
    expect(caseChoice({ caseOwn: 'wallGround' } as Thermal)).toBe('wallGround');
  });
  it('marks only the layers whose λ is by name', () => {
    expect(isIndicative({ source: 'indicative' } as ThermalLayer)).toBe(true);
    expect(isIndicative({ source: 'material' } as ThermalLayer)).toBe(false);
  });
});
