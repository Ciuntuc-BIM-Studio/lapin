/**
 * Groups (docs/groups.md): an IfcGroup and its members (IfcRelAssignsToGroup). A click on a member picks the
 * group, as SketchUp's and Revit's groups do; a double-click opens it (edit mode, scope.ts), and then its members
 * are picked one by one while the rest of the model is drawn faded and cannot be picked. The engine takes a group
 * id as its members (delete, move, copy, rotate, mirror).
 */

import { create } from 'zustand';
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

/**
 * What a module draws highlighted with what is selected (a task of the work schedule: its elements), by id: null when it
 * adds nothing for that id. Registered from the modules' `expandSelection`; `useExpansion`'s version moves when what they
 * would answer changes (the schedule read again), so the views draw it again.
 */
type Expander = (id: string, node: NodeDto | undefined) => readonly string[] | null;
const expanders: Expander[] = [];
export const useExpansion = create<{ version: number }>(() => ({ version: 0 }));
export function addSelectionExpander(f: Expander): void { if (!expanders.includes(f)) expanders.push(f); }
export function expansionChanged(): void { useExpansion.setState((s) => ({ version: s.version + 1 })); }

/** What a selection draws highlighted: its elements, every member of a group in it, and what the modules add (a task's elements). */
export function shownSelection(selected: readonly string[], nodes: ReadonlyMap<string, NodeDto> = useSession.getState().nodes, relations: Relations = useSession.getState().relations): Set<string> {
  const out = new Set(selected);
  for (const id of selected) {
    const node = nodes.get(id);
    if (node?.type === GROUP) for (const m of membersOf(id, relations)) out.add(m);
    for (const f of expanders) for (const m of f(id, node) ?? []) out.add(m);
  }
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
