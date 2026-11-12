/**
 * What a plan's snaps and selection may take (forum #31, decisions 491 and 498): only what the view shows. The engine's
 * drawing is made by the one resolver every drawing, sheet and export uses (VisibilityGraphics, the view's range and crop,
 * its phase and its filters), and it carries a mark for every element it draws (an element drawn only overhead too, with
 * no pick lines) and a mark for every annotation. An element with no mark in it is hidden here, so its modelling lines and
 * its snap points are left out of the snaps, and a click, a box or a lasso never takes it. Without a drawing (modelling
 * lines shown, or a drawing that could not be made) everything drawn on screen snaps and picks.
 *
 * Selecting from a list (the project browser, a schedule, a link in Properties, the dependency panel) still selects a
 * hidden element by name; the view in front says so (useShownHere: Properties' "Not shown in this view").
 */

import { create } from 'zustand';
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

/** The whole an element is a part of (platform/assemblies.ts' wholeOf), to climb from a part to what the drawing names. */
export type WholeOf = (id: string) => string | undefined;

/** Whether `id` or one of its wholes is among what a drawing shows (a whole drawn as one keeps its parts' own lines pickable for drilling). */
export function isShown(id: string, shown: ReadonlySet<string>, wholeOf?: WholeOf): boolean {
  const seen = new Set<string>();
  for (let x: string | undefined = id; x && !seen.has(x); x = wholeOf?.(x)) {
    if (shown.has(x)) return true;
    seen.add(x);
  }
  return false;
}

/**
 * The items a plan snaps to and picks: those its drawing shows, or all of them while there is no drawing to ask. Of an item
 * shown, the cut pieces of finish layers the view leaves out (its style by its kind, forum #30: DrawingDto.hiddenFinishes) are
 * left out too, so a wall without its plaster snaps to its core's faces, the outline the plan draws.
 */
export function shownItems<T extends { nodeId: string; type?: string; plan?: PlanItemDto[] }>(items: readonly T[], drawing: DrawingDto | null | undefined, wholeOf?: WholeOf): readonly T[] {
  const shown = drawnElements(drawing);
  const kept = shown ? items.filter((r) => isShown(r.nodeId, shown, wholeOf)) : items;
  const hidden = drawing?.hiddenFinishes;
  // The roles the view's partial structure display leaves out of every layered element (decision 500).
  const subs = drawing?.hiddenSubs;
  if (!hidden?.length && !subs?.length) return kept;
  const drawn = (type: string, it: PlanItemDto) => drawnInPlan(type, it, hidden, subs);
  return kept.map((r) => (r.plan && r.type && r.plan.some((it) => !drawn(r.type!, it)) ? { ...r, plan: r.plan.filter((it) => drawn(r.type!, it)) } : r));
}

/**
 * Whether a drawing leaves out a modelled element (`modelled`: it has geometry the views draw): false while there is no
 * drawing to ask, and for what is not modelled (a type, a material, a view), which no view draws.
 */
export function hiddenInDrawing(id: string, drawing: DrawingDto | null | undefined, modelled: (id: string) => boolean, wholeOf?: WholeOf): boolean {
  const shown = drawnElements(drawing);
  return !!shown && modelled(id) && !isShown(id, shown, wholeOf);
}

/**
 * The view in front's answer to "is this element shown here?": the pane that is active sets its own test (`owner` names it,
 * so a pane put away clears only its own), `version` moves when what it shows changes. Null: the view cannot tell (a sheet).
 */
export const useShownHere = create<{ owner: string | null; hides: ((id: string) => boolean) | null; version: number }>(() => ({ owner: null, hides: null, version: 0 }));

/** The active view's test of what it hides, set again whenever what it shows changes. */
export function publishShown(owner: string, hides: (id: string) => boolean): void {
  useShownHere.setState((s) => ({ owner, hides, version: s.version + 1 }));
}

/** A view put away or no longer in front: its test goes, unless another view's has taken its place. */
export function clearShown(owner: string): void {
  if (useShownHere.getState().owner === owner) useShownHere.setState((s) => ({ owner: null, hides: null, version: s.version + 1 }));
}

/** Whether the view in front hides `id` (false when it cannot tell). */
export function hiddenHere(id: string | null | undefined): boolean {
  const h = useShownHere.getState().hides;
  return !!id && !!h && h(id);
}
