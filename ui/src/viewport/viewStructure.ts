/**
 * A view's partial structure display changed from the view itself (decision 500), beside its scale (decision 492): the
 * entire model, without the finishes, the cores only, the load-bearing elements' cores only. The view takes it as its
 * settings give it (view.setStyle, one undo step); picking what its style says for a view of its kind takes the style's
 * again; a level's default plan, held by no view, becomes the level's plan view with it (view.create, one step too).
 */

import type { LayerMode } from '../modules/core/viewStyles';

/** The command that gives a view a partial structure display (byStyle: its style's for a view of its kind), or null when it draws by it already. */
export function structureCommand(viewId: string, next: LayerMode, current: LayerMode, byStyle: LayerMode): { command: string; args: Record<string, unknown> } | null {
  if (next === current) return null;
  if (viewId.startsWith('plan:')) return { command: 'view.create', args: { kind: 'plan', storeyId: viewId.slice(5), layers: next } };
  return { command: 'view.setStyle', args: { id: viewId, layers: next === byStyle ? null : next } };
}
