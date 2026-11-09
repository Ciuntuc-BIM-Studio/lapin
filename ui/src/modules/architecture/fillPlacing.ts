/**
 * A door or window being placed (a tester's wish, Revit's way): Space changes its hand, the side of the wall the cursor
 * is on is the side it opens to, and the plan shows it before the click. Pure geometry, the engine's own frame: the type's
 * x runs along the wall from its start, so with no flip a left-hung pane has its hinges towards the wall's start, and it
 * opens to the wall's left.
 */

import { create } from 'zustand';
import { arcLength, arcOf, pointAt, project, tangentAt, type Arc } from '../../viewport/arc';
import type { Vec2 } from '../../platform/module';

/** Whether the next door or window takes its other hand (Space), kept while placing. */
export const useFillHand = create<{ flipped: boolean }>(() => ({ flipped: false }));
export const flipFillHand = () => useFillHand.setState((s) => ({ flipped: !s.flipped }));

export interface PanelLike { col?: number; row?: number; colSpan?: number; operation?: string }
export interface FillShape { width: number; columns?: number[]; panels?: PanelLike[] }

/** Where along a wall's axis a point falls, and whether it lies on the axis's left. */
export function wallSide(c: Arc, p: Vec2): { along: number; left: boolean } {
  const along = project(c, p);
  const q = pointAt(c, along), t = tangentAt(c, along);
  return { along, left: t.x * (p.y - q.y) - t.y * (p.x - q.x) >= 0 };
}

/** The flips a door or window takes, placed from this point with this hand. */
export function fillFlips(c: Arc | null, p: Vec2 | undefined, hand: boolean): { flipHand: boolean; flipSide: boolean } {
  return { flipHand: hand, flipSide: c && p ? !wallSide(c, p).left : false };
}

/** The hand of an operation, or null when its pane does not turn on a side (fixed, sliding, top-hung). */
const hingesAtLeft = (op: string | undefined): boolean | null =>
  op && /^(swing|double-acting|folding|side|tilt-turn)-left$/.test(op) ? true : op && /^(swing|double-acting|folding|side|tilt-turn)-right$/.test(op) ? false : null;

/**
 * The opening as lines on the plan: its jambs and sill across the wall, a tick to the side it opens to, and each pane
 * that turns on a side as its leaf and its arc.
 */
export function fillGhost(props: Record<string, unknown>, at: Vec2, thickness: number, shape: FillShape, hand: boolean): Vec2[][] {
  const c = arcOf(props);
  if (!c) return [];
  const { along, left } = wallSide(c, at);
  const w = shape.width;
  const s = Math.min(Math.max(along, Math.min(w / 2, arcLength(c) / 2)), Math.max(arcLength(c) - w / 2, arcLength(c) / 2));
  const o = pointAt(c, s), t = tangentAt(c, s);
  const n = { x: -t.y * (left ? 1 : -1), y: t.x * (left ? 1 : -1) };
  const P = (x: number, y: number): Vec2 => ({ x: o.x + t.x * x + n.x * y, y: o.y + t.y * x + n.y * y });
  const half = thickness / 2;
  const out: Vec2[][] = [
    [P(-w / 2, -half), P(-w / 2, half)],
    [P(w / 2, -half), P(w / 2, half)],
    [P(-w / 2, 0), P(w / 2, 0)],
    [P(0, half), P(0, half + Math.min(0.2, w / 4))],
  ];
  const columns = shape.columns?.length ? shape.columns : [1];
  const sum = columns.reduce((a, b) => a + b, 0) || 1;
  const edge = (k: number) => -w / 2 + (w * columns.slice(0, k).reduce((a, b) => a + b, 0)) / sum;
  for (const p of shape.panels ?? []) {
    const atLeft = hingesAtLeft(p.operation);
    if (atLeft === null || (p.row ?? 0) !== 0) continue;
    const col = p.col ?? 0;
    let x0 = edge(col), x1 = edge(col + (p.colSpan ?? 1));
    if (hand) [x0, x1] = [-x1, -x0];
    const hingeLow = atLeft !== hand;
    const hinge = hingeLow ? x0 : x1, free = hingeLow ? x1 : x0;
    const r = Math.abs(x1 - x0);
    out.push([P(hinge, half), P(hinge, half + r)]);
    const arc: Vec2[] = [];
    for (let i = 0; i <= 16; i++) {
      const a = (i / 16) * (Math.PI / 2);
      arc.push(P(hinge + (free - hinge) * Math.sin(a), half + r * Math.cos(a)));
    }
    out.push(arc);
  }
  return out;
}
