/**
 * Temporary dimensions measured between straight lines in plan (decisions 693 and 733): the walls', columns' and beams' lines
 * the engine gives (`view.dimLines`: each element's axis, its faces, its core faces, a wall's kept reference line, its joins)
 * and the grid's axes. A host (the element selected, or the one being drawn or placed) measures across to the nearest parallel
 * line on each side that runs beside it and, when it has free ends, along to the first element its line runs into beyond each.
 * Pure: the walls', columns' and beams' overlays (WallTempDims, ColumnBeamDims) draw and commit what this works out.
 *
 * - Where each end of a dimension stands follows the user's dimension reference (`DimRef`, Annotate → Temporary dimensions…,
 *   Preferences → Grid and snaps, the tools' bars, the dot's menu): core faces (core face to core face), faces (finish), the
 *   centre of the core (decision 738), axis (the centre or insertion axis), or
 *   reference line (from the host's reference line, a wall's kept line else its axis, to the other's near face: decision 693's
 *   way, the default).
 * - A click on an end's dot cycles that dimension's end only: the other's near face → far face → core faces → centre of core
 *   (when off the axis) → axis; the host's own reference → near face → far face → core faces → centre of core → axis.
 * - A value typed moves the host square to its line (across) or its end along it (`commitAcross`, `commitEnd`: the new axis).
 * - A wall's length (forum topic 240, `length`): along each face (or core face, or its axis, as the reference says), from the
 *   face of the wall joined at each end that the face really runs to (a T: its near face; a corner: the near face inside, the
 *   far face outside), else from its free end. Each witness end cycles on its own (near face, far face, core faces, centre of
 *   core, axis); a value typed moves the free end, else the wall's end (`commitLength`).
 * - Across, a parallel wall beside no part of the host still counts when it turns the corner of a wall the host is joined to:
 *   the offsets from the interior and exterior corners (forum topic 240).
 */

import type { Vec2 } from '../platform/module';

/**
 * Where temporary dimensions measure from and to (decision 733; decision 738: Revit's Temporary Dimension Properties): the
 * centrelines (an element's axis, a column's centre), the faces (finish), the centre of the core (the middle of its core
 * layers, off the axis when the core is not centred on it), the core's faces (the default), or Lapin's reference line.
 */
export type DimRef = 'reference' | 'core' | 'coreCentre' | 'faces' | 'axis';
/** In Revit's order, Lapin's reference line last. */
export const DIM_REFS: readonly DimRef[] = ['axis', 'faces', 'coreCentre', 'core', 'reference'];
export const DIM_REF_LABELS: Record<DimRef, string> = {
  axis: 'Centrelines',
  faces: 'Faces',
  coreCentre: 'Centre of core',
  core: 'Faces of core',
  reference: 'Reference line',
};

/**
 * Where a door's, a window's or an opening's temporary dimensions measure from (decision 738, Revit's "Doors and Windows"):
 * its edges (Openings, the default: decision 511's way) or its centre (Centerlines), and so another opening's centre too.
 */
export type OpeningDimRef = 'openings' | 'centre';
export const OPENING_DIM_REFS: readonly OpeningDimRef[] = ['centre', 'openings'];
export const OPENING_DIM_REF_LABELS: Record<OpeningDimRef, string> = { centre: 'Centrelines', openings: 'Openings' };

export interface Joins { start: string[]; end: string[]; path: string[] }

/** A line as the engine gives it: its axis, its faces and core faces (left of the axis), a wall's reference line, its joins. */
export interface DimLineInfo {
  id: string; name: string; kind?: string; part?: string; a: Vec2; b: Vec2;
  faces: [number, number]; core?: [number, number]; reference?: number; joins?: Joins;
  /** The middle of its core, left of its axis (decision 738; the middle of `core`, else of `faces`, when not given). */
  coreCentre?: number;
}

/** A grid axis: `id` the grid's id and the axis's tag (`grid#B`). */
export interface AxisLine { id: string; name: string; a: Vec2; b: Vec2 }

/** The element measured: its axis (or the line being drawn), its reference `ref` metres left of it, its faces, core faces and joins. */
export interface Host { id?: string; a: Vec2; b: Vec2; ref: number; faces: [number, number]; core?: [number, number]; joins?: Joins; coreCentre?: number }

