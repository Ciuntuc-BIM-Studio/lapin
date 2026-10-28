/**
 * What a plan shows of the other storeys (as ArchiCAD's "show on stories"):
 * its own storey's elements only, by default; those of the storeys ticked
 * besides; or every storey's; halftone if asked. It is the plan view's own
 * setting (view.setStoreys), drawn by the engine, so a sheet prints what the
 * screen shows. In the view's properties, and as a dialog (View → Storeys)
 * for the plan in front.
 */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { storeys } from '../../engine/model';
import { frontView } from '../../viewport/frontView';
import type { NodeDto } from '../../engine/protocol';
import { Storeys3DEditor } from './Storeys3D';

interface Display { mode: 'home' | 'some' | 'all'; ids: string[]; halftone: boolean }

const displayOf = (view: NodeDto): Display => {
  const s = view.props.storeys as Partial<Display> | undefined;
  return { mode: s?.mode ?? 'home', ids: s?.ids ?? [], halftone: s?.halftone === true };
};

const level = (n: NodeDto) => { const z = Number(n.props.elevation ?? 0); return `${z >= 0 ? '+' : '−'}${Math.abs(z).toFixed(2)}`; };

/** The editor: which storeys besides its own a plan shows, and how. */
export function PlanStoreysEditor({ view }: { view: NodeDto }) {
  const model = useSession();
  const all = storeys(model);
  const home = String(view.props.storeyId ?? '');
  const d = displayOf(view);
  const set = (patch: Partial<Display>) => void execute('view.setStoreys', { id: view.id, ...patch });
  const toggle = (id: string, on: boolean) => {
    const ids = on ? [...new Set([...d.ids, id])] : d.ids.filter((x) => x !== id);
    set({ mode: ids.length ? 'some' : 'home', ids });
  };
  return (
    <div className="plan-storeys">
      <dl className="fields">
        <dt title="What this plan draws of the other storeys; its own storey is always drawn">Show</dt>
        <dd>
          <select value={d.mode} onChange={(e) => set({ mode: e.target.value as Display['mode'] })}>
            <option value="home">its own storey only</option>
            <option value="some">its own and the storeys ticked</option>
            <option value="all">every storey</option>
          </select>
        </dd>
        <dt title="The other storeys' elements drawn grey, on screen and on sheets">Others halftone</dt>
        <dd><input type="checkbox" checked={d.halftone} disabled={d.mode === 'home'} onChange={(e) => set({ halftone: e.target.checked })} /></dd>
      </dl>
      <ul className="plan-storeys-list">
        {[...all].reverse().map((s) => {
          const own = s.id === home;
          const on = own || d.mode === 'all' || (d.mode === 'some' && d.ids.includes(s.id));
          return (
            <li key={s.id} className={own ? 'own' : undefined}>
              <label>
                <input type="checkbox" checked={on} disabled={own || d.mode === 'all'} onChange={(e) => toggle(s.id, e.target.checked)} />
                {' '}{s.name} <span className="muted small">{level(s)}{own ? ' · this plan' : ''}</span>
              </label>
            </li>
          );
        })}
      </ul>
      <p className="muted small">Other storeys' elements are drawn where the plan's view range reaches them (an element rising through the cut plane is cut).</p>
    </div>
  );
}

/** In a plan view's properties. */
export function PlanStoreysSection({ node }: { node: NodeDto }) {
  if (node.props.kind !== 'plan') return null;
  return <PlanStoreysEditor view={node} />;
}

/** View → Storeys: the editor for the plan in front (its own view is made when the storey has none), or for the 3D view in front. */
export function PlanStoreysDialog() {
  const nodes = useSession((s) => s.nodes);
  const [id, setId] = useState<string | null>(null);
  const close = () => useUi.setState({ dialog: null });
  const in3d = useUi((s) => s.view) === '3d';
  useEffect(() => {
    void (async () => {
      const v = useUi.getState().view;
      if (v !== 'plan' && v !== '3d') { useUi.setState({ notice: 'Which storeys a view shows is set on a plan or a 3D view: open one.' }); close(); return; }
      setId(await frontView());
    })();
  }, []);
  const view = id ? nodes.get(id) : undefined;
  const name = in3d ? (view?.name ?? '{3D}') : view?.name;
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog" role="dialog" aria-label="Storeys shown" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Storeys shown{name ? ` · ${name}` : ''}</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">{in3d && id ? <Storeys3DEditor viewId={id} /> : view ? <PlanStoreysEditor view={view} /> : <p className="muted">…</p>}</div>
        <footer className="dialog-foot"><span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}
