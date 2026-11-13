/**
 * Sketch constraints in the interface (docs/plan-studio-parametric.md §4, stage C3): which picks a kind takes, the part of a
 * drafted shape a click means, the dimension a pick implies, what a dimension measures and says, where a glyph stands, and how a
 * sketch's solved state colours its shapes. Pure: the engine owns the constraints and the solve; this is what the drawing and the
 * tools need to ask and to show. The kinds and their allowed picks mirror `Lapin.Studio/SketchConstraints.cs`.
 */

import type { Vec2 } from '../../platform/module';
import { angleInArc, dist, nearestOnSegment, normAngle, polylines, prims, sweepOf, type Shape } from './shapes';

/** What a pick is: a point, a straight edge, a circle or an arc, a polyline as a whole. */
export type RefKind = 'point' | 'line' | 'curve' | 'shape';
const LETTER: Record<RefKind, string> = { point: 'P', line: 'L', curve: 'C', shape: 'S' };

export interface Ref { shape: string; part: string }
/** A reference as `studio.constraints` resolves it: where it is now. */
export interface ResolvedRef extends Ref {
  at?: number[]; a?: number[]; b?: number[]; c?: number[]; r?: number; start?: number[]; end?: number[]; pts?: number[][]; missing?: boolean;
}
/**
 * Where a dimension is drawn, as the engine keeps it (§4.2 of docs/plan-studio-parametric.md): `offset` metres from what it measures
 * (a distance's line across its direction, positive to the left; an angle's arc radius; a radius' text beyond the rim) and, for a
 * radius or a diameter, the `angle` (degrees) of the ray its text stands on. None: placed automatically.
 */
export interface DimensionPlace { offset: number; angle?: number }
export interface ConstraintDto {
  id: string; kind: string; refs: ResolvedRef[]; value?: number | null; formula?: string; driving: boolean; measured?: number; problem?: string; viewId?: string;
  place?: DimensionPlace | null;
}
export interface SketchStatus {
  viewId?: string; status: string; dof: number; freeShapes: Ref[]; redundant: string[]; conflicting: string[]; problem?: string;
  dimensions?: { id: string; kind: string; value?: number | null; measured?: number; driving: boolean; problem?: string }[];
}

// ─── The kinds ───────────────────────────────────────────────────────────────

/** Each kind's allowed picks, in order (P a point, L a straight edge, C a circle or arc, S a polyline whole): the engine's table. */
export const PATTERNS: Record<string, string[][]> = {
  coincident: [['P', 'P']], pointOn: [['P', 'LC']], horizontal: [['L'], ['P', 'P']], vertical: [['L'], ['P', 'P']],
  parallel: [['L', 'L']], perpendicular: [['L', 'L']], tangent: [['L', 'C'], ['C', 'L'], ['C', 'C'], ['P', 'P']], equal: [['L', 'L'], ['C', 'C']],
  symmetric: [['P', 'P', 'L'], ['P', 'P', 'P']], fix: [['P'], ['L'], ['C'], ['S']],
  distanceX: [['P'], ['P', 'P'], ['L']], distanceY: [['P'], ['P', 'P'], ['L']], distance: [['P', 'P'], ['L'], ['P', 'L']],
  radius: [['C']], diameter: [['C']], angle: [['L'], ['L', 'L']], midpoint: [['P', 'L']],
};
/** The kinds that carry a value. */
export const VALUE_KINDS = ['distanceX', 'distanceY', 'distance', 'radius', 'diameter', 'angle'];
export const isDimension = (kind: string): boolean => VALUE_KINDS.includes(kind);

const letters = (kinds: readonly RefKind[]): string[] => kinds.map((k) => LETTER[k]);
const fitsPrefix = (pattern: string[], have: string[]): boolean => have.length <= pattern.length && have.every((l, i) => pattern[i].includes(l));
const fitsWhole = (pattern: string[], have: string[]): boolean => have.length === pattern.length && fitsPrefix(pattern, have);
/** The engine takes two picks the other way round too. */
const twoWays = (pattern: string[]): string[][] => (pattern.length === 2 ? [pattern, [pattern[1], pattern[0]]] : [pattern]);

