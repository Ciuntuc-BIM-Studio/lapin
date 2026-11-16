/**
 * Offset outline (decision 609; Archicad's "Offset all edges"): every edge of a selected floor's, floor opening's,
 * ceiling's, roof's, room's or balcony's outline moved at once, outward (+) or inward (−): straight edges parallel and
 * mitred, arcs concentric, a corner where an arc meets an edge rounded as it grows. The engine works it out
 * (`outline.offset`, ArcOutline.Offset) and refuses what would vanish or cross itself; the element's own update command
 * writes it, one undo step for the whole selection. Shift-drag on an edge's middle grip does it live (GripLayer); the
 * selection's right-click menu asks the distance (plans and 3D).
 */

import type { GripStep, OutlineOffsetGrip, Vec2 } from '../platform/module';
import type { NodeDto } from '../engine/protocol';
import { query, useSession } from '../engine/session';
import { gripsOf } from '../platform/modules';
import { notify, useUi } from '../app/store';
import { fromPairs, toPairs, type Outline } from './contour';
import { runSteps } from './grips';
import { askLength } from '../shell/AskBox';
import type { MenuItem } from '../shell/ContextMenu';

interface OffsetReply { points?: number[][]; arcs?: number[] | null; from?: number[]; problem?: string }

/** The outline offset by `distance` (m, outward positive), its edges' data following the edges they came from; or why it cannot be. */
export async function offsetOutline<E>(o: Outline<E>, distance: number): Promise<{ outline: Outline<E> } | { problem: string }> {
  const r = await query<OffsetReply>('outline.offset', { points: toPairs(o.pts), ...(o.arcs ? { arcs: o.arcs } : {}), distance });
  if (!r) return { problem: 'The outline could not be offset.' };
  if (r.problem) return { problem: r.problem };
  const pts = fromPairs(r.points);
  const from = r.from?.length === pts.length ? r.from : pts.map((_, i) => i);
  const edges = o.edges ? from.map((k) => structuredClone(o.edges![k])) : undefined;
  return { outline: { pts, ...(edges ? { edges } : {}), ...(r.arcs ? { arcs: r.arcs } : {}) } };
}

/** How far a point is out from an edge's middle along its outward normal: the offset a drag gives. */
export const offsetDistance = (from: Vec2, normal: Vec2, to: Vec2): number => (to.x - from.x) * normal.x + (to.y - from.y) * normal.y;

/** The offset a node's grips lend (its first edge middle's), when its outline may be offset. */
export function offsetGripOf(n: NodeDto | undefined): OutlineOffsetGrip | null {
  if (!n?.editable) return null;
  return gripsOf(n).find((g) => g.offsetAll)?.offsetAll ?? null;
}

/** The selection's elements whose outline may be offset. */
export function offsettable(selected: readonly string[] = useUi.getState().selected, nodes: Map<string, NodeDto> = useSession.getState().nodes): { id: string; grip: OutlineOffsetGrip }[] {
  return selected.flatMap((id) => { const g = offsetGripOf(nodes.get(id)); return g ? [{ id, grip: g }] : []; });
}

/** Each outline offset by `distance` and written, one undo step; the first refusal is told and nothing changes. */
export async function offsetAll(grips: OutlineOffsetGrip[], distance: number): Promise<boolean> {
  const steps: (GripStep | null)[] = [];
  for (const g of grips) {
    const r = await offsetOutline(g.outline, distance);
    if ('problem' in r) { notify(r.problem); return false; }
    steps.push(g.save(r.outline));
  }
  await runSteps(steps);
  return !useSession.getState().error;
}

/** "Offset outline…" for the selection's right-click menu (plans and 3D): the distance asked, + outward, − inward. */
export function offsetMenuItems(): MenuItem[] {
  const list = offsettable();
  if (!list.length) return [];
  return [{
    label: 'Offset outline…',
    onSelect: () => void (async () => {
      const d = await askLength('Offset outline (+ outward, − inward)', 'Distance', 0.1);
      if (d == null || Math.abs(d) < 1e-9) return;
      await offsetAll(list.map((x) => x.grip), d);
    })(),
  }];
}
