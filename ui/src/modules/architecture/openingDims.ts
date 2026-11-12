/**
 * A door's, a window's or a bare opening's temporary dimensions in plan (a tester's report, decision 511; Revit's temporary
 * dimensions): with one selected, a dimension from each of its edges to the nearest reference along its wall on that side
 * (a face of a wall meeting it, else the wall's own end), and one to the nearest neighbouring opening when that is nearer.
 * Their ends are cycled by a click (the opening's edge or its centre; the reference's near face, far face, core faces, axis,
 * or the other opening's near edge, centre, far edge), and a value typed moves the opening along its wall so that the
 * dimension reads it, the reference staying where it is.
 *
 * Pure: what architecture.openingReferences answers, and positions along the wall's axis in metres from its start.
 */

import type { Vec2 } from '../../platform/module';

export type Side = 'left' | 'right';
/** Which way along the wall a dimension runs from the opening: −1 towards the wall's start, +1 towards its end. */
export type Way = -1 | 1;

export interface WallOn { faces: number[]; core?: number[]; axis: number }
export interface RefWall { id: string; name: string; sides: Side[]; left: WallOn; right: WallOn }
export interface RefOpening { id: string; name: string; along: number; width: number }
/** architecture.openingReferences' answer. */
export interface OpeningRefs {
  wallId: string;
  /** An arc: positions are arc lengths along its axis, `scale` how long a metre of axis is on each face. */
  curved?: boolean;
  length?: number;
  start?: number[];
  end?: number[];
  rise?: number;
  dir?: number[];
  centre?: number[];
  radius?: number;
  faces?: { left: number; right: number };
  scale?: { left: number; right: number };
  ends?: { left: number[]; right: number[] };
  /** Absolute heights: the storey's level and the wall's base. */
  levels?: { storey: number; base: number };
  /** `sill`: what architecture.moveOpening takes for the element selected; `bottom`, `top`: the hole's, absolute; `fill`: its door or window. */
  opening?: { id: string; along: number; width: number; sill?: number; bottom?: number; top?: number; fill?: string };
  walls?: RefWall[];
  openings?: RefOpening[];
}

export type RefWhat = 'nearFace' | 'farFace' | 'nearCore' | 'farCore' | 'axis' | 'end' | 'nearEdge' | 'centre' | 'farEdge';
export interface RefOption { what: RefWhat; s: number }
/** Something a dimension's far end can hold on: a wall meeting the host, the host's end, another opening. */
export interface RefTarget { key: string; kind: 'wall' | 'end' | 'opening'; name: string; options: RefOption[] }
/** The opening's own end of a dimension: its edge on that side, or its middle. */
export type OwnEnd = 'edge' | 'centre';

/** What the user chose for one dimension (by its key): the reference's option and the opening's end. */
export interface DimPick { opt?: number; own?: OwnEnd }

export interface TempDim {
  /** `${way}:${row}`: stable while the same opening stays selected. */
  key: string;
  way: Way;
  /** 0: the nearest wall or end; 1: a neighbouring opening nearer than it. */
  row: 0 | 1;
  target: RefTarget;
  opt: number;
  own: OwnEnd;
  /** Where the reference and the opening's end lie along the wall. */
  refS: number;
  ownS: number;
  /** As measured on the face (on an arc, the length along it). */
  value: number;
}

const EPS = 1e-6;
/** References closer than this (m) are one: the wall's end at a corner is the other wall's far face. */
const SAME = 5e-4;

/** Options without the ones that land where an earlier one does. */
function distinct(options: RefOption[]): RefOption[] {
  const out: RefOption[] = [];
  for (const o of options) if (Number.isFinite(o.s) && !out.some((x) => Math.abs(x.s - o.s) < SAME)) out.push(o);
  return out;
}

/** The side of the wall the dimensions go on: the face more walls meet (the inside of an outer wall, where partitions come in); the left on a tie. */
export function chooseSide(r: OpeningRefs): Side {
  const n = (side: Side) => (r.walls ?? []).filter((w) => w.sides.includes(side)).length;
  return n('right') > n('left') ? 'right' : 'left';
}

