/**
 * The field schedule's editor (docs/plan-user-extensibility.md §11): what it lists (the dimensions and conditions on any
 * field, the model's own or a file's property), how it groups, its columns (a field or a formula, aggregated) and the totals.
 * Apply is one `architecture.updateSchedule`: one undo step. It edits the schedule in front.
 */

import { NumberField } from '../../shell/cells';
import { backdropClose } from '../../shell/Dialog';
import { useEffect, useState } from 'react';
import { ArrowDown, ArrowUp, Plus, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { ConditionsEditor } from '../../shell/ConditionsEditor';
import { FieldPicker } from '../../shell/FieldPicker';
import {
  AGGREGATES, AGGREGATE_LABELS, DIMENSIONS, UNITS, cleanOptions, fieldValues, moved, shortToken,
  type FieldColumn, type FieldOptions,
} from '../../shell/fieldSchedule';

/** Chips and a box that adds what is typed (or picked from the project's values). */
function Chips({ list, onChange, suggest, label, testId }: { list: string[]; onChange: (l: string[]) => void; suggest: string[]; label: string; testId: string }) {
  const [typed, setTyped] = useState('');
  const add = (v: string) => { const t = v.trim(); if (t && !list.includes(t)) onChange([...list, t]); setTyped(''); };
  const listId = `fs-${testId}`;
  return (
    <div className="fs-chips" data-testid={testId}>
      <span className="fs-chips-label">{label}</span>
      {list.map((v) => <span key={v} className="chip on" data-no-i18n>{v}<button type="button" className="icon mini" title="Remove" onClick={() => onChange(list.filter((x) => x !== v))}><X size={10} /></button></span>)}
      <input list={listId} value={typed} placeholder="All" onChange={(e) => { setTyped(e.target.value); if (suggest.includes(e.target.value)) add(e.target.value); }}
        onKeyDown={(e) => { if (e.key === 'Enter') add(typed); }} onBlur={() => add(typed)} />
      <datalist id={listId}>{suggest.filter((s) => !list.includes(s)).map((s) => <option key={s} value={s} />)}</datalist>
    </div>
  );
}

function Order({ i, n, on }: { i: number; n: number; on: (by: -1 | 1) => void }) {
  return <>
    <button type="button" className="icon mini" disabled={i === 0} title="Move up" onClick={() => on(-1)}><ArrowUp size={11} /></button>
    <button type="button" className="icon mini" disabled={i === n - 1} title="Move down" onClick={() => on(1)}><ArrowDown size={11} /></button>
  </>;
}

export function FieldScheduleDialog() {
  const id = useUi((s) => s.scheduleId);
  const node = useSession((s) => (id ? s.nodes.get(id) : undefined));
  const [o, setO] = useState<FieldOptions>(() => ({ ...((node?.props.fields as FieldOptions | undefined) ?? {}) }));
  const [values, setValues] = useState<Record<string, string[]>>({});
  const close = () => useUi.setState({ dialog: null });
  useEffect(() => {
    let live = true;
    for (const d of DIMENSIONS) void fieldValues(d.field).then((v) => { if (live) setValues((x) => ({ ...x, [d.key]: v })); });
    return () => { live = false; };
  }, []);
  if (!node) return null;
  const filter = o.filter ?? {};
  const conditions = filter.conditions ?? [];
  const columns = o.columns ?? [];
  const groupBy = o.groupBy ?? [];
  const setFilter = (patch: Partial<typeof filter>) => setO({ ...o, filter: { ...filter, ...patch } });
  const setColumn = (i: number, patch: Partial<FieldColumn>) => setO({ ...o, columns: columns.map((c, j) => (j === i ? { ...c, ...patch } : c)) });
  const apply = async () => {
    const r = await execute('architecture.updateSchedule', { id: node.id, fields: cleanOptions(o) });
    if (r !== null) close(); else notify('The schedule was not changed: see the message at the bottom.');
  };
  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog wide field-schedule-dialog" role="dialog" aria-label="Field schedule" data-testid="field-schedule-dialog" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title" data-no-i18n>{node.name}</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <p className="muted small">A field schedule lists any field of the elements, the model's own and those a file brings (a property of an imported IFC): filter them, group them and add up their values. It follows the model.</p>

          <h4 className="eyebrow">Which elements</h4>
          {DIMENSIONS.map((d) => (
            <Chips key={d.key} list={filter[d.key] ?? []} onChange={(l) => setFilter({ [d.key]: l })} suggest={values[d.key] ?? []} label={d.label} testId={`fs-${d.key}`} />
          ))}
          <ConditionsEditor conditions={conditions} onChange={(c) => setFilter({ conditions: c })} testPrefix="fs" />

          <h4 className="eyebrow">Group by</h4>
          <p className="muted small">One row for each different value of these fields, in this order. Nothing here: one row for each element.</p>
          <div className="fs-rows" data-testid="fs-groupby">
            {groupBy.map((g, i) => (
              <div className="fs-row" key={i}>
                <FieldPicker value={g} onPick={(t) => setO({ ...o, groupBy: groupBy.map((x, j) => (j === i ? t : x)) })} testId={`fs-group-${i}`} />
                <Order i={i} n={groupBy.length} on={(by) => setO({ ...o, groupBy: moved(groupBy, i, by) })} />
                <button type="button" className="icon mini" title="Remove" onClick={() => setO({ ...o, groupBy: groupBy.filter((_, j) => j !== i) })}><Trash2 size={12} /></button>
              </div>
            ))}
            <button type="button" className="btn small" data-testid="fs-add-group" onClick={() => setO({ ...o, groupBy: [...groupBy, ''] })}><Plus size={12} />Field</button>
          </div>

          <h4 className="eyebrow">Columns</h4>
          <p className="muted small">A field or a formula (= NetSideArea * 1.1). When rows are grouped, the aggregate says how a column's values are added up.</p>
          <div className="fs-rows" data-testid="fs-columns">
            {columns.map((c, i) => {
              const formula = c.formula !== undefined;
              return (
                <div className="fs-row fs-column" key={i}>
                  {formula
                    ? <input className="fs-formula" value={c.formula ?? ''} placeholder="= NetSideArea * 1.1" aria-label="Formula" onChange={(e) => setColumn(i, { formula: e.target.value.replace(/^\s*=\s*/, '') })} />
                    : <FieldPicker value={c.field ?? ''} onPick={(t) => setColumn(i, { field: t })} testId={`fs-col-field-${i}`} />}
                  <button type="button" className={`chip${formula ? ' on' : ''}`} title="A formula instead of a field" onClick={() => setColumn(i, formula ? { formula: undefined, field: '' } : { formula: '', field: undefined })}>=</button>
                  <input className="fs-heading" value={c.heading ?? ''} placeholder={c.field ? shortToken(c.field) : 'Heading'} aria-label="Heading" onChange={(e) => setColumn(i, { heading: e.target.value })} />
                  <select value={c.aggregate ?? ''} aria-label="Aggregate" onChange={(e) => setColumn(i, { aggregate: (e.target.value || undefined) as FieldColumn['aggregate'] })}>
                    <option value="">Automatic</option>{AGGREGATES.map((a) => <option key={a} value={a}>{AGGREGATE_LABELS[a]}</option>)}
                  </select>
                  <input list="fs-units" className="fs-unit" value={c.unit ?? ''} placeholder="Unit" aria-label="Unit" onChange={(e) => setColumn(i, { unit: e.target.value })} />
                  <NumberField min={0} max={9} integer blank className="fs-decimals" value={c.decimals} placeholder="Dec." title="Decimals" onCommit={(v) => setColumn(i, { decimals: v ?? undefined })} />
                  <Order i={i} n={columns.length} on={(by) => setO({ ...o, columns: moved(columns, i, by) })} />
                  <button type="button" className="icon mini" title="Remove" onClick={() => setO({ ...o, columns: columns.filter((_, j) => j !== i) })}><Trash2 size={12} /></button>
                </div>
              );
            })}
            <datalist id="fs-units">{UNITS.map((u) => <option key={u} value={u} />)}</datalist>
            <button type="button" className="btn small" data-testid="fs-add-column" data-i18n-ctx="schedule" onClick={() => setO({ ...o, columns: [...columns, { field: '' }] })}><Plus size={12} />Column</button>
          </div>

          <div className="row">
            <label className="check"><input type="checkbox" checked={o.totals !== false} onChange={(e) => setO({ ...o, totals: e.target.checked })} data-testid="fs-totals" />Total row</label>
            <label className="check"><input type="checkbox" checked={o.count !== false} onChange={(e) => setO({ ...o, count: e.target.checked })} data-testid="fs-count" />Count column when grouped</label>
          </div>
        </div>
        <footer className="dialog-foot">
          <span className="grow" /><button type="button" className="btn" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" data-testid="fs-apply" onClick={() => void apply()}>Apply</button>
        </footer>
      </div>
    </div>
  );
}
