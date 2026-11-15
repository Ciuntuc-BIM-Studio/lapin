/**
 * A by-type index of the session's nodes (audit U7): the session replaces its node map on every change and never changes one
 * it has published, so the index is built once per map and a selector asking for "the project" or "the spaces of a stair"
 * does not walk a whole model on every store update.
 */

import type { NodeDto } from '../engine/protocol';

type Nodes = ReadonlyMap<string, NodeDto>;
const indexes = new WeakMap<Nodes, Map<string, NodeDto[]>>();
const none: NodeDto[] = [];

/** The nodes of a type, in the map's order; the same array for the same map (a selector's result stays equal). */
export function nodesOfType(nodes: Nodes, type: string): readonly NodeDto[] {
  let idx = indexes.get(nodes);
  if (!idx) {
    idx = new Map();
    for (const n of nodes.values()) { const a = idx.get(n.type); if (a) a.push(n); else idx.set(n.type, [n]); }
    indexes.set(nodes, idx);
  }
  return idx.get(type) ?? none;
}

/** The project node, when one is open. */
export const projectOf = (nodes: Nodes): NodeDto | undefined => nodesOfType(nodes, 'IfcProject')[0];
