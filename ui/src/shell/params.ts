/** What the properties panel and the types' configurators share about the user's parameters, and who a change goes to. */

import { useUi } from '../app/store';
import { useSession } from '../engine/session';
import type { NodeDto } from '../engine/protocol';

/**
 * Who a change to one element goes to: when it is among the selected, every selected element of one of `types`
 * (a tester's report, #74: Side flipped only the first of three windows); else the element alone.
 */
export function selectionLike(id: string, types: readonly string[], selected: readonly string[], typeOf: (id: string) => string | undefined): string[] {
  if (!selected.includes(id)) return [id];
  const like = selected.filter((s) => { const t = typeOf(s); return t != null && types.includes(t); });
  return like.includes(id) ? like : [id];
}

/** The selected elements a change goes to: every selected element of this one's kind, or of any of `types` (doors and windows flip together). */
export const sameKind = (node: NodeDto, types: readonly string[] = [node.type]): string[] => {
  const nodes = useSession.getState().nodes;
  return selectionLike(node.id, types, useUi.getState().selected, (id) => nodes.get(id)?.type);
};

/** `pset`: the property set it goes to in IFC (Pset_DoorCommon, a set of the user's); none: Lapin_Parameters. */
export interface Param { name: string; kind: string; value: unknown; options?: string[]; pset?: string }
/** A parameter's identity: its set and its name, case aside. */
export const paramKey = (p: Param) => `${(p.pset ?? '').toLowerCase()}/${p.name.toLowerCase()}`;
