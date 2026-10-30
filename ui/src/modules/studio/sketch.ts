/** A Studio solid as the engine describes it, and opening its sketch on its view. */

import { notify, useUi } from '../../app/store';
import { query, useSession } from '../../engine/session';

/** A step of a solid as the engine lists it: its kind, its numbers, whether it is off, and its label. */
export interface FeatureStep { op: string; label: string; off?: boolean; [key: string]: unknown }

export interface Feature {
  operation: string; view?: string; viewId?: string; viewKind?: string; holes: number; sketch: string[]; area?: number; guide?: string | null;
  upTo?: string | null; features?: FeatureStep[]; volume?: number | null; problems?: string[] | null;
}

/** The sketch of a solid opened on its view, selected for editing. */
export async function editSketch(id?: string): Promise<void> {
  const target = id ?? useUi.getState().selection;
  if (!target) { notify('Select a solid first: its sketch opens on its view.'); return; }
  const f = await query<Feature>('studio.feature', { id: target });
  if (!f?.viewId) { notify('That is not a Studio solid: it is edited with its own tool.'); return; }
  const view = useSession.getState().nodes.get(f.viewId);
  const kind = view?.props.kind ?? 'plan';
  if (kind === 'plan' || f.viewId.startsWith('plan:')) useUi.setState({ view: 'plan', planViewId: f.viewId.startsWith('plan:') ? null : f.viewId });
  else useUi.setState({ view: 'drawing', drawingViewId: f.viewId });
  useUi.setState({ selected: f.sketch, selection: f.sketch[0] ?? null });
  notify('The sketch is selected on its view: edit it and the solid follows.');
}
