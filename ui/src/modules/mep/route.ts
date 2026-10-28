/**
 * A run's route edited after it is placed (decision 164): the run a click
 * falls on, the leg under it, a detour's points kept on that leg and the
 * bypass it makes (what the engine's mep.detour makes, drawn as the cursor
 * moves). The engine does the edit; this only aims and previews it.
 */

import type { NodeDto } from '../../engine/protocol';
import type { Vec2 } from '../../platform/module';
import { isRun, runPoints } from './network';

export interface Leg { leg: number; a: Vec2; b: Vec2; dist: number }

/** The leg of a polyline nearest a point. */
export function legAt(pts: Vec2[], p: Vec2): Leg | null {
  let best: Leg | null = null;
  for (let i = 0; i + 1 < pts.length; i++) {
    const a = pts[i], b = pts[i + 1];
    const dx = b.x - a.x, dy = b.y - a.y, l2 = dx * dx + dy * dy;
    const t = l2 < 1e-18 ? 0 : Math.max(0, Math.min(1, ((p.x - a.x) * dx + (p.y - a.y) * dy) / l2));
    const d = Math.hypot(p.x - (a.x + dx * t), p.y - (a.y + dy * t));
    if (!best || d < best.dist) best = { leg: i, a, b, dist: d };
  }
  return best;
}

/** A point on the leg's line, within the leg. */
export function onLeg(l: Leg, p: Vec2): Vec2 {
  const dx = l.b.x - l.a.x, dy = l.b.y - l.a.y, l2 = dx * dx + dy * dy;
  const t = l2 < 1e-18 ? 0 : Math.max(0, Math.min(1, ((p.x - l.a.x) * dx + (p.y - l.a.y) * dy) / l2));
  return { x: l.a.x + dx * t, y: l.a.y + dy * t };
}

/** The signed distance of a point from the leg (+: the leg's left). */
export function sideOf(l: Leg, p: Vec2): number {
  const dx = l.b.x - l.a.x, dy = l.b.y - l.a.y, len = Math.hypot(dx, dy) || 1;
  return ((p.x - l.a.x) * -dy + (p.y - l.a.y) * dx) / len;
}

/** The point square off the leg through `at` that `p` falls on (a detour's side, its distance typed along it). */
export function squareOff(l: Leg, at: Vec2, p: Vec2): Vec2 {
  const dx = l.b.x - l.a.x, dy = l.b.y - l.a.y, len = Math.hypot(dx, dy) || 1;
  const nx = -dy / len, ny = dx / len;
  const s = (p.x - at.x) * nx + (p.y - at.y) * ny;
  return { x: at.x + nx * s, y: at.y + ny * s };
}

/** The bypass a detour makes between two points of a leg, out to the side of `side`: in, out, along, back. */
export function bypass(l: Leg, from: Vec2, to: Vec2, side: Vec2): Vec2[] {
  const p0 = onLeg(l, from), p1 = onLeg(l, to);
  const dx = l.b.x - l.a.x, dy = l.b.y - l.a.y, len = Math.hypot(dx, dy) || 1;
  const off = sideOf(l, side);
  const o = { x: (-dy / len) * off, y: (dx / len) * off };
  return [p0, { x: p0.x + o.x, y: p0.y + o.y }, { x: p1.x + o.x, y: p1.y + o.y }, p1];
}

/** The run a point falls on (within tol): the selected one first, else the nearest of all. */
export function runNear(nodes: Map<string, NodeDto>, selected: string[], p: Vec2, tol: number): { run: NodeDto; leg: Leg } | null {
  let best: { run: NodeDto; leg: Leg } | null = null;
  const look = (n: NodeDto | undefined) => {
    if (!n || !isRun(n)) return;
    const l = legAt(runPoints(n), p);
    if (l && l.dist <= tol && (!best || l.dist < best.leg.dist)) best = { run: n, leg: l };
  };
  for (const id of selected) look(nodes.get(id));
  if (best) return best;
  for (const n of nodes.values()) look(n);
  return best;
}
