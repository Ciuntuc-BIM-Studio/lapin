import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { floorAt, gradeText, itemCount, nearestEdge, parseGrade, regionAt, roomAt, surfaceOf } from './floorSlopes';

const node = (id: string, type: string, contour: number[][], props: Record<string, unknown> = {}, storeyId = 's1'): NodeDto =>
  ({ id, type, name: id, parentId: storeyId, px: 0, py: 0, props: { contour, ...props }, editable: true, module: 'architecture', storeyId } as NodeDto);

const rect = (x0: number, y0: number, x1: number, y1: number) => [[x0, y0], [x1, y0], [x1, y1], [x0, y1]];

describe('floor slopes in the interface', () => {
  it('reads a grade as typed and writes it as plans do', () => {
    expect(parseGrade('1.5 %')).toBeCloseTo(0.015, 12);
    expect(parseGrade('1,5%')).toBeCloseTo(0.015, 12);
    expect(parseGrade('1:50')).toBeCloseTo(0.02, 12);
    expect(parseGrade('0.02')).toBeCloseTo(0.02, 12);
    expect(parseGrade('steep')).toBeNull();
    expect(parseGrade('1:0')).toBeNull();
    expect(gradeText(0.015)).toBe('1.5 %');
    expect(gradeText(0.005)).toBe('0.50 %');
  });

  it('finds the floor a click is on, the selected one first, on its storey only', () => {
    const a = node('a', 'IfcSlab', rect(0, 0, 4, 5));
    const b = node('b', 'IfcSlab', rect(2, 2, 6, 6));
    const c = node('c', 'IfcSlab', rect(0, 0, 4, 5), {}, 's2');
    expect(floorAt([a, b, c], { x: 3, y: 3 }, 's1')?.id).toBe('b');
    expect(floorAt([a, b, c], { x: 3, y: 3 }, 's1', ['a'])?.id).toBe('a');
    expect(floorAt([a, b, c], { x: 1, y: 1 }, 's2')?.id).toBe('c');
    expect(floorAt([a, b], { x: 9, y: 9 }, 's1')).toBeUndefined();
  });

  it('takes the edge nearest a click, by its index from its first corner', () => {
    const corners = rect(0, 0, 4, 5).map(([x, y]) => ({ x, y }));
    expect(nearestEdge(corners, { x: 2, y: 0.3 })).toEqual({ index: 0, distance: expect.closeTo(0.3, 12) });
    expect(nearestEdge(corners, { x: 3.9, y: 2 })?.index).toBe(1);
    expect(nearestEdge(corners, { x: 2, y: 4.8 })?.index).toBe(2);
    expect(nearestEdge(corners, { x: 0.1, y: 2 })?.index).toBe(3);
  });

  it('knows the region a click is in, a drawn one or a room\'s, and counts what a top holds', () => {
    const room = node('r', 'IfcSpace', rect(3, 1, 5, 3));
    const floor = node('f', 'IfcSlab', rect(0, 0, 6, 4), {
      surface: { edges: [{ edge: 0, grade: 0.01, fall: 'in' }], drains: [{ at: [1, 1], depth: 0.02 }], regions: [{ outline: rect(0.5, 0.5, 2, 2) }, { roomId: 'r' }] },
    });
    const rooms = new Map([[room.id, room]]);
    const s = surfaceOf(floor);
    expect(regionAt(s, { x: 1, y: 1 }, rooms)).toBe(0);
    expect(regionAt(s, { x: 4, y: 2 }, rooms)).toBe(1);
    expect(regionAt(s, { x: 5.5, y: 3.5 }, rooms)).toBeNull();
    expect(itemCount(s!)).toBe(2);
    expect(surfaceOf(node('g', 'IfcSlab', rect(0, 0, 1, 1)))).toBeNull();
    expect(roomAt([floor, room], { x: 4, y: 2 }, 's1')?.id).toBe('r');
  });
});
