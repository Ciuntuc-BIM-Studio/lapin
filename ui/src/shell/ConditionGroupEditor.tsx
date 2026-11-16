/**
 * The one editor of conditions on elements (audit S10): view filters, Quick select, clash search sets, a field schedule's
 * filter, a task's rule and a generator's step. A group of conditions, all or any, each a field (an element's facet, or
 * any field of the project), an operator and a value, two (a range) or a list; the clash search sets and the field
 * schedules also nest groups. Each place's stored shape is converted by `conditionGroup.ts`.
 */

import { useEffect, useId, useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { FieldPicker } from './FieldPicker';
import { fieldValues } from './fieldSchedule';
import { OP_LABELS, TEXT_FACET, UNIFIED_OPS, edited, finishedGroup, isTextFacet, shapeOf, splitList, withOp, type UCondition, type UGroup, type UOp } from './conditionGroup';

export interface ConditionGroupEditorProps {
  group: UGroup;
  onChange: (g: UGroup) => void;
  /** Nested any/all groups (the clash search sets). */
  allowGroups?: boolean;
  testPrefix?: string;
  /** The text of a value handed on as it is typed (Quick select, which asks the engine at once), else when the box is left. */
  live?: boolean;
  /** The condition a new row starts as. */
  newCondition?: () => UCondition;
  /** The all/any choice's two texts. */
  matchLabels?: [string, string];
  /** All the conditions must hold, with no choice offered (a task's rule, a generator's step). */
  fixedMatch?: boolean;
}

const NEW_CONDITION = (): UCondition => ({ field: '@class', op: '=', value: '' });

/** A box that keeps its text while typed and hands it on when left (or as typed when `live`). */
function DraftInput({ value, onCommit, live, ...rest }: { value: string; onCommit: (v: string) => void; live?: boolean } & Omit<React.InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange' | 'onBlur'>) {
  const [text, setText] = useState(value);
  useEffect(() => { setText(value); }, [value]);
  return <input {...rest} value={text}
    onChange={(e) => { setText(e.target.value); if (live) onCommit(e.target.value); }}
    onBlur={() => { if (text !== value) onCommit(text); }}
    onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />;
}

function ConditionRow({ c, onChange, onRemove, testPrefix, live }: { c: UCondition; onChange: (c: UCondition) => void; onRemove: () => void; testPrefix: string; live: boolean }) {
  const [values, setValues] = useState<string[]>([]);
  const list = useId();
  const shape = shapeOf(c.op);
  const asked = isTextFacet(c.field) && !c.field.slice(TEXT_FACET.length).trim() ? '' : c.field;
  useEffect(() => {
    let on = true;
    if (asked) void fieldValues(asked).then((v) => { if (on) setValues(v); }).catch(() => undefined);
    else setValues([]);
    return () => { on = false; };
  }, [asked]);
  const hint = c.field === '@link' && (c.op === '=' || c.op === '≠') ? 'this model' : '';
  return (
    <div className="cg-row" data-testid={`${testPrefix}-condition`}>
      <FieldPicker facets value={c.field} testId={`${testPrefix}-field`} placeholder="Field…"
        onPick={(t) => onChange(edited({ field: t, op: c.op, ...(shape === 'one' ? { value: '' } : shape === 'none' ? {} : { values: shape === 'two' ? ['', ''] : [] }) }, {}))} />
      {isTextFacet(c.field) && (
        <DraftInput aria-label="Parameter" placeholder="Fire rating, height, Pset_WallCommon.IsExternal…" value={c.field.slice(TEXT_FACET.length)}
          onCommit={(n) => onChange(edited(c, { field: TEXT_FACET + n.trim() }))} />
      )}
      <select data-testid={`${testPrefix}-op`} aria-label="Operator" value={c.op} onChange={(e) => onChange(edited(withOp(c, e.target.value as UOp), {}))}>
        {UNIFIED_OPS.map((o) => <option key={o} value={o}>{OP_LABELS[o]}</option>)}
      </select>
      {shape === 'one' && <DraftInput live={live} aria-label="Value" data-testid={`${testPrefix}-value`} list={list} placeholder={hint} value={c.value ?? ''} onCommit={(v) => onChange(edited(c, { value: v }))} />}
      {shape === 'two' && <>
        <DraftInput live={live} aria-label="From" data-testid={`${testPrefix}-value`} list={list} placeholder="from" value={c.values?.[0] ?? ''} onCommit={(v) => onChange(edited(c, { values: [v, c.values?.[1] ?? ''] }))} />
        <DraftInput live={live} aria-label="To" data-testid={`${testPrefix}-to`} list={list} placeholder="to" value={c.values?.[1] ?? ''} onCommit={(v) => onChange(edited(c, { values: [c.values?.[0] ?? '', v] }))} />
      </>}
      {shape === 'list' && <DraftInput aria-label="Values" data-testid={`${testPrefix}-value`} list={list} placeholder="values, by commas" value={(c.values ?? []).join(', ')}
        onCommit={(t) => { const v = splitList(t); if (JSON.stringify(v) !== JSON.stringify(c.values ?? [])) onChange(edited(c, { values: v })); }} />}
      <datalist id={list}>{values.map((v) => <option key={v} value={v} />)}</datalist>
      <button type="button" className="icon mini" title="Remove the rule" onClick={onRemove}><Trash2 size={12} /></button>
    </div>
  );
}

function GroupView({ group, onChange, onRemove, props, depth }: { group: UGroup; onChange: (g: UGroup) => void; onRemove?: () => void; props: ConditionGroupEditorProps; depth: number }) {
  const prefix = props.testPrefix ?? 'cg';
  const [all, any] = props.matchLabels ?? ['all the rules', 'any of the rules'];
  const fresh = props.newCondition ?? NEW_CONDITION;
  return (
    <div className={depth ? 'cg-group cg-nested' : 'cg-group'} data-testid={depth ? `${prefix}-group` : `${prefix}-conditions`}>
      {(!props.fixedMatch || onRemove) && <div className="row">
        {!props.fixedMatch && <select aria-label="Match" data-testid={`${prefix}-match`} value={group.matchAll ? 'all' : 'any'} onChange={(e) => onChange({ ...group, matchAll: e.target.value === 'all' })}>
          <option value="all">{all}</option><option value="any">{any}</option>
        </select>}
        {onRemove && <button type="button" className="icon mini" title="Remove the group" onClick={onRemove}><Trash2 size={12} /></button>}
      </div>}
      {group.conditions.map((c, i) => (
        <ConditionRow key={i} c={c} testPrefix={prefix} live={!!props.live}
          onChange={(n) => onChange({ ...group, conditions: group.conditions.map((x, j) => (j === i ? n : x)) })}
          onRemove={() => onChange({ ...group, conditions: group.conditions.filter((_, j) => j !== i) })} />
      ))}
      {group.groups.map((g, i) => (
        <GroupView key={i} group={g} props={props} depth={depth + 1}
          onChange={(n) => onChange({ ...group, groups: group.groups.map((x, j) => (j === i ? n : x)) })}
          onRemove={() => onChange({ ...group, groups: group.groups.filter((_, j) => j !== i) })} />
      ))}
      <div className="row">
        <button type="button" className="btn small" data-testid={`${prefix}-add`} onClick={() => onChange({ ...group, conditions: [...group.conditions, fresh()] })}><Plus size={12} />Add a rule</button>
        {props.allowGroups && <button type="button" className="btn small" data-testid={`${prefix}-add-group`} onClick={() => onChange({ ...group, groups: [...group.groups, { matchAll: true, conditions: [fresh()], groups: [] }] })}><Plus size={12} />Add a group</button>}
      </div>
    </div>
  );
}

export function ConditionGroupEditor(props: ConditionGroupEditorProps) {
  return <GroupView group={props.group} onChange={props.onChange} props={props} depth={0} />;
}

/**
 * The editor over conditions kept elsewhere (the engine's, a dialog's): a draft while typed, its finished part handed on
 * whenever it changes (`finishedGroup`, compared as `encode` writes it); it follows the stored ones when they change
 * otherwise (an undo, another session).
 */
export function DraftGroupEditor({ value, onCommit, encode, ...props }: { value: UGroup; onCommit: (g: UGroup) => void; encode: (g: UGroup) => unknown } & Omit<ConditionGroupEditorProps, 'group' | 'onChange'>) {
  const [draft, setDraft] = useState<UGroup>(value);
  const key = JSON.stringify(encode(value));
  useEffect(() => { setDraft((d) => (JSON.stringify(encode(finishedGroup(d))) === key ? d : value)); }, [key]); // eslint-disable-line react-hooks/exhaustive-deps
  return <ConditionGroupEditor {...props} group={draft} onChange={(g) => { setDraft(g); const done = finishedGroup(g); if (JSON.stringify(encode(done)) !== key) onCommit(done); }} />;
}
