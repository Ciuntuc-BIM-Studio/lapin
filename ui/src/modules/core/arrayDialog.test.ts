import { describe, expect, it } from 'vitest';
import { arrayFieldShown, checkArray, polarArgs, polarStep } from './arrayDialog';
import { readAnswers, shownFields, type Field } from '../../shell/AskBox';

const KEYS = ['kind', 'count', 'cols', 'rows', 'levels', 'by', 'dx', 'dy', 'dz', 'rx', 'ry', 'lz', 'angle', 'centreBy', 'cx', 'cy', 'spacing', 'start', 'end', 'offset', 'orient', 'flip'];
const shown = (v: Record<string, string | number>) => KEYS.filter((k) => arrayFieldShown(k, v));

describe('the Array dialog (forum 90)', () => {
  it('shows only the fields of the method chosen', () => {
    expect(shown({ kind: 'row', by: 'typed' })).toEqual(['kind', 'count', 'by', 'dx', 'dy', 'dz']);
    expect(shown({ kind: 'row', by: 'click' })).toEqual(['kind', 'count', 'by', 'dz']);
    expect(shown({ kind: 'grid', by: 'typed', levels: '1' })).toEqual(['kind', 'cols', 'rows', 'levels', 'by', 'dx', 'dy', 'dz', 'rx', 'ry']);
    expect(shown({ kind: 'grid', by: 'typed', levels: '2' })).toContain('lz');
    // Round a centre: the copies, the angle and the centre, nothing of a step.
    expect(shown({ kind: 'polar', centreBy: 'typed' })).toEqual(['kind', 'count', 'angle', 'centreBy', 'cx', 'cy']);
    expect(shown({ kind: 'polar', centreBy: 'click' })).toEqual(['kind', 'count', 'angle', 'centreBy']);
    expect(shown({ kind: 'path' })).toEqual(['kind', 'count', 'spacing', 'start', 'end', 'offset', 'orient', 'flip']);
  });

  it('takes a polar array whatever the step fields say, and names the field that is wrong', () => {
    // Petri's case: round a centre with a step of nothing is an array; its step is not asked.
    expect(checkArray({ kind: 'polar', count: 6, angle: 360, dx: 0, dy: 0, dz: 0, cx: 0, cy: 0 })).toBeNull();
    expect(checkArray({ kind: 'polar', count: 1, angle: 360 })?.key).toBe('count');
    expect(checkArray({ kind: 'polar', count: 4, angle: 0 })?.key).toBe('angle');
    expect(checkArray({ kind: 'polar', count: 4, angle: 400 })?.key).toBe('angle');
    expect(checkArray({ kind: 'polar', count: 4, angle: -90 })).toBeNull();
    // A row: its step, unless clicked.
    expect(checkArray({ kind: 'row', count: 3, by: 'typed', dx: 0, dy: 0, dz: 0 })).toEqual({ key: 'dx', message: 'Give the step from one copy to the next (step x, y or z).' });
    expect(checkArray({ kind: 'row', count: 3, by: 'click', dx: 0, dy: 0, dz: 0 })).toBeNull();
    expect(checkArray({ kind: 'row', count: 2.5, dx: 1 })?.key).toBe('count');
    // A grid: its counts, then each step it needs.
    expect(checkArray({ kind: 'grid', cols: 1, rows: 1, levels: 1 })?.key).toBe('cols');
    expect(checkArray({ kind: 'grid', cols: 3, rows: 2, levels: 1, by: 'typed', dx: 1, dy: 0, dz: 0, rx: 0, ry: 0 })?.key).toBe('ry');
    expect(checkArray({ kind: 'grid', cols: 3, rows: 2, levels: 2, by: 'typed', dx: 1, dy: 0, dz: 0, rx: 0, ry: 1, lz: 0 })?.key).toBe('lz');
    expect(checkArray({ kind: 'grid', cols: 0, rows: 2, levels: 1 })?.key).toBe('cols');
    expect(checkArray({ kind: 'grid', cols: 3, rows: 2, levels: 1, by: 'typed', dx: 1, dy: 0, dz: 0, rx: 0, ry: 1 })).toBeNull();
    // Along a path: copies, or a spacing with none.
    expect(checkArray({ kind: 'path', count: 0, spacing: 0.5 })).toBeNull();
    expect(checkArray({ kind: 'path', count: 0, spacing: 0 })?.key).toBe('count');
  });

  it('turns polar copies as the engine shares them out', () => {
    expect(polarStep(4, 360)).toBe(90);
    expect(polarStep(3, 180)).toBe(90);
    expect(polarArgs(['a'], { count: 6, angle: 360 }, { x: 1, y: 2 })).toEqual({ ids: ['a'], kind: 'polar', count: 6, angle: 360, center: [1, 2] });
  });
});

describe('the box that asks', () => {
  const fields: Field[] = [
    { key: 'kind', label: 'Kind', value: 'row', choices: [{ value: 'row', label: 'Row' }, { value: 'polar', label: 'Polar' }] },
    { key: 'dx', label: 'Step x', value: '1', length: true, show: (v) => v.kind === 'row' },
    { key: 'angle', label: 'Angle', value: '360', show: (v) => v.kind === 'polar' },
  ];
  const parse = (t: string) => (/^-?\d+(\.\d+)?$/.test(t) ? Number(t) : null);

  it('shows a field only for the answers it belongs to', () => {
    expect(shownFields(fields, { kind: 'polar' }).map((f) => f.key)).toEqual(['kind', 'angle']);
  });

  it('names a shown field that does not read, and leaves out a hidden one', () => {
    expect(readAnswers(fields, { kind: 'row', dx: 'abc', angle: '360' }, parse).problem).toEqual({ key: 'dx', message: 'Not a length.' });
    expect(readAnswers(fields, { kind: 'polar', dx: 'abc', angle: '90' }, parse)).toEqual({ out: { kind: 'polar', angle: 90 }, problem: null });
  });
});
