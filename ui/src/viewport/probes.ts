/**
 * Reflection probes on rooms (docs/plan-render.md, S3), the part with no GPU in it: where the probes go, how much each
 * counts at a point, which of them hold the GPU's slots, and the order they are baked in. Derived from the rooms
 * (`IfcSpace`, the engine's Rooms: one truth) and the reflective faces the 3D view draws; never saved.
 *
 * After BimGo's ReflectionProbes (MIT-0), rewritten: a probe per room that holds a reflective face, at 1.70 m over its
 * floor (above a table, so less of the floor near the walls hides behind furniture), a grid at 8 m in a room longer
 * than 12 m (kept where a reflective face is within 8 m), the room's box for the box projection, neighbouring probes
 * blended over about a metre at their borders; two faces baked a frame, the nearest probe first.
 */

export type Vec3 = [number, number, number];

/** A room as the probes read it: its box (floor to ceiling) and, for an L-shaped one, whether a plan point lies inside. */
export interface ProbeRoom {
  id: string;
  min: Vec3;
  max: Vec3;
  inside?: (x: number, y: number) => boolean;
}

/** A probe: where it sees from, the box its picture is projected on (its room's), and the plan rectangle it covers. */
export interface Probe {
  /** Stable while the rooms stay (the room's id, and the grid cell in a long room). */
  key: string;
  room: string;
  position: Vec3;
  /** The room's box: the projection's (parallax-corrected cube map). */
  min: Vec3;
  max: Vec3;
  /** The plan rectangle it covers (the room's, or its cell of the grid), [x0, y0, x1, y1]. */
  zone: [number, number, number, number];
  /** How many reflective samples it serves (the most kept when there are more probes than slots). */
  weight: number;
}

/** Height of a probe over its room's floor (m), BimGo's: over table and worktop height. */
export const EYE_HEIGHT = 1.7;
/** A probe stays this far (m) under a low room's ceiling. */
export const CEILING_GAP = 0.6;
/** A room whose longest side is over this (m) gets a grid of probes. */
export const LARGE_ROOM = 12;
/** The grid's spacing in a long room (m). */
export const ROOM_SPACING = 8;
/** Neighbouring probes blend over about this width (m) across their border. */
export const BLEND = 1;
/** A reflective sample this far under a room's floor still belongs to it (a floor's finish lies on its top). */
const FLOOR_TOLERANCE = 0.35;
/** And this far over its ceiling. */
const CEILING_TOLERANCE = 0.05;

/** The room a point lies in: inside in plan, between its floor (less a finish) and its ceiling; the one whose floor is nearest under it. */
export function roomAt(rooms: readonly ProbeRoom[], p: Vec3): number {
  let best = -1, bestDz = Infinity;
  for (let i = 0; i < rooms.length; i++) {
    const r = rooms[i];
    if (p[0] < r.min[0] || p[0] > r.max[0] || p[1] < r.min[1] || p[1] > r.max[1]) continue;
    if (p[2] < r.min[2] - FLOOR_TOLERANCE || p[2] > r.max[2] + CEILING_TOLERANCE) continue;
    if (r.inside && !r.inside(p[0], p[1])) continue;
    const dz = Math.abs(p[2] - r.min[2]);
    if (dz < bestDz) { best = i; bestDz = dz; }
  }
  return best;
}

/** A plan point inside the room: its box's middle if inside, else the reflective samples' average, else one of them. */
function insideSpot(room: ProbeRoom, points: readonly Vec3[]): [number, number] {
  const inside = (x: number, y: number) => !room.inside || room.inside(x, y);
  const cx = (room.min[0] + room.max[0]) / 2, cy = (room.min[1] + room.max[1]) / 2;
  if (inside(cx, cy)) return [cx, cy];
  let ax = 0, ay = 0;
  for (const p of points) { ax += p[0]; ay += p[1]; }
  ax /= Math.max(1, points.length); ay /= Math.max(1, points.length);
  if (inside(ax, ay)) return [ax, ay];
  for (const p of points) if (inside(p[0], p[1])) return [p[0], p[1]];
  return [cx, cy];
}

/**
 * The probes for these rooms and reflective samples (points on the reflective faces, a few per square metre): one per
 * room holding a sample, a grid in a long room; at most `budget`, those serving the most samples kept.
 */
