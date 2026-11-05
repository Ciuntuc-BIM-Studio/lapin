import { describe, expect, it } from 'vitest';
import { orbitAbout, orbitAngles, orbitPivot, orthoZoomShift, zoomAbout, ZOOM_NEAR, type Rig, type V3 } from './navigate3d';

const sub = (a: V3, b: V3): V3 => ({ x: a.x - b.x, y: a.y - b.y, z: a.z - b.z });
const dot = (a: V3, b: V3) => a.x * b.x + a.y * b.y + a.z * b.z;
const cross = (a: V3, b: V3): V3 => ({ x: a.y * b.z - a.z * b.y, y: a.z * b.x - a.x * b.z, z: a.x * b.y - a.y * b.x });
const unit = (a: V3) => { const l = Math.hypot(a.x, a.y, a.z); return { x: a.x / l, y: a.y / l, z: a.z / l }; };
const dist = (a: V3, b: V3) => Math.hypot(a.x - b.x, a.y - b.y, a.z - b.z);

/** Where a point falls on the screen of a camera looking from the rig's position at its target, Z up (as three's lookAt). */
function screen(rig: Rig, p: V3, ortho?: number) {
  const f = unit(sub(rig.target, rig.position));
  const r = unit(cross(f, { x: 0, y: 0, z: 1 }));
  const u = cross(r, f);
  const v = sub(p, rig.position);
  if (ortho) return { x: dot(v, r) * ortho, y: dot(v, u) * ortho };
  const depth = dot(v, f);
  return { x: dot(v, r) / depth, y: dot(v, u) / depth };
}

const rig: Rig = { position: { x: 18, y: -22, z: 16 }, target: { x: 4, y: 3, z: 0 } };

describe('zoom to the cursor (report #89)', () => {
  const point = { x: 7, y: 1, z: 2.5 };

  it('keeps the point under the cursor where it is on the screen, in and out', () => {
    for (const k of [1.22, 1 / 1.22, 3]) {
      const next = zoomAbout(rig, point, k);
      const a = screen(rig, point), b = screen(next, point);
      expect(b.x).toBeCloseTo(a.x, 9);
      expect(b.y).toBeCloseTo(a.y, 9);
      expect(dist(next.position, point)).toBeCloseTo(dist(rig.position, point) / k, 9);
      // The view looks the same way: only the distance changes.
      const f0 = unit(sub(rig.target, rig.position)), f1 = unit(sub(next.target, next.position));
      expect(dot(f0, f1)).toBeCloseTo(1, 12);
    }
  });

  it('never comes closer to the point than the near limit, nor passes through it', () => {
    let r = rig;
    for (let i = 0; i < 200; i++) r = zoomAbout(r, point, 1.5);
    expect(dist(r.position, point)).toBeCloseTo(ZOOM_NEAR, 9);
    // The camera is still on the side it started on.
    expect(dot(sub(rig.position, point), sub(r.position, point))).toBeGreaterThan(0);
    // Out again from there.
    expect(dist(zoomAbout(r, point, 0.5).position, point)).toBeCloseTo(2 * ZOOM_NEAR, 9);
  });

  it('keeps the point in an orthographic view by sliding across it', () => {
    const dir = sub(rig.target, rig.position);
    const next = orthoZoomShift(rig, point, dir, 2, 2.44);
    const a = screen(rig, point, 2), b = screen(next, point, 2.44);
    expect(b.x).toBeCloseTo(a.x, 9);
    expect(b.y).toBeCloseTo(a.y, 9);
    // Slid across the view, not along it.
    expect(dot(sub(next.position, rig.position), unit(dir))).toBeCloseTo(0, 9);
  });
});

describe('orbit around the selection (report #89)', () => {
  it('turns about the selection\'s middle, else the point under the cursor, else the view\'s own centre', () => {
    expect(orbitPivot({ min: { x: 0, y: 0, z: 0 }, max: { x: 2, y: 4, z: 3 } }, { x: 9, y: 9, z: 9 })).toEqual({ x: 1, y: 2, z: 1.5 });
    expect(orbitPivot(null, { x: 9, y: 8, z: 7 })).toEqual({ x: 9, y: 8, z: 7 });
    expect(orbitPivot(null, null)).toBeNull();
  });

  it('keeps the pivot where it is on the screen, however far off the view\'s centre it is', () => {
    const pivot = { x: 1, y: 6, z: 1.5 };
    for (const [da, dp] of [[0.4, 0], [0, 0.3], [-0.7, -0.2], [2.5, 0.6]]) {
      const next = orbitAbout(rig, pivot, da, dp);
      const a = screen(rig, pivot), b = screen(next, pivot);
      expect(b.x).toBeCloseTo(a.x, 9);
      expect(b.y).toBeCloseTo(a.y, 9);
      expect(dist(next.position, pivot)).toBeCloseTo(dist(rig.position, pivot), 9);
    }
  });

  it('turns as the orbit controls do, and never over the top', () => {
    // A drag to the right turns the camera clockwise seen from above (the model follows the mouse); a drag down looks more from above.
    const { dAzimuth, dPolar } = orbitAngles(100, 50, 800);
    expect(dAzimuth).toBeLessThan(0);
    expect(dPolar).toBeLessThan(0);
    const az = (r: Rig) => Math.atan2(r.position.y - r.target.y, r.position.x - r.target.x);
    const polar = (r: Rig) => Math.acos((r.position.z - r.target.z) / dist(r.position, r.target));
    const next = orbitAbout(rig, rig.target, 0.3, -0.2);
    expect(az(next) - az(rig)).toBeCloseTo(0.3, 9);
    expect(polar(next) - polar(rig)).toBeCloseTo(-0.2, 9);
    const top = orbitAbout(rig, { x: 0, y: 0, z: 0 }, 0, -10);
    expect(polar(top)).toBeGreaterThan(0);
    expect(polar(top)).toBeLessThan(0.01);
  });
});
