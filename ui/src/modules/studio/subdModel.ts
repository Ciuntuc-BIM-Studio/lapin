/**
 * The SubD cage edit mode's rules (docs/plan-studio-freeform.md §8, stage F1c), with no view and no engine in them: the cage as
 * `studio.subdCage` answers it, what is picked (a vertex, an edge or a face), the pick under a cursor, the preview of a drag, and
 * the arguments of the engine's commands (`studio.subdEdit`, `studio.subdOp`, `studio.subdSet`).
 */

import type { Vec3 } from '../../viewport/camera';

/** The cage as `studio.subdCage` answers it (world coordinates). */
export interface Cage {
  id: string;
  points: number[][];
  faces: number[][];
  edges: [number, number][];
  creases: [number, number, number][];
  borders: [number, number][];
  closed: boolean;
  symmetry: { point: number[]; normal: number[] } | null;
  partners: number[] | null;
  level: number | null;
  deviation: number | null;
  levels: number | null;
  thickness: number;
  side: 'out' | 'in' | 'centre';
  problem: string | null;
}

/** What the pick filter picks. */
export type PickMode = 'vertex' | 'edge' | 'face';

/** What is picked: one kind of thing at a time. A vertex or a face is its index, an edge its two ends, "a-b" with a < b. */
export interface Picked { mode: PickMode; keys: string[] }

export const none = (mode: PickMode): Picked => ({ mode, keys: [] });

export const edgeKey = (a: number, b: number): string => (a < b ? `${a}-${b}` : `${b}-${a}`);
export const edgeOf = (key: string): [number, number] => { const [a, b] = key.split('-').map(Number); return [a!, b!]; };
const keyOf = (mode: PickMode, item: number | [number, number]): string => (mode === 'edge' ? edgeKey((item as [number, number])[0], (item as [number, number])[1]) : String(item));

/** A click on a thing: it alone, or with Shift added to what is picked (or taken off when it was). */
export function pick(cur: Picked, mode: PickMode, item: number | [number, number] | null, additive: boolean): Picked {
  const base = cur.mode === mode ? cur.keys : [];
  if (item == null) return additive ? { mode, keys: base } : none(mode);
  const k = keyOf(mode, item);
  if (!additive) return { mode, keys: [k] };
  return { mode, keys: base.includes(k) ? base.filter((x) => x !== k) : [...base, k] };
}

/** Another filter keeps nothing picked (the kinds do not mix). */
export const withMode = (cur: Picked, mode: PickMode): Picked => (cur.mode === mode ? cur : none(mode));

/** The cage's points a pick names. */
export function pointsOf(cage: Pick<Cage, 'faces'>, p: Picked): number[] {
  const out = new Set<number>();
  for (const k of p.keys) {
    if (p.mode === 'vertex') out.add(Number(k));
    else if (p.mode === 'edge') { const [a, b] = edgeOf(k); out.add(a); out.add(b); } else for (const v of cage.faces[Number(k)] ?? []) out.add(v);
  }
  return [...out].sort((a, b) => a - b);
}

/** The middle of what is picked (the mean of its points, as the engine takes it), or null with nothing picked. */
export function middleOf(cage: Pick<Cage, 'faces' | 'points'>, p: Picked): Vec3 | null {
  const pts = pointsOf(cage, p);
  if (!pts.length) return null;
  let x = 0, y = 0, z = 0;
  for (const i of pts) { const q = cage.points[i]; if (q) { x += q[0]!; y += q[1]!; z += q[2]!; } }
  return { x: x / pts.length, y: y / pts.length, z: z / pts.length };
}

/** The selection's part of a command's arguments (`vertices`, `edges` or `faces`). */
export function selectionArgs(p: Picked): Record<string, unknown> {
  if (p.mode === 'vertex') return { vertices: p.keys.map(Number) };
  if (p.mode === 'edge') return { edges: p.keys.map(edgeOf) };
  return { faces: p.keys.map(Number) };
}

