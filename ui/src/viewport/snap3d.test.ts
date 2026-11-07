import { describe, expect, it } from 'vitest';
import { arrowLock3, lineNearRay, snap3, type Ray3, type Seg3, type Snap3Input } from './snap3d';
import type { Vec3 } from './camera';

// Seen from above, 100 px a metre: x right, y up the model is down the screen.
const top = {
  project: (p: Vec3) => ({ x: p.x * 100, y: -p.y * 100 }),
  ray: (cx: number, cy: number): Ray3 => ({ origin: { x: cx / 100, y: -cy / 100, z: 10 }, dir: { x: 0, y: 0, z: -1 } }),
};
// Seen from the south, looking north: x right, z up.
const side = {
  project: (p: Vec3) => ({ x: p.x * 100, y: -p.z * 100 }),
  ray: (cx: number, cy: number): Ray3 => ({ origin: { x: cx / 100, y: -10, z: -cy / 100 }, dir: { x: 0, y: 1, z: 0 } }),
};
const at = (view: typeof top, x: number, y: number, o: Partial<Snap3Input> = {}) => {
  const cursor = view.project({ x, y: view === top ? y : 0, z: view === top ? 0 : y });
  return snap3({ cursor, ray: view.ray(cursor.x, cursor.y), project: view.project, segs: [], face: null, level: null, ...o });
};
const edge: Seg3 = { a: { x: 0, y: 0, z: 0 }, b: { x: 2, y: 0, z: 0 } };

describe('the 3D snaps', () => {
  it('take an endpoint first, then a midpoint, then a point on an edge', () => {
    expect(at(top, 0.05, 0, { segs: [edge] })).toMatchObject({ kind: 'Endpoint', point: { x: 0, y: 0, z: 0 } });
    expect(at(top, 1.03, 0.02, { segs: [edge] })).toMatchObject({ kind: 'Midpoint', point: { x: 1, y: 0, z: 0 } });
    const on = at(top, 1.5, 0.05, { segs: [edge] })!;
    expect(on.kind).toBe('On edge');
    expect(on.point.x).toBeCloseTo(1.5, 6);
    expect(on.point.y).toBeCloseTo(0, 6);
    // Out of reach: nothing but the level.
    expect(at(top, 1.5, 0.5, { segs: [edge], level: { x: 1.5, y: 0.5, z: 0 } })).toMatchObject({ kind: 'On level' });
  });

  it('leave what lies behind the face under the cursor', () => {
    // A slab's top at 0.2 over an edge at -1: the edge's end is hidden under it.
    const under: Seg3 = { a: { x: 0, y: 0, z: -1 }, b: { x: 2, y: 0, z: -1 } };
    expect(at(top, 0.02, 0, { segs: [under], face: { x: 0.02, y: 0, z: 0.2 } })).toMatchObject({ kind: 'On face' });
    // The slab's own corner, on its face, is taken.
    const corner: Seg3 = { a: { x: 0, y: 0, z: 0.2 }, b: { x: 2, y: 0, z: 0.2 } };
    expect(at(top, 0.02, 0, { segs: [corner], face: { x: 0.02, y: 0, z: 0.2 } })).toMatchObject({ kind: 'Endpoint', point: { z: 0.2 } });
  });

  it('infer the red, green and blue axes from the base', () => {
    const base = { x: 0, y: 0, z: 0 };
    const red = at(top, 3, 0.04, { base })!;
    expect(red.kind).toBe('On red axis');
    expect(red.point.y).toBeCloseTo(0, 6);
    expect(red.guide).toMatchObject({ from: base, color: 'red' });
    expect(at(top, 0.04, -2, { base })).toMatchObject({ kind: 'On green axis', color: 'green' });
    const blue = at(side, 0.03, 2, { base })!;
    expect(blue.kind).toBe('On blue axis');
    expect(blue.point.z).toBeCloseTo(2, 6);
    // Off every axis: the face.
    expect(at(top, 2, 1.5, { base, face: { x: 2, y: 1.5, z: 0 } })).toMatchObject({ kind: 'On face' });
    // A point is still taken before an axis.
    expect(at(top, 2, 0.02, { base, segs: [edge] })).toMatchObject({ kind: 'Endpoint', point: { x: 2 } });
  });

  it('hold a locked axis, bringing a point hovered onto it', () => {
    const base = { x: 0, y: 0, z: 0 };
    const corner: Seg3 = { a: { x: 2, y: 1, z: 0 }, b: { x: 2, y: 3, z: 0 } };
    const s = at(top, 2.02, 1, { base, lock: { axis: 'red' }, segs: [corner] })!;
    expect(s.kind).toBe('On red axis');
    expect(s.point).toEqual({ x: 2, y: 0, z: 0 });
    expect(s.from).toEqual({ x: 2, y: 1, z: 0 });
    // Nothing hovered: the axis's point nearest the cursor.
    const free = at(top, 1.2, 0.8, { base, lock: { axis: 'red' } })!;
    expect(free.point.x).toBeCloseTo(1.2, 6);
    expect(free.point.y).toBeCloseTo(0, 6);
  });

  it('give nothing over empty space with no level, and the arrows lock and let go', () => {
    expect(at(top, 1, 1)).toBeNull();
    expect(arrowLock3('ArrowRight', null)).toEqual({ axis: 'red' });
    expect(arrowLock3('ArrowRight', { axis: 'red' })).toBeNull();
    expect(arrowLock3('ArrowUp', { axis: 'red' })).toEqual({ axis: 'blue' });
    expect(arrowLock3('ArrowLeft', null)).toEqual({ axis: 'green' });
    expect(arrowLock3('a', null)).toBeUndefined();
  });

  it('find where a line passes nearest a ray', () => {
    const t = lineNearRay({ x: 0, y: 0, z: 0 }, { x: 1, y: 0, z: 0 }, { origin: { x: 3, y: 1, z: 10 }, dir: { x: 0, y: 0, z: -1 } });
    expect(t).toBeCloseTo(3, 9);
    expect(lineNearRay({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 1 }, { origin: { x: 0, y: 0, z: 10 }, dir: { x: 0, y: 0, z: -1 } })).toBeNull();
  });
});
