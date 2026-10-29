/** The section box's commands (View → Section box, Cut at plan, Align), for the ribbon and the 3D view's chip; a saved 3D view keeps its box. */

import { notify, useUi } from '../app/store';
import { activeStorey, execute, query, useSession } from '../engine/session';
import { num } from '../engine/model';
import { modelBounds, rotateBox, runOf, useSectionBox, type SectionBox } from './sectionBox';

/** The saved 3D view in front, if one is (not {3D}, which keeps no box). */
const savedView = (): string | null => {
  const ui = useUi.getState();
  const id = ui.view === '3d' ? ui.view3dId : null;
  return id && id !== '3d' && useSession.getState().nodes.get(id)?.props.kind === '3d' ? id : null;
};

/** The box as the 3D view now has it, kept by the saved 3D view in front (one undo step); {3D} keeps it only while open. */
export function commitBox(): void {
  const id = savedView();
  if (!id) return;
  const box = useSectionBox.getState().box;
  const kept = useSession.getState().nodes.get(id)?.props.sectionBox ?? null;
  if (JSON.stringify(kept) === JSON.stringify(box)) return;
  void execute('view.setSectionBox', { id, box });
}

const set = (box: SectionBox | null) => { useSectionBox.setState({ box }); commitBox(); };

/** The box round everything drawn (turned as it is). */
export function fitSectionBox(): void {
  const box = modelBounds(useSession.getState().render.values(), 0.5, useSectionBox.getState().box?.rot ?? 0, useSession.getState().meshes);
  if (box) set(box);
}

/** The section box on (round the model, in the 3D view), or off. */
export function toggleSectionBox(): void {
  if (useSectionBox.getState().box) { set(null); return; }
  useUi.setState({ view: '3d' });
  fitSectionBox();
}

/** The box turned the way the selected element runs in plan (a wall's axis), about its middle. */
export function alignSectionBox(): void {
  const box = useSectionBox.getState().box;
  if (!box) return;
  const id = useUi.getState().selected[0];
  const angle = runOf(id ? useSession.getState().render.get(id) : undefined);
  if (angle == null) { notify('Select a wall, a beam or another element to turn the box the way it runs.'); return; }
  // The nearest of the four ways it runs: the box turns as little as it can.
  const now = box.rot ?? 0;
  const quarter = Math.PI / 2;
  set(rotateBox(box, angle + Math.round((now - angle) / quarter) * quarter));
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
  const box = useSectionBox.getState().box ?? modelBounds(useSession.getState().render.values(), 0.5, 0, useSession.getState().meshes);
  if (!box) return;
  useUi.setState({ view: '3d' });
  set({ ...box, min: [...box.min], max: [box.max[0], box.max[1], Math.max(cut, box.min[2] + 0.05)] });
}
