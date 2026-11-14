/**
 * Quick select's rules (forum topic 73): what the dialog edits, and the engine's `select.query` arguments made of them. A
 * rule reads a field of an element (class, type, storey, material, name, or a parameter by its name) and compares it: is,
 * contains, between two numbers (either end open)… as a view filter's rules do.
 */

import { FILTER_FIELDS, FILTER_OPS } from './VisibilityDialog';

export interface QsRule { field: string; op: string; value: string; name?: string; to?: string }

/** The fields Quick select offers, the ones a selection asks most often first. */
export const QS_FIELDS: [string, string][] = [
  ...(['class', 'type', 'storey', 'material', 'param', 'name'] as const).map((k) => FILTER_FIELDS.find(([f]) => f === k)!),
  ...FILTER_FIELDS.filter(([f]) => !['class', 'type', 'storey', 'material', 'param', 'name'].includes(f)),
];

/** The operators, a range among them. */
export const QS_OPS: [string, string][] = [...FILTER_OPS.slice(0, 7), ['between', 'is between'], ...FILTER_OPS.slice(7)];

/** Whether an operator compares with a value (and a range with two). */
export const takesValue = (op: string) => op !== 'hasValue' && op !== 'hasNoValue';

/** The rules as `select.query` reads them: a range as "min..max", a parameter by its name; a rule with nothing to compare left out. */
export function queryRules(rules: QsRule[]): Record<string, string>[] {
  const out: Record<string, string>[] = [];
  for (const r of rules) {
    if (r.field === 'param' && !r.name?.trim()) continue;
    const o: Record<string, string> = { field: r.field, op: r.op };
    if (r.field === 'param') o.name = r.name!.trim();
    if (r.op === 'between') {
      if (!r.value.trim() && !(r.to ?? '').trim()) continue;
      o.value = `${r.value.trim()}..${(r.to ?? '').trim()}`;
    } else if (takesValue(r.op)) {
      if (!r.value.trim()) continue;
      o.value = r.value.trim();
    }
    out.push(o);
  }
  return out;
}

/** The selection a quick select makes: the ids found, added to what was selected when asked (each once). */
export function nextSelection(found: string[], selected: string[], add: boolean): string[] {
  return add ? [...new Set([...selected, ...found])] : found;
}
