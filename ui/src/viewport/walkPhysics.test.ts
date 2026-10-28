import { describe, expect, it } from 'vitest';
import type { RenderItem } from '../engine/protocol';
import { RADIUS, STEP_HEIGHT, TICK, TriangleWorld, Walker, worldOf } from './walkPhysics';

/** A box's 12 triangles. */
function box(x0: number, y0: number, z0: number, x1: number, y1: number, z1: number): number[] {
  const c = (i: number) => [i & 1 ? x1 : x0, i & 2 ? y1 : y0, i & 4 ? z1 : z0];
  const quads = [[0, 1, 3, 2], [4, 6, 7, 5], [0, 4, 5, 1], [2, 3, 7, 6], [0, 2, 6, 4], [1, 5, 7, 3]];
  return quads.flatMap(([a, b, d, e]) => [...c(a), ...c(b), ...c(d), ...c(a), ...c(d), ...c(e)]);
}

/** A sloped plank rising `rise` over x 0..run, 4 m wide. */
const ramp = (run: number, rise: number) => [0, -2, 0, run, -2, rise, run, 2, rise, 0, -2, 0, run, 2, rise, 0, 2, 0];

const world = (...parts: number[][]) => new TriangleWorld(new Float32Array(parts.flat()));
const floor = box(-20, -20, -0.2, 20, 20, 0);

/** Walks `seconds` at `wish` (m/s), tick by tick. */
function walk(w: Walker, wish: { x: number; y: number }, seconds: number) {
  for (let t = 0; t < seconds; t += TICK) w.step(TICK, wish);
  return w.feet;
}

const item = (type: string, tris: number[]): RenderItem => ({
  nodeId: type, type, storeyId: null, editable: true, color: '#888', positions: new Float32Array(tris),
  normals: new Float32Array(), indices: Int32Array.from({ length: tris.length / 3 }, (_, i) => i), edges: new Float32Array(), plan: [], snaps: [], diagnostics: [],
});

describe('the walker\'s triangles', () => {
  it('finds, through its hierarchy, every triangle a box meets', () => {
    const parts: number[][] = [];
    for (let i = 0; i < 20; i++) for (let j = 0; j < 20; j++) parts.push(box(i, j, 0, i + 0.5, j + 0.5, 1));
    const w = world(...parts);
    expect(w.count).toBe(400 * 12);
    const min = { x: 3.2, y: 4.2, z: 0.5 }, max = { x: 5.1, y: 4.4, z: 0.6 };
    const found = new Set(w.query(min, max, []));
    // Brute force: every triangle whose own box meets it.
    for (let i = 0; i < w.count; i++) {
      const cs = w.corners(i);
      const meets = Math.max(...cs.map((c) => c.x)) >= min.x && Math.min(...cs.map((c) => c.x)) <= max.x && Math.max(...cs.map((c) => c.y)) >= min.y && Math.min(...cs.map((c) => c.y)) <= max.y;
      if (meets) expect(found.has(i)).toBe(true);
    }
    expect(found.size).toBeLessThan(w.count / 10);
  });
});