const r6 = (v: number) => Math.round(v * 1e6) / 1e6;

/** A displacement worth applying: more than a tenth of a millimetre. */
export const moves = (by: Vec3): boolean => Math.hypot(by.x, by.y, by.z) > 1e-4;

/** A drag from one point to another as the translation of the engine's edit. */
export const dragBy = (from: Vec3, to: Vec3): Vec3 => ({ x: to.x - from.x, y: to.y - from.y, z: to.z - from.z });

/** `studio.subdEdit` for a drag: ONE command, the picked points translated. Null when it moves nothing or nothing is picked. */
export function moveArgs(id: string, p: Picked, by: Vec3): Record<string, unknown> | null {
  if (!p.keys.length || !moves(by)) return null;
  return { id, ...selectionArgs(p), translate: [r6(by.x), r6(by.y), r6(by.z)] };
}

/** What the typed scale and angle ask: a scale about the picked middle (1 = none), a turn about the upright (degrees). */
export interface Transform { scale?: number; rotate?: number }

/** `studio.subdEdit` for a scale or a turn about the middle of what is picked. */
export function transformArgs(id: string, p: Picked, t: Transform): Record<string, unknown> | null {
  const scale = t.scale != null && Math.abs(t.scale - 1) > 1e-9 && Math.abs(t.scale) > 1e-9 ? t.scale : undefined;
  const rotate = t.rotate != null && Math.abs(t.rotate) > 1e-9 ? t.rotate : undefined;
  if (!p.keys.length || (scale == null && rotate == null)) return null;
  return { id, ...selectionArgs(p), ...(scale != null ? { scale } : {}), ...(rotate != null ? { rotate } : {}) };
}

/** A shaping of the cage by the engine's `studio.subdOp`. */
export type Op =
  | { op: 'extrude'; distance: number }
  | { op: 'loop'; fraction?: number }
  | { op: 'delete' }
  | { op: 'crease'; sharpness: number | null };

/** Why an op cannot be done on what is picked now; null when it can. */
export function opProblem(p: Picked, op: Op['op']): string | null {
  const n = p.keys.length;
  switch (op) {
    case 'extrude': return p.mode === 'face' && n > 0 ? null : 'Pick faces to extrude.';
    case 'delete': return p.mode === 'face' && n > 0 ? null : 'Pick the faces to delete.';
    case 'loop': return p.mode === 'edge' && n === 1 ? null : 'Pick one edge: the loop runs across the ring of quads it starts.';
    case 'crease': return p.mode === 'edge' && n > 0 ? null : 'Pick the edges to crease.';
  }
}

/** `studio.subdOp`'s arguments, or null when the op cannot be done on what is picked. */
export function opArgs(id: string, p: Picked, o: Op): Record<string, unknown> | null {
  if (opProblem(p, o.op)) return null;
  switch (o.op) {
    case 'extrude': return Math.abs(o.distance) > 1e-9 ? { id, op: 'extrude', faces: p.keys.map(Number), distance: r6(o.distance) } : null;
    case 'delete': return { id, op: 'delete', faces: p.keys.map(Number) };
    case 'loop': return { id, op: 'loop', edge: edgeOf(p.keys[0]!), fraction: o.fraction ?? 0.5 };
    case 'crease': return { id, op: 'crease', edges: p.keys.map(edgeOf), ...(o.sharpness == null ? {} : { sharpness: o.sharpness }) };
  }
}

/** The crease sharpnesses offered: smooth (0, takes a crease off), semi-sharp (1 to 3 subdivisions), sharp (for good). */
export const SHARPNESS = [
  { label: 'Smooth', value: 0 }, { label: 'Semi-sharp 1', value: 1 }, { label: 'Semi-sharp 2', value: 2 }, { label: 'Semi-sharp 3', value: 3 }, { label: 'Sharp', value: 10 },
] as const;