/** Every reference on one side of the opening (`way`), nearest first, measured on the face `side`; `edge`: the opening's edge that way. */
export function targetsOn(r: OpeningRefs, side: Side, way: Way, edge: number): RefTarget[] {
  const out: RefTarget[] = [];
  const beyond = (s: number) => (way < 0 ? s <= edge + EPS : s >= edge - EPS);
  for (const w of r.walls ?? []) {
    const on = w[side];
    const f = [...on.faces].sort((a, b) => a - b), c = on.core ? [...on.core].sort((a, b) => a - b) : null;
    // A wall the opening stands across (a T on the other face) is no reference on either side.
    if (!f.length || !f.every(beyond)) continue;
    const [near, far] = way < 0 ? [f[f.length - 1], f[0]] : [f[0], f[f.length - 1]];
    const opts: RefOption[] = [{ what: 'nearFace', s: near }, { what: 'farFace', s: far }];
    if (c) opts.push(...(way < 0 ? [{ what: 'nearCore' as const, s: c[1] }, { what: 'farCore' as const, s: c[0] }] : [{ what: 'nearCore' as const, s: c[0] }, { what: 'farCore' as const, s: c[1] }]));
    opts.push({ what: 'axis', s: on.axis });
    out.push({ key: `wall:${w.id}`, kind: 'wall', name: w.name, options: distinct(opts) });
  }
  const ends = r.ends?.[side];
  if (ends?.length === 2) {
    const s = way < 0 ? Math.min(...ends) : Math.max(...ends);
    // The wall's end where a wall's face already is (a corner) is that face.
    const taken = out.some((t) => t.options.some((o) => Math.abs(o.s - s) < SAME));
    if (beyond(s) && !taken) out.push({ key: way < 0 ? 'end:start' : 'end:end', kind: 'end', name: way < 0 ? 'Start' : 'End', options: [{ what: 'end', s }] });
  }
  for (const o of r.openings ?? []) {
    const nearEdge = o.along - (way * o.width) / 2;
    if (!beyond(nearEdge)) continue;
    out.push({ key: `opening:${o.id}`, kind: 'opening', name: o.name, options: distinct([{ what: 'nearEdge', s: nearEdge }, { what: 'centre', s: o.along }, { what: 'farEdge', s: o.along + (way * o.width) / 2 }]) });
  }
  const dist = (t: RefTarget) => Math.abs(t.options[0].s - edge);
  return out.sort((a, b) => dist(a) - dist(b) || (a.kind === 'wall' ? -1 : b.kind === 'wall' ? 1 : 0));
}

/**
 * The dimensions shown for an opening `along` its wall, `width` wide: on each side the nearest wall face or wall end (row 0),
 * and the nearest other opening when it is nearer (row 1); each as `picks` say (its option, its own end), else the defaults
 * (the near face, the opening's edge).
 */
export function tempDims(r: OpeningRefs, side: Side, along: number, width: number, picks: Record<string, DimPick> = {}): TempDim[] {
  const out: TempDim[] = [];
  for (const way of [-1, 1] as Way[]) {
    const edge = along + (way * width) / 2;
    const all = targetsOn(r, side, way, edge);
    const wall = all.find((t) => t.kind !== 'opening');
    const opening = all.find((t) => t.kind === 'opening');
    const rows: [0 | 1, RefTarget][] = [];
    if (wall) rows.push([0, wall]);
    if (opening && (!wall || Math.abs(opening.options[0].s - edge) < Math.abs(wall.options[0].s - edge) - SAME)) rows.push([1, opening]);
    for (const [row, target] of rows) {
      const key = `${way}:${row}`;
      const pick = picks[key] ?? {};
      const opt = ((pick.opt ?? 0) % target.options.length + target.options.length) % target.options.length;
      const own = pick.own ?? 'edge';
      const refS = target.options[opt].s;
      const ownS = own === 'edge' ? edge : along;
      out.push({ key, way, row, target, opt, own, refS, ownS, value: Math.abs(ownS - refS) * faceScale(r, side) });
    }
  }
  return out;
}

/** How long a metre of the axis is on the face `side`: 1 on a straight wall, the face's radius over the axis's on an arc. */
export const faceScale = (r: OpeningRefs, side: Side): number => r.scale?.[side] ?? 1;

/** Where along its wall the opening goes for the dimension to read `value` (m, on the face it is measured on: `scale`), its reference staying put. */
export function alongFor(d: Pick<TempDim, 'way' | 'own' | 'refS'>, value: number, width: number, scale = 1): number {
  const half = d.own === 'edge' ? width / 2 : 0;
  const v = value / (scale || 1);
  return d.way < 0 ? d.refS + v + half : d.refS - v - half;
}

/** Whether the opening, `width` wide, fits between the wall's ends at `along` (the engine checks it too, and the other openings). */
export const fitsAlong = (along: number, width: number, length: number): boolean => along - width / 2 >= -1e-9 && along + width / 2 <= length + 1e-9;

