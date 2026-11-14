/**
 * Quick select… (forum topic 73; AutoCAD's QSELECT, Revit's Select by filter): elements chosen by rules, all of them or any,
 * in the view in front (what it shows) or in the whole project; the count found as the rules change, then selected (or
 * added to the selection). The engine finds them (`select.query`), so a view's crop, its hidden elements and its temporary
 * hide and isolate count as its drawing has them. From the right-click menu of a view and View → Select.
 */

import { useEffect, useMemo, useState } from 'react';
import { Plus, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { query, useSession } from '../../engine/session';
import { storeys } from '../../engine/model';
import { front2dView, tempArgs, tempOf } from '../../viewport/viewTemporary';
import { nextSelection, QS_FIELDS, QS_OPS, queryRules, takesValue, type QsRule } from './quickSelect';

/** The view a quick select looks in: the 2D view in front, else the 3D view in front; null on a sheet or a schedule. */
function viewInFront(): string | null {
  const ui = useUi.getState();
  return front2dView() ?? (ui.view === '3d' ? ui.view3dId ?? '3d' : null);
}

export function QuickSelectDialog() {
  const nodes = useSession((s) => s.nodes);
  const error = useSession((s) => s.error);
  const close = () => useUi.setState({ dialog: null });
  const [viewId] = useState(viewInFront);
  const [rules, setRules] = useState<QsRule[]>([{ field: 'type', op: 'equals', value: '' }]);
  const [any, setAny] = useState(false);
  const [inView, setInView] = useState(viewId !== null);
  const [add, setAdd] = useState(false);
  const [found, setFound] = useState<{ ids: string[]; problem?: string } | null>(null);
  const args = useMemo(() => queryRules(rules), [rules]);

  // What each field's values can be, from the model: its classes, types, storeys, materials.
  const values = useMemo(() => {
    const out: Record<string, Set<string>> = { class: new Set(), type: new Set(), material: new Set(), storey: new Set(storeys(useSession.getState()).map((s) => s.name)) };
    for (const n of nodes.values()) {
      if (n.storeyId) out.class.add(n.type);
      if (n.type.endsWith('Type')) out.type.add(n.name);
      if (n.type === 'IfcMaterial') out.material.add(n.name);
    }
    return Object.fromEntries(Object.entries(out).map(([k, v]) => [k, [...v].sort((a, b) => a.localeCompare(b))]));
  }, [nodes]);

  useEffect(() => {
    if (!args.length) { setFound(null); return; }
    let live = true;
    const t = setTimeout(() => {
      const scope = inView && viewId ? { viewId, ...tempArgs(tempOf(viewId)) } : {};
      query<{ ids: string[] }>('select.query', { rules: args, match: any ? 'any' : 'all', ...scope })
        .then((r) => { if (live) setFound({ ids: r?.ids ?? [] }); })
        .catch((e: unknown) => { if (live) setFound({ ids: [], problem: e instanceof Error ? e.message : String(e) }); });
    }, 150);
    return () => { live = false; clearTimeout(t); };
  }, [args, any, inView, viewId, nodes]);

  const put = (i: number, patch: Partial<QsRule>) => setRules(rules.map((r, j) => (j === i ? { ...r, ...patch } : r)));
  const choose = () => {
    if (!found?.ids.length) return;
    const ids = nextSelection(found.ids, useUi.getState().selected, add);
    useUi.setState({ selected: ids, selection: ids[ids.length - 1] ?? null });
    close();
  };
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog" role="dialog" aria-label="Quick select" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Quick select</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <dl className="fields">
            <dt>Look in</dt>
            <dd>
              <select data-testid="qs-scope" value={inView ? 'view' : 'project'} onChange={(e) => setInView(e.target.value === 'view')}>
                {viewId && <option value="view">what the view in front shows</option>}
                <option value="project">the whole project</option>
              </select>
            </dd>
            <dt>Matching</dt>
            <dd>
              <select value={any ? 'any' : 'all'} onChange={(e) => setAny(e.target.value === 'any')}>
                <option value="all">all the rules</option><option value="any">any of the rules</option>
              </select>
            </dd>
          </dl>
          <table className="layer-table vv-rules">
            <thead><tr><th>Field</th><th>Parameter</th><th>Operator</th><th>Value</th><th /></tr></thead>
            <tbody>
              {rules.map((rule, i) => (
                <tr key={i}>
                  <td><select data-testid="qs-field" value={rule.field} onChange={(e) => put(i, { field: e.target.value, value: '', name: e.target.value === 'param' ? rule.name ?? '' : undefined })}>{QS_FIELDS.map(([k, l]) => <option key={k} value={k}>{l}</option>)}</select></td>
                  <td>{rule.field === 'param' && <input aria-label="Parameter" value={rule.name ?? ''} placeholder="Fire rating, height, Pset_WallCommon.IsExternal…" onChange={(e) => put(i, { name: e.target.value })} />}</td>
                  <td><select data-testid="qs-op" value={rule.op} onChange={(e) => put(i, { op: e.target.value })}>{QS_OPS.map(([k, l]) => <option key={k} value={k}>{l}</option>)}</select></td>
                  <td>
                    {takesValue(rule.op) && (
                      <>
                        <input aria-label="Value" data-testid="qs-value" list={values[rule.field] ? `qs-values-${rule.field}` : undefined} value={rule.value} placeholder={rule.op === 'between' ? 'from' : ''} onChange={(e) => put(i, { value: e.target.value })} />
                        {rule.op === 'between' && <input aria-label="To" data-testid="qs-to" value={rule.to ?? ''} placeholder="to" onChange={(e) => put(i, { to: e.target.value })} />}
                      </>
                    )}
                  </td>
                  <td><button type="button" className="icon mini" title="Remove the rule" disabled={rules.length < 2} onClick={() => setRules(rules.filter((_, j) => j !== i))}><X size={12} /></button></td>
                </tr>
              ))}
            </tbody>
          </table>
          {Object.entries(values).map(([k, vs]) => <datalist key={k} id={`qs-values-${k}`}>{vs.map((v) => <option key={v} value={v} />)}</datalist>)}
          <button type="button" className="btn small" onClick={() => setRules([...rules, { field: 'class', op: 'equals', value: '' }])}><Plus size={12} />Add a rule</button>
          <p className="muted small" data-testid="qs-count">
            {found?.problem ?? (found ? `${found.ids.length} elements` : 'Give a rule a value to look for.')}
          </p>
          <label className="check"><input type="checkbox" checked={add} onChange={(e) => setAdd(e.target.checked)} /> Add to the selection</label>
        </div>
        <footer className="dialog-foot">{error && <p className="error-text" role="alert">{error}</p>}<span className="grow" />
          <button type="button" className="btn" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" data-testid="qs-select" disabled={!found?.ids.length} onClick={choose}>Select</button>
        </footer>
      </div>
    </div>
  );
}
