/** The section box's commands (View → Section box, Cut at plan), for the ribbon and the 3D view's chip. */

import { useUi } from '../app/store';
import { activeStorey, query, useSession } from '../engine/session';
import { num } from '../engine/model';
import { modelBounds, useSectionBox } from './sectionBox';

/** The box round everything drawn. */
export function fitSectionBox(): void {
  const box = modelBounds(useSession.getState().render.values());
  if (box) useSectionBox.setState({ box });
}

/** The section box on (round the model, in the 3D view), or off. */
export function toggleSectionBox(): void {
  if (useSectionBox.getState().box) { useSectionBox.setState({ box: null }); return; }
  fitSectionBox();
  useUi.setState({ view: '3d' });
}

/**
 * The box's top at the active storey's plan cut (its view range's cut plane;
 * 1.20 m over the storey when it has no plan yet), so the storey reads in 3D
 * as its plan cuts it, with what is under it.
 */
export async function cutAtPlan(): Promise<void> {
  const storey = activeStorey();
  if (!storey) return;
  const nodes = useSession.getState().nodes;
  const ui = useUi.getState();
  const plan = (ui.planViewId && nodes.get(ui.planViewId)?.props.storeyId === storey.id ? nodes.get(ui.planViewId) : undefined)
    ?? [...nodes.values()].find((n) => n.type === 'LapinView' && n.props.kind === 'plan' && n.props.storeyId === storey.id);
  const range = plan ? await query<{ resolved: { cut: number } }>('view.range', { id: plan.id }) : null;
  const cut = range?.resolved.cut ?? num(storey, 'elevation') + 1.2;
  const box = useSectionBox.getState().box ?? modelBounds(useSession.getState().render.values());
  if (!box) return;
  useSectionBox.setState({ box: { min: [...box.min], max: [box.max[0], box.max[1], Math.max(cut, box.min[2] + 0.05)] } });
  useUi.setState({ view: '3d' });
}
