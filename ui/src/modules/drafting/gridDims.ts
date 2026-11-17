/**
 * A grid axis's temporary dimensions (forum #78), as a door's or a window's are (decision 511): with one axis selected in a
 * plan, the distances to the parallel axes either side of it, square to them; for an axis of a radial grid (axes through
 * one centre, none parallel to it), the angles to the axes either side of it round that centre. A value typed moves the
 * selected axis so the dimension reads it (`drafting.updateAxis`, one undo step), the neighbour staying put; with `beyond`,
 * the axes past the selected one (away from that neighbour) move with it, as the Properties panel's spacing does.
 */

import type { GripStep, Vec2 } from '../../platform/module';
import { parallels, type GridAxis } from '../../shell/gridAxes';

const dot = (a: Vec2, b: Vec2) => a.x * b.x + a.y * b.y;
const sub = (a: Vec2, b: Vec2): Vec2 => ({ x: a.x - b.x, y: a.y - b.y });
const dirOf = (x: GridAxis): Vec2 => { const l = Math.hypot(x.b.x - x.a.x, x.b.y - x.a.y) || 1; return { x: (x.b.x - x.a.x) / l, y: (x.b.y - x.a.y) / l }; };

/** A distance to a parallel neighbour: `a` its offset across the axes, `b` the selected axis's (metres along `n`). */
export interface SpacingDim { key: string; other: string; side: -1 | 1; a: number; b: number; value: number }

/** The parallel axes' dimensions: across them along `n`, standing at `station` metres along `d` (a point is n·t + d·s). */
export interface ParallelDims { kind: 'parallel'; tag: string; n: Vec2; d: Vec2; station: number; dims: SpacingDim[] }

/** An angle to a neighbour round `centre`: `a` its direction, `b` the selected axis's (radians, b − a the signed angle). */
export interface AngleDim { key: string; other: string; side: -1 | 1; a: number; b: number; value: number }

/** A radial grid's dimensions: arcs of `radius` metres round `centre`; `value` in degrees. */
export interface RadialDims { kind: 'radial'; tag: string; centre: Vec2; radius: number; dims: AngleDim[] }

export type AxisDims = ParallelDims | RadialDims;

/** An angle as typed: degrees, a ° allowed, a comma for the point. */
export const parseDegrees = (text: string): number | null => {
  const v = Number(text.replace('°', '').replace(',', '.').trim());
  return Number.isFinite(v) ? v : null;
};
export const formatDegrees = (v: number): string => `${Math.round(v * 100) / 100}°`;

/** A point of the parallel dimensions' frame. */
export const framePoint = (g: ParallelDims, t: number, s: number): Vec2 => ({ x: g.n.x * t + g.d.x * s, y: g.n.y * t + g.d.y * s });

/**
 * Where along the axis the dimensions stand: the point clicked (`at`, metres along the axis) kept within the shared stretch
 * `lo`..`hi`; the middle of that stretch with no click; the axis's own middle (`own`) when it shares none with its neighbours.
 */
export function stationAt(lo: number, hi: number, own: [number, number], at: number | null): number {
  if (!(hi > lo)) return (own[0] + own[1]) / 2;
  return at == null || !Number.isFinite(at) ? (lo + hi) / 2 : Math.min(hi, Math.max(lo, at));
}

/** The place an axis was last clicked in a plan (world metres), so its dimensions stand there; selection by other means has none. */
let clicked: { id: string; point: Vec2 } | null = null;
export const noteAxisClick = (id: string | null | undefined, point: Vec2): void => { clicked = id && id.includes('#') ? { id, point } : null; };
export const axisClickPoint = (id: string): Vec2 | null => (clicked?.id === id ? clicked.point : null);

/** The selected axis's temporary dimensions; null when it has no neighbour to measure to. */
export function axisDims(axes: GridAxis[], tag: string, near?: Vec2 | null): AxisDims | null {
  const axis = axes.find((x) => x.tag === tag);
  if (!axis) return null;
  const list = parallels(axes, axis).filter((p) => p.axis.tag === tag || Math.abs(p.offset - parallels([axis], axis)[0].offset) > 1e-6);
  if (list.length > 1) return parallelDims(axis, list, near);
  return radialDims(axes, axis);
}