/** Whether more picks can still make `kind`, the picks so far being a start of one of its patterns. */
export function canExtend(kind: string, picked: readonly RefKind[]): boolean {
  const have = letters(picked);
  return (PATTERNS[kind] ?? []).some((p) => twoWays(p).some((q) => fitsPrefix(q, have)));
}
/** Whether the picks are a whole pattern of `kind`. */
export function isComplete(kind: string, picked: readonly RefKind[]): boolean {
  const have = letters(picked);
  return (PATTERNS[kind] ?? []).some((p) => twoWays(p).some((q) => fitsWhole(q, have)));
}
/** Whether the picks are whole and cannot grow into a longer pattern: the tool applies at once. */
export function isFinal(kind: string, picked: readonly RefKind[]): boolean {
  if (!isComplete(kind, picked)) return false;
  const have = letters(picked);
  return !(PATTERNS[kind] ?? []).some((p) => p.length > have.length && twoWays(p).some((q) => fitsPrefix(q, have)));
}
/** What the next pick may be, for the prompt (the words, each a phrase to translate). */
export function wantedWords(kind: string, picked: readonly RefKind[]): string[] {
  const have = letters(picked);
  const next = new Set<string>();
  for (const p of PATTERNS[kind] ?? []) for (const q of twoWays(p)) if (fitsPrefix(q, have) && q.length > have.length) for (const l of q[have.length]) next.add(l);
  const words: Record<string, string> = { P: 'a point', L: 'a line', C: 'a circle or an arc', S: 'a polyline' };
  return [...next].map((l) => words[l]);
}
/** What the next pick should be, for the prompt (a word). */
export function wanted(kind: string, picked: readonly RefKind[]): string {
  return wantedWords(kind, picked).join(' or ') || 'nothing';
}

// ─── Parts of a shape ────────────────────────────────────────────────────────

const arcAt = (c: Vec2, r: number, a: number): Vec2 => ({ x: c.x + Math.cos(a) * r, y: c.y + Math.sin(a) * r });

/** What a part of a shape is, written the one way the engine writes it; null when the shape has no such part. */
export function classify(s: Shape, part: string): { kind: RefKind; part: string } | null {
  const index = (prefix: string): number | null => (part.startsWith(prefix) && /^\d+$/.test(part.slice(prefix.length)) ? Number(part.slice(prefix.length)) : null);
  switch (s.type) {
    case 'line':
      if (part === 'start' || part === 'end') return { kind: 'point', part };
      return part === 'whole' || part === 'edge:0' ? { kind: 'line', part: 'whole' } : null;
    case 'polyline': {
      const n = s.pts.length, edges = s.closed ? n : n - 1;
      if (part === 'start' && !s.closed) return { kind: 'point', part: 'vertex:0' };
      if (part === 'end' && !s.closed) return { kind: 'point', part: `vertex:${n - 1}` };
      if (part === 'whole') return { kind: 'shape', part };
      const v = index('vertex:'), e = index('edge:');
      if (v != null && v < n) return { kind: 'point', part: `vertex:${v}` };
      if (e != null && e < edges) return { kind: 'line', part: `edge:${e}` };
      return null;
    }
    case 'circle': return part === 'center' ? { kind: 'point', part } : part === 'whole' ? { kind: 'curve', part } : null;
    case 'arc': return part === 'start' || part === 'end' || part === 'center' ? { kind: 'point', part } : part === 'whole' ? { kind: 'curve', part } : null;
  }
}

/** A point part's place. */
export function pointOf(s: Shape, part: string): Vec2 | null {
  if (s.type === 'line') return part === 'start' ? s.a : part === 'end' ? s.b : null;
  if (s.type === 'polyline') { const m = /^vertex:(\d+)$/.exec(part); return m && Number(m[1]) < s.pts.length ? s.pts[Number(m[1])] : null; }
  if (s.type === 'circle') return part === 'center' ? s.c : null;
  return part === 'start' ? arcAt(s.c, s.r, s.start) : part === 'end' ? arcAt(s.c, s.r, s.end) : part === 'center' ? s.c : null;
}
/** A straight edge part's ends. */
export function segmentOf(s: Shape, part: string): [Vec2, Vec2] | null {
  if (s.type === 'line') return part === 'whole' ? [s.a, s.b] : null;
  if (s.type === 'polyline') {
    const m = /^edge:(\d+)$/.exec(part);
    const i = m ? Number(m[1]) : -1, edges = s.closed ? s.pts.length : s.pts.length - 1;
    return i >= 0 && i < edges ? [s.pts[i], s.pts[(i + 1) % s.pts.length]] : null;
  }
  return null;
}

