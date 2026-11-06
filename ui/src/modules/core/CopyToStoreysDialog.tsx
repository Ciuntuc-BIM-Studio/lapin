/**
 * Copy to storeys (ArchiCAD's copy to stories, Revit's paste aligned to
 * selected levels): the selection copied onto every storey ticked, in one
 * undo step, the copies selected after. Kept with the storey (the default)
 * each copy stands at the same height over its new storey's level; kept in
 * place, where the original is in space. Hosted things (doors and windows,
 * openings, the tags and dimensions of the copied elements, a room's
 * finishes) and the relations among the copies (wall joins, attachments,
 * groups) go along unless unticked; a storey that already holds the same
 * element in the same place is skipped and said.
 */

import { useState } from 'react';
import { X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { storeys } from '../../engine/model';
import { formatLevel } from '../../engine/unitsStore';
import type { NodeDto } from '../../engine/protocol';

/** What the dialog may copy from the selection: placed elements, groups, and annotations on a plan. */
export function copyable(selected: string[], nodes: Map<string, NodeDto>): string[] {
  return selected.filter((id) => {
    const n = nodes.get(id);
    return !!n && (!!n.storeyId || n.type === 'IfcGroup' || (n.type === 'IfcAnnotation' && typeof n.props.viewId === 'string'));
  });
}

/** The storeys between two in the list (both ends in), for a Shift-click. */
export function storeyRange(order: string[], from: string, to: string): string[] {
  const [a, b] = [order.indexOf(from), order.indexOf(to)];
  if (a < 0 || b < 0) return [to];
  return order.slice(Math.min(a, b), Math.max(a, b) + 1);
}

/** The notice after a copy: how many copies on how many storeys, how many skipped. */
export function copiedNotice(copies: number, storeyCount: number, skipped: number): string {
  return `${copies} element(s) copied to ${storeyCount} storey(s)${skipped ? `; ${skipped} skipped: already there` : ''}.`;
}

export function CopyToStoreysDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const error = useSession((s) => s.error);
  const selected = useUi((s) => s.selected);
  // Highest first, as the project browser lists them.
  const all = storeys({ nodes, relations }).reverse();
  const ids = copyable(selected, nodes);
  const from = new Set(ids.map((id) => nodes.get(id)?.storeyId).filter((x): x is string => !!x));
  const fromList = all.filter((s) => from.has(s.id));
  const [ticked, setTicked] = useState<string[]>([]);
  const [last, setLast] = useState<string | null>(null);
  const [keep, setKeep] = useState<'storey' | 'world'>('storey');
  const [hosted, setHosted] = useState(true);
  const [linked, setLinked] = useState(true);
  const [skip, setSkip] = useState(true);
  const close = () => useUi.setState({ dialog: null });
  const order = all.map((s) => s.id);
  const elevation = (id: string) => Number(nodes.get(id)?.props.elevation ?? 0);
  const lowestFrom = Math.min(...[...from].map(elevation));
  const highestFrom = Math.max(...[...from].map(elevation));
  const choose = (list: string[]) => setTicked(list.filter((id) => !from.has(id)));
  const toggle = (id: string, shift: boolean) => {
    if (shift && last) {
      const range = storeyRange(order, last, id).filter((x) => !from.has(x));
      setTicked([...new Set([...ticked, ...range])]);
    } else setTicked(ticked.includes(id) ? ticked.filter((x) => x !== id) : [...ticked, id]);
    setLast(id);
  };
  const copy = async () => {
    const r = await execute('element.copyToStoreys', { ids, storeyIds: ticked, keep, hosted, relations: linked, skipDuplicates: skip }) as { ids?: string[]; storeys?: unknown[]; skipped?: unknown[] } | null;
    if (!r) return;
    const made = r.ids ?? [];
    if (made.length) useUi.setState({ selected: made, selection: made[made.length - 1] });
    notify(copiedNotice(made.length, r.storeys?.length ?? 0, r.skipped?.length ?? 0));
    close();
  };
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog copy-storeys" role="dialog" aria-label="Copy to storeys" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Copy to storeys</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          {ids.length === 0 ? <p className="muted">Select the elements to copy first.</p> : <>
            <p className="muted small">{`${ids.length} element(s) selected, now on ${fromList.map((s) => s.name).join(', ') || '—'}.`}</p>
            <div className="field-line"><span className="eyebrow">Copy to</span>
              <span className="row-buttons">
                <button type="button" className="btn small" onClick={() => choose(all.filter((s) => elevation(s.id) > highestFrom + 1e-6).map((s) => s.id))}>All above</button>
                <button type="button" className="btn small" onClick={() => choose(all.filter((s) => elevation(s.id) < lowestFrom - 1e-6).map((s) => s.id))}>All below</button>
                <button type="button" className="btn small" onClick={() => choose(order)}>All</button>
                <button type="button" className="btn small" onClick={() => setTicked([])}>None</button>
              </span>
            </div>
            <ul className="storey-ticks" aria-label="Storeys">
              {all.map((s) => (
                <li key={s.id}>
                  <label className={from.has(s.id) ? 'muted' : undefined}>
                    <input type="checkbox" disabled={from.has(s.id)} checked={ticked.includes(s.id)}
                      onChange={() => undefined} onClick={(e) => toggle(s.id, e.shiftKey)} />
                    <span className="grow">{s.name}</span>
                    <span className="muted small">{formatLevel(Number(s.props.elevation ?? 0))}</span>
                  </label>
                </li>
              ))}
            </ul>
            <p className="muted small">Shift-click a storey to tick every storey between it and the last one clicked.</p>
            <label className="copy-option"><input type="radio" checked={keep === 'storey'} onChange={() => setKeep('storey')} /> Relative: the same height over each storey's level</label>
            <label className="copy-option"><input type="radio" checked={keep === 'world'} onChange={() => setKeep('world')} /> Absolute: where the originals are in space</label>
            <label className="copy-option"><input type="checkbox" checked={hosted} onChange={(e) => setHosted(e.target.checked)} /> Hosted elements too: doors, windows, openings, their tags and dimensions, a room's finishes</label>
            <label className="copy-option"><input type="checkbox" checked={linked} onChange={(e) => setLinked(e.target.checked)} /> Rebuild relations among the copies: wall joins, attachments, groups</label>
            <label className="copy-option"><input type="checkbox" checked={skip} onChange={(e) => setSkip(e.target.checked)} /> Skip duplicates: a storey that already has the same element in the same place</label>
          </>}
        </div>
        <footer className="dialog-foot">
          {error && <p className="error-text" role="alert">{error}</p>}
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" disabled={!ids.length || !ticked.length} onClick={() => void copy()}>Copy</button>
        </footer>
      </div>
    </div>
  );
}
