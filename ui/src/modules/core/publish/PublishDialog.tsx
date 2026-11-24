/**
 * Manage → Publish…: publish sets (forum topic 225, as Revit's print and export sets). Pick a saved set or tick sheets
 * and views, choose the formats (each as a PDF, all in one PDF, each as a DXF), and export them in one step; save the
 * choice as a set, kept in the project, to export the same drawings again for the next round of a phase. Exporting
 * records nothing: an issue with its revision and transmittal is Manage → Issue…, which can start from a set's sheets.
 */

import { backdropClose } from '../../../shell/Dialog';
import { useEffect, useMemo, useState } from 'react';
import { Printer, Save, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../../app/store';
import { useSession } from '../../../engine/session';
import {
  DEFAULT_FORMATS, KIND_LABEL, PUBLISHABLE, fileCount, newSetName, publishSets, publishableViews, removePublishSet, runPublish, sameAs, savePublishSet, sheetsOf,
  type PublishChoice, type PublishFormats, type PublishSet,
} from './publish';

export function PublishDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const sheets = useMemo(() => sheetsOf(nodes), [nodes]);
  const views = useMemo(() => publishableViews(nodes, relations), [nodes, relations]);
  const [sets, setSets] = useState<PublishSet[] | null>(null);
  const [current, setCurrent] = useState('');
  const [chosenSheets, setChosenSheets] = useState<Set<string>>(new Set());
  const [chosenViews, setChosenViews] = useState<Set<string>>(new Set());
  const [formats, setFormats] = useState<PublishFormats>(DEFAULT_FORMATS);
  const [name, setName] = useState('');
  const [busy, setBusy] = useState(false);
  const close = () => useUi.setState({ dialog: null });

  useEffect(() => {
    let live = true;
    void publishSets().then((r) => { if (live) setSets(r); });
    return () => { live = false; };
  }, [nodes]);

  const set = sets?.find((s) => s.id === current) ?? null;
  // In the order the project lists them, the order the files and the one PDF take.
  const choice: PublishChoice = {
    sheets: sheets.filter((s) => chosenSheets.has(s.id)).map((s) => s.id),
    views: views.filter((v) => chosenViews.has(v.id)).map((v) => v.id),
    formats,
  };
  const count = fileCount(choice);
  const changed = set != null && (!sameAs(set, choice) || name.trim() !== set.name);

  const open = (id: string) => {
    setCurrent(id);
    const s = sets?.find((x) => x.id === id);
    setChosenSheets(new Set(s?.sheets ?? []));
    setChosenViews(new Set(s?.views ?? []));
    setFormats(s?.formats ?? DEFAULT_FORMATS);
    setName(s?.name ?? '');
  };
  const flip = (setter: (f: (c: Set<string>) => Set<string>) => void, id: string) =>
    setter((c) => { const n = new Set(c); if (n.has(id)) n.delete(id); else n.add(id); return n; });
  const all = (setter: (s: Set<string>) => void, ids: string[], on: boolean) => setter(new Set(on ? ids : []));

  const save = async (asNew: boolean) => {
    // A new set never takes another's name: the one picked keeps its name, so its copy is numbered.
    const typed = name.trim();
    const nm = !typed || (asNew && (sets ?? []).some((x) => x.name.trim().toLowerCase() === typed.toLowerCase())) ? newSetName(sets ?? [], typed || 'Set') : typed;
    try {
      const r = await savePublishSet({ ...(asNew || !set ? {} : { id: set.id }), name: nm, ...choice });
      const fresh = await publishSets();
      setSets(fresh);
      const id = r?.id ?? set?.id ?? '';
      setCurrent(id);
      setName(fresh.find((x) => x.id === id)?.name ?? nm);
      notify(`Saved the publish set ${nm}.`);
    } catch (e) { notify(e instanceof Error ? e.message : String(e)); }
  };
  const remove = async () => {
    if (!set) return;
    try {
      await removePublishSet(set.id);
      setSets(await publishSets());
      open('');
    } catch (e) { notify(e instanceof Error ? e.message : String(e)); }
  };
  const go = async () => {
    if (count === 0 || busy) return;
    setBusy(true);
    try {
      // A set saved as it is exports by its id; anything else by what is ticked, named after the set or the name typed.
      if (await runPublish(choice, name.trim() || set?.name || 'Sheets and views', set && !changed ? set.id : undefined)) close();
    } finally { setBusy(false); }
  };

  const byKind = PUBLISHABLE.map((k) => ({ kind: k, items: views.filter((v) => v.kind === k) })).filter((g) => g.items.length > 0);
  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog wide" role="dialog" aria-label="Publish" onClick={(e) => e.stopPropagation()} data-testid="publish-dialog">
        <header className="dialog-head"><span className="panel-title">Publish</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body publish">
          <p className="muted small">Sheets and views exported in one step, as PDF and DXF. Save the choice as a set to export the same drawings again; an issue with its revision and transmittal is Manage → Issue…</p>
          <div className="row">
            <span className="muted small loc-label">Publish set</span>
            <select value={current} onChange={(e) => open(e.target.value)} aria-label="Publish set" data-field="publish-set">
              <option value="">(none: tick below)</option>
              {(sets ?? []).map((s) => <option key={s.id} value={s.id} data-no-i18n>{s.name}</option>)}
            </select>
            {set && set.missing > 0 && <span className="muted small" title="Deleted since the set was saved"><span data-no-i18n>{set.missing}</span> <span>no longer in the project</span></span>}
          </div>
          <div className="row">
            <span className="muted small loc-label">Name</span>
            <input value={name} onChange={(e) => setName(e.target.value)} placeholder={newSetName(sets ?? [])} aria-label="Set name" data-field="publish-name" />
            {set && <button type="button" className="btn" disabled={!changed} onClick={() => void save(false)} title="Keep the changes in this set"><Save size={13} /> Save set</button>}
            <button type="button" className="btn" disabled={choice.sheets.length + choice.views.length === 0} onClick={() => void save(true)} title="Keep what is ticked as a new set, in the project"><Save size={13} /> Save as new set</button>
            {set && <button type="button" className="icon" onClick={() => void remove()} title="Delete this set (the sheets and views stay)" aria-label="Delete set"><Trash2 size={14} /></button>}
          </div>

          <div className="publish-columns">
            <section>
              <h4>Sheets <span className="muted small" data-no-i18n>{`${choice.sheets.length}/${sheets.length}`}</span></h4>
              {sheets.length === 0 ? <p className="muted small">The project has no sheet.</p> : (
                <>
                  <p className="row small"><button type="button" className="link" onClick={() => all(setChosenSheets, sheets.map((s) => s.id), true)}>All</button> <button type="button" className="link" onClick={() => all(setChosenSheets, [], false)}>None</button></p>
                  {sheets.map((s) => (
                    <label key={s.id} className="row small">
                      <input type="checkbox" checked={chosenSheets.has(s.id)} onChange={() => flip(setChosenSheets, s.id)} data-field={`publish-sheet-${s.id}`} />
                      <span data-no-i18n>{`${String(s.props.number ?? '')} ${s.name}`.trim()}</span>
                    </label>
                  ))}
                </>
              )}
            </section>
            <section>
              <h4>Views <span className="muted small" data-no-i18n>{`${choice.views.length}/${views.length}`}</span></h4>
              <p className="muted small">Each on a page of its own, at its scale.</p>
              {byKind.map((g) => (
                <div key={g.kind} className="publish-kind">
                  <p className="row small"><b>{KIND_LABEL[g.kind]}</b> <button type="button" className="link" onClick={() => setChosenViews((c) => new Set([...c, ...g.items.map((i) => i.id)]))}>All</button> <button type="button" className="link" onClick={() => setChosenViews((c) => new Set([...c].filter((id) => !g.items.some((i) => i.id === id))))}>None</button></p>
                  {g.items.map((v) => (
                    <label key={v.id} className="row small">
                      <input type="checkbox" checked={chosenViews.has(v.id)} onChange={() => flip(setChosenViews, v.id)} data-field={`publish-view-${v.id}`} />
                      <span data-no-i18n>{v.name}</span>
                    </label>
                  ))}
                </div>
              ))}
            </section>
          </div>

          <h4>Formats</h4>
          <label className="row small"><input type="checkbox" checked={formats.pdf} onChange={(e) => setFormats({ ...formats, pdf: e.target.checked })} data-field="publish-pdf" /> <span>Each as a PDF</span></label>
          <label className="row small"><input type="checkbox" checked={formats.combined} onChange={(e) => setFormats({ ...formats, combined: e.target.checked })} data-field="publish-combined" /> <span>All in one PDF</span></label>
          <label className="row small"><input type="checkbox" checked={formats.dxf} onChange={(e) => setFormats({ ...formats, dxf: e.target.checked })} data-field="publish-dxf" /> <span>Each as a DXF</span></label>
          <p className="muted small" data-field="publish-count">{count === 0 ? <span>Tick a sheet or a view and a format.</span> : <><span data-no-i18n>{count}</span> <span>{count === 1 ? 'file' : 'files'}</span></>}</p>
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" disabled={count === 0 || busy} onClick={() => void go()} data-field="publish-export"
            title="On the desktop into a folder you pick; in a browser as one file, or one zip"><Printer size={13} /> {busy ? 'Exporting…' : 'Export…'}</button>
        </footer>
      </div>
    </div>
  );
}
