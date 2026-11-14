/**
 * The last selections (forum topic 73): a selection becomes history when it is cleared or replaced, and "Previous
 * selections" in the right-click menu (and the command `app.previousSelection`) selects one again. At most ten, the newest
 * first, none twice.
 */

import { create } from 'zustand';
import { useUi } from '../app/store';
import { useSession } from '../engine/session';

export const HISTORY_SIZE = 10;

const sameSet = (a: string[], b: string[]) => a.length === b.length && a.every((x) => b.includes(x));

/** `history` with `sel` put in front: an empty selection is not kept, one already there moves to the front, only the newest `size` stay. */
export function remember(history: string[][], sel: string[], size = HISTORY_SIZE): string[][] {
  if (!sel.length) return history;
  return [[...sel], ...history.filter((h) => !sameSet(h, sel))].slice(0, size);
}

/** The history without ids that are gone, and without what became empty or the same as another. */
export function pruned(history: string[][], alive: (id: string) => boolean): string[][] {
  return history.reduce<string[][]>((out, h) => {
    const live = h.filter(alive);
    return live.length && !out.some((o) => sameSet(o, live)) ? [...out, live] : out;
  }, []);
}

export const useSelectionHistory = create<{ items: string[][] }>(() => ({ items: [] }));

let last: string[] = useUi.getState().selected;
useUi.subscribe((s) => {
  if (s.selected === last) return;
  const before = last;
  last = s.selected;
  if (before.length && !sameSet(before, s.selected)) useSelectionHistory.setState((h) => ({ items: remember(h.items, before) }));
});

/** The selections that can be taken again, the one now selected left out. */
export function previousSelections(): string[][] {
  const { nodes } = useSession.getState();
  const now = useUi.getState().selected;
  return pruned(useSelectionHistory.getState().items, (id) => nodes.has(id)).filter((h) => !sameSet(h, now));
}

/** Select a past selection (the one now selected goes to history by itself). */
export function reselect(ids: string[]): void {
  useUi.setState({ selected: ids, selection: ids[ids.length - 1] ?? null });
}

/** The newest past selection selected again: pressed again it goes back and forth between the last two. */
export function previousSelection(): void {
  const prev = previousSelections()[0];
  if (prev) reselect(prev);
}

/** A selection in a few words for the menu: its names, the first three and how many more. */
export function describeSelection(ids: string[], nameOf: (id: string) => string | undefined): string {
  const names = ids.map(nameOf).filter((x): x is string => !!x);
  if (ids.length === 1) return names[0] ?? ids[0];
  return `${names.slice(0, 3).join(', ')}${ids.length > 3 ? ` +${ids.length - 3}` : ''}`;
}
