import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { lineOfEdge, ownPoints, parseSlope, readyToGrade, sideOf, slopeSide, slopeText, withPick } from './grading';

const node = (type: string, props: Record<string, unknown>): NodeDto => ({ id: 'n', type, name: 'n', props } as unknown as NodeDto);

describe('grading (site grading T5)', () => {
  it('tells the side of a line a click is on, by its nearest segment', () => {
    const line = [{ x: 0, y: 0 }, { x: 10, y: 0 }, { x: 10, y: 10 }];
    expect(sideOf(line, { x: 5, y: 2 })).toBe('left');
    expect(sideOf(line, { x: 5, y: -2 })).toBe('right');
    expect(sideOf(line, { x: 12, y: 5 })).toBe('right');
    expect(sideOf(line, { x: 8, y: 5 })).toBe('left');
  });

  it('slopes outwards from an outline, to the side clicked of an open line', () => {
    expect(slopeSide(node('LapinHardscape', { contour: [[0, 0], [10, 0], [10, 10]] }), { x: 5, y: 5 })).toBe('out');
    expect(slopeSide(node('LapinFeatureLine', { points: [[0, 0], [10, 0]] }), { x: 5, y: -1 })).toBe('right');
    expect(slopeSide(node('IfcPavement', { path: [[0, 0], [10, 0]] }), { x: 5, y: 1 })).toBe('left');
    expect(lineOfEdge(node('LapinFeatureLine', { points: [[0, 0], [1, 0], [1, 1]], closed: true })).closed).toBe(true);
  });

  it('reads a slope typed as 1:N, N or a percentage, and writes it back as 1:N', () => {
    expect(parseSlope('1:2')).toBe(2);
    expect(parseSlope(' 1 : 1,5 ')).toBe(1.5);
    expect(parseSlope('3')).toBe(3);
    expect(parseSlope('50 %')).toBe(2);
    expect(parseSlope('0')).toBeNull();
    expect(parseSlope('steep')).toBeNull();
    expect(slopeText(2)).toBe('1:2');
    expect(slopeText(1.5)).toBe('1:1.5');
  });

  it('grades once two edges are picked, unless more are to come; an edge picked again is let go', () => {
    let picked = withPick([], 'a');
    expect(readyToGrade(picked, false)).toBe(false);
    picked = withPick(picked, 'a');
    expect(picked).toEqual([]);
    picked = withPick(withPick(picked, 'a'), 'b');
    expect(readyToGrade(picked, false)).toBe(true);
    expect(readyToGrade(picked, true)).toBe(false);
  });

  it('reads an edge of its own points with their heights, a missing one left to the ground', () => {
    expect(ownPoints({ own: true, points: [[1, 2, -1.5], [3, 4], [5, 6, null]] })).toEqual([{ x: 1, y: 2, z: -1.5 }, { x: 3, y: 4, z: null }, { x: 5, y: 6, z: null }]);
  });
});