/** The user's choices for one dimension: which of the other's places (`opt`), which of the host's own (`own`). */
export interface DimPick { opt?: number; own?: number }

/**
 * One dimension: `a` the other's place, `b` the host's own (across: metres left of the host's axis; along: metres along it
 * from its start), `value` |b − a|; across ones stand `station` metres along the host.
 */
export interface LineDim {
  key: string; kind: 'across' | 'end' | 'length';
  /** A length's side (1: the host's left, −1: its right) and the line it runs along, metres left of the host's axis. */
  side?: 1 | -1; y?: number;
  target: { id: string; name: string; kind: string };
  options: { what: string; at: number }[]; opt: number;
  ownOptions: { what: string; at: number }[]; own: number; ownWhat: string;
  a: number; b: number; value: number; station: number;
}

const sub = (p: Vec2, q: Vec2): Vec2 => ({ x: p.x - q.x, y: p.y - q.y });
const dot = (p: Vec2, q: Vec2) => p.x * q.x + p.y * q.y;
const cross = (p: Vec2, q: Vec2) => p.x * q.y - p.y * q.x;
const unit = (p: Vec2): Vec2 => { const l = Math.hypot(p.x, p.y) || 1; return { x: p.x / l, y: p.y / l }; };
/** Lines this near parallel (the sine of the angle between them) are parallel. */
const PARALLEL = 0.02;
/** How far (m) beyond an end an element may be and still be the one it runs toward. */
const REACH = 50;

/** A core distinct from the faces, else none (a one-layer wall, a column without finish). */
export const distinctCore = (faces: [number, number], core?: [number, number]): [number, number] | undefined =>
  core && (Math.abs(core[0] - faces[0]) > 1e-6 || Math.abs(core[1] - faces[1]) > 1e-6) ? core : undefined;

/** A line taken as the host, its reference `ref` metres left of its axis (else the reference line it keeps, else its axis). */
export const hostOf = (w: DimLineInfo, ref?: number): Host => ({
  id: w.id, a: w.a, b: w.b, ref: ref ?? w.reference ?? 0, faces: w.faces, core: w.core, joins: w.joins ?? { start: [], end: [], path: [] }, coreCentre: w.coreCentre,
});

/** The middle of a line's core, left of its axis: as the engine gives it, else the middle of its core (or its faces). */
export const coreCentreOf = (w: { faces: [number, number]; core?: [number, number]; coreCentre?: number }): number =>
  w.coreCentre ?? ((w.core ?? w.faces)[0] + (w.core ?? w.faces)[1]) / 2;
/** A centre of core off the axis by more than this (m) is a place of its own; nearer, it is the axis. */
const OFF_AXIS = 1e-6;

/** The host's frame: its start, direction, left normal and length. */
export function frameOf(h: Host): { o: Vec2; d: Vec2; n: Vec2; length: number } {
  const v = sub(h.b, h.a), d = unit(v);
  return { o: h.a, d, n: { x: -d.y, y: d.x }, length: Math.hypot(v.x, v.y) };
}

/** A place in the host's frame: `t` left of its axis, `s` along it from its start. */
export const framePoint = (h: Host, t: number, s: number): Vec2 => {
  const f = frameOf(h);
  return { x: f.o.x + f.n.x * t + f.d.x * s, y: f.o.y + f.n.y * t + f.d.y * s };
};

/** The other's places, ordered from the one nearest `from` outwards: near face, far face, core faces, centre of core, axis. */
function places(at: (y: number) => number, w: DimLineInfo | null, from: number): { what: string; at: number }[] {
  if (!w) return [{ what: 'axis', at: at(0) }];
  const byNear = (p: number[]) => [...p].sort((x, y) => Math.abs(x - from) - Math.abs(y - from));
  const [nf, ff] = byNear(w.faces.map(at));
  const out = [{ what: 'near face', at: nf }, { what: 'far face', at: ff }];
  const core = distinctCore(w.faces, w.core);
  if (core) { const [nc, fc] = byNear(core.map(at)); out.push({ what: 'near core face', at: nc }, { what: 'far core face', at: fc }); }
  const cc = coreCentreOf(w);
  if (Math.abs(cc) > OFF_AXIS) out.push({ what: 'core centre', at: at(cc) });
  out.push({ what: 'axis', at: at(0) });
  return out;
}