export interface Candidate { part: string; kind: RefKind; at?: Vec2; d: number }

/**
 * The parts of a shape a click at `p` may mean, best first: the shape's points within `tol` (nearest first), then the edge
 * nearest the click, then the circle or arc itself, then a polyline as a whole.
 */
export function candidatesAt(s: Shape, p: Vec2, tol: number): Candidate[] {
  const out: Candidate[] = [];
  const parts: string[] = s.type === 'line' ? ['start', 'end'] : s.type === 'polyline' ? s.pts.map((_, i) => `vertex:${i}`) : s.type === 'circle' ? ['center'] : ['start', 'end', 'center'];
  for (const part of parts) {
    const at = pointOf(s, part)!;
    const d = dist(at, p);
    if (d <= tol) out.push({ part, kind: 'point', at, d });
  }
  out.sort((x, y) => x.d - y.d);
  if (s.type === 'line' || s.type === 'polyline') {
    let best: Candidate | null = null;
    const list = prims(s);
    list.forEach((pr, i) => {
      if (pr.kind !== 'seg') return;
      const d = nearestOnSegment(p, pr.a, pr.b).dist;
      if (d <= tol && (!best || d < best.d)) best = { part: s.type === 'line' ? 'whole' : `edge:${i}`, kind: 'line', d };
    });
    if (best) out.push(best);
    if (s.type === 'polyline') out.push({ part: 'whole', kind: 'shape', d: 0 });
  } else {
    const d = Math.abs(dist(p, s.c) - s.r);
    const onArc = s.type === 'circle' || angleInArc(Math.atan2(p.y - s.c.y, p.x - s.c.x), s.start, s.end);
    if (onArc && d <= tol) out.push({ part: 'whole', kind: 'curve', d });
  }
  return out;
}

/** The first candidate that can follow the picks, and is not a pick already made. */
export function choose(kind: string, picked: readonly { ref: Ref; kind: RefKind }[], found: { shape: string; candidates: Candidate[] }[]): { ref: Ref; kind: RefKind; at?: Vec2 } | null {
  for (const f of found) for (const c of f.candidates) {
    if (picked.some((p) => p.ref.shape === f.shape && p.ref.part === c.part)) continue;
    if (canExtend(kind, [...picked.map((p) => p.kind), c.kind])) return { ref: { shape: f.shape, part: c.part }, kind: c.kind, at: c.at };
  }
  return null;
}

/** The point a drafting grip drags, by the grip's key (a line's start and end, a polyline's vertices, a circle's centre, an arc's centre and ends); null for any other grip. */
export function sketchPartOf(s: Shape, gripKey: string): string | null {
  switch (s.type) {
    case 'line': return gripKey === 'start' || gripKey === 'end' ? gripKey : null;
    case 'polyline': { const m = /^[vp](\d+)$/.exec(gripKey); return m && Number(m[1]) < s.pts.length ? `vertex:${m[1]}` : null; }
    case 'circle': return gripKey === 'c' ? 'center' : null;
    case 'arc': return gripKey === 'c' ? 'center' : gripKey === 's' ? 'start' : gripKey === 'e' ? 'end' : null;
  }
}

/** A part named for the hover label. */
export function partLabel(kind: RefKind, part: string): string {
  if (kind === 'point') return part === 'center' ? 'centre' : part === 'start' || part === 'end' ? part : 'vertex';
  return kind === 'line' ? 'edge' : kind === 'curve' ? 'curve' : 'whole';
}

// ─── Dimensions ──────────────────────────────────────────────────────────────

export interface Picked { shape: Shape; part: string }

