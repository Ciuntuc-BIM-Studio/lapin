/**
 * The section box (View → Section box): the model seen in 3D inside a box
 * whose six faces each cut it, dragged by the handles on their middles, and
 * turned about the vertical by the handle on its top; the cut elements closed
 * by their caps in their own materials, as a section shows them
 * (section3d.ts). "Cut at plan" sets its top at the active plan's cut, so a
 * storey reads in 3D as its plan cuts it. A saved 3D view keeps its box.
 *
 * A box turned by `rot` (radians, about the vertical through the world's
 * origin) has `min` and `max` in its turned axes: a point's box coordinates
 * are its world ones turned back by `rot`.
 */

import { create } from 'zustand';
import type { RenderItem, SharedMesh } from '../engine/protocol';
import { placedBox } from '../engine/instances';
import type { Vec3 } from './camera';
import type { ClipPlane } from './section3d';

export type Triple = [number, number, number];
export interface SectionBox { min: Triple; max: Triple; rot?: number }

/** The box the 3D view is cut by (null: none). */
export const useSectionBox = create<{ box: SectionBox | null }>(() => ({ box: null }));

/** How thin the box may get along any axis, metres. */
export const MIN_SIZE = 0.05;

/** The box's axes in the world: its x and y turned by its rotation, its z up. */
export function boxAxes(b: SectionBox): [Vec3, Vec3, Vec3] {
  const r = b.rot ?? 0, c = Math.cos(r), s = Math.sin(r);
  return [{ x: c, y: s, z: 0 }, { x: -s, y: c, z: 0 }, { x: 0, y: 0, z: 1 }];
}

/** A point given in the box's axes, in the world. */
export function toWorld(b: SectionBox, p: Triple): Vec3 {
  const [ex, ey] = boxAxes(b);
  return { x: ex.x * p[0] + ey.x * p[1], y: ex.y * p[0] + ey.y * p[1], z: p[2] };
}

/** A world point in the box's axes. */
export function toBox(b: SectionBox, p: Vec3): Triple {
  const [ex, ey] = boxAxes(b);
  return [ex.x * p.x + ex.y * p.y, ey.x * p.x + ey.y * p.y, p.z];
}

/** The six planes of a box, each keeping its inside (normal·p + constant ≥ 0). */
export function boxPlanes(b: SectionBox): ClipPlane[] {
  const planes: ClipPlane[] = [];
  boxAxes(b).forEach((e, k) => {
    planes.push({ normal: [e.x, e.y, e.z], constant: -b.min[k] });
    planes.push({ normal: [-e.x, -e.y, -e.z], constant: b.max[k] });
  });
  return planes;
}

/** Everything drawn, as a box (turned by `rot`) a little larger than it (null: nothing drawn); an instance by its mesh's box, placed. */
export function modelBounds(items: Iterable<RenderItem>, pad = 0.5, rot = 0, meshes?: ReadonlyMap<string, SharedMesh>): SectionBox | null {
  const frame: SectionBox = { min: [0, 0, 0], max: [0, 0, 0], rot };
  const min: Triple = [Infinity, Infinity, Infinity], max: Triple = [-Infinity, -Infinity, -Infinity];
  const take = (p: ArrayLike<number>) => {
    for (let i = 0; i + 2 < p.length; i += 3) {
      const q = rot ? toBox(frame, { x: p[i], y: p[i + 1], z: p[i + 2] }) : [p[i], p[i + 1], p[i + 2]];
      for (let k = 0; k < 3; k++) { min[k] = Math.min(min[k], q[k]); max[k] = Math.max(max[k], q[k]); }
    }
  };
  for (const item of items) {
    const mesh = item.instance ? meshes?.get(item.instance.mesh) : undefined;
    if (item.instance && mesh) {
      const q = placedBox(mesh.box, item.instance.matrix);
      for (let k = 0; k < 8; k++) take([q[k & 1 ? 3 : 0], q[k & 2 ? 4 : 1], q[k & 4 ? 5 : 2]]);
      continue;
    }
    take(item.positions);
    for (const p of item.parts ?? []) take(p.positions);
  }
  if (!(min[0] <= max[0])) return null;
  const box: SectionBox = { min: [min[0] - pad, min[1] - pad, min[2] - pad], max: [max[0] + pad, max[1] + pad, max[2] + pad] };
  if (rot) box.rot = rot;
  return box;
}

export type FaceKey = 'x0' | 'x1' | 'y0' | 'y1' | 'z0' | 'z1';
export const FACES: FaceKey[] = ['x0', 'x1', 'y0', 'y1', 'z0', 'z1'];

const axisOf = (f: FaceKey) => (f[0] === 'x' ? 0 : f[0] === 'y' ? 1 : 2);
const isMax = (f: FaceKey) => f[1] === '1';
const centreOf = (b: SectionBox): Triple => [(b.min[0] + b.max[0]) / 2, (b.min[1] + b.max[1]) / 2, (b.min[2] + b.max[2]) / 2];

