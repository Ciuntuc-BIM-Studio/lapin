/**
 * Manage → Map imported types (docs/spec-ifc.md, Î9): each type an import made (a wall type named in the file, a door
 * of its size) becomes one of the project's own of its kind: its elements take the project's type, the imported one
 * goes. The choice is remembered in the project, so a later import of a type of that kind and name takes the project's
 * at once; the remembered choices are listed, each forgotten on its own.
 */

import { useEffect, useMemo, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { KINDS, sortTypes, type ImportedType, type RememberedType } from './mapTypes';

interface Listing { types: ImportedType[]; remembered: RememberedType[] }

export function MapTypesDialog() {
  const nodes = useSession((s) => s.nodes);
  const [listing, setListing] = useState<Listing | null>(null);
  const [choice, setChoice] = useState<Record<string, string>>({});
  const [remember, setRemember] = useState(true);
  const [busy, setBusy] = useState(false);
  const close = () => useUi.setState({ dialog: null });
  useEffect(() => { void query<Listing>('types.imported').then((r) => setListing(r ?? { types: [], remembered: [] })); }, [nodes]);
  const rows = useMemo(() => sortTypes(listing?.types ?? []), [listing]);
  const chosen = Object.entries(choice).filter(([, to]) => to);
  const map = async () => {
    setBusy(true);
    await execute('types.map', { map: chosen.map(([from, to]) => ({ from, to })), remember });
    setChoice({});
    setBusy(false);
  };
  if (!listing) return null;
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide" role="dialog" aria-label="Map imported types" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Map imported types</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <p className="muted small">Each type an import made can become one of the project's own: its elements take the project's type, and the imported one goes. A wall keeps its core where it was.</p>
          {rows.length === 0 ? <p className="muted">No imported types: an IFC, Fragments or mesh import makes them.</p> : (
            <table className="layer-table map-types">
              <thead><tr><th>Kind</th><th>Imported type</th><th className="num">Elements</th><th>Becomes</th></tr></thead>
              <tbody>
                {rows.map((t) => (
                  <tr key={t.id} className={choice[t.id] ? 'changed' : ''}>
                    <td>{KINDS[t.class] ?? t.class}</td>
                    <td title={t.file ? `${t.sourceName} (${t.file})` : t.sourceName}>{t.name}{t.file && <span className="muted small"> · {t.file}</span>}</td>
                    <td className="num">{t.count}</td>
                    <td>
                      <select value={choice[t.id] ?? ''} disabled={t.candidates.length === 0} onChange={(e) => setChoice({ ...choice, [t.id]: e.target.value })}>
                        <option value="">{t.candidates.length ? 'keep it' : 'no project type of this kind'}</option>
                        {t.candidates.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                      </select>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          <label className="check-line"><input type="checkbox" checked={remember} onChange={(e) => setRemember(e.target.checked)} /> Remember: a later import of a type of that kind and name takes the project's at once</label>
          {listing.remembered.length > 0 && (
            <>
              <p className="eyebrow">Remembered</p>
              <table className="layer-table map-types">
                <thead><tr><th>Kind</th><th>Type in the file</th><th>Becomes</th><th /></tr></thead>
                <tbody>
                  {listing.remembered.map((r) => (
                    <tr key={r.key}>
                      <td>{KINDS[r.class] ?? r.class}</td>
                      <td>{r.name}</td>
                      <td>{r.toName ?? <span className="muted">a type no longer in the project</span>}</td>
                      <td><button type="button" className="btn mini" onClick={() => void execute('types.forget', { keys: [r.key] })}>Forget</button></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </>
          )}
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Close</button>
          <button type="button" className="btn primary" disabled={busy || chosen.length === 0} onClick={() => void map()}>{busy ? 'Mapping…' : 'Map'}</button>
        </footer>
      </div>
    </div>
  );
}
