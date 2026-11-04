/**
 * Tasks and the model's elements (docs/plan-4d5d.md, stage 2), apart from React so it can be tested: which tasks hold
 * the elements selected (their rows marked in the Gantt), a rule in a few words, a condition of a rule toggled, what a
 * drop of the selection onto a row does, and the elements and parts a task takes as the engine worked them out.
 */

import type { NodeDto } from '../../engine/protocol';
import type { TaskDto, TaskRule } from './gantt';

export const TASK = 'IfcTask';

/** The data a drag of the selection carries (its ids, as JSON), so a Gantt row knows it from any other drag. */
export const DRAG_TYPE = 'application/x-lapin-selection';

/** The ids of a selection that are elements of the model (not tasks, which a Gantt row cannot take). */
export function elementIds(selected: readonly string[], nodes: ReadonlyMap<string, NodeDto>): string[] {
  return selected.filter((id) => { const n = nodes.get(id); return !!n && n.type !== TASK; });
}

/** The tasks holding any of the elements given (a summary holds its tasks' too): the rows the Gantt marks. */
export function tasksHolding(tasks: readonly TaskDto[], elements: readonly string[]): Set<string> {
  const out = new Set<string>();
  if (elements.length === 0) return out;
  const wanted = new Set(elements);
  for (const t of tasks) if (t.elements?.some((e) => wanted.has(e))) out.add(t.id);
  return out;
}

/** The tasks a single element is in, leaves only (where it is put, not the summaries over them). */
export function tasksOf(tasks: readonly TaskDto[], element: string): TaskDto[] {
  return tasks.filter((t) => !t.summary && t.elements?.includes(element));
}

export const RULE_KEYS = ['classes', 'types', 'storeys', 'materials', 'groups', 'phases'] as const;
export type RuleKey = (typeof RULE_KEYS)[number];

/** A rule with one value of a condition switched on or off; a condition left empty goes (it then lets everything through). */
export function toggleRule(rule: TaskRule | undefined, key: RuleKey, value: string): TaskRule {
  const next: TaskRule = { ...(rule ?? {}) };
  const now = new Set(next[key] ?? []);
  if (now.has(value)) now.delete(value); else now.add(value);
  if (now.size) next[key] = [...now]; else delete next[key];
  return next;
}

/** The part a task takes in words ("core", "finishes", a material's name, a category); null: the whole elements. */
export function partLabel(part: string | undefined, nameOf: (id: string) => string | undefined): string | null {
  if (!part) return null;
  if (part === 'core') return 'core';
  if (part === 'finishes') return 'finishes';
  if (part.startsWith('material:')) return nameOf(part.slice(9)) ?? part.slice(9);
  if (part.startsWith('category:')) return part.slice(9);
  return part;
}

/**
 * A rule in a few words, as a row's tooltip says it: its conditions' values by name, the part, the exceptions
 * ("Wall, Column · Ground floor · core · +2 −1"); "" when the task takes no elements.
 */
export function ruleSummary(t: Pick<TaskDto, 'rule' | 'part' | 'added' | 'removed'>, nameOf: (id: string) => string | undefined, classLabel: (cls: string) => string): string {
  const bits: string[] = [];
  if (t.rule) {
    for (const k of RULE_KEYS) {
      const v = t.rule[k];
      if (v?.length) bits.push(v.map((x) => (k === 'classes' ? classLabel(x) : nameOf(x) ?? x)).join(', '));
    }
    if (!bits.length) bits.push('every element');
  }
  const part = partLabel(t.part, nameOf);
  if (part) bits.push(part);
  const ex = [t.added ? `+${t.added}` : '', t.removed ? `−${t.removed}` : ''].filter(Boolean).join(' ');
  if (ex) bits.push(ex);
  return bits.join(' · ');
}

/** What a drop of the selection on a row does: added to the task, or (Shift held) taken out of it; null when it carries no elements. */
export function dropOf(data: string, shift: boolean): { ids: string[]; remove: boolean } | null {
  try {
    const ids = JSON.parse(data) as unknown;
    if (!Array.isArray(ids)) return null;
    const list = ids.filter((x): x is string => typeof x === 'string');
    return list.length ? { ids: list, remove: shift } : null;
  } catch {
    return null;
  }
}

/** The camera's distance that frames a box of this size (its diagonal) with some room round it; at least 3 m. */
export function frameDistance(min: { x: number; y: number; z: number }, max: { x: number; y: number; z: number }, fov: number): number {
  const d = Math.hypot(max.x - min.x, max.y - min.y, max.z - min.z);
  return Math.max(3, (d * 0.6) / Math.tan(fov / 2));
}
