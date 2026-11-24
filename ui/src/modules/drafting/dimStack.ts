/**
 * Dimension lines stacked at a set distance (forum topic 236, as Revit does): a dimension line dragged near a parallel one
 * on the same view snaps at the stacking spacing from it, on either side, with a guide along the line it keeps its distance
 * from. The spacing is the drafting standards' distance between dimension strings (Dimension rules → Spacing, mm on
 * paper; 7 mm when none is set), so hand-placed strings stand as the generated ones do. Pure over the nodes.
 */

import type { NodeDto } from '../../engine/protocol';
import type { Vec2 } from '../../platform/module';

/** The stacking distance on paper (mm) when the project sets none: the dimension rules' default. */
export const STACK_SPACING_MM = 7;
/** How near (mm on paper) the cursor must come to the stacked place to snap onto it. */
export const STACK_CAPTURE_MM = 2;

/** A linear dimension's line: its direction `u`, its normal `n`, a point on it, and how far along `u` its points reach. */
export interface DimLine { id: string; viewId: string | null; u: Vec2; n: Vec2; at: Vec2; lo: number; hi: number }

const P = (raw: unknown): Vec2 | null => (Array.isArray(raw) && typeof raw[0] === 'number' && typeof raw[1] === 'number' ? { x: raw[0], y: raw[1] } : null);
const dot = (a: Vec2, b: Vec2) => a.x * b.x + a.y * b.y;

/** A linear or aligned dimension's line from its stored points, `at` and `dir`; null for another node (an angle, a radius…). */
export function dimLineOf(node: NodeDto | undefined): DimLine | null {
  if (!node || node.props.predefinedType !== 'DIMENSION' || node.props.dimKind) return null;
  const raw = Array.isArray(node.props.pts) ? node.props.pts : [node.props.a, node.props.b];
  const pts = raw.map(P).filter((p): p is Vec2 => !!p);
  if (pts.length < 2) return null;
  const dir = P(node.props.dir);
  const d = dir ?? { x: pts[pts.length - 1].x - pts[0].x, y: pts[pts.length - 1].y - pts[0].y };
  const l = Math.hypot(d.x, d.y);
  if (l < 1e-9) return null;
  const u = { x: d.x / l, y: d.y / l };
  const s = pts.map((p) => dot(p, u));
  return { id: node.id, viewId: typeof node.props.viewId === 'string' ? node.props.viewId : null, u, n: { x: -u.y, y: u.x }, at: P(node.props.at) ?? pts[0], lo: Math.min(...s), hi: Math.max(...s) };
}

/** The drafting standards' spacing between dimension strings (mm on paper): the exterior rules', else the default. */
export function stackSpacingMm(nodes: ReadonlyMap<string, NodeDto>): number {
  for (const n of nodes.values()) {
    if (n.type !== 'IfcProject') continue;
    const v = (n.props.dimensionRules as { exterior?: { spacing?: unknown } } | undefined)?.exterior?.spacing;
    return typeof v === 'number' && v > 0 ? v : STACK_SPACING_MM;
  }
  return STACK_SPACING_MM;
}

export interface StackSnap {
  /** Where the dragged line goes through. */
  at: Vec2;
  /** The parallel dimension it keeps its distance from, and that line drawn over both strings' reach (the guide). */
  from: string;
  guide: { a: Vec2; b: Vec2 };
}

/**
 * Where a dimension line dragged to `to` snaps: at `spacing` (model metres) from a parallel dimension's line on the same view
 * whose reach along it overlaps its own, when the cursor is within `capture` of that place; the nearest such, else null.
 */
export function stackSnap(own: DimLine, others: readonly DimLine[], to: Vec2, spacing: number, capture: number): StackSnap | null {
  const c = dot(to, own.n);
  let best: { d: number; at: number; other: DimLine } | null = null;
  for (const o of others) {
    if (o.id === own.id || o.viewId !== own.viewId) continue;
    if (Math.abs(own.u.x * o.u.y - own.u.y * o.u.x) > 0.01) continue;
    // Their points' reach along the line overlaps (a string beside another, not one far along).
    const lo = Math.min(dot({ x: o.u.x * o.lo, y: o.u.y * o.lo }, own.u), dot({ x: o.u.x * o.hi, y: o.u.y * o.hi }, own.u));
    const hi = Math.max(dot({ x: o.u.x * o.lo, y: o.u.y * o.lo }, own.u), dot({ x: o.u.x * o.hi, y: o.u.y * o.hi }, own.u));
    if (hi < own.lo - spacing || lo > own.hi + spacing) continue;
    const line = dot(o.at, own.n);
    for (const place of [line - spacing, line + spacing]) {
      const d = Math.abs(c - place);
      if (d <= capture && (!best || d < best.d)) best = { d, at: place, other: o };
    }
  }
  if (!best) return null;
  const at = { x: to.x + own.n.x * (best.at - c), y: to.y + own.n.y * (best.at - c) };
  // The guide: the other line, over both strings' reach.
  const line = dot(best.other.at, own.n);
  const from = Math.min(own.lo, ...[best.other.lo, best.other.hi].map((s) => s * dot(best!.other.u, own.u)));
  const till = Math.max(own.hi, ...[best.other.lo, best.other.hi].map((s) => s * dot(best!.other.u, own.u)));
  const on = (s: number): Vec2 => ({ x: own.u.x * s + own.n.x * line, y: own.u.y * s + own.n.y * line });
  return { at, from: best.other.id, guide: { a: on(from), b: on(till) } };
}

/** The view's scale (1:100 → 100), to turn paper millimetres into the model's metres. */
export function viewScale(nodes: ReadonlyMap<string, NodeDto>, viewId: string | null): number {
  const s = viewId ? nodes.get(viewId)?.props.scale : undefined;
  return typeof s === 'number' && s > 0 ? s : 100;
}

/** A dragged dimension line's stacked place for the nodes as they are (null: none near). */
export function stackedAt(nodes: ReadonlyMap<string, NodeDto>, id: string, to: Vec2): StackSnap | null {
  const own = dimLineOf(nodes.get(id));
  if (!own) return null;
  const others: DimLine[] = [];
  for (const n of nodes.values()) { if (n.id !== id && n.props.viewId === own.viewId) { const l = dimLineOf(n); if (l) others.push(l); } }
  const mm = viewScale(nodes, own.viewId) / 1000;
  return stackSnap(own, others, to, stackSpacingMm(nodes) * mm, STACK_CAPTURE_MM * mm);
}