/** The sharpness of a cage edge (0 when it has no crease). */
export function sharpnessOf(cage: Pick<Cage, 'creases'>, a: number, b: number): number {
  return cage.creases.find((c) => (c[0] === a && c[1] === b) || (c[0] === b && c[1] === a))?.[2] ?? 0;
}

/** The cage's points as a drag of the pick by `by` shows them: the picked ones moved, a symmetric cage's partners by the mirrored move. */
export function previewPoints(cage: Pick<Cage, 'points' | 'faces' | 'partners' | 'symmetry'>, p: Picked, by: Vec3): number[][] {
  const out = cage.points.map((q) => [...q]);
  if (!moves(by)) return out;
  const picked = pointsOf(cage, p);
  const n = cage.symmetry ? cage.symmetry.normal : null;
  const mirrored = n ? (() => { const d = by.x * n[0]! + by.y * n[1]! + by.z * n[2]!; return { x: by.x - 2 * d * n[0]!, y: by.y - 2 * d * n[1]!, z: by.z - 2 * d * n[2]! }; })() : null;
  const set = new Set(picked);
  for (const i of picked) { out[i]![0]! += by.x; out[i]![1]! += by.y; out[i]![2]! += by.z; }
  if (cage.partners && mirrored) {
    for (const i of picked) {
      const j = cage.partners[i];
      if (j == null || j < 0 || j === i || set.has(j)) continue;
      out[j]![0]! += mirrored.x; out[j]![1]! += mirrored.y; out[j]![2]! += mirrored.z;
    }
  }
  return out;
}

/** A point on the screen (px). */
export interface Px { x: number; y: number }

/** How the screen sees the cage: a point's place (null behind the camera) and its depth from the camera. */
export interface Lens { project: (p: number[]) => Px | null; depth: (p: number[]) => number }

const segDist = (p: Px, a: Px, b: Px): number => {
  const dx = b.x - a.x, dy = b.y - a.y, l2 = dx * dx + dy * dy;
  const t = l2 < 1e-12 ? 0 : Math.max(0, Math.min(1, ((p.x - a.x) * dx + (p.y - a.y) * dy) / l2));
  return Math.hypot(p.x - (a.x + t * dx), p.y - (a.y + t * dy));
};

const inside = (p: Px, poly: Px[]): boolean => {
  let c = false;
  for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
    const a = poly[i]!, b = poly[j]!;
    if ((a.y > p.y) !== (b.y > p.y) && p.x < ((b.x - a.x) * (p.y - a.y)) / (b.y - a.y) + a.x) c = !c;
  }
  return c;
};

/** What the cursor is on, by the filter: the nearest point or edge within `tol` px, the nearest face (to the camera) it is inside of. */
export function pickAt(cage: Pick<Cage, 'points' | 'faces' | 'edges'>, mode: PickMode, lens: Lens, cursor: Px, tol = 9): number | [number, number] | null {
  const pts = cage.points.map((q) => lens.project(q));
  if (mode === 'vertex') {
    let best = -1, bd = tol, bz = Infinity;
    pts.forEach((s, i) => {
      if (!s) return;
      const d = Math.hypot(s.x - cursor.x, s.y - cursor.y), z = lens.depth(cage.points[i]!);
      if (d < bd - 1e-9 || (best >= 0 && Math.abs(d - bd) <= 1e-9 && z < bz)) { bd = d; bz = z; best = i; }
    });
    return best >= 0 ? best : null;
  }
  if (mode === 'edge') {
    let best: [number, number] | null = null, bd = tol, bz = Infinity;
    for (const e of cage.edges) {
      const a = pts[e[0]], b = pts[e[1]];
      if (!a || !b) continue;
      const d = segDist(cursor, a, b), z = Math.min(lens.depth(cage.points[e[0]]!), lens.depth(cage.points[e[1]]!));
      if (d < bd - 1e-9 || (best && Math.abs(d - bd) <= 1e-9 && z < bz)) { bd = d; bz = z; best = [e[0], e[1]]; }
    }
    return best;
  }
  let best = -1, bz = Infinity;
  cage.faces.forEach((f, i) => {
    const poly = f.map((v) => pts[v]);
    if (poly.some((s) => !s)) return;
    if (!inside(cursor, poly as Px[])) return;
    const z = f.reduce((s, v) => s + lens.depth(cage.points[v]!), 0) / f.length;
    if (z < bz) { bz = z; best = i; }
  });
  return best >= 0 ? best : null;
}