/** The host's own places on the side toward `side` (1: its left, −1: its right): its reference, near face, far face, core faces, axis. */
function ownPlaces(h: Host, side: 1 | -1): { what: string; at: number }[] {
  const near = (p: [number, number]) => (side > 0 ? p[1] : p[0]), far = (p: [number, number]) => (side > 0 ? p[0] : p[1]);
  const out = [{ what: Math.abs(h.ref) > 1e-9 ? 'reference' : 'axis', at: h.ref },
    { what: 'near face', at: near(h.faces) }, { what: 'far face', at: far(h.faces) }];
  const core = distinctCore(h.faces, h.core);
  if (core) out.push({ what: 'near core face', at: near(core) }, { what: 'far core face', at: far(core) });
  const cc = coreCentreOf(h);
  if (Math.abs(cc) > OFF_AXIS && Math.abs(cc - h.ref) > OFF_AXIS) out.push({ what: 'core centre', at: cc });
  if (Math.abs(h.ref) > 1e-9) out.push({ what: 'axis', at: 0 });
  return out;
}

/**
 * Which of the places the reference chooses: a core face where there is one (else the face), the near face, the centre of the
 * core (else the axis: a core centred on it), the axis.
 */
function defaultIndex(opts: { what: string }[], mode: DimRef): number {
  const find = (w: string) => opts.findIndex((o) => o.what === w);
  if (mode === 'reference') return 0; // the host's reference line; the other's near face
  if (mode === 'coreCentre') { const i = find('core centre'); if (i >= 0) return i; }
  if (mode === 'axis' || mode === 'coreCentre') { const i = find('axis'); return i >= 0 ? i : 0; }
  if (mode === 'core') { const i = find('near core face'); if (i >= 0) return i; }
  const i = find('near face');
  return i >= 0 ? i : 0;
}

/**
 * What a dimension's ends come to as the reference for every dimension (the dot's "make this the default"): the other end's
 * place (a core face, a face, the axis); its near face measured from the host's own reference line is the reference line's way.
 */
export function refOfPlace(what: string, ownWhat?: string): DimRef {
  if (what === 'near face' && ownWhat === 'reference') return 'reference';
  return what === 'axis' ? 'axis' : what === 'core centre' ? 'coreCentre' : what.includes('core') ? 'core' : 'faces';
}

const pickOf = (n: number, i: number) => ((i % n) + n) % n;

export interface DimOptions {
  /** Where the ends stand unless a dot was clicked (decision 733); `reference` by default, as decision 693 measured. */
  mode?: DimRef;
  /** Along dimensions from the host's free ends (walls, beams; not a column). */
  along?: boolean;
  /** Put before each dimension's key (a column's `x:`/`y:` lines). */
  prefix?: string;
  /** The host's length along its faces, from the walls joined at its ends (walls: forum topic 240). */
  length?: boolean;
}

