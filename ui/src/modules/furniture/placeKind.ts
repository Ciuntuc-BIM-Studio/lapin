/** The kind of piece the Place furniture tool puts down now (S8): set by its picker, by an old kind's tool (Sofa, Bed…) and by My furniture's Place. */
import { create } from 'zustand';

export const usePlaceKind = create<{ kind: string }>(() => ({ kind: 'sofa' }));

/** The tool that places a type of the kind (a kitchen is drawn along a wall, the others put down at a click, by the Place tool set to the kind). */
export function toolForKind(kind: string): string {
  if (kind === 'kitchen') return 'furniture.kitchen';
  usePlaceKind.setState({ kind });
  return 'furniture.place';
}