export function placeProbes(rooms: readonly ProbeRoom[], shiny: readonly Vec3[], budget = 64): Probe[] {
  const byRoom = new Map<number, Vec3[]>();
  for (const p of shiny) {
    const r = roomAt(rooms, p);
    if (r < 0) continue;
    let list = byRoom.get(r);
    if (!list) byRoom.set(r, (list = []));
    list.push(p);
  }
  const out: Probe[] = [];
  for (const [index, points] of byRoom) {
    const room = rooms[index];
    // 1.70 m over the floor; in a low room 0.6 m under the ceiling (never under half its height). BimGo's own rule, half the
    // height at most, put it at 1.40 m in an ordinary 2.80 m room.
    const z = Math.min(room.min[2] + EYE_HEIGHT, Math.max((room.min[2] + room.max[2]) / 2, room.max[2] - CEILING_GAP));
    const sx = room.max[0] - room.min[0], sy = room.max[1] - room.min[1];
    const first = out.length;
    const add = (key: string, x: number, y: number, zone: Probe['zone']) =>
      out.push({ key, room: room.id, position: [x, y, z], min: [...room.min], max: [...room.max], zone, weight: 0 });
    if (Math.max(sx, sy) <= LARGE_ROOM) {
      const [x, y] = insideSpot(room, points);
      add(room.id, x, y, [room.min[0], room.min[1], room.max[0], room.max[1]]);
    } else {
      // A grid, its cells kept where they are inside the room and a reflective sample is within a spacing.
      const cx = Math.max(1, Math.ceil(sx / ROOM_SPACING)), cy = Math.max(1, Math.ceil(sy / ROOM_SPACING));
      const near = ROOM_SPACING * ROOM_SPACING;
      for (let i = 0; i < cx; i++) for (let j = 0; j < cy; j++) {
        const x0 = room.min[0] + (i * sx) / cx, x1 = room.min[0] + ((i + 1) * sx) / cx;
        const y0 = room.min[1] + (j * sy) / cy, y1 = room.min[1] + ((j + 1) * sy) / cy;
        const x = (x0 + x1) / 2, y = (y0 + y1) / 2;
        if (room.inside && !room.inside(x, y)) continue;
        if (!points.some((p) => (p[0] - x) ** 2 + (p[1] - y) ** 2 < near)) continue;
        add(`${room.id}:${i},${j}`, x, y, [x0, y0, x1, y1]);
      }
      if (out.length === first) { const [x, y] = insideSpot(room, points); add(room.id, x, y, [room.min[0], room.min[1], room.max[0], room.max[1]]); }
    }
    // Each sample counts for its room's nearest probe.
    for (const p of points) {
      let best = first, bestD = Infinity;
      for (let k = first; k < out.length; k++) {
        const q = out[k].position, d = (q[0] - p[0]) ** 2 + (q[1] - p[1]) ** 2 + (q[2] - p[2]) ** 2;
        if (d < bestD) { best = k; bestD = d; }
      }
      out[best].weight++;
    }
  }
  return out.sort((a, b) => b.weight - a.weight || (a.key < b.key ? -1 : 1)).slice(0, Math.max(0, budget));
}

/** The signed distance (m) in plan from a point to a zone: negative inside (to its nearest edge), positive outside. */
export function zoneDistance(x: number, y: number, zone: Probe['zone']): number {
  const dx = Math.max(zone[0] - x, x - zone[2]), dy = Math.max(zone[1] - y, y - zone[3]);
  return dx > 0 || dy > 0 ? Math.hypot(Math.max(dx, 0), Math.max(dy, 0)) : Math.max(dx, dy);
}

/** Whether a point's height belongs to a probe's room (its floor, less a finish, to its ceiling). */
const inHeight = (z: number, p: Pick<Probe, 'min' | 'max'>) => z >= p.min[2] - FLOOR_TOLERANCE && z <= p.max[2] + CEILING_TOLERANCE;

/**
 * How much each probe counts at a point, and how much the look outside the rooms keeps (the shader does the same,
 * probes3d.ts): each probe 1 half a blend inside its zone, ½ on its border, none half a blend outside; the rooms' share
 * twice their sum, at most 1 (full inside a zone and across the wall between two, a doorway's floor; fading past an
 * outer wall), the probes sharing it by their weights.
 */
export function blendAt(p: Vec3, probes: readonly Pick<Probe, 'min' | 'max' | 'zone'>[]): { weights: number[]; outside: number } {
  const raw = probes.map((q) => (inHeight(p[2], q) ? Math.min(1, Math.max(0, 0.5 - zoneDistance(p[0], p[1], q.zone) / BLEND)) : 0));
  const sum = raw.reduce((a, b) => a + b, 0);
  const cover = Math.min(1, 2 * sum);
  return { weights: raw.map((w) => (sum > 0 ? (cover * w) / sum : 0)), outside: 1 - cover };
}

