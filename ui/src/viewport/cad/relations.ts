/**
 * Parallel and tangent as inferences while drawing (decision 520, docs/plan-studio-parametric.md §4.4): a line drawn
 * near a drafted line, in a direction within a few degrees of it, locks to it (a parallel hint); a line begun on an arc or a
 * circle, or ending where it would just touch one, locks to the tangent (a tangent hint). The lock says what held the point
 * (`to`: the shape and part), and the recorded constraints (recorded.ts) keep it. Pure.
 */

import type { Vec2 } from '../../platform/module';
import type { Ref } from './constraints';
import type { Shape } from './shapes';

/** A drafted straight side (a line, or one side of a polyline) a new line may be held parallel to. */
export interface SideRef { ref: Ref; a: Vec2; b: Vec2 }
/** A drafted arc or circle a new line may be held tangent to. */
export interface CurveRef { ref: Ref; c: Vec2; r: number }
export interface Related { point: Vec2; lock: 'parallel' | 'tangent'; to: Ref; guide: { from: Vec2; to: Vec2 } }

/** How far off the direction still holds (degrees). */
export const RELATION_DEG = 3;

const len = (v: Vec2) => Math.hypot(v.x, v.y);

/** The sides and curves of the drafted shapes of a view, with the parts a constraint names them by. */
export function relatables(drafts: ReadonlyMap<string, readonly Shape[]>): { sides: SideRef[]; curves: CurveRef[] } {
  const sides: SideRef[] = [], curves: CurveRef[] = [];
  for (const [id, list] of drafts) for (const s of list) {
    if (s.type === 'line') sides.push({ ref: { shape: id, part: 'whole' }, a: s.a, b: s.b });
    else if (s.type === 'polyline') {
      const n = s.closed ? s.pts.length : s.pts.length - 1;
      for (let i = 0; i < n; i++) sides.push({ ref: { shape: id, part: `edge:${i}` }, a: s.pts[i], b: s.pts[(i + 1) % s.pts.length] });
    } else if (s.type === 'circle' || s.type === 'arc') curves.push({ ref: { shape: id, part: 'whole' }, c: s.c, r: s.r });
  }
  return { sides, curves };
}

/** The distance from `p` to the segment a–b. */
function segDist(p: Vec2, a: Vec2, b: Vec2): number {
  const dx = b.x - a.x, dy = b.y - a.y, l2 = dx * dx + dy * dy;
  const t = l2 < 1e-18 ? 0 : Math.max(0, Math.min(1, ((p.x - a.x) * dx + (p.y - a.y) * dy) / l2));
  return Math.hypot(p.x - a.x - t * dx, p.y - a.y - t * dy);
}

/** The cursor held on the line through `base` along `dir` when its direction is within the angle tolerance; null otherwise. */
function along(base: Vec2, raw: Vec2, dir: Vec2, tol: number): Vec2 | null {
  const l = len(dir), d = Math.hypot(raw.x - base.x, raw.y - base.y);
  if (l < 1e-12 || d <= tol) return null;
  const u = { x: dir.x / l, y: dir.y / l };
  const t = (raw.x - base.x) * u.x + (raw.y - base.y) * u.y;
  const off = Math.abs((raw.x - base.x) * u.y - (raw.y - base.y) * u.x);
  if (off / d > Math.sin((RELATION_DEG * Math.PI) / 180)) return null;
  return { x: base.x + u.x * t, y: base.y + u.y * t };
}

/**
 * The cursor held parallel to a drafted side near it: `reach` is how near (the side must pass within it of the cursor), `tol` the
 * snap's own tolerance. A side the base lies on the line of is left out (that would be a continuation).
 */
export function parallelHold(base: Vec2, raw: Vec2, sides: readonly SideRef[], reach: number, tol: number): Related | null {
  let best: { r: Related; d: number } | null = null;
  for (const s of sides) {
    const dir = { x: s.b.x - s.a.x, y: s.b.y - s.a.y };
    const l = len(dir);
    if (l < 1e-9 || segDist(raw, s.a, s.b) > reach) continue;
    if (Math.abs(((base.x - s.a.x) * dir.y - (base.y - s.a.y) * dir.x) / l) < tol / 10) continue;
    const q = along(base, raw, dir, tol);
    if (!q) continue;
    const d = segDist(raw, s.a, s.b);
    if (!best || d < best.d) best = { d, r: { point: q, lock: 'parallel', to: s.ref, guide: { from: { x: (s.a.x + s.b.x) / 2, y: (s.a.y + s.b.y) / 2 }, to: q } } };
  }
  return best?.r ?? null;
}

/**
 * The cursor held tangent to a drafted curve: along the tangent at the base when the base lies on it, or at the point where a
 * line from the base would just touch it (the cursor within `tol` of that point).
 */
export function tangentHold(base: Vec2, raw: Vec2, curves: readonly CurveRef[], tol: number): Related | null {
  let best: { r: Related; d: number } | null = null;
  const offer = (r: Related, d: number) => { if (!best || d < best.d) best = { r, d }; };
  for (const k of curves) {
    const rx = base.x - k.c.x, ry = base.y - k.c.y, dc = Math.hypot(rx, ry);
    if (Math.abs(dc - k.r) < 1e-6) {
      const q = along(base, raw, { x: -ry, y: rx }, tol);
      if (q) offer({ point: q, lock: 'tangent', to: k.ref, guide: { from: base, to: q } }, Math.hypot(q.x - raw.x, q.y - raw.y));
    } else if (dc > k.r) {
      // The two points from which the base sees the curve edge-on: at ±acos(r/dc) around the direction from the centre to the base.
      const a0 = Math.atan2(ry, rx), da = Math.acos(k.r / dc);
      for (const a of [a0 + da, a0 - da]) {
        const q = { x: k.c.x + k.r * Math.cos(a), y: k.c.y + k.r * Math.sin(a) };
        const d = Math.hypot(q.x - raw.x, q.y - raw.y);
        if (d <= tol) offer({ point: q, lock: 'tangent', to: k.ref, guide: { from: base, to: q } }, d);
      }
    }
  }
  return (best as { r: Related } | null)?.r ?? null;
}