describe('walking with a body', () => {
  it('stands on the floor it is dropped onto', () => {
    const w = new Walker(world(floor), { x: 0, y: 0, z: 1 });
    w.drop();
    expect(w.feet.z).toBeCloseTo(0, 2);
    expect(w.grounded).toBe(true);
  });

  it('is stopped by a wall, its body short of it', () => {
    const w = new Walker(world(floor, box(2, -5, 0, 2.2, 5, 3)), { x: 0, y: 0, z: 0 });
    w.drop();
    const at = walk(w, { x: 1.4, y: 0 }, 3);
    expect(at.x).toBeLessThan(2 - RADIUS + 0.01);
    expect(at.x).toBeGreaterThan(2 - RADIUS - 0.05);
    expect(at.z).toBeCloseTo(0, 2);
  });

  it('slides along a wall walked into on a slant', () => {
    const w = new Walker(world(floor, box(2, -5, 0, 2.2, 5, 3)), { x: 1.5, y: 0, z: 0 });
    w.drop();
    const at = walk(w, { x: 1, y: 1 }, 2);
    expect(at.x).toBeLessThan(2 - RADIUS + 0.01);
    // Along the wall at the speed the slant leaves (1 m/s along y).
    expect(at.y).toBeGreaterThan(1.6);
  });

  it('steps up a 0.17 m stair step, and climbs a flight of them', () => {
    const w = new Walker(world(floor, box(1, -2, 0, 3, 2, 0.17)), { x: 0, y: 0, z: 0 });
    w.drop();
    const at = walk(w, { x: 1.4, y: 0 }, 1.5);
    expect(at.x).toBeGreaterThan(1.5);
    expect(at.z).toBeCloseTo(0.17, 2);
    // Ten risers of 0.17 m, treads of 0.28 m.
    const steps = Array.from({ length: 10 }, (_, i) => box(1 + 0.28 * i, -1, 0, 1 + 0.28 * (i + 1) + (i === 9 ? 2 : 0), 1, 0.17 * (i + 1)));
    const s = new Walker(world(floor, ...steps), { x: 0, y: 0, z: 0 });
    s.drop();
    const top = walk(s, { x: 1.4, y: 0 }, 4);
    expect(top.z).toBeCloseTo(1.7, 2);
    // And down again.
    const down = walk(s, { x: -1.4, y: 0 }, 4);
    expect(down.z).toBeCloseTo(0, 2);
    expect(down.x).toBeLessThan(1);
  });

  it('does not climb a 0.6 m box', () => {
    const w = new Walker(world(floor, box(1, -2, 0, 3, 2, 0.6)), { x: 0, y: 0, z: 0 });
    w.drop();
    const at = walk(w, { x: 1.4, y: 0 }, 2);
    expect(at.x).toBeLessThan(1 - RADIUS + 0.01);
    expect(at.z).toBeCloseTo(0, 2);
  });

  it('walks up a 30° ramp but not a 60° one', () => {
    const up = new Walker(world(floor, ramp(3, 3 * Math.tan(Math.PI / 6))), { x: -1, y: 0, z: 0 });
    up.drop();
    expect(walk(up, { x: 1.4, y: 0 }, 2).z).toBeGreaterThan(0.8);
    const steep = new Walker(world(floor, ramp(1, Math.tan(Math.PI / 3))), { x: -1, y: 0, z: 0 });
    steep.drop();
    expect(walk(steep, { x: 1.4, y: 0 }, 2).z).toBeLessThan(STEP_HEIGHT);
  });

  it('falls off an edge onto the floor below', () => {
    const w = new Walker(world(floor, box(-2, -2, 2.8, 2, 2, 3)), { x: 0, y: 0, z: 3.5 });
    w.drop();
    expect(w.feet.z).toBeCloseTo(3, 2);
    walk(w, { x: 1.4, y: 0 }, 2.5);
    walk(w, { x: 0, y: 0 }, 1.5);
    expect(w.feet.x).toBeGreaterThan(2);
    expect(w.feet.z).toBeCloseTo(0, 2);
    expect(w.grounded).toBe(true);
  });

  it('passes through a door\'s opening, its leaf not in the way', () => {
    // A wall at x 2 with a 0.9 m opening 2.1 m high round y 0, and a door's leaf closing it.
    const wall = [box(2, -5, 0, 2.2, -0.45, 3), box(2, 0.45, 0, 2.2, 5, 3), box(2, -0.45, 2.1, 2.2, 0.45, 3)];
    const leaf = box(2.05, -0.45, 0, 2.1, 0.45, 2.1);
    const items = [item('IfcSlab', floor), item('IfcWall', wall.flat()), item('IfcDoor', leaf), item('IfcSpace', box(-5, -5, 0, 2, 5, 2.8))];
    const w = new Walker(worldOf(items), { x: 0, y: 0, z: 0 });
    w.drop();
    expect(walk(w, { x: 1.4, y: 0 }, 3).x).toBeGreaterThan(3);
    // Beside the opening the wall stops it.
    const beside = new Walker(worldOf(items), { x: 0, y: 2, z: 0 });
    beside.drop();
    expect(walk(beside, { x: 1.4, y: 0 }, 3).x).toBeLessThan(2);
    // Hidden elements do not stop it either.
    const hidden = new Walker(worldOf(items, (i) => i.type !== 'IfcWall'), { x: 0, y: 2, z: 0 });
    hidden.drop();
    expect(walk(hidden, { x: 1.4, y: 0 }, 3).x).toBeGreaterThan(3);
  });
});
