/**
 * Instances of shared meshes (docs/plan-instancing.md): an element the engine
 * sends as a mesh's id and 12 numbers, placed where it stands for what needs
 * its triangles in the world (the walker, the model's bounds).
 */

import type { RenderItem, RenderPart, SharedMesh } from './protocol';

type M = ArrayLike<number>;

/** A point placed by an instance's numbers: O + X·x + Y·y + Z·z. */
export const place = (m: M, x: number, y: number, z: number): [number, number, number] =>
  [m[9] + m[0] * x + m[3] * y + m[6] * z, m[10] + m[1] * x + m[4] * y + m[7] * z, m[11] + m[2] * x + m[5] * y + m[8] * z];

/** The 12 numbers as a three.js Matrix4's column-major elements. */
export const elements = (m: M): number[] => [m[0], m[1], m[2], 0, m[3], m[4], m[5], 0, m[6], m[7], m[8], 0, m[9], m[10], m[11], 1];

/** The box of a mesh's box placed by `m` (its eight corners), min then max. */
export function placedBox(box: M, m: M): [number, number, number, number, number, number] {
  const o: [number, number, number, number, number, number] = [Infinity, Infinity, Infinity, -Infinity, -Infinity, -Infinity];
  for (let k = 0; k < 8; k++) {
    const p = place(m, box[k & 1 ? 3 : 0], box[k & 2 ? 4 : 1], box[k & 4 ? 5 : 2]);
    for (let a = 0; a < 3; a++) { o[a] = Math.min(o[a], p[a]); o[3 + a] = Math.max(o[3 + a], p[a]); }
  }
  return o;
}

const positionsOf = (src: Float32Array, m: M) => {
  const out = new Float32Array(src.length);
  for (let i = 0; i + 2 < src.length; i += 3) { const p = place(m, src[i], src[i + 1], src[i + 2]); out[i] = p[0]; out[i + 1] = p[1]; out[i + 2] = p[2]; }
  return out;
};
const normalsOf = (src: Float32Array, m: M) => {
  const out = new Float32Array(src.length);
  for (let i = 0; i + 2 < src.length; i += 3) {
    const x = m[0] * src[i] + m[3] * src[i + 1] + m[6] * src[i + 2], y = m[1] * src[i] + m[4] * src[i + 1] + m[7] * src[i + 2], z = m[2] * src[i] + m[5] * src[i + 1] + m[8] * src[i + 2];
    const l = Math.hypot(x, y, z) || 1;
    out[i] = x / l; out[i + 1] = y / l; out[i + 2] = z / l;
  }
  return out;
};

const placed = new WeakMap<RenderItem, RenderItem>();

/** An item with its own triangles in the world: an instance's mesh placed (kept per item); any other as it is. */
export function inWorld(item: RenderItem, meshes: ReadonlyMap<string, SharedMesh>): RenderItem {
  const inst = item.instance;
  if (!inst) return item;
  const known = placed.get(item);
  if (known) return known;
  const mesh = meshes.get(inst.mesh);
  const parts: RenderPart[] = (mesh?.parts ?? []).map((p) => ({
    // The coarser copies stay with the mesh: what needs the triangles in the world needs the true ones.
    ...p, positions: positionsOf(p.positions, inst.matrix), normals: normalsOf(p.normals, inst.matrix), edges: positionsOf(p.edges, inst.matrix), lods: null,
  }));
  const out = { ...item, parts, instance: null };
  placed.set(item, out);
  return out;
}
