/**
 * A level's plan (a tester's report #113): its default plan (`plan:<storey>`, held by no entity) stands in until the
 * level has a floor plan view of its own (made by the first drafting or DXF import on it, or by hand); from then on
 * that view is the level's plan, wherever a plan is shown or drawn on, as the project browser lists it. Before, a
 * reopened project showed the empty default plan, its imported DXF on the view beside it.
 */

import type { NodeDto } from '../engine/protocol';

/** Whether a view is a level's floor plan: a plan on the storey, not a site or location plan, not a foundation plan. */
export const isFloorPlanOf = (n: NodeDto | undefined, storeyId: string): boolean =>
  !!n && n.type === 'LapinView' && (n.props.kind ?? 'plan') === 'plan' && n.props.storeyId === storeyId && typeof n.props.site !== 'string' && !n.props.foundation;

/** The level's own floor plan view, the first made; undefined while it has none. */
export function storeyPlanView(nodes: ReadonlyMap<string, NodeDto>, storeyId: string): NodeDto | undefined {
  for (const n of nodes.values()) if (isFloorPlanOf(n, storeyId)) return n;
  return undefined;
}

/** The plan shown for a level: the plan view open (when it is the level's), else the level's own floor plan view, else its default plan. */
export function planViewIdOf(nodes: ReadonlyMap<string, NodeDto>, storeyId: string, planViewId: string | null | undefined): string {
  if (planViewId && nodes.get(planViewId)?.props.storeyId === storeyId) return planViewId;
  return storeyPlanView(nodes, storeyId)?.id ?? `plan:${storeyId}`;
}

/** The same as an entity id: null for the default plan. */
export function ownPlanOf(nodes: ReadonlyMap<string, NodeDto>, storeyId: string, planViewId: string | null | undefined): string | null {
  const id = planViewIdOf(nodes, storeyId, planViewId);
  return id.startsWith('plan:') ? null : id;
}
