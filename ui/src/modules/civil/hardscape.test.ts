import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { gradeText, hardscapeAt, kerbDrops, kerbEdges, parseGrade, raisedCorners, topGrips, withEdge } from './hardscape';

const node = (id: string, props: Record<string, unknown>): NodeDto => ({ id, type: 'LapinHardscape', name: id, parentId: null, px: 0, py: 0, props: props as NodeDto['props'], editable: true, module: 'civil', storeyId: null });
const rect = [[0, 0], [10, 0], [10, 6], [0, 6]];

describe('a hardscape in the interface', () => {
  it('reads a grade typed as a percentage, one in n or a fraction, and shows it as a percentage', () => {
    expect(parseGrade('2 %')).toBeCloseTo(0.02);
    expect(parseGrade('2,5%')).toBeCloseTo(0.025);
    expect(parseGrade('1:50')).toBeCloseTo(0.02);
    expect(parseGrade('0.015')).toBeCloseTo(0.015);
    expect(parseGrade('steep')).toBeNull();
    expect(parseGrade('1:0')).toBeNull();
    expect(gradeText(0.02)).toBe('2.0 %');
    expect(gradeText(0.125)).toBe('13 %');
  });

  it('keeps the kerb on every edge until some are chosen, and each dropped edge its reveal', () => {
    expect(kerbEdges(4, undefined)).toEqual([true, true, true, true]);
    expect(kerbEdges(4, [true, false])).toEqual([true, false, false, false]);
    expect(withEdge([true, true, true], 1, false)).toEqual([true, false, true]);
    expect(kerbDrops(3, [null, 0, 0.02])).toEqual([null, 0, 0.02]);
    expect(kerbDrops(2, undefined)).toEqual([null, null]);
  });

  it('finds the hardscape under a point, the selected one first', () => {
    const a = node('a', { contour: rect });
    const b = node('b', { contour: [[5, 2], [20, 2], [20, 5], [5, 5]] });
    expect(hardscapeAt([a, b], { x: 2, y: 2 })?.id).toBe('a');
    expect(hardscapeAt([a, b], { x: 7, y: 3 })?.id).toBe('b');
    expect(hardscapeAt([a, b], { x: 7, y: 3 }, ['a'])?.id).toBe('a');
    expect(hardscapeAt([a, b], { x: 30, y: 3 })).toBeUndefined();
  });

  it('has a height grip on each corner by its corners, else one for its elevation at its plane\'s origin', () => {
    const level = node('l', { contour: rect, topMode: 'level', elevation: 1, topZ: [1, 1, 1, 1] });
    expect(topGrips(level)).toEqual([{ key: 'hsTop', at: { x: 5, y: 3, z: 1 }, corner: null }]);
    const plane = node('p', { contour: rect, topMode: 'slope', elevation: 1, slopeOrigin: [0, 0], topZ: [1, 0.8, 0.8, 1] });
    expect(topGrips(plane)[0].at).toEqual({ x: 0, y: 0, z: 1 });
    const corners = node('c', { contour: rect, topMode: 'corners', topZ: [1, 1.1, 1.3, 1.2] });
    expect(topGrips(corners).map((g) => g.at.z)).toEqual([1, 1.1, 1.3, 1.2]);
    expect(raisedCorners(corners, 2, 0.2)).toEqual([1, 1.1, 1.5, 1.2]);
  });
});
