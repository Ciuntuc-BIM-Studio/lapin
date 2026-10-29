import { describe, expect, it } from 'vitest';
import type { RenderItem } from '../engine/protocol';
import { alongAxis, boxCentre, boxEdges, boxPlanes, faceHandle, modelBounds, moveFace, MIN_SIZE, rotateBox, runOf, toBox, type SectionBox } from './sectionBox';

const box: SectionBox = { min: [0, 0, 0], max: [10, 6, 3] };
const keeps = (b: SectionBox, p: number[]) => boxPlanes(b).every((pl) => pl.normal[0] * p[0] + pl.normal[1] * p[1] + pl.normal[2] * p[2] + pl.constant >= -1e-9);
const near = (a: { x: number; y: number; z: number }, b: { x: number; y: number; z: number }) => {
  expect(a.x).toBeCloseTo(b.x); expect(a.y).toBeCloseTo(b.y); expect(a.z).toBeCloseTo(b.z);
};

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
    near(h.at, { x: 5, y: 6, z: 1.5 });
    near(h.out, { x: 0, y: 1, z: 0 });
    expect(boxEdges(box)).toHaveLength(12);
  });

  it('drags a face to where the pointer\'s ray passes nearest its axis', () => {
    // Looking down the y axis at x = 7: the x face's handle follows to 7.
    expect(alongAxis(box, { x: 10, y: 3, z: 1.5 }, 0, { x: 7, y: -20, z: 1.5 }, { x: 0, y: 1, z: 0 })).toBeCloseTo(7);
    // Along the axis itself: no answer.
    expect(alongAxis(box, { x: 10, y: 3, z: 1.5 }, 0, { x: -20, y: 3, z: 1.5 }, { x: 1, y: 0, z: 0 })).toBeNull();
  });

  it('turns about its middle, its size kept, and cuts along its turned faces', () => {
    const turned = rotateBox(box, Math.PI / 2);
    near(boxCentre(turned), boxCentre(box));
    expect(turned.max[0] - turned.min[0]).toBeCloseTo(10);
    // A quarter turn: its long side now runs along the world's y.
    expect(keeps(turned, [5, 7, 1.5])).toBe(true);
    expect(keeps(turned, [0.5, 3, 1.5])).toBe(false);
    // Its x face's handle points the turned x way, and a drag along it is read in the box's axes.
    near(faceHandle(turned, 'x1').out, { x: 0, y: 1, z: 0 });
    const at = faceHandle(turned, 'x1').at;
    const v = alongAxis(turned, at, 0, { x: -20, y: at.y - 2, z: 1.5 }, { x: 1, y: 0, z: 0 });
    expect(v).toBeCloseTo(toBox(turned, { x: at.x, y: at.y - 2, z: 1.5 })[0]);
  });

  it('fits round the model in its own turned axes, and takes the way an element runs', () => {
    const wall = { positions: new Float32Array([0, 0, 0, 10, 10, 0, 10, 10, 3]), parts: [], plan: [{ kind: 'cut', closed: false, points: [0, 0, 10, 10] }] } as unknown as RenderItem;
    const along = runOf(wall)!;
    expect(along).toBeCloseTo(Math.PI / 4);
    const b = modelBounds([wall], 0, along)!;
    expect(b.max[0] - b.min[0]).toBeCloseTo(Math.hypot(10, 10));
    expect(b.max[1] - b.min[1]).toBeCloseTo(0);
  });
});

describe('the model\'s bounds with instances (docs/plan-instancing.md)', () => {
  it('an instance counts by its mesh\'s box, placed', () => {
    const mesh = { id: 'm', parts: [], box: new Float32Array([-0.5, -0.5, 0, 0.5, 0.5, 1]) };
    const inst = { nodeId: 'a', positions: new Float32Array(), parts: [], instance: { mesh: 'm', matrix: new Float32Array([1, 0, 0, 0, 1, 0, 0, 0, 1, 20, 4, 0]) } } as unknown as RenderItem;
    const b = modelBounds([inst], 0, 0, new Map([['m', mesh]]))!;
    expect(b.min).toEqual([19.5, 3.5, 0]);
    expect(b.max).toEqual([20.5, 4.5, 1]);
  });
});

