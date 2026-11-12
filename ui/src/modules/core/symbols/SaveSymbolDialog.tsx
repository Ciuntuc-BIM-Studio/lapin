/** Save as symbol… (docs/plan-user-extensibility.md §3): the name, category and scale of the symbol made from the drafting chosen. */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { notify, select, useUi } from '../../../app/store';
import { execute, useSession } from '../../../engine/session';
import { CATEGORY_FALLBACK } from './symbolModel';
import { listSymbols } from './symbols';
import { useSaveSymbol } from './symbolTools';

export function SaveSymbolDialog() {
  const request = useSaveSymbol((s) => s.request);
  const [name, setName] = useState('Symbol');
  const [category, setCategory] = useState('other');
  const [scale, setScale] = useState<'annotation' | 'model'>('annotation');
  const [replace, setReplace] = useState(true);
  const [categories, setCategories] = useState<string[]>(CATEGORY_FALLBACK);
  const [busy, setBusy] = useState(false);
  useEffect(() => { void listSymbols(false).then((l) => { if (l.categories.length) setCategories(l.categories); }); }, []);
  const close = () => { useSaveSymbol.setState({ request: null }); useUi.setState({ dialog: null }); };
  if (!request) return null;

  const go = async () => {
    if (!name.trim() || busy) return;
    setBusy(true);
    const r = await execute('drafting.toSymbol', {
      viewId: request.viewId, ids: request.ids, base: [request.base.x, request.base.y], angle: request.angle,
      name: name.trim(), category: category.trim() || 'other', scale, replace,
    }) as { instanceId?: string } | null;
    setBusy(false);
    if (!r) { notify(useSession.getState().error ?? 'The symbol could not be saved.'); return; }
    if (r.instanceId) select(r.instanceId);
    notify('Symbol saved: it is in Manage → Symbols….');
    close();
  };

  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog tpl-save" role="dialog" aria-label="Save as symbol" data-dialog="save-symbol" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Save as symbol</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <label className="doc-info-row"><span className="muted small">Name</span>
            <input autoFocus value={name} onChange={(e) => setName(e.target.value)} data-field="sy-save-name" data-no-i18n onKeyDown={(e) => { if (e.key === 'Enter') void go(); }} /></label>
          <label className="doc-info-row"><span className="muted small">Category</span>
            <input list="sy-save-categories" value={category} onChange={(e) => setCategory(e.target.value)} data-field="sy-save-category" data-no-i18n />
            <datalist id="sy-save-categories">{categories.map((c) => <option key={c} value={c} />)}</datalist></label>
          <label className="doc-info-row"><span className="muted small">Symbol scale</span>
            <select value={scale} onChange={(e) => setScale(e.target.value as 'annotation' | 'model')} data-field="sy-save-scale">
              <option value="annotation">Annotation: paper size, the same at any view scale</option>
              <option value="model">Model: real size, millimetres on the ground</option>
            </select></label>
          <label className="row"><input type="checkbox" checked={replace} onChange={(e) => setReplace(e.target.checked)} data-field="sy-save-replace" />Replace the drafting with the symbol</label>
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" disabled={!name.trim() || busy} onClick={() => void go()} data-field="sy-save-go">Save</button>
        </footer>
      </div>
    </div>
  );
}
