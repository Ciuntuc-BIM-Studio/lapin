import { describe, expect, it } from 'vitest';
import { boxOfPlanes, distanceTo, fromBcfCamera, planesOfBox, toBcfCamera, upOf, type Lens } from './bcfCamera';
import { lookOf } from '../../../viewport/camera';

const FOV = (45 * Math.PI) / 180;
const lens = (p: Partial<Lens> = {}): Lens => ({ azimuth: Math.PI / 4, pitch: -0.5, distance: 20, target: { x: 6, y: 4, z: 1 }, fov: FOV, ortho: false, zoom: 1, aspect: 1.5, ...p });
const near = (a: number[], b: number[], eps = 1e-5) => a.forEach((v, i) => expect(v).toBeCloseTo(b[i], Math.round(-Math.log10(eps))));

describe('a BCF camera from the 3D view', () => {
  it('stands where the camera does, looks along its pose, with Z projected as its up and the lens in degrees', () => {
    const c = toBcfCamera(lens());
    expect(c.perspective).toBe(true);
    const d = lookOf({ azimuth: Math.PI / 4, pitch: -0.5 });
    near(c.direction, [d.x, d.y, d.z]);
    near(c.viewPoint, [6 - d.x * 20, 4 - d.y * 20, 1 - d.z * 20]);
    expect(c.fieldOfView).toBeCloseTo(45, 5);
    expect(c.aspectRatio).toBe(1.5);
    // The up vector is perpendicular to the direction and has a positive Z.
    expect(c.up[0] * c.direction[0] + c.up[1] * c.direction[1] + c.up[2] * c.direction[2]).toBeCloseTo(0, 5);
    expect(c.up[2]).toBeGreaterThan(0);
    expect(Math.hypot(...c.up)).toBeCloseTo(1, 5);
  });

  it('writes an orthogonal camera with the view height in metres: 2·distance·tan(fov/2)/zoom', () => {
    const c = toBcfCamera(lens({ ortho: true, distance: 20, zoom: 2 }));
    expect(c.perspective).toBe(false);
    expect(c.fieldOfView).toBe(0);
    expect(c.viewToWorldScale).toBeCloseTo((2 * 20 * Math.tan(FOV / 2)) / 2, 5);
  });

  it('gives a straight-down look an up vector in plan', () => {
    near(upOf([0, 0, -1]), [0, 1, 0]);
    near(upOf([0, 1, 0]), [0, 0, 1]);
  });
});

describe('the 3D view from a BCF camera', () => {
  it('comes back to the pose it was written from, at the distance given', () => {
    const l = lens();
    const { pose, ortho } = fromBcfCamera(toBcfCamera(l), FOV, 20);
    expect(ortho).toBe(false);
    expect(pose.azimuth).toBeCloseTo(l.azimuth, 5);
    expect(pose.pitch).toBeCloseTo(l.pitch, 5);
    expect(pose.distance).toBeCloseTo(20, 5);
    near([pose.target.x, pose.target.y, pose.target.z], [6, 4, 1]);
  });

  it('takes an orthogonal camera back at the distance its height means through the view fov, zoom 1', () => {
    const l = lens({ ortho: true, distance: 20, zoom: 1 });
    const { pose, ortho } = fromBcfCamera(toBcfCamera(l), FOV);
    expect(ortho).toBe(true);
    expect(pose.distance).toBeCloseTo(20, 5);
    near([pose.target.x, pose.target.y, pose.target.z], [6, 4, 1]);
    // Another tool's plan view, 18 m high: the distance the lens needs for that height.
    const plan = fromBcfCamera({ perspective: false, viewPoint: [5, 4, 40], direction: [0, 0, -1], up: [0, 1, 0], fieldOfView: 0, viewToWorldScale: 18 }, FOV);
    expect(plan.pose.distance).toBeCloseTo(18 / (2 * Math.tan(FOV / 2)), 5);
  });

  it('reads a straight-down look with its azimuth from the up vector, its pitch held within what the view turns to', () => {
    const { pose } = fromBcfCamera({ perspective: true, viewPoint: [5, 4, 40], direction: [0, 0, -1], up: [0, 1, 0], fieldOfView: 60, viewToWorldScale: 0 }, FOV, 40);
    expect(pose.azimuth).toBeCloseTo(Math.PI / 2, 5);
    expect(pose.pitch).toBeCloseTo(-Math.PI / 2 + 0.01, 5);
    expect(pose.target.z).toBeLessThan(1);
    const west = fromBcfCamera({ perspective: true, viewPoint: [5, 4, 40], direction: [0, 0, -1], up: [-1, 0, 0], fieldOfView: 60, viewToWorldScale: 0 }, FOV, 40);
    expect(west.pose.azimuth).toBeCloseTo(Math.PI, 5);
  });

  it('measures how far a point is along the look, for the target', () => {
    const c = { perspective: true, viewPoint: [0, 0, 0] as [number, number, number], direction: [1, 0, 0] as [number, number, number], up: [0, 0, 1] as [number, number, number], fieldOfView: 45, viewToWorldScale: 0 };
    expect(distanceTo(c, [8, 3, 0])).toBe(8);
    expect(distanceTo(c, [-8, 3, 0])).toBeNull();
  });
});

describe('clipping planes and the section box', () => {
  it('writes the six faces, each normal pointing out of the box, and reads them back as the box', () => {
    const box = { min: [0, 1, -0.5] as [number, number, number], max: [12, 8, 3] as [number, number, number] };
    const planes = planesOfBox(box);
    expect(planes).toHaveLength(6);
    const top = planes.find((p) => p.direction[2] === 1)!;
    expect(top.location[2]).toBe(3);
    const bottom = planes.find((p) => p.direction[2] === -1)!;
    expect(bottom.location[2]).toBe(-0.5);
    const fallback = { min: [-100, -100, -100] as [number, number, number], max: [100, 100, 100] as [number, number, number] };
    expect(boxOfPlanes(planes, fallback)).toEqual(box);
  });

  it('takes a single plane (a tool\'s cut at a height) over the model\'s bounds, and nothing when the planes are off the axes', () => {
    const fallback = { min: [0, 0, 0] as [number, number, number], max: [12, 8, 6] as [number, number, number] };
    expect(boxOfPlanes([{ location: [0, 0, 2.5], direction: [0, 0, 1] }], fallback)).toEqual({ min: [0, 0, 0], max: [12, 8, 2.5] });
    expect(boxOfPlanes([{ location: [0, 0, 2.5], direction: [0.7071, 0.7071, 0] }], fallback)).toBeNull();
    expect(boxOfPlanes([], fallback)).toBeNull();
    expect(boxOfPlanes([{ location: [0, 0, 2.5], direction: [0, 0, 1] }], null)).toBeNull();
  });

  it('keeps a turned box through its planes', () => {
    const box = { min: [0, 0, 0] as [number, number, number], max: [10, 6, 3] as [number, number, number], rot: Math.PI / 6 };
    const back = boxOfPlanes(planesOfBox(box), { min: [-100, -100, -100], max: [100, 100, 100] }, Math.PI / 6)!;
    near(back.min, box.min);
    near(back.max, box.max);
    expect(back.rot).toBeCloseTo(Math.PI / 6);
  });
});