/** What the Dimension tool makes of picks: the kind, and the other kinds a keyword may turn it into. */
export function dimensionFor(picked: readonly RefKind[]): { kind: string; alternatives: string[] } | null {
  const key = picked.join(',');
  switch (key) {
    case 'point,point': return { kind: 'distance', alternatives: ['distanceX', 'distanceY'] };
    case 'point,line': case 'line,point': return { kind: 'distance', alternatives: [] };
    case 'line': return { kind: 'distance', alternatives: ['distanceX', 'distanceY'] };
    case 'line,line': return { kind: 'angle', alternatives: [] };
    case 'curve': return { kind: 'radius', alternatives: ['diameter'] };
    default: return null;
  }
}
/** Whether the Dimension tool applies at these picks without waiting (a line may still become an angle). */
export const dimensionIsFinal = (picked: readonly RefKind[]): boolean => { const k = picked.join(','); return k === 'point,point' || k === 'point,line' || k === 'line,point' || k === 'line,line' || k === 'curve'; };

export const normAngleDeg = (deg: number): number => { let a = deg % 360; if (a <= -180) a += 360; if (a > 180) a -= 360; return Math.round(a * 1e9) / 1e9; };
const deg = (v: Vec2) => (Math.atan2(v.y, v.x) * 180) / Math.PI;

/** What a dimension measures on picked shapes (metres, degrees), as the engine does; null when the picks do not fit. */
export function measure(kind: string, picks: readonly Picked[]): number | null {
  const pt = (i: number) => (picks[i] ? pointOf(picks[i].shape, picks[i].part) : null);
  const seg = (i: number) => (picks[i] ? segmentOf(picks[i].shape, picks[i].part) : null);
  switch (kind) {
    case 'distanceX': case 'distanceY': {
      let d: Vec2 | null = null;
      if (picks.length === 1 && pt(0)) d = pt(0);
      else if (picks.length === 1 && seg(0)) { const [a, b] = seg(0)!; d = { x: b.x - a.x, y: b.y - a.y }; }
      else if (picks.length === 2 && pt(0) && pt(1)) d = { x: pt(1)!.x - pt(0)!.x, y: pt(1)!.y - pt(0)!.y };
      return d ? (kind === 'distanceY' ? d.y : d.x) : null;
    }
    case 'distance': {
      if (picks.length === 1 && seg(0)) { const [a, b] = seg(0)!; return dist(a, b); }
      if (pt(0) && pt(1)) return dist(pt(0)!, pt(1)!);
      if (pt(0) && seg(1)) return distToLine(pt(0)!, ...seg(1)!);
      if (seg(0) && pt(1)) return distToLine(pt(1)!, ...seg(0)!);
      return null;
    }
    case 'radius': case 'diameter': {
      const s = picks[0]?.shape;
      if (!s || (s.type !== 'circle' && s.type !== 'arc')) return null;
      return kind === 'diameter' ? 2 * s.r : s.r;
    }
    case 'angle': {
      if (picks.length === 1 && seg(0)) { const [a, b] = seg(0)!; return normAngleDeg(deg({ x: b.x - a.x, y: b.y - a.y })); }
      if (picks.length === 2 && seg(0) && seg(1)) {
        const [a, b] = seg(0)!, [c, d] = seg(1)!;
        return normAngleDeg(deg({ x: d.x - c.x, y: d.y - c.y }) - deg({ x: b.x - a.x, y: b.y - a.y }));
      }
      return null;
    }
  }
  return null;
}
function distToLine(p: Vec2, a: Vec2, b: Vec2): number {
  const dx = b.x - a.x, dy = b.y - a.y, l = Math.hypot(dx, dy);
  return l < 1e-12 ? dist(p, a) : Math.abs((dx * (p.y - a.y) - dy * (p.x - a.x)) / l);
}

/** What was typed for a dimension: a number (a length in the project's unit unless it carries its own; degrees for an angle) or `=formula`. */
export function parseDimensionInput(text: string, kind: string, parseLength: (s: string) => number | null): { value: number } | { formula: string } | { error: string } {
  const t = text.trim();
  if (!t) return { error: 'Type a value, or =formula.' };
  if (t.startsWith('=')) {
    const f = t.slice(1).trim();
    return f ? { formula: f } : { error: 'Type a formula after =.' };
  }
  if (kind === 'angle') {
    const n = Number(t.replace(/\s*°$/, '').replace(',', '.'));
    return Number.isFinite(n) ? { value: normAngleDeg(n) } : { error: 'An angle is a number of degrees.' };
  }
  const v = parseLength(t);
  if (v == null || !Number.isFinite(v)) return { error: 'A length is a number, with its unit if you like.' };
  if (kind === 'radius' || kind === 'diameter') return v > 0 ? { value: v } : { error: 'A radius or a diameter is more than nothing.' };
  if (kind === 'distance' && v < 0) return { error: 'A distance is not negative.' };
  return { value: v };
}

