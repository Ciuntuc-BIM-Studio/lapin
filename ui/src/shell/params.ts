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

/**
 * Who a change any element takes goes to (its phases, its discipline; a forum user's report, topic #29: 23 beams given a phase,
 * only the first took it): when it is among the selected, every selected element that `takes` it, whatever its class; else the
 * element alone.
 */
export function selectionWhere(id: string, selected: readonly string[], takes: (id: string) => boolean): string[] {
  if (!selected.includes(id)) return [id];
  const like = selected.filter(takes);
  return like.includes(id) ? like : [id];
}

/** The selected elements a change any element takes goes to (see {@link selectionWhere}). */
export const selectionWith = (node: NodeDto, takes: (n: NodeDto) => boolean = () => true): string[] => {
  const nodes = useSession.getState().nodes;
  return selectionWhere(node.id, useUi.getState().selected, (id) => { const n = nodes.get(id); return !!n && takes(n); });
};

/** The value of `key` the elements share (the first's, and whether the others differ: shown as "varies"). */
export function sharedValue(ids: readonly string[], key: string, propsOf: (id: string) => Record<string, unknown> | undefined): { value: unknown; varies: boolean } {
  const values = ids.map((id) => propsOf(id)?.[key]);
  const first = values[0];
  return { value: first, varies: values.some((v) => JSON.stringify(v ?? null) !== JSON.stringify(first ?? null)) };
}

/** `pset`: the property set it goes to in IFC (Pset_DoorCommon, a set of the user's); none: Lapin_Parameters. */
/** `instance`: on a type, an instance default (each element given the type gets a copy it may change), not the type's property. */
export interface Param { name: string; kind: string; value: unknown; options?: string[]; pset?: string; instance?: boolean; /** A parameter worked out by a formula (docs/plan-user-extensibility.md §5.3): kept as written, its value worked out on every ask. */ formula?: string | null }
/** A parameter's identity: its set and its name, case aside. */
export const paramKey = (p: Param) => `${(p.pset ?? '').toLowerCase()}/${p.name.toLowerCase()}`;
