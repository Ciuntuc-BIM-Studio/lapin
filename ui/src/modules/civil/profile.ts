/**
 * The profile editor (site grading T3, docs/plan-site-grading.md): the pure parts of the graph. A profile is a line's heights along
 * its stations; the engine (`civil.profile`, `civil.crossProfile`) gives it, the graph draws it with a vertical exaggeration, a
 * point dragged is previewed here and sent as one command on release. Nothing here reads the session or the DOM.
 */

import { readGrade } from './featureLines';

export interface ProfilePoint {
  station: number; z: number;
  /** The design gives this point its height (else it is on the ground). */
  designed: boolean; locked: boolean; curve: number;
  /** May slide along the station. */
  movable: boolean;
  /** The typed grade onwards, a fraction, or null. */
  grade: number | null;
}

export type Series = [number, number | null][];

/** The engine leaves `existing` and `design` out when there is no terrain under the line: both are then empty series, so every plot can draw and read them. */
export function withGround<T extends { existing?: Series; design?: Series }>(r: T): T & { existing: Series; design: Series } {
  return { ...r, existing: Array.isArray(r.existing) ? r.existing : [], design: Array.isArray(r.design) ? r.design : [] };
}

export interface ProfileData {
  kind: 'featureLine' | 'hardscape' | 'corridor';
  id: string; name: string; start: number; length: number; closed: boolean;
  /** The plan polyline with its stations: [x, y, station]. */
  path: [number, number, number][];
  line: [number, number][];
  points: ProfilePoint[];
  existing: Series; design: Series;
  terrainId?: string | null;
  section?: boolean; drape?: boolean; contour?: boolean;
  reach?: number | null; reachUsed?: number | null; change?: number | null;
  crossOf?: { id: string; station: number; width: number } | null;
  mode?: 'ground' | 'design';
}

export interface CrossPoint { offset: number; z: number; designed: boolean; locked: boolean; movable: boolean }

export interface CrossData {
  station: number; label: string; width: number;
  at: [number, number]; from: [number, number]; to: [number, number];
  z: number;
  existing: Series; design: Series;
  crossId?: string | null; points?: CrossPoint[]; line?: [number, number][]; reachUsed?: number | null;
}

// ---------------------------------------------------------------------------------------------------------------- the frame

/** A graph's mapping between data (station, height) and its pixels; heights are exaggerated by `ex` against the horizontal scale. */
export interface Frame {
  s0: number; s1: number; z0: number; z1: number;
  /** Pixels per metre along the station, and up. */
  k: number; kz: number;
  /** The pixel of (s0, z0): left and bottom of the plotted area. */
  ox: number; oy: number;
  width: number; height: number;
}

export interface FrameSpec { s0: number; s1: number; z0: number; z1: number; width: number; height: number; padL?: number; padR?: number; padT?: number; padB?: number; exaggeration: number }

/**
 * The frame that fits the stations and heights in the box, the heights exaggerated by `exaggeration`: the one scale (pixels per
 * metre) is the largest at which both fit, so 1× is true and 10× is ten times steeper than that.
 */
export function makeFrame(f: FrameSpec): Frame {
  const { padL = 52, padR = 16, padT = 14, padB = 30 } = f;
  const ex = Math.max(1, f.exaggeration);
  const span = Math.max(f.s1 - f.s0, 1e-6);
  const rise = Math.max(f.z1 - f.z0, 0.5);
  const innerW = Math.max(1, f.width - padL - padR), innerH = Math.max(1, f.height - padT - padB);
  const k = Math.min(innerW / span, innerH / (rise * ex));
  const kz = k * ex;
  // The graph sits at the left; its heights are centred in the box when the vertical fit left room.
  const used = rise * kz;
  return { s0: f.s0, s1: f.s1, z0: f.z0, z1: f.z1, k, kz, ox: padL, oy: f.height - padB - (innerH - used) / 2, width: f.width, height: f.height };
}