/** A dimension as it reads in the view: R and Ø in front, degrees after an angle, driven ones in parentheses. */
export function dimensionText(c: Pick<ConstraintDto, 'kind' | 'value' | 'measured' | 'driving'>, length: (m: number) => string): string {
  const v = c.driving ? c.value ?? c.measured : c.measured ?? c.value;
  if (v == null) return '?';
  const body = c.kind === 'angle' ? `${Math.round(v * 100) / 100}°` : c.kind === 'radius' ? `R ${length(v)}` : c.kind === 'diameter' ? `Ø ${length(v)}` : length(v);
  return c.driving ? body : `(${body})`;
}

// ─── Geometry of a dimension and a glyph ─────────────────────────────────────

const world = (a: number[] | undefined): Vec2 | null => (a && a.length >= 2 ? { x: a[0], y: a[1] } : null);
const mid = (a: Vec2, b: Vec2): Vec2 => ({ x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 });

/** A resolved reference's place as a point (a point; an edge's middle; a curve's rim at 45 degrees; a polyline's first vertex). */
export function refAnchor(r: ResolvedRef): Vec2 | null {
  const at = world(r.at);
  if (at) return at;
  const a = world(r.a), b = world(r.b);
  if (a && b) return mid(a, b);
  const c = world(r.c);
  if (c && r.r != null) {
    const s = world(r.start), e = world(r.end);
    if (s && e) { const a0 = Math.atan2(s.y - c.y, s.x - c.x), a1 = Math.atan2(e.y - c.y, e.x - c.x); return arcAt(c, r.r, a0 + sweepOf(a0, a1) / 2); }
    return arcAt(c, r.r, Math.PI / 4);
  }
  return r.pts?.length ? world(r.pts[0]) : null;
}

/** Where a constraint's glyph stands: on its reference, or midway between two. */
export function glyphAnchor(c: ConstraintDto): Vec2 | null {
  const pts = c.refs.map(refAnchor).filter((p): p is Vec2 => !!p);
  if (!pts.length) return null;
  if (pts.length === 1 || c.kind === 'coincident') return pts[0];
  if (c.kind === 'pointOn' || c.kind === 'midpoint') return pts[0];
  return mid(pts[0], pts[1]);
}

/** What each kind's glyph looks like: a letter or symbol, or a path drawn in a 14-unit box about its centre. */
export const GLYPHS: Record<string, { text?: string; path?: string; label: string }> = {
  coincident: { path: 'M-4,0a4,4 0 1,0 8,0a4,4 0 1,0 -8,0M-1.4,0a1.4,1.4 0 1,0 2.8,0a1.4,1.4 0 1,0 -2.8,0', label: 'Coincident' },
  pointOn: { path: 'M-5,4L5,-4M-1.5,-2a1.5,1.5 0 1,0 3,0a1.5,1.5 0 1,0 -3,0', label: 'Point on object' },
  horizontal: { text: 'H', label: 'Horizontal' },
  vertical: { text: 'V', label: 'Vertical' },
  parallel: { text: '∥', label: 'Parallel' },
  perpendicular: { text: '⊥', label: 'Perpendicular' },
  tangent: { path: 'M-6,3L6,3M-3,-1a3,3 0 1,0 6,0a3,3 0 1,0 -6,0', label: 'Tangent' },
  equal: { text: '=', label: 'Equal' },
  symmetric: { path: 'M0,-6V6M-5,-2L-2,0L-5,2M5,-2L2,0L5,2', label: 'Symmetric' },
  fix: { path: 'M-4,0H4V6H-4ZM-2.5,0V-2a2.5,2.5 0 0,1 5,0V0', label: 'Fix' },
  midpoint: { text: 'M', label: 'Midpoint' },
};

/**
 * Glyphs at nearly one place are fanned out so none hides another: each is shifted along x by 15 px for every glyph before it
 * within `gap` px, and all stand a little above their anchor. Screen pixels in, offsets out.
 */
