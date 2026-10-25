/**
 * A path element's axis as the engine has it (PathCurve): straight from its
 * start to its end, or an arc through them rising `rise` off the chord at its
 * middle (to the left of start → end when positive). Distances run along the
 * axis from the start.
 */

import type { Vec2 } from '../platform/module';

export interface Arc { a: Vec2; b: Vec2; rise: number }

const sub = (p: Vec2, q: Vec2): Vec2 => ({ x: p.x - q.x, y: p.y - q.y });
const len = (p: Vec2) => Math.hypot(p.x, p.y);

export const isArc = (c: Arc) => Math.abs(c.rise) > 1e-6 && len(sub(c.b, c.a)) > 1e-9;

function frame(c: Arc) {
  const chord = len(sub(c.b, c.a));
  const d = { x: (c.b.x - c.a.x) / chord, y: (c.b.y - c.a.y) / chord };
  const left = { x: -d.y, y: d.x };
  const mid = { x: (c.a.x + c.b.x) / 2, y: (c.a.y + c.b.y) / 2 };
  const r = (chord * chord / 4 + c.rise * c.rise) / (2 * Math.abs(c.rise));
  const k = c.rise - Math.sign(c.rise) * r;
  const centre = { x: mid.x + left.x * k, y: mid.y + left.y * k };
  const sense = c.rise > 0 ? -1 : 1;
  const start = Math.atan2(c.a.y - centre.y, c.a.x - centre.x);
  const sweep = 4 * Math.atan((2 * Math.abs(c.rise)) / chord);
  return { chord, d, r, centre, sense, start, sweep };
}

export const arcLength = (c: Arc): number => {
  if (!isArc(c)) return len(sub(c.b, c.a));
  const f = frame(c);
  return f.r * f.sweep;
};

/** The point s along the axis (clamped to it). */
export function pointAt(c: Arc, s: number): Vec2 {
  if (!isArc(c)) { const l = len(sub(c.b, c.a)) || 1; return { x: c.a.x + ((c.b.x - c.a.x) * s) / l, y: c.a.y + ((c.b.y - c.a.y) * s) / l }; }
  const f = frame(c);
  const t = f.start + (f.sense * Math.max(0, Math.min(s, f.r * f.sweep))) / f.r;
  return { x: f.centre.x + Math.cos(t) * f.r, y: f.centre.y + Math.sin(t) * f.r };
}

/** The axis's direction at s. */
export function tangentAt(c: Arc, s: number): Vec2 {
  if (!isArc(c)) { const l = len(sub(c.b, c.a)) || 1; return { x: (c.b.x - c.a.x) / l, y: (c.b.y - c.a.y) / l }; }
  const f = frame(c);
  const t = f.start + (f.sense * Math.max(0, Math.min(s, f.r * f.sweep))) / f.r;
  return { x: -Math.sin(t) * f.sense, y: Math.cos(t) * f.sense };
}

/** Where the point nearest p lies along the axis, between its ends. */
export function project(c: Arc, p: Vec2): number {
  const l = arcLength(c);
  if (!isArc(c)) { const d = tangentAt(c, 0); return Math.max(0, Math.min(l, (p.x - c.a.x) * d.x + (p.y - c.a.y) * d.y)); }
  const f = frame(c);
  let turned = f.sense * (Math.atan2(p.y - f.centre.y, p.x - f.centre.x) - f.start);
  turned -= 2 * Math.PI * Math.floor(turned / (2 * Math.PI));
  if (turned <= f.sweep) return turned * f.r;
  return turned > f.sweep + (2 * Math.PI - f.sweep) / 2 ? 0 : l;
}

/** The axis drawn as a line (steps of at most 5°). */
export function axisLine(c: Arc): Vec2[] {
  if (!isArc(c)) return [c.a, c.b];
  const f = frame(c);
  const n = Math.max(2, Math.ceil(f.sweep / (Math.PI / 36)));
  const l = f.r * f.sweep;
  return Array.from({ length: n + 1 }, (_, i) => pointAt(c, (l * i) / n));
}

/** The middle of the axis (on the arc). */
export const middle = (c: Arc): Vec2 => pointAt(c, arcLength(c) / 2);

/** The rise of the arc through a, b and p (0: in line). */
export function riseThrough(a: Vec2, b: Vec2, p: Vec2): number {
  const chord = sub(b, a);
  const c = len(chord);
  if (c < 1e-9) return 0;
  const d = { x: chord.x / c, y: chord.y / c };
  const mid = { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
  const side = (p.x - mid.x) * -d.y + (p.y - mid.y) * d.x;
  if (Math.abs(side) < 1e-9) return 0;
  const along = (p.x - mid.x) * d.x + (p.y - mid.y) * d.y;
  const k = (along * along + side * side - (c * c) / 4) / (2 * side);
  return k + Math.sign(side) * Math.sqrt((c * c) / 4 + k * k);
}

/** A node's axis: its start, end and arc. */
export function arcOf(props: Record<string, unknown>): Arc | null {
  const a = props.start, b = props.end;
  if (!Array.isArray(a) || !Array.isArray(b)) return null;
  return { a: { x: Number(a[0]), y: Number(a[1]) }, b: { x: Number(b[0]), y: Number(b[1]) }, rise: typeof props.arc === 'number' ? props.arc : 0 };
}