export const sx = (fr: Frame, s: number): number => fr.ox + (s - fr.s0) * fr.k;
export const sy = (fr: Frame, z: number): number => fr.oy - (z - fr.z0) * fr.kz;
export const stationAtPx = (fr: Frame, px: number): number => fr.s0 + (px - fr.ox) / fr.k;
export const heightAtPx = (fr: Frame, py: number): number => fr.z0 + (fr.oy - py) / fr.kz;

/** The range of heights a profile draws (its grounds, its line, its points), padded a little; null where nothing has a height. */
export function heightRange(series: (readonly [number, number | null][] | undefined)[], extra: number[] = []): [number, number] {
  let lo = Infinity, hi = -Infinity;
  for (const s of series) for (const p of s ?? []) if (p[1] != null && Number.isFinite(p[1])) { lo = Math.min(lo, p[1]); hi = Math.max(hi, p[1]); }
  for (const z of extra) if (Number.isFinite(z)) { lo = Math.min(lo, z); hi = Math.max(hi, z); }
  if (!(lo <= hi)) return [0, 1];
  const pad = Math.max((hi - lo) * 0.08, 0.1);
  return [lo - pad, hi + pad];
}

/** A step that makes about `n` lines over `range` of 1, 2, 2.5, 5 × 10ⁿ. */
export function niceStep(range: number, n: number): number {
  const raw = Math.max(range, 1e-9) / Math.max(1, n);
  const p = 10 ** Math.floor(Math.log10(raw));
  const m = raw / p;
  return (m <= 1 ? 1 : m <= 2 ? 2 : m <= 2.5 ? 2.5 : m <= 5 ? 5 : 10) * p;
}

/** The multiples of `step` inside [lo, hi]. */
export function ticks(lo: number, hi: number, step: number): number[] {
  const out: number[] = [];
  for (let v = Math.ceil(lo / step - 1e-9) * step; v <= hi + 1e-9; v += step) out.push(Math.round(v / step) * step + 0);
  return out;
}

/** A series as an SVG path, a gap (null height) lifting the pen. */
export function seriesPath(fr: Frame, s: readonly [number, number | null][]): string {
  let d = '', pen = false;
  for (const [st, z] of s) {
    if (z == null) { pen = false; continue; }
    d += `${pen ? 'L' : 'M'}${sx(fr, st).toFixed(1)} ${sy(fr, z).toFixed(1)}`;
    pen = true;
  }
  return d;
}

/** A series' height at a station, interpolated between its samples; null off its range or in a gap. */
export function valueAt(s: readonly [number, number | null][], station: number): number | null {
  for (let i = 0; i + 1 < s.length; i++) {
    const [a, b] = [s[i], s[i + 1]];
    if (station < a[0] - 1e-9 || station > b[0] + 1e-9) continue;
    if (a[1] == null || b[1] == null) return null;
    const span = b[0] - a[0];
    return span < 1e-12 ? a[1] : a[1] + (b[1] - a[1]) * ((station - a[0]) / span);
  }
  return null;
}

// ---------------------------------------------------------------------------------------------------------------- hit tests

/** The point whose mark is within `radius` pixels of (px, py), the nearest; -1 for none. */
export function hitPoint(fr: Frame, points: readonly { station: number; z: number }[], px: number, py: number, radius = 9): number {
  let best = -1, bd = radius;
  points.forEach((p, i) => {
    const d = Math.hypot(sx(fr, p.station) - px, sy(fr, p.z) - py);
    if (d <= bd) { bd = d; best = i; }
  });
  return best;
}

/** A run of the profile between two of its points. */
export interface Segment { from: number; to: number; s0: number; z0: number; s1: number; z1: number }

/** The segments between consecutive points; a closed line's last runs from its last point back to the first one's height at the line's end. */
export function segmentsOf(points: readonly { station: number; z: number }[], closed: boolean, end: number): Segment[] {
  const out: Segment[] = [];
  for (let i = 0; i + 1 < points.length; i++) out.push({ from: i, to: i + 1, s0: points[i].station, z0: points[i].z, s1: points[i + 1].station, z1: points[i + 1].z });
  if (closed && points.length >= 2) {
    const last = points[points.length - 1];
    if (end - last.station > 1e-6) out.push({ from: points.length - 1, to: 0, s0: last.station, z0: last.z, s1: end, z1: points[0].z });
  }
  return out;
}

