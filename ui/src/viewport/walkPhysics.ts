/**
 * The walker's body: a capsule that stands on floors, climbs stairs and
 * ramps, slides along walls and falls off edges, against the model's
 * triangles held in a bounding volume hierarchy. Z is up; positions are the
 * feet (the capsule's bottom).
 *
 * Adapted from BimGo (MIT No Attribution): its CharacterController, GeoMath
 * and Bvh (src/BimGo.App/Physics), rewritten for Lapin. As there, stairs are
 * climbed two ways: a contact low on the capsule's round bottom lifts it
 * straight up (it rolls over nosings and riser tops), and a riser too tall
 * for that is stepped up by probing a radius ahead at step height for a
 * floor to land on. Doors do not collide (their leaves would close the
 * opening), nor rooms' volumes.
 */

import type { RenderItem } from '../engine/protocol';
import type { Vec3 } from './camera';

/** The capsule: its radius and height (m), the eye over the feet, the highest step it climbs. */
export const RADIUS = 0.25;
export const BODY_HEIGHT = 1.8;
export const STEP_HEIGHT = 0.35;
/** A surface is walkable while its normal's Z is above this: slopes up to about 45°. */
export const WALKABLE = 0.7;
export const GRAVITY = 12;
export const JUMP_SPEED = 4.2;
/** The physics tick (s); a frame runs as many as its time holds. */
export const TICK = 1 / 120;
const SUBSTEP = 0.08;
const ITERATIONS = 4;
/** Contacts this little above the feet are the floor itself, not a step's edge. */
const EDGE_MIN_RISE = 0.004;
/** A step's edge sits at least this far below the bottom sphere's centre (else it is a wall). */
const EDGE_BELOW_CENTRE = 0.02;

/** What never stops the walker: rooms' volumes, doors (open for it), and what is not built. */
export const PASSABLE = new Set(['IfcSpace', 'IfcDoor', 'IfcOpeningElement', 'IfcAnnotation', 'IfcGrid', 'IfcVirtualElement', 'IfcZone', 'IfcSpatialZone']);

// ---------------------------------------------------------------- vectors

