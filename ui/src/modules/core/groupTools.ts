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

/** What is selected made a component (docs/components.md): an instance of it, selected. */
/** What is selected made an apartment type (docs/plan-units.md): a component that takes walls, with their doors. */
export async function makeUnit(): Promise<void> {
  if (!selected().length) { notify('Select the walls, rooms and furniture of an apartment.'); return; }
  const { ask, textField } = await import('../../shell/AskBox');
  const r = await ask('Make apartment type', [textField('name', 'Name', '')]);
  if (!r) return;
  const made = await execute('component.make', { ids: selected(), kind: 'unit', ...(r.name ? { name: String(r.name) } : {}) }) as { id?: string } | null;
  if (made?.id) { selectOnly([made.id]); notify('Made an apartment type: copies of it are instances. Double-click one to edit them all.'); }
}

export async function makeComponent(): Promise<void> {
  if (!selected().length) { notify('Select walls, floors, rooms or Studio solids to make a component.'); return; }
  const { ask, textField } = await import('../../shell/AskBox');
  const r = await ask('Make component', [textField('name', 'Name', '')]);
  if (!r) return;
  const made = await execute('component.make', { ids: selected(), ...(r.name ? { name: String(r.name) } : {}) }) as { id?: string } | null;
  if (made?.id) { selectOnly([made.id]); notify('Made a component: copies of it are instances. Double-click one to edit them all.'); }
}

/**
 * The selected 2D drafting put in a detail set of a component (forum #224): the instance is the open one, or the one
 * selected with the drafting. Every instance then draws it, in every plan of its storey that shows the set.
 */
export async function addToDetailSet(): Promise<void> {
  const nodes = useSession.getState().nodes;
  const scope = useScope.getState().scope;
  const isInstance = (id: string | undefined | null) => !!id && nodes.get(id)?.type === GROUP && typeof nodes.get(id)?.props.component === 'string';
  const picked = selected();
  const instance = isInstance(scope?.id) ? scope!.id : picked.find((id) => isInstance(id)) ?? picked.map((id) => groupOf(id)).find((g) => isInstance(g));
  const ids = picked.filter((id) => id !== instance && nodes.get(id)?.type !== GROUP);
  if (!instance || !ids.length) { notify('Select the 2D drafting (lines, hatches, texts, symbols, tags, dimensions) drawn on a plan, together with the instance of a component, or open the instance first.'); return; }
  const component = nodes.get(instance)!.props.component;
  const sets = [...nodes.values()].filter((n) => typeof n.props.detailSet === 'string' && [...nodes.values()].some((g) => g.props.component === component && groupOf(n.id) === g.id)).map((n) => String(n.props.detailSet));
  const { ask, textField } = await import('../../shell/AskBox');
  const r = await ask('Add to detail set', [textField('set', 'Detail set', sets[0] ?? 'Technical')]);
  if (!r || !String(r.set ?? '').trim()) return;
  const done = await execute('component.addDetail', { id: instance, ids, set: String(r.set).trim() });
  if (done) notify('Added to the detail set: every instance draws it. Choose per view which sets show in Visibility / Graphics → Annotations.');
}