/** A face's middle, the axis it moves along (the box's), and the way out of the box, in the world. */
export function faceHandle(b: SectionBox, f: FaceKey): { at: Vec3; axis: 0 | 1 | 2; out: Vec3 } {
  const k = axisOf(f), c = centreOf(b);
  c[k] = isMax(f) ? b.max[k] : b.min[k];
  const e = boxAxes(b)[k], sign = isMax(f) ? 1 : -1;
  return { at: toWorld(b, c), axis: k as 0 | 1 | 2, out: { x: e.x * sign, y: e.y * sign, z: e.z * sign } };
}

/** The box with one face moved to `value` along its axis (in the box's axes), never past the opposite face (less MIN_SIZE). */
export function moveFace(b: SectionBox, f: FaceKey, value: number): SectionBox {
  const k = axisOf(f);
  const min: Triple = [...b.min], max: Triple = [...b.max];
  if (isMax(f)) max[k] = Math.max(value, min[k] + MIN_SIZE);
  else min[k] = Math.min(value, max[k] - MIN_SIZE);
  return { ...b, min, max };
}

/** The box's twelve edges, as point pairs in the world. */
export function boxEdges(b: SectionBox): [Vec3, Vec3][] {
  const p = (i: number): Vec3 => toWorld(b, [i & 1 ? b.max[0] : b.min[0], i & 2 ? b.max[1] : b.min[1], i & 4 ? b.max[2] : b.min[2]]);
  const out: [Vec3, Vec3][] = [];
  for (let i = 0; i < 8; i++) for (const bit of [1, 2, 4]) if (!(i & bit)) out.push([p(i), p(i | bit)]);
  return out;
}

/**
 * Where a face dragged along its axis goes: the point of the axis (through
 * `at`, along the box's axis `axis`) nearest the pointer's ray (from `origin`
 * along `dir`), as a coordinate in the box's axes; null when the ray runs
 * along the axis.
 */
export function alongAxis(b: SectionBox, at: Vec3, axis: 0 | 1 | 2, origin: Vec3, dir: Vec3): number | null {
  const e = boxAxes(b)[axis];
  const u: Triple = [e.x, e.y, e.z];
  const d: Triple = [dir.x, dir.y, dir.z], w: Triple = [at.x - origin.x, at.y - origin.y, at.z - origin.z];
  const dot = (a: Triple, c: Triple) => a[0] * c[0] + a[1] * c[1] + a[2] * c[2];
  const bb = dot(u, d), c = dot(d, d), dd = dot(u, w), ee = dot(d, w);
  const den = c - bb * bb; // |u| = 1
  if (Math.abs(den) < 1e-9) return null;
  const t = (bb * ee - c * dd) / den;
  return toBox(b, at)[axis] + t;
}

/** Where the turning handle stands: past the top face's corner, along the box's x. */
export function rotateHandle(b: SectionBox, reach = 0.6): Vec3 {
  return toWorld(b, [b.max[0] + reach, b.max[1], b.max[2]]);
}

/** The box's middle, in the world. */
export const boxCentre = (b: SectionBox): Vec3 => toWorld(b, centreOf(b));

/** The box turned to `rot` about its own middle (its size kept). */
export function rotateBox(b: SectionBox, rot: number): SectionBox {
  const centre = boxCentre(b);
  const half: Triple = [(b.max[0] - b.min[0]) / 2, (b.max[1] - b.min[1]) / 2, (b.max[2] - b.min[2]) / 2];
  const turned: SectionBox = { min: [0, 0, 0], max: [0, 0, 0], rot };
  const c = toBox(turned, centre);
  const out: SectionBox = { min: [c[0] - half[0], c[1] - half[1], c[2] - half[2]], max: [c[0] + half[0], c[1] + half[1], c[2] + half[2]] };
  const r = Math.atan2(Math.sin(rot), Math.cos(rot));
  if (Math.abs(r) > 1e-9) out.rot = r;
  return out;
}

/** The way an element runs in plan (its longest plan edge), as an angle; null when it has none. */
export function runOf(item: RenderItem | undefined): number | null {
  let best = 0, angle: number | null = null;
  for (const p of item?.plan ?? []) {
    const pts = p.points;
    const n = pts.length / 2;
    for (let i = 0; i + 1 < n + (p.closed ? 1 : 0); i++) {
      const j = (i + 1) % n;
      const dx = pts[2 * j] - pts[2 * i], dy = pts[2 * j + 1] - pts[2 * i + 1], len = Math.hypot(dx, dy);
      if (len > best + 1e-9) { best = len; angle = Math.atan2(dy, dx); }
    }
  }
  return angle;
}

/** A stable key of a list of planes, to know when they changed. */
export const planesKey = (planes: ClipPlane[]) => planes.map((p) => [...p.normal, p.constant].map((v) => v.toFixed(4)).join(',')).join(';');
