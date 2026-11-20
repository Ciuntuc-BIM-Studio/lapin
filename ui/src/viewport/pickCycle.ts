/**
 * Tab cycles through what lies under the pointer (forum topic 105, as ArchiCAD and Revit do): while the pointer is over a plan,
 * section or elevation and more than one element is under it, Tab lights the next candidate (Shift+Tab the one before), the
 * status line says which ("2 of 3: Slab …"), and a click takes the lit one. With nothing or one element there, Tab keeps its
 * meaning (it folds the panels), as over a shared grip it still goes through the grip's members.
 */

import { create } from 'zustand';
import type { NodeDto } from '../engine/protocol';
import type { Vec2 } from '../platform/module';
import { notify } from '../app/store';

export interface Cycle {
  /** The view that made it. */
  key: string;
  /** Where the pointer was (view coordinates). */
  at: Vec2;
  /** The candidates, the one a click takes first. */
  ids: string[];
  index: number;
}

/** What a view offers the Tab key: the pointer's place, and the candidates there. */
export interface CycleSource {
  key: string;
  at: () => Vec2 | null;
  /** The candidates at a point, the one a plain click takes first (none while a tool or a drag is busy). */
  ids: (at: Vec2) => string[];
  /** How far (view units) the pointer may drift and still be on the same cycle. */
  tol: () => number;
  /** Takes what the pointer has lit as a click would (a tool's pick, the selection): Tab let go after a hold (tabHold.ts). */
  take?: () => void;
}

const dist = (a: Vec2, b: Vec2) => Math.hypot(a.x - b.x, a.y - b.y);
const sameSet = (a: string[], b: string[]) => a.length === b.length && a.every((x) => b.includes(x));

/**
 * Whether Tab / Shift+Tab may cycle what lies under the pointer now (forum topic 149): when nothing is running, and while a
 * tool or a command is picking elements (a pick or pickThenPoint gesture, a command step asking for a pick or a selection),
 * but not while it waits for a point or a value, nor while a drag is on (there Tab keeps its meaning).
 */
export function cycleOpen(gesture: string | undefined, stepWant: string | undefined, dragging: boolean, gestures: readonly string[] = ['pick', 'pickThenPoint']): boolean {
  if (dragging) return false;
  if (gesture && !gestures.includes(gesture)) return false;
  if (stepWant && stepWant !== 'pick' && stepWant !== 'select') return false;
  return true;
}

/** The candidates with the one a plain click takes first. */
export const withFirst = (first: string | null | undefined, ids: string[]): string[] => (first ? [first, ...ids.filter((i) => i !== first)] : ids);

/** The cycle after a Tab (dir 1) or Shift+Tab (−1): a new one starts on the second candidate (Shift: the last); null when fewer than two lie there. */
export function stepCycle(c: Cycle | null, key: string, at: Vec2, ids: string[], dir: 1 | -1, tol: number): Cycle | null {
  if (ids.length < 2) return null;
  if (!c || c.key !== key || dist(c.at, at) > tol || !sameSet(c.ids, ids)) return { key, at, ids, index: dir === 1 ? 1 : ids.length - 1 };
  return { ...c, index: (c.index + dir + ids.length) % ids.length };
}

/** The lit candidate of a cycle at a point of the same view, when the pointer has not moved off it. */
export function cycledId(c: Cycle | null, key: string, at: Vec2, tol: number): string | null {
  return c && c.key === key && c.ids.length > 1 && dist(c.at, at) <= tol ? c.ids[c.index] ?? null : null;
}

/** "2 of 3: Slab Floor 1" */
export function cycleLabel(c: Cycle, name: (id: string) => string): string {
  return `${c.index + 1} of ${c.ids.length}: ${name(c.ids[c.index])}`;
}

export const usePickCycle = create<{ cycle: Cycle | null }>(() => ({ cycle: null }));
export const clearCycle = () => { if (usePickCycle.getState().cycle) usePickCycle.setState({ cycle: null }); };

let source: CycleSource | null = null;
/** The view the pointer is over, if any. */
export const cycleSource = (): CycleSource | null => source;
/** The view the pointer is over offers itself; null (with its key) when the pointer leaves it. */
export function setCycleSource(s: CycleSource | null, key?: string): void {
  if (s) source = s;
  else if (!key || source?.key === key) { source = null; clearCycle(); }
}

/** An element's words in the status line: its class, then its name. */
const kindOf = (n: NodeDto): string => n.type.replace(/^Ifc/, '').replace(/^Lapin/, '').replace(/([a-z])([A-Z])/g, '$1 $2');
export const describeNode = (n: NodeDto | undefined, id: string): string => {
  if (!n) return id;
  if (id.includes('#')) return `Axis ${id.slice(id.indexOf('#') + 1)}`;
  return n.name && n.name !== kindOf(n) ? `${kindOf(n)} ${n.name}` : kindOf(n);
};

/**
 * Tab (back: Shift+Tab) pressed: true when more than one element lies under the pointer and the next was lit, so Tab does
 * nothing else then.
 */
export function pickCycleTab(back: boolean, nodes: Map<string, NodeDto>): boolean {
  const s = source;
  if (!s) return false;
  const at = s.at();
  if (!at) return false;
  const next = stepCycle(usePickCycle.getState().cycle, s.key, at, s.ids(at), back ? -1 : 1, s.tol());
  if (!next) return false;
  usePickCycle.setState({ cycle: next });
  notify(cycleLabel(next, (id) => describeNode(nodes.get(id.includes('#') ? id.slice(0, id.indexOf('#')) : id), id)));
  return true;
}
