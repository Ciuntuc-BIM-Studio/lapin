/**
 * Smart Magnets (`= | =`) (docs/plan-ux-game-like.md, P4):
 * detects when the cursor / placed element is equidistant between two
 * neighboring elements along X or Y on the active storey, returning
 * visual `= | =` equality spans to render over the plan.
 */

import type { Vec2 } from '../platform/module';
import type { NodeDto } from '../engine/protocol';

export interface MagnetSpan {
  axis: 'x' | 'y';
  a: Vec2;
  mid: Vec2;
  b: Vec2;
}

/** Extracts reference anchor points (column centres, wall endpoints) from storey nodes. */
export function storeyAnchorPoints(nodes: Iterable<NodeDto>, storeyId: string | null): Vec2[] {
  const pts: Vec2[] = [];
  for (const n of nodes) {
    if (storeyId && n.storeyId !== storeyId) continue;
    if (n.type === 'IfcColumn' && typeof n.props.x === 'number' && typeof n.props.y === 'number') {
      pts.push({ x: n.props.x, y: n.props.y });
    } else if (n.type === 'IfcWall' && Array.isArray(n.props.start) && Array.isArray(n.props.end)) {
      pts.push({ x: Number(n.props.start[0]), y: Number(n.props.start[1]) });
      pts.push({ x: Number(n.props.end[0]), y: Number(n.props.end[1]) });
    }
  }
  return pts;
}

/**
 * Finds the closest `= | =` equal-spacing magnet along X and/or Y where `cursor`
 * sits at the midpoint between two distinct anchor points (`a` and `b`) within `tol` metres.
 */
export function equalSpacingMagnets(cursor: Vec2, anchors: readonly Vec2[], tol = 0.08): MagnetSpan[] {
  const out: MagnetSpan[] = [];
  let bestX: { d: number; span: MagnetSpan } | null = null;
  let bestY: { d: number; span: MagnetSpan } | null = null;

  for (let i = 0; i < anchors.length; i++) {
    for (let j = i + 1; j < anchors.length; j++) {
      const a = anchors[i], b = anchors[j];
      const dx = Math.abs(b.x - a.x), dy = Math.abs(b.y - a.y);
      if (dx > 0.4 && dy < 0.2 && Math.abs(cursor.y - (a.y + b.y) / 2) < 0.35) {
        const mx = (a.x + b.x) / 2;
        const err = Math.abs(cursor.x - mx);
        if (err <= tol && (!bestX || err < bestX.d)) {
          bestX = { d: err, span: { axis: 'x', a, mid: { x: mx, y: (a.y + b.y) / 2 }, b } };
        }
      }
      if (dy > 0.4 && dx < 0.2 && Math.abs(cursor.x - (a.x + b.x) / 2) < 0.35) {
        const my = (a.y + b.y) / 2;
        const err = Math.abs(cursor.y - my);
        if (err <= tol && (!bestY || err < bestY.d)) {
          bestY = { d: err, span: { axis: 'y', a, mid: { x: (a.x + b.x) / 2, y: my }, b } };
        }
      }
    }
  }
  if (bestX) out.push(bestX.span);
  if (bestY) out.push(bestY.span);
  return out;
}