function parallelDims(axis: GridAxis, list: { axis: GridAxis; offset: number }[], click?: Vec2 | null): ParallelDims | null {
  const i = list.findIndex((p) => p.axis.tag === axis.tag);
  const d = dirOf(axis);
  const n = { x: -d.y, y: d.x };
  // Offsets along n (the sign of parallels' own across may differ: read them again here).
  const off = (x: GridAxis) => dot(x.a, n);
  const b = off(axis);
  const near = [list[i - 1], list[i + 1]].filter((p): p is { axis: GridAxis; offset: number } => !!p);
  if (!near.length) return null;
  const range = (x: GridAxis): [number, number] => { const s = [dot(x.a, d), dot(x.b, d)]; return [Math.min(...s), Math.max(...s)]; };
  // Where the dimensions stand: the middle of the stretch the selected axis shares with its neighbours (else its own middle).
  let [lo, hi] = range(axis);
  for (const p of near) { const [l, h] = range(p.axis); lo = Math.max(lo, l); hi = Math.min(hi, h); }
  const station = stationAt(lo, hi, range(axis), click ? dot(click, d) : null);
  const dims = near.map((p): SpacingDim => {
    const a = off(p.axis);
    const side = a < b ? -1 : 1;
    return { key: `g:${p.axis.tag}`, other: p.axis.tag, side, a, b, value: Math.abs(b - a) };
  });
  return { kind: 'parallel', tag: axis.tag, n, d, station, dims };
}

/** Where two axes' lines cross; null when parallel. */
function crossing(p: GridAxis, q: GridAxis): Vec2 | null {
  const r = sub(p.b, p.a), s = sub(q.b, q.a);
  const den = r.x * s.y - r.y * s.x;
  if (Math.abs(den) < 1e-9 * Math.hypot(r.x, r.y) * Math.hypot(s.x, s.y)) return null;
  const t = ((q.a.x - p.a.x) * s.y - (q.a.y - p.a.y) * s.x) / den;
  return { x: p.a.x + r.x * t, y: p.a.y + r.y * t };
}