/** The grade of a segment, a fraction (null for one of no length). */
export const gradeOfSegment = (s: Segment): number | null => (s.s1 - s.s0 > 1e-9 ? (s.z1 - s.z0) / (s.s1 - s.s0) : null);

/** The segment whose line passes within `tol` pixels of (px, py), the nearest; -1 for none. */
export function hitSegment(fr: Frame, segs: readonly Segment[], px: number, py: number, tol = 6): number {
  let best = -1, bd = tol;
  segs.forEach((g, i) => {
    const [ax, ay, bx, by] = [sx(fr, g.s0), sy(fr, g.z0), sx(fr, g.s1), sy(fr, g.z1)];
    const dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
    const t = l2 < 1e-9 ? 0 : Math.max(0, Math.min(1, ((px - ax) * dx + (py - ay) * dy) / l2));
    const d = Math.hypot(px - (ax + t * dx), py - (ay + t * dy));
    if (d <= bd) { bd = d; best = i; }
  });
  return best;
}

// ---------------------------------------------------------------------------------------------------------------- dragging

/** How close to a neighbour a dragged point may come along the station. */
export const MIN_GAP = 0.05;

export interface Drag { z?: number; station?: number }

/**
 * The points as they stand while a point is dragged: its height follows, and so does its station when it may slide (`movable`),
 * kept between its neighbours. A locked point does not move (null); an end stays at its station.
 */
export function dragPoint(points: readonly ProfilePoint[], index: number, to: Drag, end: number, closed = false): ProfilePoint[] | null {
  const p = points[index];
  if (!p || p.locked) return null;
  const last = index === points.length - 1;
  let station = p.station;
  if (to.station != null && p.movable && index > 0 && !(last && !closed)) {
    const lo = points[index - 1].station + MIN_GAP;
    const hi = (last ? end : points[index + 1].station) - MIN_GAP;
    station = Math.min(Math.max(to.station, lo), Math.max(lo, hi));
  }
  const z = to.z != null && Number.isFinite(to.z) ? to.z : p.z;
  return points.map((q, i) => (i === index ? { ...q, station, z, designed: to.z != null ? true : q.designed, grade: null } : i === index - 1 ? { ...q, grade: null } : q));
}

/** A series of (station, z) straight between the points: what the line looks like while a point is dragged. */
export const polylineOf = (points: readonly { station: number; z: number }[]): [number, number][] => points.map((p) => [p.station, p.z]);

// ---------------------------------------------------------------------------------------------------------------- grades

/** A grade as the panel shows it: "2.00 %". */
export const gradeText = (g: number): string => `${(Math.abs(g * 100) < 5e-3 ? 0 : g * 100).toFixed(2)} %`;

/** What a typed grade does to a segment: the point whose height changes, and the height it takes (the other end is kept). */
export function gradeTarget(seg: Segment, text: string, keep: 'start' | 'end' = 'start'): { index: number; z: number } | null | undefined {
  const g = readGrade(text);
  if (g === undefined) return undefined;
  if (g === null) return null;
  const run = seg.s1 - seg.s0;
  if (run <= 1e-9) return undefined;
  return keep === 'start' ? { index: seg.to, z: seg.z0 + g * run } : { index: seg.from, z: seg.z1 - g * run };
}

// ---------------------------------------------------------------------------------------------------------------- commands

export interface EngineCall { command: string; args: Record<string, unknown> }
export type Planned = { call: EngineCall } | { refused: string };

const r6 = (v: number): number => Math.round(v * 1e6) / 1e6;
const pvisOf = (d: ProfileData): [number, number, number][] => d.points.map((p) => [r6(p.station), r6(p.z), p.curve]);

