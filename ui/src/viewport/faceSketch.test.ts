import { describe, expect, it } from 'vitest';
import { circleOnFace, frameOf, local, onPlane, parsePair, rectOnFace, rectTyped, roundOnFace, snapPoints } from './faceSketch';

const east = [{ x: 2, y: 0, z: 0 }, { x: 2, y: 2, z: 0 }, { x: 2, y: 2, z: 3 }, { x: 2, y: 0, z: 3 }];

describe('drawing on a face', () => {
  it('reads a face as seen from outside: x left to right and level, y up (decision 378)', () => {
    // The east side, tall and narrow: x still level (north, the right hand looking west at it), y up.
    const f = frameOf({ x: 1, y: 0, z: 0 }, east);
    expect(f.right).toEqual({ x: 0, y: 1, z: 0 });
    expect(f.up.z).toBeCloseTo(1);
    // A north face seen from the north: its right is west; a top face: the model's x and y.
    const north = frameOf({ x: 0, y: 1, z: 0 }, []);
    expect(north.right.x).toBeCloseTo(-1);
    expect(north.up.z).toBeCloseTo(1);
    const top = frameOf({ x: 0, y: 0, z: 1 }, []);
    expect(top.right).toEqual({ x: 1, y: 0, z: 0 });
    expect(top.up.y).toBeCloseTo(1);
    // A roof sloping down to the south: x along the eaves, y up the slope.
    const c = Math.cos(Math.PI / 6), sn = Math.sin(Math.PI / 6);
    const roof = frameOf({ x: 0, y: -sn, z: c }, []);
    expect(roof.right.x).toBeCloseTo(1);
    expect(roof.up.z).toBeGreaterThan(0);
  });

  it('meets the face\'s plane along a ray, not behind it', () => {
    const f = frameOf({ x: 1, y: 0, z: 0 }, east);
    expect(onPlane(f, { x: 10, y: 1, z: 1 }, { x: -1, y: 0, z: 0 })).toEqual({ x: 2, y: 1, z: 1 });
    expect(onPlane(f, { x: 10, y: 1, z: 1 }, { x: 1, y: 0, z: 0 })).toBeNull();
    expect(onPlane(f, { x: 10, y: 1, z: 1 }, { x: 0, y: 1, z: 0 })).toBeNull();
  });

  it('keeps a rectangle square to the face and on it, from two corners or typed', () => {
    const f = frameOf({ x: 1, y: 0, z: 0 }, east);
    const r = rectOnFace(f, { x: 2, y: 0.5, z: 0.5 }, { x: 2, y: 1.5, z: 2 });
    expect(r.every((p) => Math.abs(p.x - 2) < 1e-9)).toBe(true);
    const zs = r.map((p) => p.z).sort(), ys = r.map((p) => p.y).sort();
    expect(zs[0]).toBeCloseTo(0.5); expect(zs[3]).toBeCloseTo(2);
    expect(ys[0]).toBeCloseTo(0.5); expect(ys[3]).toBeCloseTo(1.5);
    // Typed 1 × 0.4 the way the cursor lies from the first corner.
    const t = rectTyped(f, { x: 2, y: 1, z: 1 }, { x: 2, y: 0.2, z: 1.6 }, 1, 0.4);
    const a = local(f, t[0]), c = local(f, t[2]);
    expect(Math.abs(c.x - a.x)).toBeCloseTo(1);
    expect(Math.abs(c.y - a.y)).toBeCloseTo(0.4);
  });

  it('snaps to the face\'s corners, edge middles and middle, else to the centimetre', () => {
    const s = snapPoints(east);
    expect(s).toHaveLength(9);
    expect(s[8].at).toEqual({ x: 2, y: 1, z: 1.5 });
    const f = frameOf({ x: 1, y: 0, z: 0 }, east);
    const p = roundOnFace(f, { x: 2, y: 0.4567, z: 1.2345 });
    expect(p.y).toBeCloseTo(0.46); expect(p.z).toBeCloseTo(1.23);
  });

  it('draws a circle round its centre on the face', () => {
    const f = frameOf({ x: 0, y: 0, z: 1 }, [{ x: 0, y: 0, z: 1 }, { x: 4, y: 0, z: 1 }, { x: 4, y: 2, z: 1 }, { x: 0, y: 2, z: 1 }]);
    const c = circleOnFace(f, { x: 2, y: 1, z: 1 }, 0.5, 8);
    expect(c).toHaveLength(8);
    for (const p of c) { expect(p.z).toBeCloseTo(1); expect(Math.hypot(p.x - 2, p.y - 1)).toBeCloseTo(0.5); }
  });

  it('reads a typed width and height', () => {
    const mm = (s: string) => (Number.isFinite(Number(s)) ? Number(s) / 1000 : null);
    expect(parsePair('1200, 600', mm)).toEqual([1.2, 0.6]);
    expect(parsePair('1200x600', mm)).toEqual([1.2, 0.6]);
    expect(parsePair('1200 600', mm)).toEqual([1.2, 0.6]);
    expect(parsePair('1200', mm)).toBeNull();
  });
});
