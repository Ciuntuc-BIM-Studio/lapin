/**
 * A straight wall's temporary dimensions in plan (decision 693), as a door's (decision 511) and a grid axis's are: across it, from
 * its reference (the line it keeps, else its axis) to the nearest parallel wall or grid axis on each side that runs beside it;
 * along it, from each free end to the first wall its line runs into beyond that end. The walls come from the engine
 * (`architecture.wallLines`: each straight wall's axis, faces, core faces and joins); everything else here. Pure.
 *
 * - The reference's end cycles near face → far face → core faces → axis (a grid axis has only itself); the wall's own end
 *   cycles its reference → its face towards that side → its far face.
 * - A value typed moves the wall square to itself (across) or the end along it, the reference staying (`commitAcross`,
 *   `commitEnd`: the wall's new axis).
 */

import type { Vec2 } from '../../platform/module';

/** A wall as the engine gives it: its axis, its faces and core faces (left of the axis), what is joined at its ends and along it. */
export interface WallLineInfo {
  id: string; name: string; a: Vec2; b: Vec2;
  faces: [number, number]; core?: [number, number];
  joins: { start: string[]; end: string[]; path: string[] };
}

/** A grid axis: `id` the grid's id and the axis's tag (`grid#B`). */
export interface AxisLine { id: string; name: string; a: Vec2; b: Vec2 }

/** The wall measured: its axis (or the line being drawn), its reference `ref` metres left of it, its faces, its joins. */
export interface Host { id?: string; a: Vec2; b: Vec2; ref: number; faces: [number, number]; joins: WallLineInfo['joins'] }

/** The user's choices for one dimension: which of the reference's places (`opt`), which of the wall's own (`own`). */
export interface WallDimPick { opt?: number; own?: number }

/**
 * One dimension: `a` the reference's place, `b` the wall's own (across: metres left of the host's axis; along: metres along it
 * from its start), `value` |b − a|; across ones stand `station` metres along the host.
 */
export interface WallDim {
  key: string; kind: 'across' | 'end';
  target: { id: string; name: string; kind: 'wall' | 'axis' };
  options: { what: string; at: number }[]; opt: number; own: number; ownWhat: string;
  a: number; b: number; value: number; station: number;
}

const sub = (p: Vec2, q: Vec2): Vec2 => ({ x: p.x - q.x, y: p.y - q.y });
const dot = (p: Vec2, q: Vec2) => p.x * q.x + p.y * q.y;
const cross = (p: Vec2, q: Vec2) => p.x * q.y - p.y * q.x;
const unit = (p: Vec2): Vec2 => { const l = Math.hypot(p.x, p.y) || 1; return { x: p.x / l, y: p.y / l }; };
/** Lines this near parallel (the sine of the angle between them) are parallel. */
const PARALLEL = 0.02;
/** How far (m) beyond an end a wall may be and still be the one it runs toward. */
const REACH = 50;

/** A wall selected as the host, its reference `ref` metres left of its axis (0: the axis). */
export const hostOf = (w: WallLineInfo, ref = 0): Host => ({ id: w.id, a: w.a, b: w.b, ref, faces: w.faces, joins: w.joins });

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

/** The wall's places, ordered from the one nearest `from` outwards: near face, far face, core faces, axis. */
function places(at: (y: number) => number, w: WallLineInfo | null, from: number): { what: string; at: number }[] {
  if (!w) return [{ what: 'axis', at: at(0) }];
  const byNear = (p: number[]) => [...p].sort((x, y) => Math.abs(x - from) - Math.abs(y - from));
  const [nf, ff] = byNear(w.faces.map(at));
  const out = [{ what: 'near face', at: nf }, { what: 'far face', at: ff }];
  if (w.core) { const [nc, fc] = byNear(w.core.map(at)); out.push({ what: 'near core face', at: nc }, { what: 'far core face', at: fc }); }
  out.push({ what: 'axis', at: at(0) });
  return out;
}

const pickOf = (n: number, i: number | undefined) => (((i ?? 0) % n) + n) % n;

