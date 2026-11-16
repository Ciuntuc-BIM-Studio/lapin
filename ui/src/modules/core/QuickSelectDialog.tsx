/**
 * Quick select… (forum topic 73; AutoCAD's QSELECT, Revit's Select by filter): elements chosen by rules, all of them or any,
 * in the view in front (what it shows) or in the whole project; the count found as the rules change, then selected (or
 * added to the selection). The engine finds them (`select.query`), so a view's crop, its hidden elements and its temporary
 * hide and isolate count as its drawing has them. From the right-click menu of a view and View → Select.
 */

import { backdropClose } from '../../shell/Dialog';
import { useEffect, useMemo, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { query, useSession } from '../../engine/session';
import { ConditionGroupEditor } from '../../shell/ConditionGroupEditor';
import { queryArgs, type UGroup } from '../../shell/conditionGroup';
import { front2dView, tempArgs, tempOf } from '../../viewport/viewTemporary';
import { initialGroup, nextSelection } from './quickSelect';

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
  const [group, setGroup] = useState<UGroup>(initialGroup);
  const [inView, setInView] = useState(viewId !== null);
  const [add, setAdd] = useState(false);
  const [found, setFound] = useState<{ ids: string[]; problem?: string } | null>(null);
  const args = useMemo(() => queryArgs(group), [group]);

  useEffect(() => {
    if (!args.rules.length) { setFound(null); return; }
    let live = true;
    const t = setTimeout(() => {
      const scope = inView && viewId ? { viewId, ...tempArgs(tempOf(viewId)) } : {};
      query<{ ids: string[] }>('select.query', { ...args, ...scope })
        .then((r) => { if (live) setFound({ ids: r?.ids ?? [] }); })
        .catch((e: unknown) => { if (live) setFound({ ids: [], problem: e instanceof Error ? e.message : String(e) }); });
    }, 150);
    return () => { live = false; clearTimeout(t); };
  }, [args, inView, viewId, nodes]);

  const choose = () => {
    if (!found?.ids.length) return;
    const ids = nextSelection(found.ids, useUi.getState().selected, add);
    useUi.setState({ selected: ids, selection: ids[ids.length - 1] ?? null });
    close();
  };
  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
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
          </dl>
          <ConditionGroupEditor testPrefix="qs" live group={group} onChange={setGroup} />
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
