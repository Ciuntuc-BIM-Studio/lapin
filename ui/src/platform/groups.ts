/**
 * Groups (docs/groups.md): an IfcGroup and its members (IfcRelAssignsToGroup). A click on a member picks the
 * group, as SketchUp's and Revit's groups do; a double-click opens it (edit mode, scope.ts), and then its members
 * are picked one by one while the rest of the model is drawn faded and cannot be picked. The engine takes a group
 * id as its members (delete, move, copy, rotate, mirror).
 */

import type { NodeDto, RelationDto } from '../engine/protocol';
import { useSession } from '../engine/session';
import { enterScope, useScope } from './scope';

export const GROUP = 'IfcGroup';
export const ASSIGNS = 'IfcRelAssignsToGroup';

type Relations = ReadonlyMap<string, RelationDto>;

const cache = new WeakMap<Relations, Map<string, string>>();

/** Each member's group, from the relationships (kept per relationships map). */
function groupIndex(relations: Relations): Map<string, string> {
  let m = cache.get(relations);
  if (m) return m;
  m = new Map();
  for (const r of relations.values()) if (r.class === ASSIGNS) for (const id of r.related) m.set(id, r.relating);
  cache.set(relations, m);
  return m;
}

export const groupOf = (id: string, relations: Relations = useSession.getState().relations): string | undefined => groupIndex(relations).get(id);

export function membersOf(groupId: string, relations: Relations = useSession.getState().relations): string[] {
  const out: string[] = [];
  for (const r of relations.values()) if (r.class === ASSIGNS && r.relating === groupId) out.push(...r.related);
  return out;
}

/** What a pick of `id` selects: its group, unless a group (or another scope) is open. */
export function pickTarget(id: string | null | undefined): string | null {
  if (!id) return null;
  if (useScope.getState().scope) return id;
  return groupOf(id) ?? id;
}

/** Several picks (a box, a lasso): each as its group, once. */
export const pickTargets = (ids: readonly string[]): string[] => [...new Set(ids.map(pickTarget).filter((x): x is string => !!x))];

/** What a selection draws highlighted: its elements, and every member of a group in it. */
export function shownSelection(selected: readonly string[], nodes: ReadonlyMap<string, NodeDto> = useSession.getState().nodes, relations: Relations = useSession.getState().relations): Set<string> {
  const out = new Set(selected);
  for (const id of selected) if (nodes.get(id)?.type === GROUP) for (const m of membersOf(id, relations)) out.add(m);
  return out;
}

/** The group opened for editing: its members picked one by one, the rest faded. */
export function openGroup(groupId: string): void {
  const node = useSession.getState().nodes.get(groupId);
  if (node?.type !== GROUP) return;
  enterScope({ id: groupId, label: node.name, kind: 'Group', includes: (n) => groupOf(n.id) === groupId });
}

/** A double-click on `id`: its group opened, when it is in one and nothing is open. */
export function openGroupOf(id: string | null | undefined): boolean {
  if (!id || useScope.getState().scope) return false;
  const g = groupOf(id);
  if (!g) return false;
  openGroup(g);
  return true;
}
