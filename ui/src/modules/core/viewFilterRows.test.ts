import { describe, expect, it } from 'vitest';
import { cellMode, cellValue, fieldAllowed, filterRowPayload, filtersPayload, withCutLine, KNOWN_FILTER_FIELDS } from './viewFilterRows';

describe('a view filter row', () => {
  it('is sent without what is not set', () => {
    expect(filterRowPayload({ id: 'f1', hidden: undefined, cutHatch: 'CROSS', cutFill: 'none', hatchHalftone: true })).toEqual({ id: 'f1', cutHatch: 'CROSS', cutFill: 'none', hatchHalftone: true });
  });

  it('is sent with only the fields the engine says it knows (it refuses the others)', () => {
    const row = { id: 'f1', color: '#ff0000', cutHatch: 'CROSS', surfaceFill: '#ffee00', cutLine: { color: '#ff0000', weight: 0.7 } };
    expect(filterRowPayload(row, ['id', 'color', 'cutHatch'])).toEqual({ id: 'f1', color: '#ff0000', cutHatch: 'CROSS' });
    expect(filterRowPayload(row, ['id'])).toEqual({ id: 'f1' });
    expect(filterRowPayload(row)).toEqual(row);
    expect(fieldAllowed(undefined, 'cutLine')).toBe(true);
    expect(fieldAllowed(['id'], 'cutLine')).toBe(false);
    expect(KNOWN_FILTER_FIELDS).toContain('surfaceHatch');
  });

  it('keeps an unknown field of an older engine out of the whole list', () => {
    expect(filtersPayload([{ id: 'a', cutHatch: 'X' }, { id: 'b', hatchColor: '#000000' }], ['id', 'hatchColor'])).toEqual([{ id: 'a' }, { id: 'b', hatchColor: '#000000' }]);
  });

  it('gives the cut line a colour and a weight apart, and drops it when both are cleared', () => {
    const a = withCutLine({ id: 'f' }, { color: '#ff0000' });
    expect(a.cutLine).toEqual({ color: '#ff0000' });
    const b = withCutLine(a, { weight: 0.7 });
    expect(b.cutLine).toEqual({ color: '#ff0000', weight: 0.7 });
    const c = withCutLine(withCutLine(b, { color: undefined }), { weight: undefined });
    expect(c).toEqual({ id: 'f' });
    expect(filterRowPayload({ id: 'f', cutLine: {} })).toEqual({ id: 'f' });
  });

  it('shows a hatch or a fill as not set, none or a value, and goes between them', () => {
    expect(cellMode(undefined)).toBe('unset');
    expect(cellMode('none')).toBe('none');
    expect(cellMode('#000000')).toBe('value');
    expect(cellValue('unset', '#111111', '#000000')).toBeUndefined();
    expect(cellValue('none', '#111111', '#000000')).toBe('none');
    expect(cellValue('value', 'none', '#000000')).toBe('#000000');
    expect(cellValue('value', '#111111', '#000000')).toBe('#111111');
  });
});
