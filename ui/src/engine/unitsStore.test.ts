import { describe, expect, it } from 'vitest';
import { formatLength, lengthText, parseImperial, parseLength, useStandards } from './unitsStore';

describe('project units', () => {
  it('formats a length in the project unit', () => {
    expect(formatLength(2.4, 'mm')).toBe('2400');
    expect(formatLength(2.4, 'cm')).toBe('240');
    expect(formatLength(2.4, 'm')).toBe('2.4');
    expect(formatLength(12 * 0.3048, 'ft-in')).toBe("12'-0\"");
    expect(formatLength(147.5 * 0.0254, 'ft-in')).toBe("12'-3 1/2\"");
    expect(formatLength(147.5 * 0.0254, 'in')).toBe('147 1/2"');
    expect(formatLength(0.5 * 0.0254, 'ft-in')).toBe('1/2"');
    expect(formatLength(0, 'ft-in')).toBe('0"');
    expect(lengthText(2.4, 'mm')).toBe('2400 mm');
    expect(lengthText(0.3048, 'ft-in')).toBe("1'-0\"");
  });
  it('reads feet and inches however they are typed', () => {
    expect(parseImperial("12'-3 1/2\"")).toBeCloseTo(147.5 * 0.0254, 9);
    expect(parseImperial("3'6\"")).toBeCloseTo(42 * 0.0254, 9);
    expect(parseImperial("3' 6")).toBeCloseTo(42 * 0.0254, 9);
    expect(parseImperial("42\"")).toBeCloseTo(42 * 0.0254, 9);
    expect(parseImperial("3.5'")).toBeCloseTo(42 * 0.0254, 9);
    expect(parseImperial("6 1/2\"")).toBeCloseTo(6.5 * 0.0254, 9);
    expect(parseImperial("1/2\"")).toBeCloseTo(0.5 * 0.0254, 9);
    expect(parseImperial('2400')).toBeNull();
  });
  it('reads a typed length in the project unit unless it names its own', () => {
    expect(parseLength('2400', 'mm')).toBeCloseTo(2.4, 9);
    expect(parseLength('2.4m', 'mm')).toBeCloseTo(2.4, 9);
    expect(parseLength('3000+250', 'mm')).toBeCloseTo(3.25, 9);
    expect(parseLength('240', 'cm')).toBeCloseTo(2.4, 9);
    expect(parseLength('2,5', 'm')).toBeCloseTo(2.5, 9);
    expect(parseLength('12', 'ft-in')).toBeCloseTo(12 * 0.3048, 9);
    expect(parseLength('6', 'in')).toBeCloseTo(6 * 0.0254, 9);
    expect(parseLength('12in', 'mm')).toBeCloseTo(0.3048, 9);
    expect(parseLength("3'6\"", 'mm')).toBeCloseTo(42 * 0.0254, 9);
    useStandards.setState({ units: 'ft-in' });
    expect(parseLength('10')).toBeCloseTo(3.048, 9);
    expect(formatLength(3.048)).toBe("10'-0\"");
    useStandards.setState({ units: 'mm' });
  });
  it('keeps a metric length to a tenth of a millimetre, whole ones without decimals (report #94)', () => {
    // A 12.5 mm gypsum board typed, kept and shown as typed, in each metric unit.
    expect(parseLength('12.5', 'mm')).toBeCloseTo(0.0125, 12);
    expect(parseLength('12,5', 'mm')).toBeCloseTo(0.0125, 12);
    expect(parseLength('1.25', 'cm')).toBeCloseTo(0.0125, 12);
    expect(parseLength('1,25', 'cm')).toBeCloseTo(0.0125, 12);
    expect(parseLength('0.1', 'mm')).toBeCloseTo(0.0001, 12);
    expect(formatLength(0.0125, 'mm')).toBe('12.5');
    expect(formatLength(0.0125, 'cm')).toBe('1.25');
    expect(formatLength(0.0125, 'm')).toBe('0.0125');
    expect(formatLength(0.0001, 'mm')).toBe('0.1');
    expect(formatLength(0.3375, 'mm')).toBe('337.5');
    expect(lengthText(0.0125, 'mm')).toBe('12.5 mm');
    // Whole values stay whole (no trailing zeros), finer ones round to the tenth, no "-0".
    expect(formatLength(0.012, 'mm')).toBe('12');
    expect(formatLength(0.1 + 0.2, 'mm')).toBe('300');
    expect(formatLength(1.23456, 'mm')).toBe('1234.6');
    expect(formatLength(1.23456, 'cm')).toBe('123.46');
    expect(formatLength(-0.00001, 'mm')).toBe('0');
    expect(formatLength(-0.0125, 'mm')).toBe('-12.5');
    for (const u of ['mm', 'cm', 'm'] as const) expect(parseLength(formatLength(0.0125, u), u)).toBeCloseTo(0.0125, 12);
    // Imperial keeps its sixteenths.
    expect(formatLength(0.0125, 'in')).toBe('1/2"');
  });
});
