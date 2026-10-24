/**
 * Grips as the views use them. The selection's grips at one point (within a
 * few pixels) become one group: dragging it moves every element there, as one
 * edit; Alt moves only the element the group is shown for, and Tab, over a
 * shared grip, picks which one that is (each, then all). One element selected
 * moves only its own end (its neighbours stay; the join holds while they meet).
 */

import type { Grip, GripStep, Vec2 } from '../platform/module';
import { execute } from '../engine/session';

export interface GripGroup { key: string; at: Vec2; members: Grip[] }

/** Grips of different elements at one point (screen tolerance in model units) gathered; one element's own grips stay apart. */
export function groupGrips(grips: Grip[], tol: number): GripGroup[] {
  const groups: GripGroup[] = [];
  for (const g of grips) {
    const near = groups.find((x) => Math.hypot(x.at.x - g.at.x, x.at.y - g.at.y) <= tol
      && (g.kind ?? 'end') === (x.members[0].kind ?? 'end') && !x.members.some((m) => m.owner === g.owner));
    if (near) near.members.push(g);
    else groups.push({ key: `${g.owner ?? ''}:${g.key}`, at: g.at, members: [g] });
  }
  return groups;
}

/**
 * Who a drag moves: everyone in the group, or (Alt, or a picked owner) one of
 * them. `pick` counts through the members then "all" (members.length).
 */
export function movers(group: GripGroup, pick: number, alone: boolean): Grip[] {
  const n = group.members.length;
  const p = ((pick % (n + 1)) + n + 1) % (n + 1);
  if (p < n) return [group.members[p]];
  return alone ? [group.members[0]] : group.members;
}

/** Run grip steps: one command alone, several as one edit. */
export async function runSteps(steps: (GripStep | null | undefined)[]): Promise<unknown> {
  const list = steps.filter((s): s is GripStep => !!s);
  if (!list.length) return null;
  if (list.length === 1) return execute(list[0].command, list[0].args);
  return execute('edit.batch', { steps: list });
}

/** A drag let go: each mover goes to the point (its step, else its own move). */
export async function dropGrips(members: Grip[], to: Vec2): Promise<unknown> {
  const withSteps = members.filter((g) => g.step);
  for (const g of members) if (!g.step && g.move) await g.move(to);
  return runSteps(withSteps.map((g) => g.step!(to)));
}