/** How far (m) the eye is from a probe's room box (0 inside). */
export function distanceToBox(eye: Vec3, p: Pick<Probe, 'min' | 'max'>): number {
  let d = 0;
  for (let a = 0; a < 3; a++) { const o = Math.max(p.min[a] - eye[a], 0, eye[a] - p.max[a]); d += o * o; }
  return Math.sqrt(d);
}

/**
 * Which probes hold the GPU's `slots` (there may be more probes than a frame can read): the nearest to the eye. A probe
 * already in a slot keeps it (its picture is kept) unless one outside is nearer by more than `hysteresis` metres; a
 * slot's probe changed is baked again. Returns the probe key per slot (null: empty).
 */
export function assignSlots(current: readonly (string | null)[], probes: readonly Probe[], eye: Vec3, slots: number, hysteresis = 2): (string | null)[] {
  const ranked = probes.map((p) => ({ key: p.key, d: distanceToBox(eye, p) })).sort((a, b) => a.d - b.d || (a.key < b.key ? -1 : 1));
  const wanted = ranked.slice(0, slots);
  const known = new Set(probes.map((p) => p.key));
  const dist = new Map(ranked.map((r) => [r.key, r.d]));
  const out: (string | null)[] = Array.from({ length: slots }, (_, i) => (current[i] != null && known.has(current[i]!) ? current[i]! : null));
  const placed = new Set(out.filter((k): k is string => k != null));
  // The farthest kept probes give way to wanted ones clearly nearer.
  for (const w of wanted) {
    if (placed.has(w.key)) continue;
    let slot = out.indexOf(null);
    if (slot < 0) {
      let worst = -1, worstD = -Infinity;
      for (let i = 0; i < out.length; i++) { const d = dist.get(out[i]!) ?? Infinity; if (d > worstD) { worst = i; worstD = d; } }
      if (worst < 0 || worstD <= w.d + hysteresis) continue;
      placed.delete(out[worst]!);
      slot = worst;
    }
    out[slot] = w.key;
    placed.add(w.key);
  }
  return out;
}

/** One frame's bake: the slot, the cube faces to draw (0–5), and whether its picture is finished with them. */
export interface BakeStep { slot: number; faces: number[]; finish: boolean }

interface SlotState { centre: Vec3 | null; baked: boolean; dirty: boolean; again: boolean; face: number }

/**
 * The bake's order: a few cube faces a frame (BimGo's two), one probe at a time to its end; the nearest unbaked probe
 * first, then the nearest stale one (the sun moved, a light, the model changed: its old picture shown until then).
 * A probe marked stale while it is being baked is finished, then baked again.
 */
export class BakeQueue {
  private slots: SlotState[] = [];
  private current = -1;

  constructor(readonly facesPerFrame = 2) {}

  /** A slot's probe set (null: emptied): not baked yet. */
  set(slot: number, centre: Vec3 | null): void {
    while (this.slots.length <= slot) this.slots.push({ centre: null, baked: false, dirty: false, again: false, face: 0 });
    this.slots[slot] = { centre, baked: false, dirty: centre != null, again: false, face: 0 };
    if (this.current === slot) this.current = -1;
  }

  /** The slots beyond `count` dropped. */
  truncate(count: number): void {
    this.slots.length = Math.min(this.slots.length, count);
    if (this.current >= count) this.current = -1;
  }

  /** Every baked probe stale (kept shown until baked again). */
  stale(): void {
    for (let i = 0; i < this.slots.length; i++) {
      const s = this.slots[i];
      if (!s.centre) continue;
      if (i === this.current) s.again = true; else s.dirty = true;
    }
  }

  /** Whether a slot's picture may be shown (baked once at least). */
  baked(slot: number): boolean { return this.slots[slot]?.baked ?? false; }

  /** Whether anything is waiting to be baked. */
  get pending(): boolean { return this.current >= 0 || this.slots.some((s) => s.centre && s.dirty); }

  /** How many slots hold a baked picture, and how many are waiting. */
  counts(): { baked: number; waiting: number } {
    let baked = 0, waiting = 0;
    for (const s of this.slots) { if (!s.centre) continue; if (s.baked) baked++; if (s.dirty || s.again) waiting++; }
    return { baked, waiting };
  }

