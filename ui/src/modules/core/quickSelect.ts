/**
 * Quick select's rules (forum topic 73): what the dialog edits is the one condition group (shell/conditionGroup), and
 * the engine's `select.query` arguments are made of it. A condition reads a field of an element (a facet such as class,
 * type, storey, material, name; a parameter as text; or any field of the project, quantities included) and compares it:
 * is, contains, between two numbers (either end open), one of a list… as a view filter's rules do.
 */

import { queryArgs, type UCondition, type UGroup } from '../../shell/conditionGroup';

/** The first rule: a type that is… (the type most often asked for). */
export const initialGroup = (): UGroup => ({ matchAll: true, conditions: [{ field: '@type', op: '=', value: '' } as UCondition], groups: [] });

/** The rules as `select.query` reads them: a range as "min..max", a parameter by its name; a rule with nothing to compare left out. */
export const queryRules = (g: UGroup) => queryArgs(g).rules;

/** The selection a quick select makes: the ids found, added to what was selected when asked (each once). */
export function nextSelection(found: string[], selected: string[], add: boolean): string[] {
  return add ? [...new Set([...selected, ...found])] : found;
}
