/**
 * "Edit sketch" on an outline (forum topic 97, stage 3): a floor's, a floor opening's or a ceiling's outline opened as a sketch on
 * a plan view of its own (`studio.editOutline`), the element hidden there; drawn with the Draft tab's commands (Line, Arc, Fillet,
 * Trim, Offset…) and constrained with Studio → Constraints, as any sketch; Finish sketch writes it back to the element in one undo
 * step (`studio.finishOutline`) and Cancel drops it (`studio.cancelOutline`). The bar over the view is every board's (SketchBar, boards.ts).
 */

import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';

/** Whether a node's outline can be opened as a sketch: a floor, a floor opening, a ceiling (not one kept as a reference). */
export function isOutlineElement(n: NodeDto | undefined): boolean {
  if (!n || n.props.reference || !Array.isArray(n.props.contour)) return false;
  if (n.type === 'IfcSlab' || n.type === 'LapinCeiling') return true;
  // A drawn room and a platform too (audit S4).
  if (n.type === 'IfcSpace') return n.props.auto === false;
  if (n.type === 'IfcEarthworksCut') return n.props.predefinedType !== 'TRENCH';
  return n.type === 'IfcOpeningElement' && !n.props.stairId;
}

/** The view the user was on before the sketch, to go back to. */
const back: { planViewId: string | null } = { planViewId: null };

/** Opens the element's outline as a sketch on its own plan view. */
export async function editOutline(id: string): Promise<void> {
  const before = useUi.getState().planViewId;
  const r = await execute('studio.editOutline', { id }) as { viewId?: string; rebuilt?: boolean; constraints?: number } | null;
  if (!r?.viewId) return;
  if (useSession.getState().nodes.get(before ?? '')?.props.outlineSketchOf == null) back.planViewId = before;
  useUi.setState({ view: 'plan', planViewId: r.viewId, tool: null, selected: [], selection: null });
  notify(r.rebuilt
    ? 'The outline was changed since its sketch was finished: the sketch starts again from the outline, without its constraints.'
    : 'Draw the outline with Draft (Line, Arc, Fillet, Trim, Offset) and constrain it with Studio → Constraints, then Finish sketch.');
}

/** Writes the sketch back to its element, back to the view before. */
export async function finishOutline(viewId: string): Promise<void> {
  const r = await execute('studio.finishOutline', { viewId }) as { id?: string } | null;
  if (!r?.id) return;
  useUi.setState({ view: 'plan', planViewId: back.planViewId, selected: [r.id], selection: r.id });
}

/** Drops the sketch: the element as it was. */
export async function cancelOutline(viewId: string): Promise<void> {
  const of = useSession.getState().nodes.get(viewId)?.props.outlineSketchOf as string | undefined;
  await execute('studio.cancelOutline', { viewId });
  useUi.setState({ view: 'plan', planViewId: back.planViewId, ...(of ? { selected: [of], selection: of } : {}) });
}
