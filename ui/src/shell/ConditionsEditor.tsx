/**
 * Conditions on any field (the filter of a field schedule, the rule of a task, a generator step): each a field from the
 * project's fields, an operator and a value suggested from the values the model has. All must hold. The engine checks them.
 */

import { useEffect, useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { FieldPicker } from './FieldPicker';
import { NO_VALUE, OPERATORS, conditionValue, fieldValues, type Condition, type Operator } from './fieldSchedule';

export function ConditionsEditor({ conditions, onChange, testPrefix = 'fs' }: { conditions: Condition[]; onChange: (c: Condition[]) => void; testPrefix?: string }) {
  const [values, setValues] = useState<Record<string, string[]>>({});
  const set = (i: number, patch: Partial<Condition>) => onChange(conditions.map((c, j) => (j === i ? { ...c, ...patch } : c)));
  const load = (field: string) => { if (field && !values[field]) void fieldValues(field).then((v) => setValues((x) => ({ ...x, [field]: v }))); };
  return (
    <div className="fs-rows" data-testid={`${testPrefix}-conditions`}>
      {conditions.map((c, i) => (
        <div className="fs-row" key={i}>
          <FieldPicker value={c.field} onPick={(t) => { set(i, { field: t }); load(t); }} placeholder="Field…" testId={`${testPrefix}-cond-field-${i}`} />
          <select value={c.op} aria-label="Operator" onChange={(e) => set(i, { op: e.target.value as Operator })}>{OPERATORS.map((p) => <option key={p} value={p}>{p}</option>)}</select>
          {!NO_VALUE.includes(c.op) && <>
            <input list={`${testPrefix}-cv-${i}`} value={c.value == null ? '' : String(c.value)} placeholder="Value" aria-label="Value" data-testid={`${testPrefix}-cond-value-${i}`}
              onFocus={() => load(c.field)} onChange={(e) => set(i, { value: e.target.value })} onBlur={(e) => set(i, { value: conditionValue(e.target.value) })} />
            <datalist id={`${testPrefix}-cv-${i}`}>{(values[c.field] ?? []).map((v) => <option key={v} value={v} />)}</datalist>
          </>}
          <button type="button" className="icon mini" title="Remove" onClick={() => onChange(conditions.filter((_, j) => j !== i))}><Trash2 size={12} /></button>
        </div>
      ))}
      <button type="button" className="btn small" data-testid={`${testPrefix}-add-condition`} onClick={() => onChange([...conditions, { field: '', op: '=', value: '' }])}><Plus size={12} />Condition</button>
    </div>
  );
}

/** Conditions as the engine takes them: no empty field, and no value when the operator takes none. */
export function cleanConditions(list: Condition[] | undefined): Condition[] {
  return (list ?? []).filter((c) => c.field.trim()).map((c): Condition => (NO_VALUE.includes(c.op) ? { field: c.field, op: c.op } : { field: c.field, op: c.op, value: c.value ?? '' }));
}

/** The conditions that are finished (a field, and a value when the operator takes one): what is sent while the others are still being typed. */
export const completeConditions = (list: Condition[] | undefined): Condition[] =>
  cleanConditions((list ?? []).filter((c) => c.field.trim() && (NO_VALUE.includes(c.op) || (c.value != null && String(c.value).trim() !== ''))));

/** A list of conditions kept as a draft while typed, handed on whenever the finished ones change; it follows the engine's own (an undo). */
export function DraftConditions({ value, onCommit, testPrefix }: { value: Condition[] | undefined; onCommit: (c: Condition[]) => void; testPrefix?: string }) {
  const [draft, setDraft] = useState<Condition[]>(value ?? []);
  const key = JSON.stringify(value ?? []);
  useEffect(() => { setDraft((d) => (JSON.stringify(completeConditions(d)) === key ? d : value ?? [])); }, [key]); // eslint-disable-line react-hooks/exhaustive-deps
  return <ConditionsEditor conditions={draft} testPrefix={testPrefix} onChange={(c) => { setDraft(c); const done = completeConditions(c); if (JSON.stringify(done) !== key) onCommit(done); }} />;
}