const v = (x: number, y: number, z: number): Vec3 => ({ x, y, z });
const add = (a: Vec3, b: Vec3) => v(a.x + b.x, a.y + b.y, a.z + b.z);
const sub = (a: Vec3, b: Vec3) => v(a.x - b.x, a.y - b.y, a.z - b.z);
const mul = (a: Vec3, s: number) => v(a.x * s, a.y * s, a.z * s);
const dot = (a: Vec3, b: Vec3) => a.x * b.x + a.y * b.y + a.z * b.z;
const cross = (a: Vec3, b: Vec3) => v(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
const dist2 = (a: Vec3, b: Vec3) => { const d = sub(a, b); return dot(d, d); };
const clamp01 = (t: number) => Math.max(0, Math.min(1, t));

// ---------------------------------------------------------------- closest points (after Ericson, Real-Time Collision Detection)

/** The point of triangle ABC nearest P. */
export function closestOnTriangle(p: Vec3, a: Vec3, b: Vec3, c: Vec3): Vec3 {
  const ab = sub(b, a), ac = sub(c, a), ap = sub(p, a);
  const d1 = dot(ab, ap), d2 = dot(ac, ap);
  if (d1 <= 0 && d2 <= 0) return a;
  const bp = sub(p, b);
  const d3 = dot(ab, bp), d4 = dot(ac, bp);
  if (d3 >= 0 && d4 <= d3) return b;
  const vc = d1 * d4 - d3 * d2;
  if (vc <= 0 && d1 >= 0 && d3 <= 0) return add(a, mul(ab, d1 / (d1 - d3)));
  const cp = sub(p, c);
  const d5 = dot(ab, cp), d6 = dot(ac, cp);
  if (d6 >= 0 && d5 <= d6) return c;
  const vb = d5 * d2 - d1 * d6;
  if (vb <= 0 && d2 >= 0 && d6 <= 0) return add(a, mul(ac, d2 / (d2 - d6)));
  const va = d3 * d6 - d5 * d4;
  if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return add(b, mul(sub(c, b), (d4 - d3) / (d4 - d3 + (d5 - d6))));
  const den = 1 / (va + vb + vc);
  return add(a, add(mul(ab, vb * den), mul(ac, vc * den)));
}

/** The nearest points of segments P1Q1 and P2Q2, and their squared distance. */
function closestSegments(p1: Vec3, q1: Vec3, p2: Vec3, q2: Vec3): { d: number; c1: Vec3; c2: Vec3 } {
  const d1 = sub(q1, p1), d2 = sub(q2, p2), r = sub(p1, p2);
  const a = dot(d1, d1), e = dot(d2, d2), f = dot(d2, r);
  let s: number, t: number;
  if (a <= 1e-12 && e <= 1e-12) return { d: dist2(p1, p2), c1: p1, c2: p2 };
  if (a <= 1e-12) { s = 0; t = clamp01(f / e); }
  else {
    const c = dot(d1, r);
    if (e <= 1e-12) { t = 0; s = clamp01(-c / a); }
    else {
      const b = dot(d1, d2), den = a * e - b * b;
      s = den !== 0 ? clamp01((b * f - c * e) / den) : 0;
      t = (b * s + f) / e;
      if (t < 0) { t = 0; s = clamp01(-c / a); }
      else if (t > 1) { t = 1; s = clamp01((b - c) / a); }
    }
  }
  const c1 = add(p1, mul(d1, s)), c2 = add(p2, mul(d2, t));
  return { d: dist2(c1, c2), c1, c2 };
}

/** Where segment P→P+dir crosses triangle ABC, both sides (its parameter in 0..1), or null. */
function segmentHits(p: Vec3, dir: Vec3, a: Vec3, b: Vec3, c: Vec3): number | null {
  const e1 = sub(b, a), e2 = sub(c, a);
  const pv = cross(dir, e2);
  const det = dot(e1, pv);
  if (Math.abs(det) < 1e-12) return null;
  const inv = 1 / det, tv = sub(p, a);
  const u = dot(tv, pv) * inv;
  if (u < 0 || u > 1) return null;
  const qv = cross(tv, e1);
  const w = dot(dir, qv) * inv;
  if (w < 0 || u + w > 1) return null;
  const t = dot(e2, qv) * inv;
  return t >= 0 && t <= 1 ? t : null;
}

/** The nearest points of segment PQ and triangle ABC, and their squared distance (0: the segment pierces it). */
export function closestSegmentTriangle(p: Vec3, q: Vec3, a: Vec3, b: Vec3, c: Vec3): { d: number; onSegment: Vec3; onTriangle: Vec3 } {
  const dir = sub(q, p);
  const t = segmentHits(p, dir, a, b, c);
  if (t != null) { const x = add(p, mul(dir, t)); return { d: 0, onSegment: x, onTriangle: x }; }
  const tp = closestOnTriangle(p, a, b, c);
  let best = { d: dist2(p, tp), onSegment: p, onTriangle: tp };
  const tq = closestOnTriangle(q, a, b, c);
  const dq = dist2(q, tq);
  if (dq < best.d) best = { d: dq, onSegment: q, onTriangle: tq };
  for (const [e0, e1] of [[a, b], [b, c], [c, a]]) {
    const s = closestSegments(p, q, e0, e1);
    if (s.d < best.d) best = { d: s.d, onSegment: s.c1, onTriangle: s.c2 };
  }
  return best;
}

// ---------------------------------------------------------------- the triangles and their hierarchy

/**
 * The model's triangles (9 numbers each) in a bounding volume hierarchy:
 * nodes split at the median of their triangles' centres along their widest
 * axis, down to a few triangles a leaf.
 */
export class TriangleWorld {
  readonly tris: Float32Array;
  readonly count: number;
  /** Per node: min xyz, max xyz. */
  private readonly box: Float32Array;
  /** Per node: its first child (an inner node) or first triangle (a leaf), and its triangle count (0: inner). */
  private readonly first: Int32Array;
  private readonly size: Int32Array;
  private readonly stack = new Int32Array(256);
  readonly min: Vec3;
  readonly max: Vec3;

  constructor(triangles: Float32Array) {
    const count = Math.floor(triangles.length / 9);
    this.count = count;
    const cen = new Float32Array(count * 3);
    const order = new Int32Array(count);
    for (let i = 0; i < count; i++) {
      for (let k = 0; k < 3; k++) cen[3 * i + k] = (triangles[9 * i + k] + triangles[9 * i + 3 + k] + triangles[9 * i + 6 + k]) / 3;
      order[i] = i;
    }
    const nodes = Math.max(1, 2 * count);
    this.box = new Float32Array(nodes * 6);
    this.first = new Int32Array(nodes);
    this.size = new Int32Array(nodes);
    this.size[0] = count;
    let used = 1;
    const work = [0];
    while (work.length) {
      const n = work.pop()!;
      const f = this.first[n], c = this.size[n];
      const lo = [Infinity, Infinity, Infinity], hi = [-Infinity, -Infinity, -Infinity];
      const clo = [Infinity, Infinity, Infinity], chi = [-Infinity, -Infinity, -Infinity];
      for (let i = f; i < f + c; i++) {
        const t = order[i];
        for (let j = 0; j < 9; j++) { const x = triangles[9 * t + j], k = j % 3; if (x < lo[k]) lo[k] = x; if (x > hi[k]) hi[k] = x; }
        for (let k = 0; k < 3; k++) { const x = cen[3 * t + k]; if (x < clo[k]) clo[k] = x; if (x > chi[k]) chi[k] = x; }
      }
      if (!c) { lo.fill(0); hi.fill(0); }
      this.box.set([...lo, ...hi], n * 6);
      if (c <= 4) continue;
      const ext = [chi[0] - clo[0], chi[1] - clo[1], chi[2] - clo[2]];
      const axis = ext[0] > ext[1] ? (ext[0] > ext[2] ? 0 : 2) : (ext[1] > ext[2] ? 1 : 2);
      if (ext[axis] < 1e-6) continue;
      const mid = f + (c >> 1);
      nthElement(order, cen, axis, f, f + c - 1, mid);
      const left = used;
      used += 2;
      this.first[left] = f; this.size[left] = mid - f;
      this.first[left + 1] = mid; this.size[left + 1] = f + c - mid;
      this.first[n] = left; this.size[n] = 0;
      work.push(left, left + 1);
    }
    // The triangles in their leaves' order.
    this.tris = new Float32Array(count * 9);
    for (let i = 0; i < count; i++) this.tris.set(triangles.subarray(9 * order[i], 9 * order[i] + 9), 9 * i);
    this.min = count ? v(this.box[0], this.box[1], this.box[2]) : v(0, 0, 0);
    this.max = count ? v(this.box[3], this.box[4], this.box[5]) : v(0, 0, 0);
  }

  /** The triangles whose node boxes meet the box min–max, by index, into `out` (cleared first). */
  query(min: Vec3, max: Vec3, out: number[]): number[] {
    out.length = 0;
    if (!this.count) return out;
    const b = this.box, st = this.stack;
    let top = 0;
    st[top++] = 0;
    while (top) {
      const n = st[--top], o = n * 6;
      if (b[o + 3] < min.x || b[o] > max.x || b[o + 4] < min.y || b[o + 1] > max.y || b[o + 5] < min.z || b[o + 2] > max.z) continue;
      const c = this.size[n];
      if (c) for (let i = this.first[n]; i < this.first[n] + c; i++) out.push(i);
      else if (top < st.length - 2) { st[top++] = this.first[n]; st[top++] = this.first[n] + 1; }
    }
    return out;
  }

  /** Triangle i's corners. */
  corners(i: number): [Vec3, Vec3, Vec3] {
    const t = this.tris, o = 9 * i;
    return [v(t[o], t[o + 1], t[o + 2]), v(t[o + 3], t[o + 4], t[o + 5]), v(t[o + 6], t[o + 7], t[o + 8])];
  }
}

function nthElement(order: Int32Array, cen: Float32Array, axis: number, left: number, right: number, nth: number) {
  const at = (i: number) => cen[3 * order[i] + axis];
  while (right > left) {
    const pivot = at((left + right) >> 1);
    let i = left, j = right;
    while (i <= j) {
      while (at(i) < pivot) i++;
      while (at(j) > pivot) j--;
      if (i <= j) { const s = order[i]; order[i] = order[j]; order[j] = s; i++; j--; }
    }
    if (nth <= j) right = j;
    else if (nth >= i) left = i;
    else return;
  }
}

/** The world the walker collides with: the triangles of every element that stops it (`shown` leaves out what is hidden). */
export function worldOf(items: Iterable<RenderItem>, shown: (item: RenderItem) => boolean = () => true): TriangleWorld {
  const meshes: [Float32Array, Int32Array][] = [];
  for (const item of items) {
    if (PASSABLE.has(item.type) || !shown(item)) continue;
    meshes.push([item.positions, item.indices]);
    for (const p of item.parts ?? []) meshes.push([p.positions, p.indices]);
  }
  const out = new Float32Array(meshes.reduce((n, [, idx]) => n + Math.floor(idx.length / 3) * 9, 0));
  let o = 0;
  for (const [positions, indices] of meshes) for (let i = 0; i + 2 < indices.length; i += 3) for (let k = 0; k < 3; k++) {
    const at = 3 * indices[i + k];
    out[o++] = positions[at]; out[o++] = positions[at + 1]; out[o++] = positions[at + 2];
  }
  return new TriangleWorld(out);
}

// ---------------------------------------------------------------- the body

interface Contact { ground: boolean; ceiling: boolean; wall: boolean; wallNormal: Vec3 }
const contact = (): Contact => ({ ground: false, ceiling: false, wall: false, wallNormal: v(0, 0, 0) });
const flat = (a: Vec3) => v(a.x, a.y, 0);

/**
 * The walker's capsule. `step` advances one tick with a wished horizontal
 * velocity; flying, it goes where it is sent, through everything.
 */
export class Walker {
  feet: Vec3;
  vel = v(0, 0, 0);
  grounded = false;
  /** Below this nothing falls (the ground, where the model has none). */
  groundZ: number;
  private readonly found: number[] = [];

  constructor(public world: TriangleWorld, feet: Vec3, groundZ = -Infinity) {
    this.feet = { ...feet };
    this.groundZ = groundZ;
  }

  /** Lets the walker down onto whatever is under it, within `below` metres. */
  drop(below = 6) {
    const r = contact();
    this.feet = this.move(this.feet, v(0, 0, -below), r);
    this.vel = v(0, 0, 0);
    this.grounded = r.ground || this.feet.z <= this.groundZ;
  }

  /** At rest: on the ground and not moving. */
  get still() { return this.grounded && Math.hypot(this.vel.x, this.vel.y) < 1e-3; }

  /** One tick of `dt` seconds wishing to go at `wish` (m/s, horizontal); `jump` jumps from the ground. */
  step(dt: number, wish: { x: number; y: number }, jump = false) {
    // Snappy on the ground, a little steering in the air.
    const accel = this.grounded ? 14 : 2.5, k = Math.min(1, accel * dt);
    this.vel = v(this.vel.x + (wish.x - this.vel.x) * k, this.vel.y + (wish.y - this.vel.y) * k, this.vel.z);
    let jumped = false;
    if (this.grounded && jump) { this.vel.z = JUMP_SPEED; this.grounded = false; jumped = true; }
    this.vel.z = Math.max(-40, this.vel.z - GRAVITY * dt);
    const wasGrounded = this.grounded;

    // Across, stepping up what the round bottom cannot ride.
    const delta = v(this.vel.x * dt, this.vel.y * dt, 0);
    const wanted = Math.hypot(delta.x, delta.y);
    if (wanted > 1e-9) {
      let r = contact();
      let moved = this.move(this.feet, delta, r);
      const got = Math.hypot(moved.x - this.feet.x, moved.y - this.feet.y);
      if (wasGrounded && !jumped && got < wanted * 0.5) {
        const up = this.stepUp(delta, wanted, got);
        if (up) { moved = up.at; r = up.r; }
      }
      this.feet = moved;
      if (r.wall) this.clip(r.wallNormal);
    }

    // Up or down.
    const vr = contact();
    this.feet = this.move(this.feet, v(0, 0, this.vel.z * dt), vr);
    this.grounded = false;
    if (vr.ground && this.vel.z <= 0) { this.grounded = true; this.vel.z = 0; }
    if (vr.ceiling && this.vel.z > 0) this.vel.z = 0;

    // Down a stair or off a small lip: kept on the ground rather than falling.
    if (!this.grounded && wasGrounded && !jumped && this.vel.z <= 0) {
      const s = contact();
      const snapped = this.move(this.feet, v(0, 0, -STEP_HEIGHT - 0.02), s);
      if (s.ground) { this.feet = snapped; this.grounded = true; this.vel.z = 0; }
    }
    if (this.feet.z <= this.groundZ) {
      this.feet.z = this.groundZ;
      if (this.vel.z <= 0) { this.grounded = true; this.vel.z = 0; }
    }
  }

  /** Moves straight by `delta` with collisions (the wheel's steps), sliding along walls. */
  shove(delta: Vec3) {
    const r = contact();
    this.feet = this.move(this.feet, delta, r);
  }

  /** Blocked: a radius ahead at step height, a floor to land on? Then up onto it, and this tick's move from there. */
  private stepUp(delta: Vec3, wanted: number, got: number): { at: Vec3; r: Contact } | null {
    const raised = add(this.feet, v(0, 0, STEP_HEIGHT));
    if (this.overlaps(raised)) return null;
    const dir = mul(delta, 1 / wanted), probe = Math.max(wanted, RADIUS + 0.05);
    const across = this.move(raised, mul(dir, probe), contact());
    const r3 = contact();
    const landed = this.move(across, v(0, 0, -STEP_HEIGHT - 0.01), r3);
    const rise = landed.z - this.feet.z;
    const probed = Math.hypot(landed.x - this.feet.x, landed.y - this.feet.y);
    if (!r3.ground || probed <= got + probe * 0.5 || rise <= 0.01 || rise > STEP_HEIGHT + 0.01) return null;
    const lifted = add(this.feet, v(0, 0, rise));
    if (this.overlaps(lifted)) return null;
    const r = contact();
    return { at: this.move(lifted, delta, r), r };
  }

  /** Takes off the velocity pushing into the walls touched. */
  private clip(n: Vec3) {
    const f = flat(n), len = Math.hypot(f.x, f.y);
    if (len < 1e-5) return;
    const into = (this.vel.x * f.x + this.vel.y * f.y) / len;
    if (into < 0) { this.vel.x -= (f.x / len) * into; this.vel.y -= (f.y / len) * into; }
  }

  /** Moves in short sub-steps, pushed out of what it meets after each. */
  private move(feet: Vec3, delta: Vec3, r: Contact): Vec3 {
    const steps = Math.max(1, Math.ceil(Math.hypot(delta.x, delta.y, delta.z) / SUBSTEP));
    const s = mul(delta, 1 / steps);
    let at = feet;
    for (let i = 0; i < steps; i++) at = this.resolve(add(at, s), r);
    return at;
  }

  private bounds(feet: Vec3, pad: number): [Vec3, Vec3] {
    return [v(feet.x - RADIUS - pad, feet.y - RADIUS - pad, feet.z - pad), v(feet.x + RADIUS + pad, feet.y + RADIUS + pad, feet.z + BODY_HEIGHT + pad)];
  }

  /** Pushes the capsule out of the triangles near it: off floors straight up (it does not slide down ramps), off the rest along the contact. */
  private resolve(feet: Vec3, r: Contact): Vec3 {
    let at = { ...feet };
    for (let it = 0; it < ITERATIONS; it++) {
      let pushed = false;
      let [min, max] = this.bounds(at, 0.02);
      for (const i of [...this.world.query(min, max, this.found)]) {
        const [a, b, c] = this.world.corners(i);
        if (Math.max(a.x, b.x, c.x) < min.x || Math.min(a.x, b.x, c.x) > max.x || Math.max(a.y, b.y, c.y) < min.y || Math.min(a.y, b.y, c.y) > max.y || Math.max(a.z, b.z, c.z) < min.z || Math.min(a.z, b.z, c.z) > max.z) continue;
        const next = this.pushOut(at, a, b, c, r);
        if (next) { at = next; pushed = true; [min, max] = this.bounds(at, 0.02); }
      }
      if (at.z < this.groundZ) { at.z = this.groundZ; r.ground = true; }
      if (!pushed) break;
    }
    return at;
  }

  /** One triangle's contact: where the feet go to clear it, or null when it does not touch. */
  private pushOut(feet: Vec3, a: Vec3, b: Vec3, c: Vec3, r: Contact): Vec3 | null {
    const p = v(feet.x, feet.y, feet.z + RADIUS), q = v(feet.x, feet.y, feet.z + BODY_HEIGHT - RADIUS);
    const { d, onSegment, onTriangle } = closestSegmentTriangle(p, q, a, b, c);
    if (d >= RADIUS * RADIUS) return null;
    // Riding an edge: a contact low on the round bottom, no higher than a step (and the whole triangle no higher,
    // else a steep face would be ridden up), lifts the capsule until its bottom sphere just clears it.
    const rise = onTriangle.z - feet.z, top = Math.max(a.z, b.z, c.z) - feet.z;
    if (rise > EDGE_MIN_RISE && rise <= STEP_HEIGHT && top <= STEP_HEIGHT + 0.01 && onTriangle.z < p.z - EDGE_BELOW_CENTRE && onSegment.z <= p.z + 1e-4) {
      const h2 = (p.x - onTriangle.x) ** 2 + (p.y - onTriangle.y) ** 2;
      if (h2 < RADIUS * RADIUS) {
        const lift = onTriangle.z + Math.sqrt(RADIUS * RADIUS - h2) + 5e-4 - p.z;
        if (lift > 0) { r.ground = true; return v(feet.x, feet.y, feet.z + lift); }
      }
    }
    const dist = Math.sqrt(d);
    let n: Vec3;
    if (dist > 1e-5) n = mul(sub(onSegment, onTriangle), 1 / dist);
    else {
      // Pierced: the face's normal, towards the capsule's middle.
      n = cross(sub(b, a), sub(c, a));
      const len = Math.hypot(n.x, n.y, n.z);
      if (len < 1e-12) return null;
      n = mul(n, 1 / len);
      if (dot(sub(mul(add(p, q), 0.5), onTriangle), n) < 0) n = mul(n, -1);
    }
    const depth = RADIUS - dist + 5e-4;
    if (n.z > WALKABLE) { r.ground = true; return v(feet.x, feet.y, feet.z + depth / n.z); }
    if (n.z < -WALKABLE) r.ceiling = true;
    else { r.wall = true; r.wallNormal = add(r.wallNormal, n); }
    return add(feet, mul(n, depth));
  }

  /** Whether the capsule standing at `feet` meets anything. */
  overlaps(feet: Vec3): boolean {
    const [min, max] = this.bounds(feet, 0);
    const p = v(feet.x, feet.y, feet.z + RADIUS), q = v(feet.x, feet.y, feet.z + BODY_HEIGHT - RADIUS);
    const limit = (RADIUS - 0.01) ** 2;
    for (const i of [...this.world.query(min, max, this.found)]) {
      const [a, b, c] = this.world.corners(i);
      if (closestSegmentTriangle(p, q, a, b, c).d < limit) return true;
    }
    return false;
  }
}