const distToLine = (p: Vec2, x: GridAxis) => { const d = dirOf(x); return Math.abs((p.x - x.a.x) * d.y - (p.y - x.a.y) * d.x); };
/** An axis's direction from the centre: towards its end further from it. */
const away = (x: GridAxis, c: Vec2): number => {
  const far = Math.hypot(x.a.x - c.x, x.a.y - c.y) > Math.hypot(x.b.x - c.x, x.b.y - c.y) ? x.a : x.b;
  return Math.atan2(far.y - c.y, far.x - c.x);
};
const farDist = (x: GridAxis, c: Vec2) => Math.max(Math.hypot(x.a.x - c.x, x.a.y - c.y), Math.hypot(x.b.x - c.x, x.b.y - c.y));
/** An angle brought into [0, 2π). */
const wrap = (r: number) => ((r % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI);

function radialDims(axes: GridAxis[], axis: GridAxis): RadialDims | null {
  const others = axes.filter((x) => x.tag !== axis.tag);
  // The centre: where the selected axis's line meets the most others' lines (1 mm), among the crossings with them.
  let best: { c: Vec2; on: GridAxis[] } | null = null;
  for (const o of others) {
    const c = crossing(axis, o);
    if (!c) continue;
    const on = others.filter((x) => distToLine(c, x) < 1e-3);
    if (!best || on.length > best.on.length) best = { c, on };
  }
  if (!best) return null;
  const { c, on } = best;
  // A radial grid's axes start at its centre or near it: the centre lies off (or at the end of) each, not across its middle.
  const radial = (x: GridAxis) => { const d = dirOf(x); const s = [dot(sub(x.a, c), d), dot(sub(x.b, c), d)]; return Math.min(...s) > -1e-3 || Math.max(...s) < 1e-3; };
  if (!radial(axis)) return null;
  const spokes = on.filter(radial);
  if (!spokes.length) return null;
  const th = away(axis, c);
  const rel = spokes.map((x) => ({ x, ccw: wrap(away(x, c) - th) })).filter((r) => r.ccw > 1e-6 && r.ccw < 2 * Math.PI - 1e-6);
  if (!rel.length) return null;
  const ccw = rel.reduce((m, r) => (r.ccw < m.ccw ? r : m));
  const cw = rel.reduce((m, r) => (r.ccw > m.ccw ? r : m));
  const radius = 0.6 * Math.min(farDist(axis, c), ...[ccw, cw].map((r) => farDist(r.x, c)));
  const dims: AngleDim[] = [];
  // The neighbour counter-clockwise: its direction is th + ccw, so the selected axis lies at a = th + ccw, b = th (b − a < 0).
  dims.push({ key: `r:${ccw.x.tag}`, other: ccw.x.tag, side: 1, a: th + ccw.ccw, b: th, value: (ccw.ccw * 180) / Math.PI });
  if (cw.x.tag !== ccw.x.tag) dims.push({ key: `r:${cw.x.tag}`, other: cw.x.tag, side: -1, a: th + cw.ccw - 2 * Math.PI, b: th, value: ((2 * Math.PI - cw.ccw) * 180) / Math.PI });
  return { kind: 'radial', tag: axis.tag, centre: c, radius, dims };
}

const moved = (gridId: string, x: GridAxis, f: (p: Vec2) => Vec2): GripStep => {
  const [a, b] = [f(x.a), f(x.b)];
  return { command: 'drafting.updateAxis', args: { gridId, tag: x.tag, x1: a.x, y1: a.y, x2: b.x, y2: b.y } };
};

/**
 * The moves that make a dimension read `value` (metres, or degrees for an angle): the selected axis, and with `beyond`
 * the axes past it away from the neighbour measured to, by the same; none when it already reads so or cannot.
 */
export function commitSteps(gridId: string, axes: GridAxis[], dims: AxisDims, key: string, value: number, beyond: boolean): GripStep[] {
  const axis = axes.find((x) => x.tag === dims.tag);
  if (!axis || !(value >= 0)) return [];
  if (dims.kind === 'parallel') {
    const dim = dims.dims.find((d) => d.key === key);
    if (!dim || !(value > 1e-6)) return [];
    const by = (dim.b - dim.a >= 0 ? 1 : -1) * value - (dim.b - dim.a);
    if (Math.abs(by) < 1e-9) return [];
    const off = (x: GridAxis) => dot(x.a, dims.n);
    const sgn = dim.b - dim.a >= 0 ? 1 : -1;
    const group = parallels(axes, axis).map((p) => p.axis).filter((x) => x.tag === axis.tag || (beyond && (off(x) - dim.b) * sgn > 1e-6));
    return group.map((x) => moved(gridId, x, (p) => ({ x: p.x + dims.n.x * by, y: p.y + dims.n.y * by })));
  }
  const dim = dims.dims.find((d) => d.key === key);
  if (!dim || !(value > 1e-6) || !(value < 360)) return [];
  const sgn = dim.b - dim.a >= 0 ? 1 : -1;
  const turn = dim.a + (sgn * value * Math.PI) / 180 - dim.b;
  if (Math.abs(turn) < 1e-9) return [];
  const c = dims.centre, cos = Math.cos(turn), sin = Math.sin(turn);
  const rot = (p: Vec2): Vec2 => ({ x: c.x + (p.x - c.x) * cos - (p.y - c.y) * sin, y: c.y + (p.x - c.x) * sin + (p.y - c.y) * cos });
  // Beyond: the spokes further round from the neighbour than the selected axis, the same way.
  const start = wrap(dim.a);
  const relOf = (x: GridAxis) => wrap(sgn * (away(x, c) - start));
  const mine = relOf(axis);
  // A fan ends at its widest gap between spokes: the axes beyond go round to it, not across it to the fan's other end.
  const spokes = axes.filter((x) => x.tag !== dim.other && distToLine(c, x) < 1e-3).map((x) => ({ x, rel: relOf(x) })).sort((p, q) => p.rel - q.rel);
  let end = 2 * Math.PI, widest = 0;
  spokes.forEach((p, i) => {
    const gap = (i + 1 < spokes.length ? spokes[i + 1].rel : 2 * Math.PI) - p.rel;
    if (gap > widest + 1e-9) { widest = gap; end = p.rel; }
  });
  if (end < mine) end = 2 * Math.PI;
  const group = [axis, ...(beyond ? spokes.filter((p) => p.x.tag !== axis.tag && p.rel > mine + 1e-6 && p.rel <= end + 1e-9).map((p) => p.x) : [])];
  return group.map((x) => moved(gridId, x, rot));
}
