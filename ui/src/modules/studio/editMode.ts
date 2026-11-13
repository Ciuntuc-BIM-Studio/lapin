/**
 * Part edit mode (docs/plan-studio-parametric.md §3.2, Blender's Tab): one Studio solid or placed part is isolated in an edit
 * scope (platform/scope.ts): the rest of the model is drawn faded, cannot be picked, and stays to snap to. Entered by Tab,
 * a double click in 3D or a button; left by Tab, Esc (the scope bar) or Finish part. Leaving is not an undo step.
 */

import type { NodeDto } from '../../engine/protocol';
import { notify, useUi } from '../../app/store';
import { useSession } from '../../engine/session';
import { doubleClickHandlers, enterScope, exitScope, tabHandlers, useScope, type Scope } from '../../platform/scope';

/** The scope's kind: what the bar says it is editing. */
export const PART_KIND = 'Part';
const PREFIX = 'part:';

/** Whether a node is a Studio solid or a placed part: what the edit mode can open. */
export const isEditable = (node: NodeDto | null | undefined): boolean => !!node && typeof node.props.operation === 'string' && node.props.operation !== '';

/** The scope of a part: only that element belongs to it. */
export function partScope(node: NodeDto): Scope {
  return { id: PREFIX + node.id, kind: PART_KIND, label: node.name, includes: (n) => n.id === node.id };
}

/** The part being edited (its element's id), or null. */
export function editedPartId(scope: Scope | null = useScope.getState().scope): string | null {
  return scope && scope.kind === PART_KIND && scope.id.startsWith(PREFIX) ? scope.id.slice(PREFIX.length) : null;
}

/** The ids that can be selected in the edit mode: the part alone. All of them with no edit mode on. */
export const selectableIds = (ids: readonly string[], partId: string | null): string[] => (partId ? ids.filter((i) => i === partId) : [...ids]);

/** What the interface was doing when Tab was pressed. */
export interface TabState {
  /** The part being edited, or null. */
  editing: string | null;
  /** Another edit scope (an MEP system…) is on. */
  otherScope: boolean;
  selected: readonly string[];
  /** The selected element, when there is one. */
  selectedNode: NodeDto | null | undefined;
  tool: string | null;
  view: string;
  dialog: string | null;
  /** A text field has the focus. */
  inField: boolean;
}

/**
 * What Tab does: leaves the edit mode when it is on; with one Studio solid selected in 3D and no tool, no dialog and no
 * field in the way, enters it; otherwise nothing (Tab then folds the panels as it always did).
 */
export function tabIntent(s: TabState): 'enter' | 'leave' | null {
  if (s.inField || s.dialog) return null;
  if (s.editing) return s.tool ? null : 'leave';
  if (s.otherScope || s.tool || s.view !== '3d') return null;
  return s.selected.length === 1 && isEditable(s.selectedNode) ? 'enter' : null;
}

/** Enters the edit mode on a part. Returns whether it is on. */
export function editPart(id?: string | null): boolean {
  const target = id ?? useUi.getState().selection;
  const node = target ? useSession.getState().nodes.get(target) : undefined;
  if (!isEditable(node)) { notify('Select one Studio solid or part first.'); return false; }
  const scope = useScope.getState().scope;
  if (scope && editedPartId(scope) === node!.id) return true;
  if (scope) { notify('Finish the edit mode that is on first.'); return false; }
  enterScope(partScope(node!));
  useUi.setState({ selection: node!.id, selected: [node!.id] });
  return true;
}

/** Leaves the part edit mode (not an undo step). */
export async function finishPart(): Promise<void> {
  if (editedPartId()) await exitScope(false);
}

const inField = (): boolean => !!(document.activeElement as HTMLElement | null)?.closest?.('input, select, textarea, [contenteditable="true"], [role="dialog"]');

/** Tab and the double click, taken before the shell's. Registered once, when Studio loads. */
export function registerEditMode(): void {
  tabHandlers.add(() => {
    const ui = useUi.getState();
    const scope = useScope.getState().scope;
    const intent = tabIntent({
      editing: editedPartId(scope), otherScope: !!scope && !editedPartId(scope), selected: ui.selected,
      selectedNode: ui.selection ? useSession.getState().nodes.get(ui.selection) : undefined,
      tool: ui.tool, view: ui.view, dialog: ui.dialog, inField: inField(),
    });
    if (intent === 'enter') return editPart(ui.selection);
    if (intent === 'leave') { void finishPart(); return true; }
    return false;
  });
  doubleClickHandlers.add((id) => {
    if (useScope.getState().scope || useUi.getState().tool || !id) return false;
    return isEditable(useSession.getState().nodes.get(id)) && editPart(id);
  });
}