/** Where a thing is picked from in the model: the point itself, the edge's nearest point to a ray (its middle here), the face's middle. */
export function anchorOf(cage: Pick<Cage, 'points' | 'faces'>, mode: PickMode, item: number | [number, number]): Vec3 | null {
  const idx = mode === 'vertex' ? [item as number] : mode === 'edge' ? [...(item as [number, number])] : cage.faces[item as number];
  if (!idx?.length) return null;
  const q = idx.map((i) => cage.points[i]);
  if (q.some((x) => !x)) return null;
  return { x: q.reduce((s, x) => s + x![0]!, 0) / q.length, y: q.reduce((s, x) => s + x![1]!, 0) / q.length, z: q.reduce((s, x) => s + x![2]!, 0) / q.length };
}

/** A deviation in millimetres, to one decimal. */
export const deviationMm = (d: number | null | undefined): string => (d == null ? '—' : `${(d * 1000).toFixed(1)} mm`);

/** Whether a node is a SubD solid. */
export const isSubD = (n: { props: Record<string, unknown> } | null | undefined): boolean => !!n && n.props.operation === 'subd';

/** The cage's edges by the way they are drawn: a crease (with its sharpness), a border of an open surface, or a plain one. */
export function edgeClasses(cage: Pick<Cage, 'edges' | 'creases' | 'borders'>): { plain: [number, number][]; border: [number, number][]; crease: [number, number, number][] } {
  const crease = new Map(cage.creases.filter((c) => c[2] > 0).map((c) => [edgeKey(c[0], c[1]), c[2]]));
  const border = new Set(cage.borders.map(([a, b]) => edgeKey(a, b)));
  const out = { plain: [] as [number, number][], border: [] as [number, number][], crease: [] as [number, number, number][] };
  for (const e of cage.edges) {
    const k = edgeKey(e[0], e[1]);
    const s = crease.get(k);
    if (s != null) out.crease.push([e[0], e[1], s]);
    else if (border.has(k)) out.border.push([e[0], e[1]]);
    else out.plain.push([e[0], e[1]]);
  }
  return out;
}

/** The symmetry plane as the card names it: x, y or z when its normal is that world axis, 'plane' for any other, 'none'. */
export function symmetryAxis(cage: Pick<Cage, 'symmetry'>): 'none' | 'x' | 'y' | 'z' | 'plane' {
  const n = cage.symmetry?.normal;
  if (!n) return 'none';
  const near = (v: number, w: number) => Math.abs(Math.abs(v) - w) < 1e-6;
  if (near(n[0]!, 1) && near(n[1]!, 0) && near(n[2]!, 0)) return 'x';
  if (near(n[0]!, 0) && near(n[1]!, 1) && near(n[2]!, 0)) return 'y';
  if (near(n[0]!, 0) && near(n[1]!, 0) && near(n[2]!, 1)) return 'z';
  return 'plane';
}

/** `studio.subdSet` for a thickness typed: 0 or less takes it off (null). */
export const thicknessArgs = (m: number): Record<string, unknown> => ({ thickness: m > 1e-9 ? m : null });

/** `studio.subdSet` for the symmetry chosen: none clears it. */
export const symmetryArgs = (v: 'none' | 'x' | 'y' | 'z'): Record<string, unknown> => ({ symmetry: v === 'none' ? null : v });