/** An arc's frame: its centre, radius, the start's angle and the way it turns (+1 counter-clockwise); null when straight. */
function arcFrame(r: OpeningRefs) {
  if (!r.curved || !r.centre || !r.radius || !r.start) return null;
  const [cx, cy] = r.centre, [sx, sy] = r.start;
  return { cx, cy, R: r.radius, a0: Math.atan2(sy - cy, sx - cx), sense: (r.rise ?? 0) > 0 ? -1 : 1 };
}

/** The point `s` along the wall's axis and `y` across it (left positive), from the answer's frame; on an arc, round it (past its ends too). */
export function at(r: OpeningRefs, s: number, y: number): Vec2 {
  const f = arcFrame(r);
  if (f) {
    const a = f.a0 + (f.sense * s) / f.R;
    // The tangent is (−sin a, cos a)·sense; its left, (−cos a, −sin a)·sense.
    const rr = f.R - f.sense * y;
    return { x: f.cx + Math.cos(a) * rr, y: f.cy + Math.sin(a) * rr };
  }
  const [x0, y0] = r.start ?? [0, 0], [dx, dy] = r.dir ?? [1, 0];
  return { x: x0 + dx * s - dy * y, y: y0 + dy * s + dx * y };
}

/** Where along the wall's axis a model point falls (from the answer's frame, not clamped; on an arc, by its angle). */
export function alongOf(r: OpeningRefs, p: Vec2): number {
  const f = arcFrame(r);
  if (f) {
    let turned = f.sense * (Math.atan2(p.y - f.cy, p.x - f.cx) - f.a0);
    turned -= 2 * Math.PI * Math.round(turned / (2 * Math.PI));
    return turned * f.R;
  }
  const [x0, y0] = r.start ?? [0, 0], [dx, dy] = r.dir ?? [1, 0];
  return (p.x - x0) * dx + (p.y - y0) * dy;
}

/** An arc-length dimension of the plan for a temporary one on an arc (drafting.addDimension, dimKind arc): its centre, radius, start angle and sweep. */
export function arcDimension(r: OpeningRefs, side: Side, s0: number, s1: number): { c: number[]; r: number; start: number; sweep: number } | null {
  const f = arcFrame(r);
  if (!f || !r.faces) return null;
  return { c: [f.cx, f.cy], r: f.R - f.sense * r.faces[side], start: f.a0 + (f.sense * s0) / f.R, sweep: (f.sense * (s1 - s0)) / f.R };
}

// ------------------------------------------------------------------ sections and elevations

export interface Frame3 { origin: number[]; right: number[]; up: number[]; dir: number[] }

/** A model point at height z as a section or an elevation draws it: x along the view, y up. */
export function toView(f: Frame3, p: Vec2, z: number): Vec2 {
  const d = [p.x - f.origin[0], p.y - f.origin[1], z - f.origin[2]];
  return { x: d[0] * f.right[0] + d[1] * f.right[1] + d[2] * f.right[2], y: d[0] * f.up[0] + d[1] * f.up[1] + d[2] * f.up[2] };
}

/** How near parallel to the view a wall must run to be seen face-on (the cosine of the angle: 10°). */
const FACE_ON = Math.cos((10 * Math.PI) / 180);

/**
 * Whether a section or an elevation sees the opening's wall face-on: straight, running along the view (within 10°), the
 * view level (its x horizontal). Its face towards the viewer, then; null when the wall is curved, cut obliquely or seen
 * edge-on.
 */
export function faceOn(r: OpeningRefs, f: Frame3): Side | null {
  if (r.curved || !r.dir || !r.faces) return null;
  if (Math.abs(f.right[2]) > 1e-6) return null;
  const [dx, dy] = r.dir;
  const rl = Math.hypot(f.right[0], f.right[1]) || 1;
  if (Math.abs((dx * f.right[0] + dy * f.right[1]) / rl) < FACE_ON) return null;
  // The face whose outward normal looks back at the viewer (the view looks along `dir`).
  return -dy * f.dir[0] + dx * f.dir[1] < 0 ? 'left' : 'right';
}

/** The opening's height dimensions in a section: the storey's level to its bottom (its sill height) and to its top (its head). */
export interface VDim { key: 'v:sill' | 'v:head'; from: number; to: number; value: number }
export function verticalDims(r: OpeningRefs): VDim[] {
  const o = r.opening, level = r.levels?.storey;
  if (!o || level == null || o.bottom == null || o.top == null) return [];
  return [
    { key: 'v:sill', from: level, to: o.bottom, value: o.bottom - level },
    { key: 'v:head', from: level, to: o.top, value: o.top - level },
  ];
}

/** The sill architecture.moveOpening takes for a height dimension to read `value` (m): the opening moved up or down by the difference. */
export const sillFor = (r: OpeningRefs, d: VDim, value: number): number => (r.opening?.sill ?? 0) + (value - d.value);
