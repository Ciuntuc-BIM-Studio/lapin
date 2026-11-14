/**
 * What a double-click (or ✎) on a view placed on a sheet opens: a level's plan, a plan view, a schedule, a saved 3D view
 * in the 3D pane with its camera (forum topic 91: it used to open the drawing pane, empty for a 3D view), or a section,
 * an elevation or another drawing in the drawing pane.
 */

import type { NodeDto } from '../engine/protocol';
import type { UiState } from '../app/store';

export function openPlaced(viewId: string, node: NodeDto | undefined): Partial<UiState> | null {
  if (viewId.startsWith('plan:')) return { view: 'plan', storeyId: viewId.slice(5), planViewId: null };
  if (!node) return null;
  if (node.type === 'LapinSchedule') return { view: 'schedule', scheduleId: node.id };
  if (node.props.kind === 'plan') return { view: 'plan', planViewId: node.id, storeyId: String(node.props.storeyId ?? '') || null };
  if (node.props.kind === '3d') return { view: '3d', view3dId: node.id, selection: node.id, selected: [node.id] };
  return { view: 'drawing', drawingViewId: node.id };
}
