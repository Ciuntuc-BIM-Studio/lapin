/**
 * Select all (Ctrl+A, forum topic 130): everything the view in front shows that a click could take. A plan or a
 * drawing view asks its canvas for all it draws, by the same rule a selection window uses (groups and assemblies
 * taken whole, as a window takes them); the 3D view takes the editable elements it shows (not hidden by the view,
 * by Hide or by Isolate).
 *
 * With a tool on that places a class of elements (the Wall tool), Select all takes that class alone (forum topic 250, as
 * in other programs: the Wall tool, then Ctrl+A, selects the walls), each element itself, and the tool ends so the
 * selection can be worked on.
 */

import { notify, useUi } from '../app/store';
import { findTool } from '../platform/modules';
import { useSession } from '../engine/session';
import { pickRegion } from '../platform/assemblies';
import { useCad } from '../viewport/cad/session';
import { hiddenHere } from '../viewport/cad/shown';
import { useTemporary } from '../viewport/temporary';
import type { RegionTest } from '../viewport/selectBox';

/** A region that takes whatever has something drawn. */
const everything: RegionTest = (segs) => segs.length > 0;

/** Every element the view in front shows that a click could take, each itself (not yet taken up into its group). */
function shownIds(): string[] {
  const ui = useUi.getState();
  const host = useCad.getState().host;
  if (ui.view !== '3d' && host?.inRegion) return host.inRegion(everything);
  if (ui.view !== '3d') return [];
  const { hidden, isolated } = useTemporary.getState();
  const ids: string[] = [];
  for (const n of useSession.getState().nodes.values()) {
    if (!n.editable || !n.storeyId || hiddenHere(n.id) || hidden.has(n.id) || (isolated && !isolated.has(n.id))) continue;
    ids.push(n.id);
  }
  return ids;
}

/** The ids Select all takes in the view in front. */
export function selectAllIds(): string[] {
  return pickRegion(shownIds());
}

/** The class of elements the tool on places, from its types' class (the Wall tool's IfcWallType → IfcWall); null for another tool or none. */
export function toolClass(toolId: string | null = useUi.getState().tool): string | null {
  const cls = findTool(toolId)?.typed?.cls;
  return cls && /.Type$/.test(cls) ? cls.slice(0, -4) : null;
}

/** The elements of a class the view in front shows (editable ones). */
export function classIdsShown(cls: string): string[] {
  const nodes = useSession.getState().nodes;
  return shownIds().filter((id) => { const n = nodes.get(id); return n?.type === cls && n.editable; });
}

export function selectAll(): void {
  const cls = toolClass();
  if (cls) {
    const ids = classIdsShown(cls);
    if (!ids.length) { notify('Nothing of what this tool places is in this view.'); return; }
    useUi.setState({ tool: null, selected: ids, selection: ids[ids.length - 1] ?? null });
    return;
  }
  const ids = selectAllIds();
  if (!ids.length) return;
  useUi.setState({ selected: ids, selection: ids[ids.length - 1] ?? null });
}
