/**
 * Move to storey: the selection goes to another storey (doors and windows
 * with their walls), with the level (it keeps its heights over its storey) or
 * kept where it is in space (its heights make up the difference).
 */

import { useState } from 'react';
import { X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { storeys } from '../../engine/model';

export function MoveToStoreyDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const selected = useUi((s) => s.selected);
  const all = storeys({ nodes, relations });
  // What can move: elements on a storey (a door or window goes with its wall, which the engine checks).
  const ids = selected.filter((id) => nodes.get(id)?.storeyId);
  const current = new Set(ids.map((id) => nodes.get(id)!.storeyId));
  const [target, setTarget] = useState(all.find((s) => !current.has(s.id))?.id ?? all[0]?.id ?? '');
  const [keep, setKeep] = useState<'storey' | 'world'>('storey');
  const close = () => useUi.setState({ dialog: null });
  const move = async () => {
    const r = await execute('element.moveToStorey', { ids, storeyId: target, keep }) as { moved?: string[] } | null;
    if (r) { notify(`${r.moved?.length ?? 0} element(s) moved to ${nodes.get(target)?.name ?? 'the storey'}.`); close(); }
  };
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog" role="dialog" aria-label="Move to storey" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Move to storey</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          {ids.length === 0 ? <p className="muted">Select the elements drawn on the wrong storey first.</p> : <>
            <p className="muted small">{ids.length} element(s) selected, now on {[...current].map((id) => nodes.get(id ?? '')?.name).join(', ')}.</p>
            <label className="field-line"><span className="eyebrow">To</span>
              <select value={target} onChange={(e) => setTarget(e.target.value)}>{all.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</select>
            </label>
            <label className="field-line"><input type="radio" checked={keep === 'storey'} onChange={() => setKeep('storey')} /> With the level: the same heights over the new storey</label>
            <label className="field-line"><input type="radio" checked={keep === 'world'} onChange={() => setKeep('world')} /> Kept in place: they stay where they are in space</label>
          </>}
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" disabled={!ids.length || !target} onClick={() => void move()}>Move</button>
        </footer>
      </div>
    </div>
  );
}