/** The host's dimensions: across to each side's nearest parallel neighbour, along from each free end. */
export function lineDims(h: Host, lines: DimLineInfo[], axes: AxisLine[], picks: Record<string, DimPick>, o: DimOptions = {}): LineDim[] {
  const mode = o.mode ?? 'reference', prefix = o.prefix ?? '';
  const f = frameOf(h);
  if (!(f.length > 1e-3)) return [];
  const out: LineDim[] = [];
  const along = (p: Vec2) => dot(sub(p, f.o), f.d);
  const across = (p: Vec2) => dot(sub(p, f.o), f.n);

  // Across: the parallel lines and axes running beside the host (their stretch along it overlapping its own).
  type Side = { line: DimLineInfo | AxisLine; el: DimLineInfo | null; y: number; at: (yw: number) => number; lo: number; hi: number };
  const sides: Record<'left' | 'right', Side | null> = { left: null, right: null };
  const nearOf = (s: { el: DimLineInfo | null; at: (yw: number) => number; y: number }) =>
    s.el ? Math.min(...s.el.faces.map((v) => Math.abs(s.at(v) - h.ref))) : Math.abs(s.y - h.ref);
  const all: { line: DimLineInfo | AxisLine; el: DimLineInfo | null }[] = [
    ...lines.filter((w) => w.id !== h.id).map((w) => ({ line: w, el: w })), ...axes.map((x) => ({ line: x, el: null }))];
  // The walls joined at the host's ends, and a parallel one beyond the corner of one of them (forum topic 240): taken when
  // nothing beside the host faces it on that side.
  const atEnds = new Set([...(h.joins?.start ?? []), ...(h.joins?.end ?? [])]);
  const byId = new Map(lines.map((w) => [w.id, w]));
  const turnsCorner = (el: DimLineInfo | null) => !!el && atEnds.size > 0 && !atEnds.has(el.id) && (
    [...(el.joins?.start ?? []), ...(el.joins?.end ?? [])].some((id) => atEnds.has(id))
    || [...atEnds].some((id) => { const j = byId.get(id)?.joins; return !!j && (j.start.includes(el.id) || j.end.includes(el.id)); }));
  const beyond: Record<'left' | 'right', Side | null> = { left: null, right: null };
  for (const { line, el } of all) {
    const dw = unit(sub(line.b, line.a));
    if (Math.abs(cross(f.d, dw)) > PARALLEL) continue;
    const y = across(line.a);
    // In line with the host (or an axis on its reference): nothing to measure across.
    if (Math.abs(y) < 1e-3 || Math.abs(y - h.ref) < 1e-3) continue;
    const s0 = along(line.a), s1 = along(line.b);
    const lo = Math.max(0, Math.min(s0, s1)), hi = Math.min(f.length, Math.max(s0, s1));
    const apart = !(hi - lo > 1e-3);
    if (apart && !turnsCorner(el)) continue;
    // Its left is the host's left or right: a place yw left of its axis is y ± yw left of the host's.
    const sgn = dot({ x: -dw.y, y: dw.x }, f.n) >= 0 ? 1 : -1;
    const at = (yw: number) => y + sgn * yw;
    if (el && Math.min(...el.faces.map(at)) < h.ref && Math.max(...el.faces.map(at)) > h.ref) continue; // across the host: not beside it
    const side = y > h.ref ? 'left' : 'right';
    // Beyond a corner: it stands at the host's end nearer it.
    const station = Math.min(Math.max((s0 + s1) / 2, 0), f.length);
    const pool = apart ? beyond : sides;
    const cur = pool[side];
    const cand = apart ? { line, el, y, at, lo: station, hi: station } : { line, el, y, at, lo, hi };
    if (nearOf(cand) < (cur ? nearOf(cur) : Infinity) - 1e-9) pool[side] = cand;
  }
  for (const side of ['left', 'right'] as const) {
    const s = sides[side] ?? beyond[side];
    if (!s) continue;
    const key = `${prefix}x:${side}`;
    const options = places(s.at, s.el, h.ref);
    const opt = pickOf(options.length, picks[key]?.opt ?? defaultIndex(options, mode));
    const ownOptions = ownPlaces(h, side === 'left' ? 1 : -1);
    const own = pickOf(ownOptions.length, picks[key]?.own ?? defaultIndex(ownOptions, mode));
    const a = options[opt].at, b = ownOptions[own].at;
    out.push({
      key, kind: 'across', target: { id: s.line.id, name: s.line.name, kind: s.el ? s.el.kind ?? 'wall' : 'axis' },
      options, opt, ownOptions, own, ownWhat: ownOptions[own].what, a, b, value: Math.abs(b - a), station: (s.lo + s.hi) / 2,
    });
  }

  if (o.length) out.push(...lengthDims(h, lines, picks, mode, prefix));

  // Along: from each end not joined to anything, the first element across the host's line beyond it.
  if (o.along === false || !h.joins) return out;
  for (const end of ['start', 'end'] as const) {
    if (h.joins[end].length) continue;
    const sE = end === 'start' ? 0 : f.length, out1 = end === 'start' ? -1 : 1;
    let best: { w: DimLineInfo; at: (yw: number) => number; near: number } | null = null;
    for (const w of lines) {
      if (w.id === h.id || h.joins[end].includes(w.id)) continue;
      const dw = unit(sub(w.b, w.a)), c = cross(f.d, dw);
      if (Math.abs(c) <= PARALLEL) continue;
      const nw = { x: -dw.y, y: dw.x };
      // Where the host's line meets the line yw left of w's axis, metres along the host.
      const at = (yw: number) => { const p0 = { x: w.a.x + nw.x * yw, y: w.a.y + nw.y * yw }; return cross(sub(p0, f.o), dw) / c; };
      // The host's line must reach the element itself, not its line's extension.
      const sAxis = at(0), hit = { x: f.o.x + f.d.x * sAxis, y: f.o.y + f.d.y * sAxis };
      const u = dot(sub(hit, w.a), dw), len = Math.hypot(w.b.x - w.a.x, w.b.y - w.a.y), half = Math.max(...w.faces.map(Math.abs));
      if (u < -half - 1e-3 || u > len + half + 1e-3) continue;
      const ahead = w.faces.map((v) => out1 * (at(v) - sE));
      if (Math.min(...ahead) < 1e-3 || Math.min(...ahead) > REACH) continue;
      const near = Math.min(...ahead);
      if (!best || near < best.near) best = { w, at, near };
    }
    if (!best) continue;
    const key = `${prefix}e:${end}`;
    const options = places(best.at, best.w, sE);
    // The host's end is its end whatever the reference: only the other's place follows it ('reference': its near face).
    const opt = pickOf(options.length, picks[key]?.opt ?? defaultIndex(options, mode));
    out.push({
      key, kind: 'end', target: { id: best.w.id, name: best.w.name, kind: best.w.kind ?? 'wall' }, options, opt,
      ownOptions: [{ what: 'end', at: sE }], own: 0, ownWhat: 'end',
      a: options[opt].at, b: sE, value: Math.abs(sE - options[opt].at), station: sE,
    });
  }
  return out;
}

