/**
 * A part's native members (audit S6): walls, slabs, furniture… held by a part (`studio.addPartMembers`), their drives (an attribute
 * given by a formula of the part's parameters) and the rules the interface follows: what "Add to part" sends, which elements a
 * part's edit mode lets be picked. The commands and the tree are the engine's; this is the part without the engine.
 */

import type { NodeDto } from '../../engine/protocol';

/** A member as `studio.partTree` lists it. */
export interface PartMember {
  key: string; class: string; name: string;
  /** This placed part's element. */
  id: string;
  drives?: Record<string, string>;
  /** The attributes a formula can drive. */
  drivable: string[];
}

/** The elements a placed part holds now (`props.memberIds`, {slot: id}): the slots' ids, each once. */
export function memberIdsOf(props: Record<string, unknown> | null | undefined): string[] {
  const m = props?.memberIds;
  if (!m || typeof m !== 'object' || Array.isArray(m)) return [];
  return [...new Set(Object.values(m as Record<string, unknown>).filter((v): v is string => typeof v === 'string' && v !== ''))];
}

/** The member's drives as rows, in the order of its attributes. */
export const driveRows = (m: PartMember): { attr: string; formula: string }[] =>
  Object.entries(m.drives ?? {}).map(([attr, formula]) => ({ attr, formula: String(formula) }));

/** The attributes of a member still free to take a drive. */
export const freeAttrs = (m: PartMember): string[] => m.drivable.filter((a) => !(m.drives && a in m.drives));

/** The `drives` the engine takes to set an attribute's formula (a blank one clears it) or to clear it. */
export function driveChange(attr: string, formula: string | null): Record<string, string | null> {
  const f = formula?.trim().replace(/^=\s*/, '') ?? '';
  return { [attr]: f ? f : null };
}

/** What Add to part sends: the one placed part of the selection with the others as its new members, or the sentence saying what to select. */
export function addToPartRequest(selected: readonly string[], nodes: ReadonlyMap<string, NodeDto>): { partId: string; ids: string[] } | { notice: string } {
  const parts = selected.filter((id) => nodes.get(id)?.props.operation === 'part');
  if (parts.length !== 1) return { notice: 'Select one placed part and the elements to put into it: they become part of it, in every object of the part.' };
  const ids = selected.filter((id) => id !== parts[0] && nodes.has(id));
  if (!ids.length) return { notice: 'Select the elements to put into the part together with the part itself.' };
  return { partId: parts[0], ids };
}
