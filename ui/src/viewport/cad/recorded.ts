/**
 * Constraints recorded while drawing (stage C2 of decision 520, docs/plan-studio-parametric.md §4.4): a point placed by
 * inference (an object snap, a tracking line) says how it was placed, and the shape drawn from it keeps that as constraints: an
 * endpoint snap is a coincidence, a midpoint snap the midpoint, "on edge" a point on the object, a perpendicular snap a
 * perpendicular; a side drawn along an ortho or polar line is horizontal or vertical; a typed length or angle is a driving
 * dimension; a rectangle is two horizontal and two vertical sides. Ctrl held on a click places that point free. Pure: the
 * session gathers what was placed and sends the shapes with what this returns as one command (`studio.drawConstrained`,
 * `drawConstrainedArgs`), so one Undo takes a shape and its constraints together.
 */

import type { Vec2 } from '../../platform/module';
import { normAngleDeg, pointOf, type Ref } from './constraints';
import { dist, nearestOnSegment, prims, type Shape } from './shapes';

export type InferenceLock = 'ortho' | 'polar' | 'tracking';
/** How a point was found: the snap that took it (and the drafted shape it is on), the line that held it. */
export interface Inference { mode?: string; shapeId?: string; lock?: InferenceLock }
/** A point put down: where, how it was found, whether Ctrl held it free, what was typed for it (a length in metres, an angle in degrees). */
export interface PlacedPoint { p: Vec2; inference: Inference | null; free: boolean; typed?: { length?: number; angle?: number } }
export interface Drawn { id: string; shape: Shape }
export interface ConstraintSpec { kind: string; refs: Ref[]; value?: number }
export interface DrawHints { rect?: boolean; radius?: number }

const EPS = 1e-7;
const near = (a: Vec2, b: Vec2) => dist(a, b) < EPS;

const pointParts = (s: Shape): string[] => (s.type === 'line' ? ['start', 'end'] : s.type === 'polyline' ? s.pts.map((_, i) => `vertex:${i}`) : s.type === 'circle' ? ['center'] : ['start', 'end', 'center']);

/** The point part of a shape lying at `p`. */
export const partAt = (s: Shape, p: Vec2): string | null => pointParts(s).find((part) => { const q = pointOf(s, part); return !!q && near(q, p); }) ?? null;

/** The edge or the curve of a shape nearest `p`: a line's whole, a polyline's side, a circle or an arc whole; null when it has none. */
export function edgeNear(s: Shape, p: Vec2): { part: string; line: boolean } | null {
  if (s.type === 'circle' || s.type === 'arc') return { part: 'whole', line: false };
  if (s.type === 'line') return { part: 'whole', line: true };
  let best = -1, bd = Infinity;
  prims(s).forEach((pr, i) => { if (pr.kind !== 'seg') return; const d = nearestOnSegment(p, pr.a, pr.b).dist; if (d < bd) { bd = d; best = i; } });
  return best < 0 ? null : { part: `edge:${best}`, line: true };
}

/** The sides of a drawn line or polyline as [part, from, to, the point that ends it]. */
function sides(s: Shape): { part: string; a: Vec2; b: Vec2; endPart: string }[] {
  if (s.type === 'line') return [{ part: 'whole', a: s.a, b: s.b, endPart: 'end' }];
  if (s.type !== 'polyline') return [];
  const out = [];
  for (let i = 0; i + 1 < s.pts.length; i++) out.push({ part: `edge:${i}`, a: s.pts[i], b: s.pts[i + 1], endPart: `vertex:${i + 1}` });
  return out;
}

/**
 * What a shape just drawn keeps of how its points were placed. `world` is the drafted shapes already there (by id), `prev` the shape
 * drawn just before in the same command (a chain of lines: each starts where the last ended), `placed` the points put down.
 */