export function spreadGlyphs(at: readonly { x: number; y: number }[], gap = 16): { dx: number; dy: number }[] {
  return at.map((p, i) => {
    let n = 0;
    for (let j = 0; j < i; j++) if (Math.hypot(at[j].x - p.x, at[j].y - p.y) < gap) n++;
    return { dx: n * 15, dy: -12 };
  });
}

export interface DimensionDraw { lines: Vec2[][]; text: Vec2 }

/** A dimension's ends as the engine measures them: a line's ends, two points, a point and its foot on a line, a lone point from the origin. */
function dimensionEnds(c: ConstraintDto): [Vec2, Vec2] | null {
  const r0 = c.refs[0], r1 = c.refs[1];
  const a0 = world(r0?.a), b0 = world(r0?.b);
  if (c.refs.length === 1) {
    if (a0 && b0) return [a0, b0];
    const p = world(r0?.at);
    return p ? [{ x: 0, y: 0 }, p] : null;
  }
  const p0 = world(r0?.at), p1 = world(r1?.at);
  if (p0 && p1) return [p0, p1];
  const a1 = world(r1?.a), b1 = world(r1?.b);
  if (p0 && a1 && b1) return [p0, foot(p0, a1, b1)];
  if (a0 && b0 && p1) return [p1, foot(p1, a0, b0)];
  return null;
}

/**
 * The frame a linear dimension's offset is measured in (the engine's `LinearFrame`): its first end `a`, its second `b`, and `n` the
 * unit vector to the left of its direction (a distance's from a to b, a horizontal one's +x, a vertical one's +y).
 */
export function linearFrame(c: ConstraintDto): { a: Vec2; b: Vec2; n: Vec2 } | null {
  if (c.kind !== 'distance' && c.kind !== 'distanceX' && c.kind !== 'distanceY') return null;
  const e = dimensionEnds(c);
  if (!e) return null;
  const [a, b] = e;
  if (c.kind === 'distanceX') return { a, b, n: { x: 0, y: 1 } };
  if (c.kind === 'distanceY') return { a, b, n: { x: -1, y: 0 } };
  const l = dist(a, b);
  return { a, b, n: l < 1e-12 ? { x: 0, y: 1 } : { x: -(b.y - a.y) / l, y: (b.x - a.x) / l } };
}

/** Shapes this many screen pixels across or less are cleared whole by an automatic dimension; past it only its ends are. */
export const SMALL_SHAPE_PX = 240;

/**
 * Where a dimension goes when the user has not placed it. A linear one stands on the side of what it measures away from the middle
 * of its shapes (`shapes`, those its references are on: a rectangle's side is dimensioned outside the rectangle), `gap` (view units)
 * past them when they are small on screen (`scale` pixels per unit), else past its own ends; with no shapes, on the left. An
 * angle's arc is 1.6 gaps out; a radius' text 1.2 gaps beyond the rim, half way round an arc or at 45 degrees on a circle.
 */
export function autoPlace(c: ConstraintDto, gap: number, shapes: readonly Shape[] = [], scale = 1): DimensionPlace | null {
  if (c.kind === 'angle') return { offset: gap * 1.6 };
  if (c.kind === 'radius' || c.kind === 'diameter') {
    const r = c.refs[0], ctr = world(r?.c);
    if (!ctr) return null;
    const s = world(r.start), e = world(r.end);
    let ang = Math.PI / 4;
    if (s && e) { const a0 = Math.atan2(s.y - ctr.y, s.x - ctr.x), a1 = Math.atan2(e.y - ctr.y, e.x - ctr.x); ang = a0 + sweepOf(a0, a1) / 2; }
    return { offset: gap * 1.2, angle: normAngleDeg((ang * 180) / Math.PI) };
  }
  const f = linearFrame(c);
  if (!f) return null;
  const across = (p: Vec2) => (p.x - f.a.x) * f.n.x + (p.y - f.a.y) * f.n.y;
  const e1 = across(f.b), endHi = Math.max(0, e1), endLo = Math.min(0, e1);
  const pts = shapes.flatMap((sh) => polylines(sh).flat());
  if (!pts.length) return { offset: endHi + gap };
  let lo = Infinity, hi = -Infinity, minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
  for (const p of pts) {
    const t = across(p);
    lo = Math.min(lo, t); hi = Math.max(hi, t);
    minX = Math.min(minX, p.x); maxX = Math.max(maxX, p.x); minY = Math.min(minY, p.y); maxY = Math.max(maxY, p.y);
  }
  const middle = across({ x: (minX + maxX) / 2, y: (minY + maxY) / 2 });
  // Away from the shapes' middle; a middle on the dimension's own line (a diagonal) keeps the left.
  const left = middle <= (endHi + endLo) / 2 + 1e-9;
  if (left) return { offset: ((hi - endHi) * scale <= SMALL_SHAPE_PX ? Math.max(hi, endHi) : endHi) + gap };
  return { offset: ((endLo - lo) * scale <= SMALL_SHAPE_PX ? Math.min(lo, endLo) : endLo) - gap };
}

