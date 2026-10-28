import { describe, expect, it } from 'vitest';
import { alongAxis, boxEdges, boxPlanes, faceHandle, moveFace, MIN_SIZE, type SectionBox } from './sectionBox';

const box: SectionBox = { min: [0, 0, 0], max: [10, 6, 3] };
const keeps = (b: SectionBox, p: number[]) => boxPlanes(b).every((pl) => pl.normal[0] * p[0] + pl.normal[1] * p[1] + pl.normal[2] * p[2] + pl.constant >= 0);

describe('the section box', () => {
  it('keeps what is inside it and cuts the rest, on each of its six faces', () => {
    expect(boxPlanes(box)).toHaveLength(6);
    expect(keeps(box, [5, 3, 1.5])).toBe(true);
    expect(keeps(box, [11, 3, 1.5])).toBe(false);
    expect(keeps(box, [5, -1, 1.5])).toBe(false);
    expect(keeps(box, [5, 3, 3.5])).toBe(false);
  });

  it('moves one face, never through the opposite one', () => {
    expect(moveFace(box, 'z1', 1.2).max).toEqual([10, 6, 1.2]);
    expect(moveFace(box, 'x0', 4).min).toEqual([4, 0, 0]);
    expect(moveFace(box, 'z1', -5).max[2]).toBeCloseTo(MIN_SIZE);
  });

  it('has its handles on its faces\' middles, pointing out', () => {
    const h = faceHandle(box, 'y1');
    expect(h.at).toEqual({ x: 5, y: 6, z: 1.5 });
    expect(h.out).toEqual({ x: 0, y: 1, z: 0 });
    expect(boxEdges(box)).toHaveLength(12);
  });

  it('drags a face to where the pointer\'s ray passes nearest its axis', () => {
    // Looking down the y axis at x = 7: the x face's handle follows to 7.
    const v = alongAxis({ x: 10, y: 3, z: 1.5 }, 0, { x: 7, y: -20, z: 1.5 }, { x: 0, y: 1, z: 0 });
    expect(v).toBeCloseTo(7);
    // Along the axis itself: no answer.
    expect(alongAxis({ x: 10, y: 3, z: 1.5 }, 0, { x: -20, y: 3, z: 1.5 }, { x: 1, y: 0, z: 0 })).toBeNull();
  });
});
