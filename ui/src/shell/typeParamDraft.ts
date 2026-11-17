/**
 * A new type's parameters before the type exists (issue #32): held in the dialog, sent with the command that makes the type, so
 * every element drawn with it has them from the start. The same rules as a type's parameters in the properties panel: a name once
 * in its set, a yes/no starting unticked (false), a standard property taking its kind from IFC's set.
 */

import { paramKey, type Param } from './params';

/** The value a new parameter starts with: unticked, empty, the first choice, zero. */
export const startValue = (kind: string, options?: string[]): unknown =>
  kind === 'bool' ? false : kind === 'text' ? '' : kind === 'choice' ? (options?.[0] ?? '') : 0;

/** The list with a parameter added (its name trimmed); the list as it was when the name is empty or already there in that set. */
export function addDraft(list: Param[], name: string, kind: string, pset?: string, options?: string[]): Param[] {
  const n = name.trim();
  if (!n) return list;
  const p: Param = { name: n, kind, value: startValue(kind, options), ...(pset ? { pset } : {}), ...(kind === 'choice' && options?.length ? { options } : {}) };
  return list.some((x) => paramKey(x) === paramKey(p)) ? list : [...list, p];
}

/** The list with one parameter changed. */
export const patchDraft = (list: Param[], key: string, patch: Partial<Param>): Param[] => list.map((p) => (paramKey(p) === key ? { ...p, ...patch } : p));

/** The list as the command takes it ([{name, kind, value, pset?, options?, instance?}]). */
export const draftJson = (list: Param[]) => list.map((p) => ({
  name: p.name, kind: p.kind, value: p.value ?? null, ...(p.pset ? { pset: p.pset } : {}), ...(p.options?.length ? { options: p.options } : {}), ...(p.instance ? { instance: true } : {}),
}));