/** Where an angle's arc turns about, and the way it runs: from `from` radians through `span`. */
function angleArc(c: ConstraintDto): { base: Vec2; from: number; span: number } | null {
  const s0 = segPair(c.refs[0]);
  if (!s0) return null;
  const s1 = c.refs.length > 1 ? segPair(c.refs[1]) : null;
  const a0 = Math.atan2(s0[1].y - s0[0].y, s0[1].x - s0[0].x);
  const sweep = c.refs.length > 1 && s1 ? Math.atan2(s1[1].y - s1[0].y, s1[1].x - s1[0].x) - a0 : a0;
  const base = c.refs.length > 1 && s1 ? crossing(s0, s1) ?? mid(s0[0], s0[1]) : s0[0];
  return { base, from: c.refs.length > 1 ? a0 : 0, span: c.refs.length > 1 ? signedSweep(sweep) : a0 };
}

/**
 * The place a dimension takes when its text is dragged to `p` (view coordinates): a linear one's line through it, an angle's arc
 * with its text there, a radius' text there. `gap` as for `dimensionDraw`.
 */
export function placeAt(c: ConstraintDto, p: Vec2, gap: number): DimensionPlace | null {
  const f = linearFrame(c);
  if (f) return { offset: round9((p.x - f.a.x) * f.n.x + (p.y - f.a.y) * f.n.y) };
  if (c.kind === 'angle') {
    const g = angleArc(c);
    return g ? { offset: round9(Math.max(dist(g.base, p) - gap * 0.5, gap * 0.3)) } : null;
  }
  if (c.kind === 'radius' || c.kind === 'diameter') {
    const r = c.refs[0], ctr = world(r?.c);
    if (!ctr || r?.r == null) return null;
    return { offset: round9(dist(ctr, p) - r.r), angle: normAngleDeg((Math.atan2(p.y - ctr.y, p.x - ctr.x) * 180) / Math.PI) };
  }
  return null;
}
const round9 = (v: number) => Math.round(v * 1e9) / 1e9;

/**
 * A dimension's lines and its text's place, in view coordinates; `off` the gap (view units) an automatic dimension leaves. Drawn
 * at `o.place` (a drag's preview), else at the place the user gave it, else automatically (`autoPlace`, given the shapes its
 * references are on and the view's pixels per unit). A radius and a diameter are a leader through the rim; an angle an arc
 * about the lines' crossing.
 */
