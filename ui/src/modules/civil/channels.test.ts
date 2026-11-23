import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { dropArg, dropEnds, dropRows, followArg, gradeShown, invertArgs, movedOutlet, nearestEdge, nextDrop, readGrade, withOutlet, withoutOutlet } from './channels';

const node = (type: string, props: Record<string, unknown>): NodeDto => ({ id: 'n1', type, name: 'N', props, editable: true } as unknown as NodeDto);
const level = (t: string) => { const v = Number(t.replace(',', '.')); return Number.isFinite(v) ? v : null; };

describe('a channel\'s grade', () => {
  it('reads a percentage, one in n and a fraction', () => {
    expect(readGrade('0.5 %')).toBeCloseTo(0.005);
    expect(readGrade('1:200')).toBeCloseTo(0.005);
    expect(readGrade('0.005')).toBeCloseTo(0.005);
    expect(readGrade('-1 %')).toBeCloseTo(-0.01);
  });
  it('refuses what does not read or is steeper than 50 %', () => {
    expect(readGrade('')).toBeNull();
    expect(readGrade('flat')).toBeNull();
    expect(readGrade('60 %')).toBeNull();
  });
  it('shows a grade as a percentage', () => {
    expect(gradeShown(0.005)).toBe('0.5 %');
    expect(gradeShown(0.02)).toBe('2 %');
  });
});

describe('the invert of a channel', () => {
  it('at a depth sends only the mode', () => {
    expect(invertArgs({ invert: 'depth', grade: 'x', startZ: '' }, level)).toEqual({ args: { invert: 'depth' } });
  });
  it('at a grade sends the grade and the start level, empty taking the top at its start', () => {
    expect(invertArgs({ invert: 'grade', grade: '0.5 %', startZ: '-0.35' }, level)).toEqual({ args: { invert: 'grade', grade: 0.005, startZ: -0.35 } });
    expect(invertArgs({ invert: 'grade', grade: '1:200', startZ: '' }, level)).toEqual({ args: { invert: 'grade', grade: 0.005, startZ: null } });
  });
  it('says which of the two does not read', () => {
    expect(invertArgs({ invert: 'grade', grade: 'steep', startZ: '' }, level)).toEqual({ bad: 'grade' });
    expect(invertArgs({ invert: 'grade', grade: '1 %', startZ: 'deep' }, level)).toEqual({ bad: 'start' });
  });
});

describe('the line a click follows', () => {
  const square = [{ x: 0, y: 0 }, { x: 10, y: 0 }, { x: 10, y: 10 }, { x: 0, y: 10 }];
  it('finds the nearest edge of an outline, the closing one included', () => {
    expect(nearestEdge(square, { x: 5, y: 0.2 }, 1.5)).toBe(0);
    expect(nearestEdge(square, { x: 0.3, y: 5 }, 1.5)).toBe(3);
    expect(nearestEdge(square, { x: 5, y: 5 }, 1.5)).toBeNull();
  });
  it('takes one edge of a hardscape when the click is near it, else the whole ring', () => {
    const h = node('LapinHardscape', { contour: square.map((p) => [p.x, p.y]) });
    expect(followArg(h, { x: 10.1, y: 5 })).toEqual({ id: 'n1', edge: 1 });
    expect(followArg(h, { x: 5, y: 5 })).toEqual({ id: 'n1' });
  });
  it('takes the side of a path\'s carriageway that was clicked', () => {
    const p = node('IfcPavement', { path: [[0, 0], [10, 0]] });
    expect(followArg(p, { x: 5, y: 2 })).toEqual({ id: 'n1', side: 'left' });
    expect(followArg(p, { x: 5, y: -2 })).toEqual({ id: 'n1', side: 'right' });
  });
  it('follows anything else as itself', () => {
    expect(followArg(node('LapinFeatureLine', {}), { x: 0, y: 0 })).toEqual({ id: 'n1' });
  });
});

describe('a channel\'s outlets', () => {
  it('adds one in order, never twice at a station', () => {
    expect(withOutlet([5], 2)).toEqual([2, 5]);
    expect(withOutlet([2, 5], 5)).toEqual([2, 5]);
  });
  it('takes one away, down to none', () => {
    expect(withoutOutlet([2, 5], 0)).toEqual([5]);
    expect(withoutOutlet([5], 0)).toEqual([]);
  });
  it('moves one', () => {
    expect(movedOutlet([2, 5], 0, 7)).toEqual([5, 7]);
    expect(movedOutlet([2, 5], 0, 5)).toEqual([5]);
  });
});

describe('a kerb\'s drops', () => {
  it('reads and orders those stored, leaving out what is malformed', () => {
    expect(dropRows([{ from: 6, to: 8, reveal: 0 }, { from: 1, to: 2, reveal: 0.02 }, { from: 'x' }])).toEqual([{ from: 1, to: 2, reveal: 0.02 }, { from: 6, to: 8, reveal: 0 }]);
    expect(dropRows(undefined)).toEqual([]);
  });
  it('puts the next one after the last, a metre long, inside the kerb', () => {
    expect(nextDrop([], 20)).toEqual({ from: 0, to: 1, reveal: 0 });
    expect(nextDrop([{ from: 0, to: 3, reveal: 0 }], 20)).toEqual({ from: 3, to: 4, reveal: 0 });
    expect(nextDrop([{ from: 0, to: 19.5, reveal: 0 }], 20)).toEqual({ from: 19.5, to: 20, reveal: 0 });
    expect(nextDrop([], 0.005)).toBeNull();
  });
  it('orders and clips the ends typed, and refuses an empty stretch', () => {
    expect(dropEnds(8, 2, 10)).toEqual({ from: 2, to: 8 });
    expect(dropEnds(-1, 12, 10)).toEqual({ from: 0, to: 10 });
    expect(dropEnds(3, 3, 10)).toBeNull();
    expect(dropEnds(Number.NaN, 3, 10)).toBeNull();
  });
  it('asks the engine to take drops away with a null reveal', () => {
    expect(dropArg(1, 2, null)).toEqual({ drop: { from: 1, to: 2, reveal: null } });
  });
});

import { kindDefault, sizeFields } from './channels';

describe('a channel\'s sizes', () => {
  it('shows each section its own', () => {
    expect(sizeFields('u')).toEqual(['width', 'depth', 'wall', 'base']);
    expect(sizeFields('grated')).toContain('grating');
    expect(sizeFields('slot')).toEqual(expect.arrayContaining(['slot', 'neck']));
    expect(sizeFields('profile')).toEqual(['depth']);
  });
  it('takes a default from the catalogue', () => {
    const catalog = [{ key: 'v', width: 0.6, depth: 0.15 }];
    expect(kindDefault(catalog, 'v', 'width')).toBe(0.6);
    expect(kindDefault(catalog, 'u', 'depth')).toBe(0.3);
    expect(kindDefault(undefined, 'u', 'wall')).toBe(0);
  });
});
