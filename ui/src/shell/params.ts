/** What the properties panel and the types' configurators share about the user's parameters, and who a change goes to. */

import { useUi } from '../app/store';
import { useSession } from '../engine/session';
import type { NodeDto } from '../engine/protocol';

/** The selected elements a change goes to: every selected element of this one's kind. */
export const sameKind = (node: NodeDto): string[] => {
  const { selected } = useUi.getState();
  const nodes = useSession.getState().nodes;
  return selected.includes(node.id) ? selected.filter((id) => nodes.get(id)?.type === node.type) : [node.id];
};

/** `pset`: the property set it goes to in IFC (Pset_DoorCommon, a set of the user's); none: Lapin_Parameters. */
export interface Param { name: string; kind: string; value: unknown; options?: string[]; pset?: string }
/** A parameter's identity: its set and its name, case aside. */
export const paramKey = (p: Param) => `${(p.pset ?? '').toLowerCase()}/${p.name.toLowerCase()}`;
