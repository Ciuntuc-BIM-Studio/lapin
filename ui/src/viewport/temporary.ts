/**
 * Temporary hide and isolate (Revit's HH, HI, HR): what is selected hidden, or everything else hidden, in the 3D
 * view until reset. Not saved, not undone: a way to reach what lies inside an imported model while cleaning it up
 * (docs/groups.md). A group selected is its members.
 */

import { create } from 'zustand';
import { setLooks, type Look } from '../platform/looks';
import { useSession } from '../engine/session';
import { notify, select, useUi } from '../app/store';
import { shownSelection } from '../platform/groups';

interface Temporary { hidden: Set<string>; isolated: Set<string> | null }

export const useTemporary = create<Temporary>(() => ({ hidden: new Set(), isolated: null }));

/** The looks the temporary state asks for: each hidden one, and everything outside the isolated set. */
export function temporaryLooks(t: Temporary, all: Iterable<string>): Record<string, Look> {
  const out: Record<string, Look> = {};
  for (const id of t.hidden) out[id] = { hidden: true };
  if (t.isolated) for (const id of all) if (!t.isolated.has(id)) out[id] = { hidden: true };
  return out;
}

function apply(): void {
  setLooks('temporary', temporaryLooks(useTemporary.getState(), useSession.getState().render.keys()));
}

const selection = (): Set<string> => {
  const { selected } = useUi.getState();
  return shownSelection(selected);
};

export function hideSelection(): void {
  const s = selection();
  if (!s.size) { notify('Select what to hide first.'); return; }
  useTemporary.setState((t) => ({ hidden: new Set([...t.hidden, ...s]) }));
  select(null);
  apply();
}

export function isolateSelection(): void {
  const s = selection();
  if (!s.size) { notify('Select what to isolate first.'); return; }
  useTemporary.setState({ isolated: s });
  apply();
}

export function resetTemporary(): void {
  useTemporary.setState({ hidden: new Set(), isolated: null });
  apply();
}

/** Whether anything is hidden for now (the 3D shows a chip to bring it back). */
export const temporaryOn = (t: Temporary): boolean => t.hidden.size > 0 || t.isolated !== null;

// Elements drawn anew while isolating stay hidden only if they were not isolated: kept in step with the model.
let last = useSession.getState().render;
useSession.subscribe((s) => {
  if (s.render === last) return;
  last = s.render;
  if (temporaryOn(useTemporary.getState())) apply();
});
