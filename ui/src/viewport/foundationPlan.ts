/**
 * Where a tool puts what it draws in a foundation plan (decision 437, a tester's report #117): the plan is the level's, seen
 * from 0.50 m under it, and the level under it (the Foundations of a new project) is shown in it; a floor or a wall drawn
 * there stood on the level above, at 0.00, not at the −1.50 the plan is read at. It goes on the level under, except a
 * footing, whose bottom level is its type's, set from the level above.
 */

import type { NodeDto } from '../engine/protocol';

/** Tools that put a footing: it stays on the plan's own level. */
export const FOOTING_TOOLS = new Set(['structure.strip', 'structure.curvedStrip', 'structure.pad']);

/** Tools whose elements stand on a level, as opposed to the drawing tools' marks. */
const onLevel = (toolId: string) => toolId.startsWith('architecture.') || toolId.startsWith('structure.') || toolId.startsWith('studio.');

const elevationOf = (n: NodeDto) => (typeof n.props.elevation === 'number' ? n.props.elevation : 0);

/** The level directly under a storey: the highest one lower than it; null when there is none. */
export function levelUnder(nodes: ReadonlyMap<string, NodeDto>, storeyId: string): NodeDto | null {
  const own = nodes.get(storeyId);
  if (!own) return null;
  let best: NodeDto | null = null;
  for (const n of nodes.values()) {
    if (n.type !== 'IfcBuildingStorey' || elevationOf(n) >= elevationOf(own) - 1e-6) continue;
    if (!best || elevationOf(n) > elevationOf(best)) best = n;
  }
  return best;
}

/** Whether a plan view shows a level besides its own (its storeys: some including it, or all). */
function shows(view: NodeDto, storeyId: string): boolean {
  const s = view.props.storeys as { mode?: string; ids?: string[] } | undefined;
  return s?.mode === 'all' || (s?.mode === 'some' && Array.isArray(s.ids) && s.ids.includes(storeyId));
}

/** The level a tool's element goes on: the plan's, or in a foundation plan that shows the level under it, that one. */
export function placingStorey(nodes: ReadonlyMap<string, NodeDto>, storeyId: string, viewId: string | null | undefined, toolId: string): string {
  const view = viewId ? nodes.get(viewId) : undefined;
  if (!view || view.type !== 'LapinView' || view.props.foundation !== true || !onLevel(toolId) || FOOTING_TOOLS.has(toolId)) return storeyId;
  const under = levelUnder(nodes, storeyId);
  return under && shows(view, under.id) ? under.id : storeyId;
}
