/** Getting into a part's edit mode from the part (its type): on a placed instance, placed first when there is none. */

import { notify, useUi } from '../../../app/store';
import { activeStorey, execute, useSession } from '../../../engine/session';
import { editPart } from '../editMode';

/** The placed instances of a part (type): Studio solids typed by it (IfcRelDefinesByType). */
export function instancesOf(typeId: string): string[] {
  const st = useSession.getState();
  const typed = new Set<string>();
  for (const r of st.relations.values()) if (r.class === 'IfcRelDefinesByType' && r.relating === typeId) for (const id of r.related) typed.add(id);
  return [...st.nodes.values()].filter((n) => n.props.operation === 'part' && typed.has(n.id)).map((n) => n.id);
}

const waitFor = async (id: string): Promise<boolean> => {
  for (let i = 0; i < 40; i++) {
    if (useSession.getState().nodes.has(id)) return true;
    await new Promise((r) => setTimeout(r, 50));
  }
  return false;
};

/**
 * Edit part: closes the dialog, turns to 3D, takes a placed instance of the part and enters its edit mode. A part nobody
 * placed gets one at [0, 0] on the active storey first, and the notice says so. Returns the instance's id, or null.
 */
export async function editPartOfType(typeId: string): Promise<string | null> {
  let id = instancesOf(typeId)[0];
  let placed = false;
  if (!id) {
    const storey = activeStorey();
    if (!storey) { notify('Open a storey first: a part is edited on an object placed on one.'); return null; }
    const r = await execute('studio.placePart', { typeId, storeyId: storey.id, at: [0, 0] }) as { id?: string } | null;
    if (!r?.id) { notify(useSession.getState().error ?? 'The part could not be placed.'); return null; }
    id = r.id; placed = true;
    await waitFor(id);
  }
  useUi.setState({ dialog: null, view: '3d', tool: null });
  if (!editPart(id)) return null;
  if (placed) notify('The part had no object yet: one was placed at the origin of the active storey to edit it on.');
  return id;
}