  /** This frame's faces (null: nothing to bake); taken as done: the caller draws them. */
  next(eye: Vec3): BakeStep | null {
    if (this.current < 0) {
      let best = -1, bestKey = Infinity;
      for (let i = 0; i < this.slots.length; i++) {
        const s = this.slots[i];
        if (!s.centre || !s.dirty) continue;
        const d = Math.hypot(s.centre[0] - eye[0], s.centre[1] - eye[1], s.centre[2] - eye[2]);
        // Unbaked before stale, then nearest.
        const key = (s.baked ? 1e9 : 0) + d;
        if (key < bestKey) { best = i; bestKey = key; }
      }
      if (best < 0) return null;
      this.current = best;
      this.slots[best].face = 0;
      this.slots[best].dirty = false;
    }
    const slot = this.current, s = this.slots[slot];
    const faces: number[] = [];
    while (faces.length < this.facesPerFrame && s.face < 6) faces.push(s.face++);
    const finish = s.face >= 6;
    if (finish) {
      s.baked = true;
      s.face = 0;
      if (s.again) { s.again = false; s.dirty = true; }
      this.current = -1;
    }
    return { slot, faces, finish };
  }
}

/**
 * Points on triangles a grid's step apart or closer (every triangle gives its corners' middle, its centroid and as
 * many more as its size asks), for the rooms' reflective samples: `positions` world xyz, `index` triangles.
 */
export function sampleTriangles(positions: ArrayLike<number>, index: ArrayLike<number> | null, step: number, out: Vec3[] = [], cap = 200_000): Vec3[] {
  const n = index ? index.length : positions.length / 3;
  const at = (k: number): Vec3 => { const i = (index ? index[k] : k) * 3; return [positions[i], positions[i + 1], positions[i + 2]]; };
  for (let t = 0; t + 2 < n && out.length < cap; t += 3) {
    const a = at(t), b = at(t + 1), c = at(t + 2);
    const ab = Math.hypot(b[0] - a[0], b[1] - a[1], b[2] - a[2]), ac = Math.hypot(c[0] - a[0], c[1] - a[1], c[2] - a[2]);
    const m = Math.max(1, Math.min(64, Math.ceil(Math.max(ab, ac) / step)));
    for (let i = 0; i <= m; i++) for (let j = 0; i + j <= m; j++) {
      const u = (i + 1 / 3) / (m + 1), v = (j + 1 / 3) / (m + 1);
      if (u + v > 1) continue;
      out.push([a[0] + (b[0] - a[0]) * u + (c[0] - a[0]) * v, a[1] + (b[1] - a[1]) * u + (c[1] - a[1]) * v, a[2] + (b[2] - a[2]) * u + (c[2] - a[2]) * v]);
      if (out.length >= cap) break;
    }
  }
  return out;
}

/** Samples on a voxel grid of `voxel` metres, one a voxel (keeps a big floor's samples few). */
export function onVoxels(points: readonly Vec3[], voxel = 0.5): Vec3[] {
  const seen = new Set<string>();
  const out: Vec3[] = [];
  for (const p of points) {
    const k = `${Math.floor(p[0] / voxel)},${Math.floor(p[1] / voxel)},${Math.floor(p[2] / voxel)}`;
    if (seen.has(k)) continue;
    seen.add(k);
    out.push(p);
  }
  return out;
}

/** How many probes, how big their faces (px) and how many faces a frame, by the profile and the graphics chip. */
export interface ProbeBudget { slots: number; size: number; facesPerFrame: number }

/**
 * The budget: the medium view a laptop's (6 probes of 128 px), the realistic a desktop's (10 of 256 px); a weak chip
 * (an integrated or software one, or a small texture limit) halves the realistic's to the medium's and the medium's to 4.
 * Memory: a probe of 256 px is 6.3 MB (half float, its blur levels), of 128 px 1.6 MB.
 */
export function probeBudget(profile: 'drawing' | 'medium' | 'realistic', weak: boolean): ProbeBudget | null {
  if (profile === 'drawing') return null;
  if (profile === 'medium') return weak ? { slots: 4, size: 128, facesPerFrame: 1 } : { slots: 6, size: 128, facesPerFrame: 2 };
  return weak ? { slots: 6, size: 128, facesPerFrame: 2 } : { slots: 10, size: 256, facesPerFrame: 2 };
}

/** Whether a graphics chip is a weak one, by its name (WEBGL_debug_renderer_info) and its texture size limit. */
export function weakGpu(renderer: string, maxTextureSize: number): boolean {
  if (maxTextureSize > 0 && maxTextureSize < 8192) return true;
  return /swiftshader|llvmpipe|software|mali|adreno|powervr|intel\(r\) (hd|uhd)|intel hd|intel uhd/i.test(renderer);
}
