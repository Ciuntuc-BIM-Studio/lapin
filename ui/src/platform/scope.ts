/**
 * An edit scope, as Blender's edit mode: a module isolates what it works on
 * (an MEP system, a sketch). While it is on, what lies outside it is drawn
 * faded (the plan's drawing and the 3D) and cannot be picked or box-selected,
 * but stays to snap to. Done (or Esc with nothing else to cancel) leaves it;
 * the module may finish its work then (resolve fittings, check).
 */

import { create } from 'zustand';
import type { NodeDto } from '../engine/protocol';
import { useUi } from '../app/store';

export interface Scope {
  /** What the scope is on (a system's id). */
  id: string;
  /** Shown in the bar: "Heating". */
  label: string;
  /** The module's word for it: "System". */
  kind?: string;
  /** Whether an element belongs to the scope (read again as the model changes). */
  includes: (node: NodeDto) => boolean;
  /** Called on Done, before the scope closes. */
  onDone?: () => void | Promise<void>;
}

export const useScope = create<{ scope: Scope | null }>(() => ({ scope: null }));

export function enterScope(scope: Scope): void {
  useScope.setState({ scope });
  // What was selected outside the scope is let go.
  const { selected } = useUi.getState();
  if (selected.length) useUi.setState({ selected: [], selection: null });
}

export async function exitScope(finish = true): Promise<void> {
  const s = useScope.getState().scope;
  if (!s) return;
  if (finish) await s.onDone?.();
  useScope.setState({ scope: null });
}

/** Whether a node may be picked now: every node outside a scope, only the scope's inside one. */
export function pickable(node: NodeDto | null | undefined): boolean {
  const s = useScope.getState().scope;
  return !!node && (!s || s.includes(node));
}