/** A point's new height and/or station, as the one command that edits this kind of line. */
export function planMove(d: ProfileData, index: number, to: Drag): Planned {
  const p = d.points[index];
  if (!p) return { refused: 'There is no such point.' };
  if (d.kind === 'featureLine') return { call: { command: 'civil.moveProfilePoint', args: { id: d.id, index, ...(to.z != null ? { z: r6(to.z) } : {}), ...(to.station != null && Math.abs(to.station - p.station) > 1e-6 ? { station: r6(to.station) } : {}) } } };
  if (d.kind === 'hardscape') {
    if (to.z == null) return { refused: 'A hardscape corner keeps its place along the edge: change its height.' };
    return { call: { command: 'civil.updateHardscape', args: { id: d.id, cornerZ: d.points.map((q, i) => r6(i === index ? to.z! : q.z)) } } };
  }
  if (d.mode !== 'design') return { refused: 'Design the profile first: it follows the ground now.' };
  const moved = dragPoint(d.points, index, to, d.start + d.length, false);
  if (!moved) return { refused: 'That point is locked.' };
  return { call: { command: 'civil.setCorridor', args: { id: d.id, profile: { mode: 'design', pvis: moved.map((q): [number, number, number] => [r6(q.station), r6(q.z), q.curve]) } } } };
}

/** A grade typed between two points, as the command that edits this kind of line. */
export function planGrade(d: ProfileData, seg: Segment, text: string, keep: 'start' | 'end'): Planned {
  const g = readGrade(text);
  if (g === undefined) return { refused: 'That grade does not read: type 2 %, 1:50 or -1,5 %.' };
  if (d.kind === 'featureLine') return { call: { command: 'civil.setFeatureLineGrade', args: { id: d.id, segment: seg.from, grade: g === null ? null : r6(g), keep } } };
  if (g === null) return { refused: 'Type a grade.' };
  const t = gradeTarget(seg, text, keep);
  if (!t) return { refused: 'That grade does not read: type 2 %, 1:50 or -1,5 %.' };
  return planMove(d, t.index, { z: t.z });
}

/** A point inserted at a station. */
export function planInsert(d: ProfileData, station: number): Planned {
  if (d.kind === 'featureLine') return { call: { command: 'civil.insertProfilePoint', args: { id: d.id, station: r6(station) } } };
  if (d.kind === 'hardscape') return { refused: 'A hardscape keeps its corners: add one in plan.' };
  if (d.mode !== 'design') return { refused: 'Design the profile first: it follows the ground now.' };
  const z = valueAt(d.line, station);
  if (z == null) return { refused: 'There is no profile at that station.' };
  const pvis = [...pvisOf(d), [r6(station), r6(z), 0] as [number, number, number]].sort((a, b) => a[0] - b[0]);
  return { call: { command: 'civil.setCorridor', args: { id: d.id, profile: { mode: 'design', pvis } } } };
}

/** A point taken out. */
export function planDelete(d: ProfileData, index: number): Planned {
  if (d.kind === 'featureLine') return { call: { command: 'civil.deleteProfilePoint', args: { id: d.id, index } } };
  if (d.kind === 'hardscape') return { refused: 'A hardscape keeps its corners: take one out in plan.' };
  if (d.mode !== 'design') return { refused: 'Design the profile first: it follows the ground now.' };
  if (d.points.length <= 2) return { refused: 'A profile keeps its two ends.' };
  return { call: { command: 'civil.setCorridor', args: { id: d.id, profile: { mode: 'design', pvis: pvisOf(d).filter((_, i) => i !== index) } } } };
}

/** A point's vertical curve (length along the station, 0: none). */
export function planCurve(d: ProfileData, index: number, length: number): Planned {
  if (d.kind === 'featureLine') return { call: { command: 'civil.setProfileCurve', args: { id: d.id, index, length: r6(length) } } };
  if (d.kind === 'hardscape') return { refused: 'A hardscape has no vertical curves.' };
  if (d.mode !== 'design') return { refused: 'Design the profile first: it follows the ground now.' };
  return { call: { command: 'civil.setCorridor', args: { id: d.id, profile: { mode: 'design', pvis: pvisOf(d).map((q, i): [number, number, number] => (i === index ? [q[0], q[1], r6(length)] : q)) } } } };
}

