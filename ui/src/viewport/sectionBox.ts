/**
 * The section box (View → Section box): the model seen in 3D inside a box
 * whose six faces each cut it, dragged by the handles on their middles; the
 * cut elements closed by their caps in their own materials, as a section
 * shows them (section3d.ts). "Cut at plan" sets its top at the active plan's
 * cut, so a storey reads in 3D as its plan cuts it.
 */

import { create } from 'zustand';
import type { RenderItem } from '../engine/protocol';
import type { Vec3 } from './camera';
import type { ClipPlane } from './section3d';

export type Triple = [number, number, number];
export interface SectionBox { min: Triple; max: Triple }

/** The box the 3D view is cut by (null: none). */
export const useSectionBox = create<{ box: SectionBox | null }>(() => ({ box: null }));

/** How thin the box may get along any axis, metres. */
export const MIN_SIZE = 0.05;

/** The six planes of a box, each keeping its inside (normal·p + constant ≥ 0). */
export function boxPlanes(b: SectionBox): ClipPlane[] {
  const planes: ClipPlane[] = [];
  for (let k = 0; k < 3; k++) {
    const n: Triple = [0, 0, 0];
    n[k] = 1;
    planes.push({ normal: [...n], constant: -b.min[k] });
    n[k] = -1;
    planes.push({ normal: [...n], constant: b.max[k] });
  }
  return planes;
}

/** Everything drawn, as a box a little larger than it (null: nothing drawn). */
export function modelBounds(items: Iterable<RenderItem>, pad = 0.5): SectionBox | null {
  const min: Triple = [Infinity, Infinity, Infinity], max: Triple = [-Infinity, -Infinity, -Infinity];
  const take = (p: Float32Array) => {
    for (let i = 0; i + 2 < p.length; i += 3) for (let k = 0; k < 3; k++) { min[k] = Math.min(min[k], p[i + k]); max[k] = Math.max(max[k], p[i + k]); }
  };
  for (const item of items) { take(item.positions); for (const p of item.parts ?? []) take(p.positions); }
  if (!(min[0] <= max[0])) return null;
  return { min: [min[0] - pad, min[1] - pad, min[2] - pad], max: [max[0] + pad, max[1] + pad, max[2] + pad] };
}

export type FaceKey = 'x0' | 'x1' | 'y0' | 'y1' | 'z0' | 'z1';
export const FACES: FaceKey[] = ['x0', 'x1', 'y0', 'y1', 'z0', 'z1'];

const axisOf = (f: FaceKey) => (f[0] === 'x' ? 0 : f[0] === 'y' ? 1 : 2);
const isMax = (f: FaceKey) => f[1] === '1';

/** A face's middle, the axis it moves along and which way is out of the box. */
export function faceHandle(b: SectionBox, f: FaceKey): { at: Vec3; axis: 0 | 1 | 2; out: Vec3 } {
  const k = axisOf(f), c: Triple = [(b.min[0] + b.max[0]) / 2, (b.min[1] + b.max[1]) / 2, (b.min[2] + b.max[2]) / 2];
  c[k] = isMax(f) ? b.max[k] : b.min[k];
  const out: Triple = [0, 0, 0];
  out[k] = isMax(f) ? 1 : -1;
  return { at: { x: c[0], y: c[1], z: c[2] }, axis: k as 0 | 1 | 2, out: { x: out[0], y: out[1], z: out[2] } };
}

/** The box with one face moved to `value` along its axis, never past the opposite face (less MIN_SIZE). */
export function moveFace(b: SectionBox, f: FaceKey, value: number): SectionBox {
  const k = axisOf(f);
  const min: Triple = [...b.min], max: Triple = [...b.max];
  if (isMax(f)) max[k] = Math.max(value, min[k] + MIN_SIZE);
  else min[k] = Math.min(value, max[k] - MIN_SIZE);
  return { min, max };
}

/** The box's twelve edges, as point pairs. */
export function boxEdges(b: SectionBox): [Vec3, Vec3][] {
  const p = (i: number): Vec3 => ({ x: i & 1 ? b.max[0] : b.min[0], y: i & 2 ? b.max[1] : b.min[1], z: i & 4 ? b.max[2] : b.min[2] });
  const out: [Vec3, Vec3][] = [];
  for (let i = 0; i < 8; i++) for (const bit of [1, 2, 4]) if (!(i & bit)) out.push([p(i), p(i | bit)]);
  return out;
}

/**
 * Where a face dragged along its axis goes: the point of the axis nearest the
 * pointer's ray (from `origin` along `dir`), as the distance along the axis
 * from the world's origin; null when the ray runs along the axis.
 */
export function alongAxis(at: Vec3, axis: 0 | 1 | 2, origin: Vec3, dir: Vec3): number | null {
  const u: Triple = [0, 0, 0];
  u[axis] = 1;
  const d: Triple = [dir.x, dir.y, dir.z], w: Triple = [at.x - origin.x, at.y - origin.y, at.z - origin.z];
  const dot = (a: Triple, b: Triple) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
  const b = dot(u, d), c = dot(d, d), dd = dot(u, w), e = dot(d, w);
  const den = c - b * b; // |u| = 1
  if (Math.abs(den) < 1e-9) return null;
  const t = (b * e - c * dd) / den;
  return [at.x, at.y, at.z][axis] + t;
}

/** A stable key of a list of planes, to know when they changed. */
export const planesKey = (planes: ClipPlane[]) => planes.map((p) => [...p.normal, p.constant].map((v) => v.toFixed(4)).join(',')).join(';');
