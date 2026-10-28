import { describe, expect, it } from 'vitest';
import { cssColor, drawingPoint, evenOddShapes, sectionPlanes, sectionPose, type ViewFrameDto } from './section3d';

// The engine's frame of a section drawn north from (3, −2) to (3, 2) (AnnotationTests): it looks west, its x runs north.
const F: ViewFrameDto = { origin: [3, -2, 0], right: [0, 1, 0], up: [0, 0, 1], dir: [-1, 0, 0] };
const kept = (planes: ReturnType<typeof sectionPlanes>, p: number[]) => planes.every((q) => q.normal[0] * p[0] + q.normal[1] * p[1] + q.normal[2] * p[2] + q.constant >= 0);

describe('a section seen in 3D', () => {
  it('lays a drawing point on the section plane: x along the line from its start, y up', () => {
    // The wall along y = 0 is cut 2 m along the line; 1.5 m up its cut is at (3, 0, 1.5).
    expect(drawingPoint(F, 2, 1.5)).toEqual({ x: 3, y: 0, z: 1.5 });
    // Lifted into the scene: westwards.
    expect(drawingPoint(F, 2, 1.5, 0.01).x).toBeCloseTo(2.99);
  });

  it('is not mirrored: looking along the section, the drawing runs to the right', () => {
    const [d, u, r] = [F.dir, F.up, F.right];
    const cross = [d[1] * u[2] - d[2] * u[1], d[2] * u[0] - d[0] * u[2], d[0] * u[1] - d[1] * u[0]];
    expect(cross.map((v) => v + 0)).toEqual(r);
  });

  it('cuts away what is in front of the line and, with a depth, what is beyond it', () => {
    const unlimited = sectionPlanes(F);
    expect(unlimited).toHaveLength(1);
    expect(kept(unlimited, [2, 0, 1])).toBe(true);    // west of the line: seen
    expect(kept(unlimited, [4, 0, 1])).toBe(false);   // east, between the viewer and the cut: gone
    expect(kept(unlimited, [-50, 0, 1])).toBe(true);
    const deep = sectionPlanes(F, 4);
    expect(deep).toHaveLength(2);
    expect(kept(deep, [0, 0, 1])).toBe(true);         // 3 m in
    expect(kept(deep, [-2, 0, 1])).toBe(false);       // 5 m in: past the depth
    expect(sectionPlanes(F, 1e4)).toHaveLength(1);    // the engine's unlimited
  });

  it('frames the section from the viewer\'s side, looking the way it looks (a little from the side)', () => {
    const p = sectionPose(F, [0, 0, 4, 3], Math.PI / 4);
    expect(Math.cos(p.azimuth)).toBeLessThan(-0.85);
    expect(p.pitch).toBeLessThan(0);
    expect(p.target).toEqual({ x: 3, y: 0, z: 1.5 });
  });

  it('fills loops even-odd: a loop inside another is its hole, one inside that an outline again', () => {
    const sq = (c: number, h: number) => [c - h, c - h, c + h, c - h, c + h, c + h, c - h, c + h];
    const shapes = evenOddShapes([sq(0, 1), sq(0, 3), sq(0, 0.5), sq(10, 1)]);
    expect(shapes).toHaveLength(3);
    const big = shapes.find((s) => s.outer[0] === -3)!;
    expect(big.holes).toEqual([sq(0, 1)]);
    expect(shapes.some((s) => s.outer[0] === -0.5 && s.holes.length === 0)).toBe(true);
  });

  it('reads pen colours with their opacity', () => {
    expect(cssColor('#a0a0a0')).toEqual({ hex: '#a0a0a0', opacity: 1 });
    expect(cssColor('rgba(255, 0, 16, 0.5)')).toEqual({ hex: '#ff0010', opacity: 0.5 });
    expect(cssColor('none')).toBeNull();
    expect(cssColor(null)).toBeNull();
  });
});