/** A point locked or let go (feature lines only). */
export function planLock(d: ProfileData, index: number, locked: boolean): Planned {
  if (d.kind !== 'featureLine') return { refused: 'Only a feature line\'s points can be locked.' };
  return { call: { command: 'civil.lockProfilePoint', args: { id: d.id, index, locked } } };
}

/** What a kind of line allows of its points, for the panel to enable its controls. */
export function capabilities(d: ProfileData): { insert: boolean; remove: boolean; curve: boolean; lock: boolean; edit: boolean } {
  if (d.kind === 'featureLine') return { insert: true, remove: true, curve: true, lock: true, edit: true };
  if (d.kind === 'hardscape') return { insert: false, remove: false, curve: false, lock: false, edit: true };
  const design = d.mode === 'design';
  return { insert: design, remove: design, curve: design, lock: false, edit: design };
}

// ---------------------------------------------------------------------------------------------------------------- cross sections

/** A cross-section point dragged: the command that sets it (the cross line is made at the first edit). */
export function planCrossPoint(axisId: string, c: CrossData, offset: number, z: number): EngineCall {
  return { command: 'civil.setCrossPoint', args: { id: axisId, station: r6(c.station), width: c.width, offset: r6(offset), z: r6(z) } };
}

/** A cross-section point slid across (its offset) and set to a height, on the cross line that exists. */
export function planCrossMove(c: CrossData, index: number, offset: number, z: number): EngineCall | null {
  if (!c.crossId) return null;
  return { command: 'civil.moveProfilePoint', args: { id: c.crossId, index, z: r6(z), station: r6(offset + c.width) } };
}

// ---------------------------------------------------------------------------------------------------------------- plan ↔ graph

/** The place in plan at a station along the path, and the direction there (radians); null for a path of no length. */
export function pointAtStation(path: readonly [number, number, number][], station: number): { x: number; y: number; angle: number } | null {
  if (path.length < 2) return null;
  const s = Math.min(Math.max(station, path[0][2]), path[path.length - 1][2]);
  for (let i = 0; i + 1 < path.length; i++) {
    const [a, b] = [path[i], path[i + 1]];
    if (s > b[2] + 1e-9) continue;
    const span = b[2] - a[2];
    const t = span < 1e-12 ? 0 : (s - a[2]) / span;
    return { x: a[0] + (b[0] - a[0]) * t, y: a[1] + (b[1] - a[1]) * t, angle: Math.atan2(b[1] - a[1], b[0] - a[0]) };
  }
  return null;
}

/** The station of the path nearest a point in plan, and how far the point is from it. */
export function stationAtPoint(path: readonly [number, number, number][], x: number, y: number): { station: number; distance: number } | null {
  if (path.length < 2) return null;
  let best: { station: number; distance: number } | null = null;
  for (let i = 0; i + 1 < path.length; i++) {
    const [a, b] = [path[i], path[i + 1]];
    const dx = b[0] - a[0], dy = b[1] - a[1], l2 = dx * dx + dy * dy;
    const t = l2 < 1e-12 ? 0 : Math.max(0, Math.min(1, ((x - a[0]) * dx + (y - a[1]) * dy) / l2));
    const d = Math.hypot(x - (a[0] + dx * t), y - (a[1] + dy * t));
    if (!best || d < best.distance) best = { station: a[2] + (b[2] - a[2]) * t, distance: d };
  }
  return best;
}

/** A station as the graph writes it: 0+020.00. */
export const stationName = (s: number): string => { const km = Math.floor(Math.abs(s) / 1000 + 1e-9); return `${s < 0 ? '-' : ''}${km}+${(Math.abs(s) - km * 1000).toFixed(2).padStart(6, '0')}`; };
