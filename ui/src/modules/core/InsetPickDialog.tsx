import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { insetSources } from './insets';

/**
 * The views that can be inset where the user clicked (the host itself left out; insets.tsx): a click on one makes the inset,
 * at the shown view's own scale, and selects it. Moved here from rebar (decision 574), where it opened from Rebar 2D → Inset view.
 */
export function InsetPickDialog() {
  const nodes = useSession((s) => s.nodes);
  const at = useUi((s) => s.insetAt);
  const close = () => useUi.setState({ dialog: null, insetAt: null });
  if (!at) return null;
  const views = insetSources(nodes, at.viewId);
  const choose = async (id: string) => {
    const r = await execute('view.inset', { viewId: at.viewId, source: id, at: [at.x, at.y] }) as { id?: string } | null;
    close();
    if (r?.id) useUi.setState({ selected: [r.id] });
  };
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Inset view">
      <div className="dialog narrow">
        <header className="dialog-head">
          <span className="panel-title">Inset view</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body">
          <p className="muted small">The view drawn where you clicked, live, at its own scale: what you draw over it goes on that view, so it fits.</p>
          {views.length === 0 && <p className="muted small">There is no other view to show. Make a section first.</p>}
          <ul className="type-list" data-testid="inset-pick">
            {views.map((v) => <li key={v.id}><button type="button" className="tree-item" data-inset-source={v.id} onClick={() => void choose(v.id)}><span data-no-i18n>{v.name}</span><span className="muted small"> · {v.kind}</span></button></li>)}
          </ul>
        </div>
      </div>
    </div>
  );
}
