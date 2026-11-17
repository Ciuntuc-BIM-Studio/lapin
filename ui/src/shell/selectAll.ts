/**
 * Select all (Ctrl+A, forum topic 130): everything the view in front shows that a click could take. A plan or a
 * drawing view asks its canvas for all it draws, by the same rule a selection window uses (groups and assemblies
 * taken whole, as a window takes them); the 3D view takes the editable elements it shows (not hidden by the view,
 * by Hide or by Isolate).
 */

import { useUi } from '../app/store';
import { useSession } from '../engine/session';
import { pickRegion } from '../platform/assemblies';
import { useCad } from '../viewport/cad/session';
import { hiddenHere } from '../viewport/cad/shown';
import { useTemporary } from '../viewport/temporary';
import type { RegionTest } from '../viewport/selectBox';

/** A region that takes whatever has something drawn. */
const everything: RegionTest = (segs) => segs.length > 0;

/** The ids Select all takes in the view in front. */
export function selectAllIds(): string[] {
  const ui = useUi.getState();
  const host = useCad.getState().host;
  if (ui.view !== '3d' && host?.inRegion) return pickRegion(host.inRegion(everything));
  if (ui.view !== '3d') return [];
  const { hidden, isolated } = useTemporary.getState();
  const ids: string[] = [];
  for (const n of useSession.getState().nodes.values()) {
    if (!n.editable || !n.storeyId || hiddenHere(n.id) || hidden.has(n.id) || (isolated && !isolated.has(n.id))) continue;
    ids.push(n.id);
  }
  return pickRegion(ids);
}

export function selectAll(): void {
  const ids = selectAllIds();
  if (!ids.length) return;
  useUi.setState({ selected: ids, selection: ids[ids.length - 1] ?? null });
}
