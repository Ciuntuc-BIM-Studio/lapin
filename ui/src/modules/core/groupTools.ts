/** The Modify tab's group tools (docs/groups.md): on the selection, a group selected being its members. */

import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { GROUP, groupOf, openGroup } from '../../platform/groups';
import { exitScope, useScope } from '../../platform/scope';

const selected = () => useUi.getState().selected;
const selectOnly = (ids: string[]) => useUi.setState({ selected: ids, selection: ids[ids.length - 1] ?? null });

/** The selection made one group, selected. */
export async function groupSelection(): Promise<void> {
  if (!selected().length) { notify('Select the elements to group first.'); return; }
  const r = await execute('group.create', { ids: selected() }) as { id?: string; members?: number } | null;
  if (r?.id) { selectOnly([r.id]); notify(`Grouped ${r.members ?? 0} element(s).`); }
}

/** The groups selected let go: their members selected. */
export async function ungroupSelection(): Promise<void> {
  const nodes = useSession.getState().nodes;
  const groups = selected().filter((id) => nodes.get(id)?.type === GROUP);
  if (!groups.length) { notify('Select a group to ungroup it.'); return; }
  const r = await execute('group.ungroup', { ids: groups }) as { ids?: string[] } | null;
  if (r) selectOnly(r.ids ?? []);
}

/** The group selected (or the selected element's group) opened; open already: closed. */
export function editSelectedGroup(): void {
  if (useScope.getState().scope) { void exitScope(); return; }
  const nodes = useSession.getState().nodes;
  const id = selected()[0];
  const group = id && nodes.get(id)?.type === GROUP ? id : id ? groupOf(id) : undefined;
  if (!group) { notify('Select a group to edit it.'); return; }
  openGroup(group);
}

/** The selected members taken out of their group. */
export async function removeFromGroup(): Promise<void> {
  const ids = selected().filter((id) => groupOf(id));
  if (!ids.length) { notify('Open a group (double-click it) and select the members to take out.'); return; }
  await execute('group.remove', { ids });
}