export function dimensionDraw(c: ConstraintDto, off: number, o: { place?: DimensionPlace | null; shapes?: readonly Shape[]; scale?: number } = {}): DimensionDraw | null {
  const place = o.place ?? c.place ?? autoPlace(c, off, o.shapes, o.scale);
  if (!place) return null;
  switch (c.kind) {
    case 'distance': case 'distanceX': case 'distanceY': {
      const f = linearFrame(c);
      if (!f) return null;
      const { a, b, n } = f, k = place.offset, kb = k - ((b.x - a.x) * n.x + (b.y - a.y) * n.y);
      const a2 = { x: a.x + n.x * k, y: a.y + n.y * k }, b2 = { x: b.x + n.x * kb, y: b.y + n.y * kb };
      return { lines: [[a, a2], [b, b2], [a2, b2]], text: mid(a2, b2) };
    }
    case 'radius': case 'diameter': {
      const r = c.refs[0], ctr = world(r?.c);
      if (!ctr || r?.r == null) return null;
      const ang = ((place.angle ?? 45) * Math.PI) / 180;
      const rim = arcAt(ctr, r.r, ang), text = arcAt(ctr, r.r + place.offset, ang);
      // The leader runs through the rim to just short of the text (a text inside stops it at the rim).
      const out = arcAt(ctr, r.r + Math.max(place.offset - off * 0.5, 0), ang);
      const from = c.kind === 'diameter' ? arcAt(ctr, r.r, ang + Math.PI) : ctr;
      return { lines: [[from, rim, out]], text };
    }
    case 'angle': {
      const g = angleArc(c);
      if (!g) return null;
      const radius = place.offset;
      const n = Math.max(6, Math.ceil(Math.abs(g.span) / (Math.PI / 24)));
      const arc = Array.from({ length: n + 1 }, (_, i) => arcAt(g.base, radius, g.from + (g.span * i) / n));
      return { lines: [arc], text: arcAt(g.base, radius + off * 0.5, g.from + g.span / 2) };
    }
  }
  return null;
}
const segPair = (r: ResolvedRef | undefined): [Vec2, Vec2] | null => { const a = world(r?.a), b = world(r?.b); return a && b ? [a, b] : null; };
/** An angle taken to the way round of least turn. */
const signedSweep = (rad: number): number => { let a = normAngle(rad); if (a > Math.PI) a -= 2 * Math.PI; return a; };
function foot(p: Vec2, a: Vec2, b: Vec2): Vec2 {
  const dx = b.x - a.x, dy = b.y - a.y, l2 = dx * dx + dy * dy || 1, t = ((p.x - a.x) * dx + (p.y - a.y) * dy) / l2;
  return { x: a.x + dx * t, y: a.y + dy * t };
}
function crossing(a: [Vec2, Vec2], b: [Vec2, Vec2]): Vec2 | null {
  const d1 = { x: a[1].x - a[0].x, y: a[1].y - a[0].y }, d2 = { x: b[1].x - b[0].x, y: b[1].y - b[0].y };
  const den = d1.x * d2.y - d1.y * d2.x;
  if (Math.abs(den) < 1e-12) return null;
  const t = ((b[0].x - a[0].x) * d2.y - (b[0].y - a[0].y) * d2.x) / den;
  return { x: a[0].x + d1.x * t, y: a[0].y + d1.y * t };
}

// ─── The solved state, as colours ────────────────────────────────────────────

export type ShapeState = 'free' | 'fixed' | 'conflict';

/** Each drafted shape's state: free (some part still moves), fixed (none does), conflict (a conflicting constraint ties it). */
export function shapeStates(shapeIds: readonly string[], constraints: readonly ConstraintDto[], status: SketchStatus | null): Map<string, ShapeState> {
  const out = new Map<string, ShapeState>();
  const free = new Set((status?.freeShapes ?? []).map((f) => f.shape));
  const bad = new Set<string>();
  for (const c of constraints) if (status?.conflicting.includes(c.id)) for (const r of c.refs) bad.add(r.shape);
  for (const id of shapeIds) out.set(id, bad.has(id) ? 'conflict' : free.has(id) || !status ? 'free' : 'fixed');
  return out;
}
/** The points still free, for small blue dots. */
export const freePoints = (status: SketchStatus | null): Ref[] => (status?.freeShapes ?? []).filter((f) => /^(start|end|center|vertex:\d+)$/.test(f.part));

export type ConstraintState = 'ok' | 'redundant' | 'conflict';
export const constraintState = (c: ConstraintDto, status: SketchStatus | null): ConstraintState =>
  status?.conflicting.includes(c.id) ? 'conflict' : status?.redundant.includes(c.id) ? 'redundant' : 'ok';

/** The chip over the view: how free the sketch is. */
export function chipOf(status: SketchStatus | null): { text: string; tone: 'free' | 'fixed' | 'conflict' | 'redundant' | 'unknown' } {
  if (!status) return { text: 'Reading the sketch…', tone: 'unknown' };
  if (status.status === 'invalid' || status.status === 'notConverged') return { text: 'The sketch does not solve', tone: 'conflict' };
  if (status.conflicting.length) return { text: 'Conflicting constraints', tone: 'conflict' };
  if (status.redundant.length) return { text: 'Redundant constraints', tone: 'redundant' };
  if (status.dof > 0) return { text: status.dof === 1 ? '1 degree of freedom' : `${status.dof} degrees of freedom`, tone: 'free' };
  return { text: 'Fully constrained', tone: 'fixed' };
}