/** How far (m) the host moves square to its line so that the dimension reads `value`; null when it already does, or cannot. */
export function acrossShift(d: LineDim, value: number): number | null {
  if (d.kind !== 'across' || !(value >= 0)) return null;
  const sgn = d.b - d.a >= 0 ? 1 : -1;
  const by = d.a + sgn * value - d.b;
  return Math.abs(by) < 1e-9 ? null : by;
}

/** The host's axis moved square to itself so that the dimension reads `value`; null when it already does, or cannot. */
export function commitAcross(h: Host, d: LineDim, value: number): { a: Vec2; b: Vec2 } | null {
  const by = acrossShift(d, value);
  if (by == null) return null;
  const { n } = frameOf(h);
  return { a: { x: h.a.x + n.x * by, y: h.a.y + n.y * by }, b: { x: h.b.x + n.x * by, y: h.b.y + n.y * by } };
}

/** The host's end moved along it so that the dimension reads `value`; null when it already does or it would have no length. */
export function commitEnd(h: Host, d: LineDim, value: number): { a: Vec2; b: Vec2 } | null {
  if (d.kind !== 'end' || !(value >= 0)) return null;
  const f = frameOf(h);
  const sgn = d.b - d.a >= 0 ? 1 : -1;
  const s = d.a + sgn * value;
  if (Math.abs(s - d.b) < 1e-9) return null;
  const start = d.key.endsWith('e:start') ? s : 0, end = d.key.endsWith('e:end') ? s : f.length;
  if (!(end - start > 1e-3)) return null;
  return { a: framePoint({ ...h, ref: 0 }, 0, start), b: framePoint({ ...h, ref: 0 }, 0, end) };
}

/** The line a length runs along on a side, metres left of the host's axis: its face, core face, centre of core or axis. */
function lengthLine(h: Host, side: 1 | -1, mode: DimRef): number {
  if (mode === 'axis') return 0;
  if (mode === 'coreCentre') return coreCentreOf(h);
  const p = mode === 'core' ? h.core ?? h.faces : h.faces;
  return side > 0 ? Math.max(...p) : Math.min(...p);
}

/**
 * The host's length along each side (forum topic 240): from the face of the wall joined at each end that the side's line runs
 * to, else from the free end. One along the axis (or the centre of core) when the reference is that; two, left and right, else.
 */
