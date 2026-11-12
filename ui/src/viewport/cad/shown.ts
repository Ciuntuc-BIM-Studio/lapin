/**
 * What a plan's snaps may take (forum #31, decision 491): only what the view shows. The engine's drawing is made by
 * the one resolver every drawing, sheet and export uses (VisibilityGraphics, the view's range and crop, its phase and
 * its filters), and it carries a pick mark for every element it draws and a mark for every annotation. An element
 * with no mark in it is hidden here, so its modelling lines and its snap points are left out of the snaps too.
 * Without a drawing (modelling lines shown, or a drawing that could not be made) everything drawn on screen snaps.
 */

import type { DrawingDto } from '../drawing';
import type { PlanItemDto } from '../../engine/protocol';
import { drawnInPlan } from '../finishSnaps';

/** The elements a view's drawing shows: each mark's owner (`id`, `id#axis`, `id:part` all name `id`); null with no drawing. */
export function drawnElements(drawing: DrawingDto | null | undefined): Set<string> | null {
  if (!drawing || drawing.problem) return null;
  const out = new Set<string>();
  for (const m of drawing.marks ?? []) {
    out.add(m.id);
    for (const sep of ['#', ':']) {
      const k = m.id.indexOf(sep);
      if (k > 0) out.add(m.id.slice(0, k));
    }
  }
  return out;
}

/**
 * The items a plan snaps to: those its drawing shows, or all of them while there is no drawing to ask. Of an item shown,
 * the cut pieces of finish layers the view leaves out (its style by its kind, forum #30: DrawingDto.hiddenFinishes) are
 * left out too, so a wall without its plaster snaps to its core's faces, the outline the plan draws.
 */
export function shownItems<T extends { nodeId: string; type?: string; plan?: PlanItemDto[] }>(items: readonly T[], drawing: DrawingDto | null | undefined): readonly T[] {
  const shown = drawnElements(drawing);
  const kept = shown ? items.filter((r) => shown.has(r.nodeId)) : items;
  const hidden = drawing?.hiddenFinishes;
  if (!hidden?.length) return kept;
  return kept.map((r) => (r.plan && r.type && r.plan.some((it) => !drawnInPlan(r.type!, it, hidden)) ? { ...r, plan: r.plan.filter((it) => drawnInPlan(r.type!, it, hidden)) } : r));
}