export function inferConstraints(drawn: Drawn, placed: readonly PlacedPoint[], world: ReadonlyMap<string, Shape>, o: { prev?: Drawn | null; hints?: DrawHints } = {}): ConstraintSpec[] {
  const out: ConstraintSpec[] = [];
  const s = drawn.shape, id = drawn.id;
  const at = (p: Vec2) => placed.find((x) => near(x.p, p));
  const mine = (part: string): Ref => ({ shape: id, part });

  for (const part of pointParts(s)) {
    const p = pointOf(s, part);
    if (!p) continue;
    const pl = at(p);
    if (pl?.free) continue;
    const inf = pl?.inference ?? null;
    const other = inf?.shapeId && inf.shapeId !== id ? world.get(inf.shapeId) : undefined;
    const otherId = inf?.shapeId;
    let done = false;
    if (other && otherId) {
      switch (inf!.mode) {
        case 'endpoint': case 'center': {
          const q = partAt(other, p);
          if (q) { out.push({ kind: 'coincident', refs: [mine(part), { shape: otherId, part: q }] }); done = true; }
          break;
        }
        case 'midpoint': {
          const e = edgeNear(other, p);
          if (e?.line) { out.push({ kind: 'midpoint', refs: [mine(part), { shape: otherId, part: e.part }] }); done = true; }
          break;
        }
        case 'nearest': case 'quadrant': case 'intersection': case 'perpendicular': {
          const e = edgeNear(other, p);
          if (e) {
            out.push({ kind: 'pointOn', refs: [mine(part), { shape: otherId, part: e.part }] });
            done = true;
            if (inf!.mode === 'perpendicular' && e.line) {
              const side = sides(s).find((x) => x.endPart === part);
              if (side) out.push({ kind: 'perpendicular', refs: [mine(side.part), { shape: otherId, part: e.part }] });
            }
          }
          break;
        }
      }
    }
    // A chain: a line begun where the one before it ended is joined to it.
    if (!done && o.prev && o.prev.id !== id) {
      const q = partAt(o.prev.shape, p);
      if (q && pointOf(o.prev.shape, q)) out.push({ kind: 'coincident', refs: [mine(part), { shape: o.prev.id, part: q }] });
    }
  }

  if (o.hints?.rect && s.type === 'polyline' && s.pts.length === 4 && s.closed) {
    const w = Math.abs(s.pts[1].x - s.pts[0].x), h = Math.abs(s.pts[3].y - s.pts[0].y);
    if (w > EPS && h > EPS) for (let i = 0; i < 4; i++) out.push({ kind: i % 2 === 0 ? 'horizontal' : 'vertical', refs: [mine(`edge:${i}`)] });
    return out;
  }

  for (const side of sides(s)) {
    const pl = at(side.b);
    if (!pl || pl.free) continue;
    const dx = side.b.x - side.a.x, dy = side.b.y - side.a.y;
    let axis = false;
    if (pl.inference?.lock) {
      if (Math.abs(dy) < EPS && Math.abs(dx) > EPS) { out.push({ kind: 'horizontal', refs: [mine(side.part)] }); axis = true; }
      else if (Math.abs(dx) < EPS && Math.abs(dy) > EPS) { out.push({ kind: 'vertical', refs: [mine(side.part)] }); axis = true; }
    }
    const t = pl.typed;
    if (t?.length != null && t.length > EPS) out.push({ kind: 'distance', refs: [mine(side.part)], value: t.length });
    if (t?.angle != null) {
      const measured = normAngleDeg((Math.atan2(dy, dx) * 180) / Math.PI);
      const axisAngle = [0, 90, -90, 180].some((a) => Math.abs(normAngleDeg(measured - a)) < 1e-6);
      // The angle of the plan's own axes only (a user's frame turns the typed one), and not one a horizontal or vertical already holds.
      if (Math.abs(normAngleDeg(measured - t.angle)) < 1e-6 && !(axis && axisAngle)) out.push({ kind: 'angle', refs: [mine(side.part)], value: measured });
    }
  }
  if ((s.type === 'circle' || s.type === 'arc') && o.hints?.radius && o.hints.radius > EPS) out.push({ kind: 'radius', refs: [mine('whole')], value: o.hints.radius });
  return out;
}

/** The views whose drafted shapes build a Studio solid: a sketch (a solid's `profile` is the drafted shape its recipe starts from). */
export function sketchViews(nodes: Iterable<{ id: string; props: Record<string, unknown> }>): Set<string> {
  const list = [...nodes];
  const draft = new Map(list.map((n) => [n.id, n]));
  const out = new Set<string>();
  for (const n of list) {
    if (typeof n.props.operation !== 'string' || !n.props.operation || typeof n.props.profile !== 'string') continue;
    const v = draft.get(n.props.profile)?.props.viewId;
    if (typeof v === 'string') out.add(v);
  }
  return out;
}

// ─── The request: shapes and their constraints, one command ─────────────────

const NEW = '\u0000new:';
/** The id a shape about to be drawn goes by until the engine names it: the `i`-th of the shapes sent. */
export const newShapeId = (i: number): string => `${NEW}${i}`;
/** Which of the shapes sent a placeholder id names; null for a drafted shape's own id. */
export const newShapeIndex = (id: string): number | null => (id.startsWith(NEW) ? Number(id.slice(NEW.length)) : null);

/** A reference as `studio.drawConstrained` takes it: a shape drawn by the same command as `{new: i, part}`. */
export type DrawRef = Ref | { new: number; part: string };

/**
 * The `studio.drawConstrained` arguments: what `drafting.draw` takes (`draw`), and the constraints recorded for the shapes, each
 * reference to one of them (a `newShapeId`) written `{new: i, part}`. The engine draws, adds and solves in one undo step, and
 * reports the constraints the solver refuses instead of refusing the drawing.
 */
export function drawConstrainedArgs<T extends Record<string, unknown>>(draw: T, specs: readonly ConstraintSpec[]): T & { constraints: { kind: string; refs: DrawRef[]; value?: number }[] } {
  const constraints = specs.map((c) => ({
    kind: c.kind,
    refs: c.refs.map((r): DrawRef => { const i = newShapeIndex(r.shape); return i == null ? { shape: r.shape, part: r.part } : { new: i, part: r.part }; }),
    ...(c.value != null ? { value: c.value } : {}),
  }));
  return { ...draw, constraints };
}
