/**
 * What a right-click on a plan, a drawing or the 3D view offers for the selection: group it, or let a group go, open
 * it, take members out (a tester's report, #98: an imported DXF's lines put together to move them as one); and copy
 * it to other storeys, or move it to another (their dialogs).
 */

import { useUi } from '../app/store';
import { useSession } from '../engine/session';
import { copyable } from '../modules/core/CopyToStoreysDialog';
import { editSelectedGroup, groupSelection, removeFromGroup, ungroupSelection } from '../modules/core/groupTools';
import { GROUP, groupOf } from '../platform/groups';
import { useScope } from '../platform/scope';
import type { MenuItem } from './ContextMenu';

/** Group, Ungroup, Edit group, Remove from group: those that apply to the selection. */
export function groupItems(): MenuItem[] {
  const { selected } = useUi.getState();
  if (!selected.length) return [];
  const nodes = useSession.getState().nodes;
  const groups = selected.filter((id) => nodes.get(id)?.type === GROUP);
  const items: MenuItem[] = [];
  // A lone group is grouped already; anything else (elements, several groups merged) can be.
  if (!(groups.length === 1 && selected.length === 1)) items.push({ label: 'Create group', onSelect: () => void groupSelection() });
  if (groups.length) {
    items.push({ label: 'Ungroup', onSelect: () => void ungroupSelection() });
    if (groups.length === 1) items.push({ label: 'Edit group', onSelect: editSelectedGroup });
  }
  if (useScope.getState().scope && selected.some((id) => groupOf(id))) items.push({ label: 'Remove from group', onSelect: () => void removeFromGroup() });
  return items;
}

export function selectionItems(): MenuItem[] {
  const { selected } = useUi.getState();
  const storeys: MenuItem[] = copyable(selected, useSession.getState().nodes).length ? [
    { label: 'Copy to storeys…', onSelect: () => useUi.setState({ dialog: 'copyToStoreys' }) },
    { label: 'Move to storey…', onSelect: () => useUi.setState({ dialog: 'moveToStorey' }) },
  ] : [];
  return [...groupItems(), ...storeys];
}
