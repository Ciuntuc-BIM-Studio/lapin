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

export function arcFrame(c: Arc) {
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

const frame = arcFrame;

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

/**
 * The axis as a drafted shape, exact (the snaps' own: forum topic 151): a line, or the arc from its centre and radius, anticlockwise
 * from `start` to `end` in radians (as cad/shapes.ts has arcs).
 */
export function arcShape(c: Arc): { type: 'line'; a: Vec2; b: Vec2 } | { type: 'arc'; c: Vec2; r: number; start: number; end: number } {
  if (!isArc(c)) return { type: 'line', a: c.a, b: c.b };
  const f = frame(c);
  const norm = (a: number) => { const r = a % (2 * Math.PI); return r < 0 ? r + 2 * Math.PI : r; };
  const ta = norm(f.start), tb = norm(Math.atan2(c.b.y - f.centre.y, c.b.x - f.centre.x));
  // Anticlockwise (sense +1) from a to b; clockwise from a to b is anticlockwise from b to a.
  return f.sense > 0 ? { type: 'arc', c: f.centre, r: f.r, start: ta, end: tb } : { type: 'arc', c: f.centre, r: f.r, start: tb, end: ta };
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

/**
 * Construct an arc from centre, start point, and end point (forum topic 293).
 * The end point is projected onto the circle of radius |start - centre|.
 * Returns null if start and end coincide (full circle) or radius is degenerate.
 */
export function arcFromCenterStartEnd(center: Vec2, start: Vec2, end: Vec2, ccw = true): Arc | null {
  const r = len(sub(start, center));
  if (r < 1e-6) return null;
  const a0 = Math.atan2(start.y - center.y, start.x - center.x);
  const a1 = Math.atan2(end.y - center.y, end.x - center.x);
  let sweep = ccw ? a1 - a0 : a0 - a1;
  sweep -= 2 * Math.PI * Math.floor(sweep / (2 * Math.PI));
  if (sweep < 1e-4 || Math.abs(sweep - 2 * Math.PI) < 1e-4) return null;
  const b: Vec2 = { x: center.x + Math.cos(a1) * r, y: center.y + Math.sin(a1) * r };
  const midAngle = a0 + (ccw ? 1 : -1) * (sweep / 2);
  const midPt: Vec2 = { x: center.x + Math.cos(midAngle) * r, y: center.y + Math.sin(midAngle) * r };
  return { a: start, b, rise: riseThrough(start, b, midPt) };
}

/** Split a full circle (given centre and a start point on the perimeter) into two semicircles for path elements. */
export function circleAsTwoArcs(center: Vec2, start: Vec2): [Arc, Arc] | null {
  const r = len(sub(start, center));
  if (r < 1e-6) return null;
  const opp: Vec2 = { x: 2 * center.x - start.x, y: 2 * center.y - start.y };
  return [
    { a: start, b: opp, rise: r },
    { a: opp, b: start, rise: r },
  ];
}

/**
 * Adjust a curved element's radius while maintaining its centre and start/end angles (forum topic 293).
 */
export function arcWithRadius(c: Arc, newRadius: number): Arc | null {
  if (!isArc(c) || !Number.isFinite(newRadius) || newRadius < 1e-4) return null;
  const f = arcFrame(c);
  const a0 = f.start;
  const a1 = f.start + f.sense * f.sweep;
  const aMid = f.start + f.sense * (f.sweep / 2);
  const a: Vec2 = { x: f.centre.x + Math.cos(a0) * newRadius, y: f.centre.y + Math.sin(a0) * newRadius };
  const b: Vec2 = { x: f.centre.x + Math.cos(a1) * newRadius, y: f.centre.y + Math.sin(a1) * newRadius };
  const mid: Vec2 = { x: f.centre.x + Math.cos(aMid) * newRadius, y: f.centre.y + Math.sin(aMid) * newRadius };
  return { a, b, rise: riseThrough(a, b, mid) };
}

/**
 * Adjust an arc's start or end angle by dragging an endpoint or entering a sweep angle, maintaining its centre and radius (forum topic 293).
 * Sets `fullCircle: true` when the dragged endpoint meets the opposite endpoint.
 */
export function arcWithEndAngle(c: Arc, which: 'start' | 'end', target: Vec2, sweepOverrideDeg?: number): { arc: Arc; fullCircle: boolean } {
  if (!isArc(c)) return { arc: c, fullCircle: false };
  const f = arcFrame(c);
  const a0 = f.start;
  const a1 = f.start + f.sense * f.sweep;
  const ptAt = (ang: number): Vec2 => ({ x: f.centre.x + Math.cos(ang) * f.r, y: f.centre.y + Math.sin(ang) * f.r });
  let nextA0 = a0, nextA1 = a1;
  let newSweep: number;
  if (sweepOverrideDeg != null && Number.isFinite(sweepOverrideDeg)) {
    const sw = Math.max(0.01, Math.min(359.9, Math.abs(sweepOverrideDeg))) * (Math.PI / 180);
    newSweep = sw;
    if (which === 'end') nextA1 = a0 + f.sense * sw;
    else nextA0 = a1 - f.sense * sw;
  } else {
    const ang = Math.atan2(target.y - f.centre.y, target.x - f.centre.x);
    if (which === 'end') {
      newSweep = f.sense * (ang - a0);
      newSweep -= 2 * Math.PI * Math.floor(newSweep / (2 * Math.PI));
      nextA1 = a0 + f.sense * newSweep;
    } else {
      newSweep = f.sense * (a1 - ang);
      newSweep -= 2 * Math.PI * Math.floor(newSweep / (2 * Math.PI));
      nextA0 = a1 - f.sense * newSweep;
    }
  }
  const fullCircle = newSweep < 0.035 || Math.abs(newSweep - 2 * Math.PI) < 0.035;
  if (fullCircle) return { arc: c, fullCircle: true };
  const a = ptAt(nextA0);
  const b = ptAt(nextA1);
  const mid = ptAt(nextA0 + f.sense * (newSweep / 2));
  return { arc: { a, b, rise: riseThrough(a, b, mid) }, fullCircle: false };
}

/**
 * Construct an arc starting at `start` tangentially along `tangentDir` and ending at `end` (forum topic 293).
 * Returns null if `end` lies on the tangent line (straight segment).
 */
export function arcTangentFrom(start: Vec2, tangentDir: Vec2, end: Vec2): Arc | null {
  const tl = len(tangentDir);
  if (tl < 1e-9) return null;
  const t = { x: tangentDir.x / tl, y: tangentDir.y / tl };
  const n = { x: -t.y, y: t.x };
  const v = sub(end, start);
  const v2 = v.x * v.x + v.y * v.y;
  if (v2 < 1e-12) return null;
  const vn = v.x * n.x + v.y * n.y;
  if (Math.abs(vn) < 1e-6 * Math.sqrt(v2)) return null;
  const k = v2 / (2 * vn);
  const centre: Vec2 = { x: start.x + n.x * k, y: start.y + n.y * k };
  const ccw = k > 0;
  return arcFromCenterStartEnd(centre, start, end, ccw);
}

/**
 * Solve for a circle tangent to two lines (`l1`, `l2`) and passing through `pt` (forum topic 293).
 * Returns the circle `{ c, r }` and the two tangent contact points `{ t1, t2 }` on `l1` and `l2`.
 */
export function circleFromTwoTangentsAndPoint(
  l1: { a: Vec2; b: Vec2 },
  l2: { a: Vec2; b: Vec2 },
  pt: Vec2,
): { c: Vec2; r: number; t1: Vec2; t2: Vec2 } | null {
  const d1 = sub(l1.b, l1.a), d2 = sub(l2.b, l2.a);
  const len1 = len(d1), len2 = len(d2);
  if (len1 < 1e-9 || len2 < 1e-9) return null;
  const u1 = { x: d1.x / len1, y: d1.y / len1 };
  const u2 = { x: d2.x / len2, y: d2.y / len2 };
  const n1 = { x: -u1.y, y: u1.x };
  const n2 = { x: -u2.y, y: u2.x };
  const projectOnto = (line: { a: Vec2; b: Vec2 }, u: Vec2, p: Vec2): Vec2 => {
    const t = (p.x - line.a.x) * u.x + (p.y - line.a.y) * u.y;
    return { x: line.a.x + u.x * t, y: line.a.y + u.y * t };
  };
  const cross = u1.x * u2.y - u1.y * u2.x;
  if (Math.abs(cross) < 1e-7) {
    // Parallel lines: radius is half the distance between them.
    const dist = Math.abs((l2.a.x - l1.a.x) * n1.x + (l2.a.y - l1.a.y) * n1.y);
    const r = dist / 2;
    if (r < 1e-6) return null;
    const sign = (l2.a.x - l1.a.x) * n1.x + (l2.a.y - l1.a.y) * n1.y >= 0 ? 1 : -1;
    const midO = { x: l1.a.x + n1.x * sign * r, y: l1.a.y + n1.y * sign * r };
    const perp = (pt.x - midO.x) * n1.x + (pt.y - midO.y) * n1.y;
    if (Math.abs(perp) > r + 1e-6) return null;
    const along = (pt.x - midO.x) * u1.x + (pt.y - midO.y) * u1.y;
    const delta = Math.sqrt(Math.max(0, r * r - perp * perp));
    const c = { x: midO.x + u1.x * (along - delta), y: midO.y + u1.y * (along - delta) };
    return { c, r, t1: projectOnto(l1, u1, c), t2: projectOnto(l2, u2, c) };
  }
  // Intersecting lines at O.
  const w = sub(l2.a, l1.a);
  const tInt = (w.x * u2.y - w.y * u2.x) / cross;
  const O: Vec2 = { x: l1.a.x + u1.x * tInt, y: l1.a.y + u1.y * tInt };
  const dp = sub(pt, O);
  // Test all 4 quadrant bisectors (±u1 + ±u2); pick the valid circle with smallest radius containing pt.
  const candidates: { c: Vec2; r: number }[] = [];
  for (const s1 of [1, -1]) {
    for (const s2 of [1, -1]) {
      const bx = s1 * u1.x + s2 * u2.x, by = s1 * u1.y + s2 * u2.y;
      const bl = Math.hypot(bx, by);
      if (bl < 1e-9) continue;
      const b = { x: bx / bl, y: by / bl };
      // sin(alpha) = |b × u1|
      const sinA = Math.abs(b.x * u1.y - b.y * u1.x);
      const cos2A = Math.max(0, 1 - sinA * sinA);
      if (cos2A < 1e-12) continue;
      const dot = dp.x * b.x + dp.y * b.y;
      const dp2 = dp.x * dp.x + dp.y * dp.y;
      const disc = dot * dot - cos2A * dp2;
      if (disc < -1e-9) continue;
      const sq = Math.sqrt(Math.max(0, disc));
      for (const s of [(dot - sq) / cos2A, (dot + sq) / cos2A]) {
        if (s <= 1e-6) continue;
        const r = s * sinA;
        const c = { x: O.x + b.x * s, y: O.y + b.y * s };
        // Verify distance to both lines matches r.
        const dLine1 = Math.abs((c.x - l1.a.x) * n1.x + (c.y - l1.a.y) * n1.y);
        const dLine2 = Math.abs((c.x - l2.a.x) * n2.x + (c.y - l2.a.y) * n2.y);
        if (Math.abs(dLine1 - r) < 1e-4 && Math.abs(dLine2 - r) < 1e-4) candidates.push({ c, r });
      }
    }
  }
  if (!candidates.length) return null;
  candidates.sort((a, b) => a.r - b.r);
  const best = candidates[0];
  return { c: best.c, r: best.r, t1: projectOnto(l1, u1, best.c), t2: projectOnto(l2, u2, best.c) };
}
