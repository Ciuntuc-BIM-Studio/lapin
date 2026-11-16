/**
 * Conditions on any field kept as the engine keeps them (a task's rule, a generator's step), all of which must hold, in the
 * one condition editor (`ConditionGroupEditor`, audit S10): each a field (an element's facet or any field of the project),
 * an operator and a value suggested from the values the model has. The engine checks them.
 */

import { DraftGroupEditor } from './ConditionGroupEditor';
import { fromConditions, toConditions } from './conditionGroup';
import type { Condition } from './fieldSchedule';

/** A list of conditions kept as a draft while typed, handed on whenever the finished ones change; it follows the engine's own (an undo). */
export function DraftConditions({ value, onCommit, testPrefix = 'fs' }: { value: Condition[] | undefined; onCommit: (c: Condition[]) => void; testPrefix?: string }) {
  return <DraftGroupEditor fixedMatch testPrefix={testPrefix} value={fromConditions(value)} encode={toConditions}
    newCondition={() => ({ field: '', op: '=', value: '' })} onCommit={(g) => onCommit(toConditions(g) as Condition[])} />;
}
