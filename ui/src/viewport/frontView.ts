/** The view in front as an entity that keeps settings: a plan's own view is made when the storey has none. */

import { useUi } from '../app/store';
import { activeStorey, execute, useSession } from '../engine/session';

let making: Promise<string | null> | null = null;

/** The view in front, for what is kept on a view (graphics, line edits): the 3D ({3D} or a saved 3D view), the section or elevation in front, or the plan's own view (made when the storey has none). */
export async function frontView(): Promise<string | null> {
  const ui = useUi.getState();
  if (ui.view === '3d') return ui.view3dId ?? '3d';
  if (ui.view === 'drawing') return ui.drawingViewId;
  if (ui.view !== 'plan') return null;
  const nodes = useSession.getState().nodes;
  const storey = activeStorey();
  if (ui.planViewId && nodes.get(ui.planViewId)?.props.storeyId === storey?.id) return ui.planViewId;
  if (!storey) return null;
  // The storey's first plan view, else a new one (made once, however often asked).
  const existing = [...nodes.values()].find((n) => n.type === 'LapinView' && n.props.kind === 'plan' && n.props.storeyId === storey.id);
  if (existing) { useUi.setState({ planViewId: existing.id }); return existing.id; }
  making ??= (execute('view.create', { kind: 'plan', storeyId: storey.id }) as Promise<{ id?: string } | null>).then((r) => {
    making = null;
    if (r?.id) useUi.setState({ planViewId: r.id });
    return r?.id ?? null;
  });
  return making;
}