function lengthDims(h: Host, lines: DimLineInfo[], picks: Record<string, DimPick>, mode: DimRef, prefix: string): LineDim[] {
  const f = frameOf(h);
  const byId = new Map(lines.map((w) => [w.id, w]));
  const out: LineDim[] = [];
  const sides: (1 | -1)[] = mode === 'axis' || mode === 'coreCentre' ? [1] : [1, -1];
  for (const side of sides) {
    const y = lengthLine(h, side, mode);
    const o = { x: f.o.x + f.n.x * y, y: f.o.y + f.n.y * y };
    const endPlaces = (end: 'start' | 'end'): { what: string; at: number }[] & { pick?: number } => {
      const sE = end === 'start' ? 0 : f.length, inward = end === 'start' ? 1 : -1;
      const w = (h.joins?.[end] ?? []).map((id) => byId.get(id)).find((x) => !!x && Math.abs(cross(f.d, unit(sub(x.b, x.a)))) > PARALLEL);
      if (!w) return Object.assign([{ what: 'end', at: sE }], { pick: 0 });
      const dw = unit(sub(w.b, w.a)), c = cross(f.d, dw), nw = { x: -dw.y, y: dw.x };
      // Where the side's line meets the line yw left of w's axis, metres along the host.
      const at = (yw: number) => cross(sub({ x: w.a.x + nw.x * yw, y: w.a.y + nw.y * yw }, o), dw) / c;
      // Near: the face toward the host's body.
      const byInward = (p: number[]) => [...p].map(at).sort((u, v) => inward * (v - u));
      const [nf, ff] = byInward(w.faces);
      const opts = [{ what: 'near face', at: nf }, { what: 'far face', at: ff }];
      const core = distinctCore(w.faces, w.core);
      if (core) { const [nc, fc] = byInward(core); opts.push({ what: 'near core face', at: nc }, { what: 'far core face', at: fc }); }
      const cc = coreCentreOf(w);
      if (Math.abs(cc) > OFF_AXIS) opts.push({ what: 'core centre', at: at(cc) });
      opts.push({ what: 'axis', at: at(0) });
      // A corner (the two walls end there): the side the other turns toward runs to its near face, the outer side to its far face.
      // A T (the host ends on its side): both to its near face.
      const corner = (w.joins?.start ?? []).includes(h.id ?? '\u0000') ? 1 : (w.joins?.end ?? []).includes(h.id ?? '\u0000') ? -1 : 0;
      const inner = corner === 0 || dot({ x: dw.x * corner, y: dw.y * corner }, f.n) * side > 0;
      const find = (what: string) => opts.findIndex((x) => x.what === what);
      const face = (kind: 'face' | 'core face') => find(`${inner ? 'near' : 'far'} ${kind}`);
      const pick = mode === 'axis' ? find('axis')
        : mode === 'coreCentre' ? (find('core centre') >= 0 ? find('core centre') : find('axis'))
          : mode === 'core' && face('core face') >= 0 ? face('core face') : face('face');
      return Object.assign(opts, { pick: Math.max(0, pick) });
    };
    const key = `${prefix}l:${sides.length === 1 ? 'axis' : side > 0 ? 'left' : 'right'}`;
    const options = endPlaces('start'), ownOptions = endPlaces('end');
    const opt = pickOf(options.length, picks[key]?.opt ?? options.pick ?? 0);
    const own = pickOf(ownOptions.length, picks[key]?.own ?? ownOptions.pick ?? 0);
    const a = options[opt].at, b = ownOptions[own].at;
    if (!(Math.abs(b - a) > 1e-3)) continue;
    out.push({
      key, kind: 'length', side, y, target: { id: h.id ?? '', name: 'length', kind: 'length' },
      options: [...options], opt, ownOptions: [...ownOptions], own, ownWhat: ownOptions[own].what, a, b, value: Math.abs(b - a), station: 0,
    });
  }
  return out;
}

/**
 * The host's axis with one end moved along it so that its length reads `value` (forum topic 240): its free end, else its end
 * (the start when only that is free); null when it already reads so or it would have no length.
 */
export function commitLength(h: Host, d: LineDim, value: number): { a: Vec2; b: Vec2 } | null {
  if (d.kind !== 'length' || !(value >= 0)) return null;
  const f = frameOf(h);
  const sgn = d.b - d.a >= 0 ? 1 : -1;
  const startMoves = !!h.joins && h.joins.start.length === 0 && h.joins.end.length > 0;
  let s0 = 0, s1 = f.length;
  if (startMoves) s0 = d.b - sgn * value - d.a;
  else s1 = f.length + (d.a + sgn * value - d.b);
  if (Math.abs(s0) < 1e-9 && Math.abs(s1 - f.length) < 1e-9) return null;
  if (!(s1 - s0 > 1e-3)) return null;
  return { a: framePoint({ ...h, ref: 0 }, 0, s0), b: framePoint({ ...h, ref: 0 }, 0, s1) };
}
