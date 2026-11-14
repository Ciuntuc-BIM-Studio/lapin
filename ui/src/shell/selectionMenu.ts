/**
 * What a right-click on a plan, a drawing or the 3D view offers for the selection: group it, or let a group go, open
 * it, take members out (a tester's report, #98: an imported DXF's lines put together to move them as one); and copy
 * it to other storeys, or move it to another (their dialogs).
 */

import { useUi } from '../app/store';
import { useSession } from '../engine/session';
import { copyable } from '../modules/core/CopyToStoreysDialog';
import { drawableElement } from '../modules/core/shopDrawing';
import { editSelectedGroup, groupSelection, removeFromGroup, ungroupSelection } from '../modules/core/groupTools';
import { GROUP, groupOf } from '../platform/groups';
import { allTools } from '../platform/modules';
import { typeOf } from '../engine/model';
import { hiddenHere, useShownHere } from '../viewport/cad/shown';
import { describeSelection, previousSelections, reselect } from './selectionHistory';
import { criteriaOf, defaultKeys, similarIds, type SimilarKey } from './similar';
import { hideSelection, isolateSelection, resetTemporary, temporaryOn, useTemporary } from '../viewport/temporary';
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

/** Whether an element is in the view in front: not hidden by it, and in a plan on its storey (when the view cannot tell). */
function inFrontView(n: { id: string; storeyId: string | null }): boolean {
  const ui = useUi.getState();
  if (hiddenHere(n.id)) return false;
  if (!useShownHere.getState().hides && ui.view === 'plan' && ui.storeyId) return n.storeyId === ui.storeyId;
  return true;
}

function chooseAll(ids: string[]): void {
  if (!ids.length) return;
  useUi.setState({ selected: ids, selection: ids[ids.length - 1] ?? null });
}

/** Select similar (the same class and type) in this view or in the whole project, and "Select all with the same…" a thing the selection is told by (forum topic 73). */
export function similarItems(): MenuItem[] {
  const { selected } = useUi.getState();
  const { nodes, relations } = useSession.getState();
  const model = { nodes, relations };
  const ids = selected.filter((id) => nodes.has(id));
  if (!ids.length || ids.some((id) => !nodes.get(id)?.editable)) return [];
  if (ids.some((id) => nodes.get(id)!.type !== nodes.get(ids[0])!.type)) return [];
  const keys = defaultKeys(model, ids);
  const items: MenuItem[] = [
    { label: 'Select similar in this view', onSelect: () => chooseAll(similarIds(model, ids, keys, inFrontView)) },
    { label: 'Select similar in the whole project', onSelect: () => chooseAll(similarIds(model, ids, keys)) },
  ];
  // What the clicked element is told apart by: one entry for each, matched in the project (one element selected).
  if (ids.length === 1) {
    const sub: MenuItem[] = criteriaOf(model, ids[0]).map((c) => ({
      label: c.label, hint: c.value, raw: false,
      onSelect: () => chooseAll(similarIds(model, ids, [c.key as SimilarKey])),
    }));
    if (sub.length) items.push({ label: 'Select all with the same…', submenu: sub, onSelect: () => undefined });
  }
  return items;
}

/** Edit type: the types dialog of the selected element's type (the same one Edit types… opens), the type made the current one. */
export function editTypeItems(): MenuItem[] {
  const { selected } = useUi.getState();
  if (selected.length !== 1) return [];
  const state = useSession.getState();
  const type = typeOf({ nodes: state.nodes, relations: state.relations }, selected[0]);
  if (!type) return [];
  const tool = allTools().find((t) => t.typed?.cls === type.type && t.typed.dialog);
  if (!tool?.typed?.dialog) return [];
  const dialog = tool.typed.dialog;
  return [{ label: 'Edit type…', onSelect: () => { tool.typed!.set(type.id); useUi.setState({ dialog }); } }];
}

/** Previous selections: the last ten, the newest first, each one a click to select it again. */
export function historyItems(): MenuItem[] {
  const nodes = useSession.getState().nodes;
  const past = previousSelections();
  if (!past.length) return [];
  const sub: MenuItem[] = past.map((ids) => ({ label: describeSelection(ids, (id) => nodes.get(id)?.name), raw: true, onSelect: () => reselect(ids) }));
  return [{ label: 'Previous selections', submenu: sub, onSelect: () => undefined }];
}

export function selectionItems(): MenuItem[] {
  const { selected } = useUi.getState();
  const storeys: MenuItem[] = copyable(selected, useSession.getState().nodes).length ? [
    { label: 'Copy to storeys…', onSelect: () => useUi.setState({ dialog: 'copyToStoreys' }) },
    { label: 'Move to storey…', onSelect: () => useUi.setState({ dialog: 'moveToStorey' }) },
  ] : [];
  const shop: MenuItem[] = drawableElement(selected, useSession.getState().nodes) ? [{ label: 'Make drawing…', onSelect: () => useUi.setState({ dialog: 'shopDrawing' }) }] : [];
  return [...groupItems(), ...storeys, ...shop, ...similarItems(), ...editTypeItems(), ...historyItems()];
}

/** The 3D view's temporary Hide and Isolate (HH, HI) for the selection, and Show all (HR) while something is hidden. */
export function temporaryItems(): MenuItem[] {
  const items: MenuItem[] = [];
  if (useUi.getState().selected.length) {
    items.push({ label: 'Hide', shortcut: 'HH', onSelect: hideSelection }, { label: 'Isolate', shortcut: 'HI', onSelect: isolateSelection });
  }
  if (temporaryOn(useTemporary.getState())) items.push({ label: 'Show all', shortcut: 'HR', onSelect: resetTemporary });
  return items;
}
