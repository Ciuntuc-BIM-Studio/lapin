/**
 * Which copy of a part the 3D draws (docs/plan-display-precision.md): the
 * engine sends heavy parts with coarser copies, each with the most it strays
 * from the part in metres; the view draws the coarsest whose error, at the
 * part's distance, stays under the project's pixel budget. The model,
 * drawings, exports and picking always use the part itself.
 */

import type { NodeDto, RenderLod } from '../engine/protocol';

export type DisplayQuality = 'fast' | 'balanced' | 'precise';

export const QUALITIES: readonly DisplayQuality[] = ['fast', 'balanced', 'precise'];

/** The pixels a copy may stray on screen: precise draws every part as it is. */
export const PIXELS: Record<DisplayQuality, number> = { fast: 3, balanced: 1, precise: 0 };

/** The project's display quality (IfcProject's `displayQuality`, project.setDisplayQuality); balanced when it has none. */
export function displayQualityOf(nodes: ReadonlyMap<string, NodeDto>): DisplayQuality {
  for (const n of nodes.values())
    if (n.type === 'IfcProject') return QUALITIES.includes(n.props.displayQuality as DisplayQuality) ? n.props.displayQuality as DisplayQuality : 'balanced';
  return 'balanced';
}

/** What the camera is, for the pixels a metre covers. */
export type LodCamera =
  | { kind: 'perspective'; eye: readonly [number, number, number]; fovDeg: number; heightPx: number }
  | { kind: 'ortho'; metresHigh: number; heightPx: number };

/**
 * How many pixels a metre covers at a sphere (its centre and radius) seen by
 * the camera: in perspective at its nearest point (the eye inside it: as near
 * as can be), in orthographic the same everywhere.
 */
export function pixelsPerMetre(cam: LodCamera, centre: readonly [number, number, number], radius: number): number {
  if (cam.kind === 'ortho') return cam.heightPx / Math.max(cam.metresHigh, 1e-9);
  const d = Math.hypot(centre[0] - cam.eye[0], centre[1] - cam.eye[1], centre[2] - cam.eye[2]) - radius;
  if (d <= 1e-3) return Infinity;
  return cam.heightPx / 2 / (d * Math.tan((cam.fovDeg * Math.PI) / 360));
}

/** The copy to draw: 0 the part itself, k its k-th coarser copy (errors finest first). */
export function levelFor(errors: readonly number[], ppm: number, quality: DisplayQuality): number {
  const budget = PIXELS[quality];
  if (budget <= 0 || !Number.isFinite(ppm)) return 0;
  let level = 0;
  for (let k = 0; k < errors.length; k++) if (errors[k] * ppm <= budget) level = k + 1;
  return level;
}

/** The fold under which a copy's surface is shaded smooth (the engine draws its edges past the same). */
export const COPY_CREASE_DEG = 40;

/**
 * Normals for triangles given as three corners each: at every corner, the mean (by area) of the triangles meeting
 * there that fold from this one by less than `creaseDeg`. Shaded flat, a coarse copy showed its facets in the light
 * even where its shape was within a pixel; smooth across every fold, a box would lose its edges.
 */
export function creasedNormals(pos: Float32Array, creaseDeg = COPY_CREASE_DEG): Float32Array {
  const tris = Math.floor(pos.length / 9);
  const face = new Float32Array(tris * 3);
  for (let t = 0; t < tris; t++) {
    const o = 9 * t;
    const ux = pos[o + 3] - pos[o], uy = pos[o + 4] - pos[o + 1], uz = pos[o + 5] - pos[o + 2];
    const vx = pos[o + 6] - pos[o], vy = pos[o + 7] - pos[o + 1], vz = pos[o + 8] - pos[o + 2];
    // Unnormalised: its length is twice the area, the weight.
    face[3 * t] = uy * vz - uz * vy; face[3 * t + 1] = uz * vx - ux * vz; face[3 * t + 2] = ux * vy - uy * vx;
  }
  const at = new Map<string, number[]>();
  for (let c = 0; c < tris * 3; c++) {
    const key = `${pos[3 * c]},${pos[3 * c + 1]},${pos[3 * c + 2]}`;
    const list = at.get(key);
    if (list) list.push(c); else at.set(key, [c]);
  }
  const cos = Math.cos((creaseDeg * Math.PI) / 180);
  const out = new Float32Array(tris * 9);
  for (const corners of at.values())
    for (const c of corners) {
      const f = Math.floor(c / 3);
      const fl = Math.hypot(face[3 * f], face[3 * f + 1], face[3 * f + 2]) || 1;
      let x = 0, y = 0, z = 0;
      for (const d of corners) {
        const g = Math.floor(d / 3);
        const gl = Math.hypot(face[3 * g], face[3 * g + 1], face[3 * g + 2]) || 1;
        if ((face[3 * f] * face[3 * g] + face[3 * f + 1] * face[3 * g + 1] + face[3 * f + 2] * face[3 * g + 2]) / (fl * gl) < cos) continue;
        x += face[3 * g]; y += face[3 * g + 1]; z += face[3 * g + 2];
      }
      const l = Math.hypot(x, y, z) || 1;
      out[3 * c] = x / l; out[3 * c + 1] = y / l; out[3 * c + 2] = z / l;
    }
  return out;
}

const made = new WeakMap<Float32Array, { normals: Float32Array; indices: Int32Array }>();

/**
 * A copy as a mesh to draw: the engine sends three corners a triangle and nothing else (half the weight), so its
 * corners are numbered in order and its normals made here, smooth under the crease (made once per copy).
 */
export function copyArrays(l: RenderLod): { positions: Float32Array; normals: Float32Array; indices: Int32Array; edges: Float32Array } {
  if (l.indices.length) return l;
  let m = made.get(l.positions);
  if (!m) {
    const indices = new Int32Array(l.positions.length / 3);
    for (let i = 0; i < indices.length; i++) indices[i] = i;
    m = { normals: creasedNormals(l.positions), indices };
    made.set(l.positions, m);
  }
  return { positions: l.positions, normals: m.normals, indices: m.indices, edges: l.edges };
}