/** The host's dimensions: across to each side's nearest parallel neighbour, along from each free end. */
export function wallDims(h: Host, walls: WallLineInfo[], axes: AxisLine[], picks: Record<string, WallDimPick>): WallDim[] {
  const f = frameOf(h);
  if (!(f.length > 1e-3)) return [];
  const out: WallDim[] = [];
  const along = (p: Vec2) => dot(sub(p, f.o), f.d);
  const across = (p: Vec2) => dot(sub(p, f.o), f.n);

  // Across: the parallel walls and axes running beside the host (their stretch along it overlapping its own).
  type Side = { line: WallLineInfo | AxisLine; wall: WallLineInfo | null; y: number; at: (yw: number) => number; lo: number; hi: number };
  const sides: Record<'left' | 'right', Side | null> = { left: null, right: null };
  const lines: { line: WallLineInfo | AxisLine; wall: WallLineInfo | null }[] = [
    ...walls.filter((w) => w.id !== h.id).map((w) => ({ line: w, wall: w })), ...axes.map((x) => ({ line: x, wall: null }))];
  for (const { line, wall } of lines) {
    const dw = unit(sub(line.b, line.a));
    if (Math.abs(cross(f.d, dw)) > PARALLEL) continue;
    const y = across(line.a);
    // In line with the host (or an axis on its reference): nothing to measure across.
    if (Math.abs(y) < 1e-3 || Math.abs(y - h.ref) < 1e-3) continue;
    const s0 = along(line.a), s1 = along(line.b);
    const lo = Math.max(0, Math.min(s0, s1)), hi = Math.min(f.length, Math.max(s0, s1));
    if (!(hi - lo > 1e-3)) continue;
    // Its left is the host's left or right: a place yw left of its axis is y ± yw left of the host's.
    const sgn = dot({ x: -dw.y, y: dw.x }, f.n) >= 0 ? 1 : -1;
    const at = (yw: number) => y + sgn * yw;
    const near = wall ? Math.min(...wall.faces.map((v) => Math.abs(at(v) - h.ref))) : Math.abs(y - h.ref);
    if (wall && Math.min(...wall.faces.map(at)) < h.ref && Math.max(...wall.faces.map(at)) > h.ref) continue; // across the host: not beside it
    const side = y > h.ref ? 'left' : 'right';
    const cur = sides[side];
    const curNear = cur ? (cur.wall ? Math.min(...cur.wall.faces.map((v) => Math.abs(cur.at(v) - h.ref))) : Math.abs(cur.y - h.ref)) : Infinity;
    if (near < curNear - 1e-9) sides[side] = { line, wall, y, at, lo, hi };
  }
  for (const side of ['left', 'right'] as const) {
    const s = sides[side];
    if (!s) continue;
    const key = `x:${side}`;
    const options = places(s.at, s.wall, h.ref);
    const opt = pickOf(options.length, picks[key]?.opt);
    const [lf, rf] = [h.faces[1], h.faces[0]];
    const ownOpts = [{ what: 'reference', at: h.ref }, { what: 'near face', at: side === 'left' ? lf : rf }, { what: 'far face', at: side === 'left' ? rf : lf }];
    const own = pickOf(ownOpts.length, picks[key]?.own);
    const a = options[opt].at, b = ownOpts[own].at;
    out.push({
      key, kind: 'across', target: { id: s.line.id, name: s.line.name, kind: s.wall ? 'wall' : 'axis' },
      options, opt, own, ownWhat: ownOpts[own].what, a, b, value: Math.abs(b - a), station: (s.lo + s.hi) / 2,
    });
  }

  // Along: from each end not joined to a wall, the first wall across the host's line beyond it.
  for (const end of ['start', 'end'] as const) {
    if (h.joins[end].length) continue;
    const sE = end === 'start' ? 0 : f.length, out1 = end === 'start' ? -1 : 1;
    let best: { w: WallLineInfo; at: (yw: number) => number; near: number } | null = null;
    for (const w of walls) {
      if (w.id === h.id || h.joins[end].includes(w.id)) continue;
      const dw = unit(sub(w.b, w.a)), c = cross(f.d, dw);
      if (Math.abs(c) <= PARALLEL) continue;
      const nw = { x: -dw.y, y: dw.x };
      // Where the host's line meets the line yw left of w's axis, metres along the host.
      const at = (yw: number) => { const p0 = { x: w.a.x + nw.x * yw, y: w.a.y + nw.y * yw }; return cross(sub(p0, f.o), dw) / c; };
      // The host's line must reach the wall itself, not its line's extension.
      const sAxis = at(0), hit = { x: f.o.x + f.d.x * sAxis, y: f.o.y + f.d.y * sAxis };
      const u = dot(sub(hit, w.a), dw), len = Math.hypot(w.b.x - w.a.x, w.b.y - w.a.y), half = Math.max(...w.faces.map(Math.abs));
      if (u < -half - 1e-3 || u > len + half + 1e-3) continue;
      const ahead = w.faces.map((v) => out1 * (at(v) - sE));
      if (Math.min(...ahead) < 1e-3 || Math.min(...ahead) > REACH) continue;
      const near = Math.min(...ahead);
      if (!best || near < best.near) best = { w, at, near };
    }
    if (!best) continue;
    const key = `e:${end}`;
    const options = places(best.at, best.w, sE);
    const opt = pickOf(options.length, picks[key]?.opt);
    out.push({
      key, kind: 'end', target: { id: best.w.id, name: best.w.name, kind: 'wall' }, options, opt, own: 0, ownWhat: 'end',
      a: options[opt].at, b: sE, value: Math.abs(sE - options[opt].at), station: sE,
    });
  }
  return out;
}

/** The host's axis moved square to itself so that the dimension reads `value`; null when it already does, or cannot. */
export function commitAcross(h: Host, d: WallDim, value: number): { a: Vec2; b: Vec2 } | null {
  if (d.kind !== 'across' || !(value >= 0)) return null;
  const sgn = d.b - d.a >= 0 ? 1 : -1;
  const by = d.a + sgn * value - d.b;
  if (Math.abs(by) < 1e-9) return null;
  const { n } = frameOf(h);
  return { a: { x: h.a.x + n.x * by, y: h.a.y + n.y * by }, b: { x: h.b.x + n.x * by, y: h.b.y + n.y * by } };
}

/** The host's end moved along it so that the dimension reads `value`; null when it already does or the wall would have no length. */
export function commitEnd(h: Host, d: WallDim, value: number): { a: Vec2; b: Vec2 } | null {
  if (d.kind !== 'end' || !(value >= 0)) return null;
  const f = frameOf(h);
  const sgn = d.b - d.a >= 0 ? 1 : -1;
  const s = d.a + sgn * value;
  if (Math.abs(s - d.b) < 1e-9) return null;
  const start = d.key === 'e:start' ? s : 0, end = d.key === 'e:end' ? s : f.length;
  if (!(end - start > 1e-3)) return null;
  return { a: framePoint({ ...h, ref: 0 }, 0, start), b: framePoint({ ...h, ref: 0 }, 0, end) };
}
