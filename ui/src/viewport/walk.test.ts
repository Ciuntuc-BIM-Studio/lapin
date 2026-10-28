import { describe, expect, it } from 'vitest';
import { centroid, easeEye, roomEye, roomLook, walkInto, walkStep, walkWish, EYE_HEIGHT, RUN_FACTOR, WALK_SPEED } from './walk';
import { pointInPolygon } from './snap';

const pts = (...xy: number[]) => Array.from({ length: xy.length / 2 }, (_, i) => ({ x: xy[2 * i], y: xy[2 * i + 1] }));

describe('walking into a room', () => {
  it('stands a rectangular room\'s visitor at its centre, looking along its longest side the longer way', () => {
    const room = pts(0, 0, 6, 0, 6, 4, 0, 4);
    const eye = roomEye(room);
    expect(eye.x).toBeCloseTo(3);
    expect(eye.y).toBeCloseTo(2);
    expect(Math.abs(Math.cos(roomLook(room, eye)))).toBeCloseTo(1);
    // Off centre: it looks towards the farther wall.
    expect(roomLook(room, { x: 1, y: 2 })).toBeCloseTo(0);
    expect(Math.abs(roomLook(room, { x: 5, y: 2 }))).toBeCloseTo(Math.PI);
  });

  it('stands inside an L-shaped room whose centroid lies outside it, clear of its walls', () => {
    // A thin L: two 1 m wide arms, 8 m long; its centroid is in the empty corner.
    const room = pts(0, 0, 8, 0, 8, 1, 1, 1, 1, 8, 0, 8);
    const c = centroid(room);
    expect(pointInPolygon(c, room)).toBe(false);
    const eye = roomEye(room);
    expect(pointInPolygon(eye, room)).toBe(true);
    // In an arm's middle: about half a metre from its sides.
    const across = eye.y < 1 ? eye.y : eye.x;
    expect(across).toBeGreaterThan(0.35);
    expect(across).toBeLessThan(0.65);
  });

  it('keeps out of an island (a shaft in the room)', () => {
    const room = pts(0, 0, 4, 0, 4, 4, 0, 4);
    const shaft = pts(1, 1, 3, 1, 3, 3, 1, 3);
    const eye = roomEye(room, [shaft]);
    expect(pointInPolygon(eye, shaft)).toBe(false);
    expect(pointInPolygon(eye, room)).toBe(true);
  });

  it('puts the eye 1.60 m over the floor', () => {
    const w = walkInto('Living', [0, 0, 5, 0, 5, 3, 0, 3], [], 3)!;
    expect(w.eye.z).toBeCloseTo(3 + EYE_HEIGHT);
    expect(w.pitch).toBe(0);
    expect(walkInto('Nothing', [0, 0, 1, 0], [], 0)).toBeNull();
  });
});

describe('a walk step', () => {
  const at = { x: 0, y: 0, z: 1.6 };
  it('walks forward along the look, level, and sideways to its right', () => {
    const f = walkStep(at, Math.PI / 2, new Set(['KeyW']), 1);
    expect(f.x).toBeCloseTo(0);
    expect(f.y).toBeCloseTo(WALK_SPEED);
    expect(f.z).toBe(1.6);
    // Looking north, D steps east.
    const r = walkStep(at, Math.PI / 2, new Set(['KeyD']), 1);
    expect(r.x).toBeCloseTo(WALK_SPEED);
    expect(r.y).toBeCloseTo(0);
    const back = walkStep(at, 0, new Set(['ArrowDown']), 1);
    expect(back.x).toBeCloseTo(-WALK_SPEED);
  });

  it('is no faster on a diagonal, runs with Shift, rises with E and stands still with nothing held', () => {
    const d = walkStep(at, 0, new Set(['KeyW', 'KeyA']), 1);
    expect(Math.hypot(d.x, d.y)).toBeCloseTo(WALK_SPEED);
    const run = walkStep(at, 0, new Set(['KeyW']), 1, true);
    expect(run.x).toBeCloseTo(WALK_SPEED * RUN_FACTOR);
    expect(walkStep(at, 0, new Set(['KeyE']), 1).z).toBeGreaterThan(1.6);
    expect(walkStep(at, 0, new Set(['KeyW', 'KeyS']), 1)).toEqual(at);
    expect(walkStep(at, 0, new Set(), 1)).toEqual(at);
  });
});

describe('the eye over a walker', () => {
  it('asks for a level velocity whatever is held', () => {
    const w = walkWish(0, new Set(['KeyW', 'KeyD']));
    expect(Math.hypot(w.x, w.y)).toBeCloseTo(WALK_SPEED);
    expect(w.z).toBe(0);
  });

  it('eases up a stair\'s riser on the ground, follows a fall at once', () => {
    const eased = easeEye(1.6, 1.77, 1 / 60, true);
    expect(eased).toBeGreaterThan(1.6);
    expect(eased).toBeLessThan(1.77);
    expect(easeEye(4.6, 1.6, 1 / 60, false)).toBe(1.6);
    expect(easeEye(1.6, 3.0, 1 / 60, true)).toBe(3.0);
  });
});
