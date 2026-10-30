/**
 * Manage → Issue…: an issue (docs/plan-cde.md). The sheets to print and
 * whether the model goes too, the revision (proposed from the last issue),
 * the status and the container, a note; the checks run first and are listed
 * — a blocking finding stops, warnings go with "Issue anyway". The engine
 * makes the files, named by the convention, hashes them, keeps them in the
 * project file (the IFC only when asked), writes them into the project
 * folder's container too when the project has one on this computer, records
 * the issue and saves the project as a version named after it, in one call:
 * the project is clean after. The dialog then shows that issue as issued —
 * its names, its files — until it is closed or a new issue is started.
 */

import { useCallback, useEffect, useMemo, useReducer, useState } from 'react';
import { FileText, FolderOpen, Plus, Send, X } from 'lucide-react';
import { notify, useUi } from '../../../app/store';
import { engineInBrowser, useSession } from '../../../engine/session';
import { keepProjectFile, saveProject } from '../../../engine/files';
import { FindingsList } from './CheckDialog';
import {
  canIssue, canUseFolder, cdeCheck, cdeNames, cdeSettings, containerFor, isRevision, issue, issuedSummary, issuePhase, nextRevision, openFolder, openIssued,
  type CdeSettings, type CheckResult, type Names,
} from './cde';

export function IssueDialog() {
  const nodes = useSession((s) => s.nodes);
  const path = useSession((s) => s.doc?.path ?? null);
  const sheets = useMemo(() => [...nodes.values()].filter((n) => n.type === 'LapinSheet'), [nodes]);
  const [s, setS] = useState<CdeSettings | null>(null);
  const [chosen, setChosen] = useState<Set<string> | null>(null);
  const [model, setModel] = useState(true);
  const [keepModel, setKeepModel] = useState(false);
  const [toFolder, setToFolder] = useState(true);
  const [dxf, setDxf] = useState(false);
  const [set, setSet] = useState(false);
  const [name, setName] = useState('');
  const [revision, setRevision] = useState('');
  const [contractual, setContractual] = useState(false);
  const [status, setStatus] = useState('S2');
  const [container, setContainer] = useState('Shared');
  const [note, setNote] = useState('');
  const [check, setCheck] = useState<CheckResult | null>(null);
  const [names, setNames] = useState<Names | null>(null);
  const [anyway, setAnyway] = useState(false);
  const [phase, dispatch] = useReducer(issuePhase, { kind: 'form' });
  const done = phase.kind === 'done' ? phase.result : null;
  const busy = phase.kind === 'issuing';
  const close = () => useUi.setState({ dialog: null });

  // The settings follow the project, but the form's fields are not touched while an issue is shown: it stays as issued.
  useEffect(() => {
    void cdeSettings().then((r) => {
      if (!r) return;
      setS(r);
      setRevision((cur) => cur || r.nextRevision);
      setName((cur) => cur || `Issue ${r.nextNumber}`);
    });
  }, [nodes]);
  useEffect(() => { setChosen((c) => c ?? new Set(sheets.map((x) => x.id))); }, [sheets]);
  const ids = useMemo(() => sheets.filter((x) => chosen?.has(x.id)).map((x) => x.id), [sheets, chosen]);

  // The checks and the names follow what is chosen (not while the issue made is shown).
  const showing = done != null;
  const recheck = useCallback(async () => {
    if (showing || !chosen || !isRevision(revision)) return;
    setCheck(await cdeCheck(ids, revision));
    setNames(await cdeNames({ sheets: ids, revision, status }));
  }, [showing, chosen, ids, revision, status]);
  useEffect(() => { void recheck(); }, [recheck, nodes]);

  const toggle = (id: string) => setChosen((c) => { const n = new Set(c); if (n.has(id)) n.delete(id); else n.add(id); return n; });
  const chooseStatus = (code: string) => { setStatus(code); setContainer(containerFor(code)); };
  const chooseKind = (c: boolean) => { setContractual(c); setRevision(nextRevision(s?.lastRevision ?? null, c)); };

  const folderHere = canUseFolder() && Boolean(s?.folder);
  const blocked = (check?.blocking ?? 0) > 0;
  const warned = (check?.warnings ?? 0) > 0;
  const ready = canIssue() && chosen && (ids.length > 0 || model) && isRevision(revision) && !blocked && (!warned || anyway) && !busy && !done;

  const go = async () => {
    if (!ready) return;
    // An issue is a version in the project's file: on the desktop the project needs its file first (as Manage → Versions asks).
    if (!engineInBrowser && !path) {
      notify('Save the project first: an issue is kept as a version in its file.');
      if (!(await saveProject(true))) return;
    }
    dispatch({ type: 'start' });
    try {
      const r = await issue({
        name: name.trim(), revision, status, container, sheets: ids, model, keepModel: model && keepModel, dxf, set, note: note.trim(),
        toFolder: folderHere && toFolder, ...(engineInBrowser ? { bytes: true } : {}),
      });
      if (r.file) await keepProjectFile(r.file);
      dispatch({ type: 'issued', result: r });
      if (r.folderProblem) notify(r.folderProblem);
      if (r.versionError) notify(r.versionError);
      notify(`Issued ${r.name}: ${r.files.length} files${r.path ? ` in ${r.container}` : ' in the project file'}.`);
    } catch (e) {
      dispatch({ type: 'failed' });
      notify(e instanceof Error ? e.message : String(e));
    }
  };

  // A new issue: the form back, its name and revision proposed after the one just made.
  const again = async () => {
    const r = await cdeSettings();
    if (r) { setS(r); setName(`Issue ${r.nextNumber}`); setRevision(nextRevision(r.lastRevision, contractual)); }
    setAnyway(false);
    dispatch({ type: 'new' });
  };

  if (!s) return null;
  const summary = done ? issuedSummary(done) : null;
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide" role="dialog" aria-label="Issue" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Issue</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        {done && summary ? (
          <div className="dialog-body cde" data-testid="cde-issued">
            <p className="cde-ok"><span>Issued</span> {summary.title} · {done.files.length} <span>files</span></p>
            <p className="muted small"><span>Kept in the project file:</span> <b>{summary.kept}/{done.files.length}</b></p>
            {done.saved && <p className="muted small">Saved as the version named after the issue.</p>}
            {done.path && <p className="muted small"><span>Also written into the project folder:</span> <code>{done.path}</code></p>}
            <ul className="cde-issued-files">
              {summary.files.map((f) => (
                <li key={f.name} className="row small">
                  {f.kept
                    ? <button type="button" className="link" title="Open the file kept in the project" onClick={() => void openIssued(done.id, f.name)}><FileText size={12} /> {f.name}</button>
                    : <span className="muted" title="Listed with its hash; not kept in the project file"><FileText size={12} /> {f.name}</span>}
                </li>
              ))}
            </ul>
            {done.path && canUseFolder() && <button type="button" className="link" onClick={() => void openFolder(done.path!)}><FolderOpen size={12} /> Open folder</button>}
          </div>
        ) : (
          <div className="dialog-body cde">
            {!canIssue() && <p className="cde-warn">Only the host of a shared session can issue.</p>}
            <div className="cde-issue-grid">
              <label className="row"><span className="muted small loc-label">Issue</span><input value={name} onChange={(e) => setName(e.target.value)} aria-label="Issue name" /></label>
              <label className="row"><span className="muted small loc-label">Revision</span>
                <input value={revision} onChange={(e) => setRevision(e.target.value.toUpperCase())} aria-label="Revision" className={isRevision(revision) ? undefined : 'invalid'} style={{ width: 60 }} />
                <label className="small"><input type="radio" checked={!contractual} onChange={() => chooseKind(false)} /> preliminary (P)</label>
                <label className="small"><input type="radio" checked={contractual} onChange={() => chooseKind(true)} /> contractual (C)</label>
                {s.lastRevision && <span className="muted small">last {s.lastRevision}</span>}
              </label>
              <label className="row"><span className="muted small loc-label">Status</span>
                <select value={status} onChange={(e) => chooseStatus(e.target.value)} aria-label="Status">{s.statuses.map((x) => <option key={x.code} value={x.code}>{x.code} · {x.label}</option>)}</select>
              </label>
              <label className="row"><span className="muted small loc-label">Container</span>
                <select value={container} onChange={(e) => setContainer(e.target.value)} aria-label="Container">{s.containers.map((c) => <option key={c} value={c}>{c}</option>)}</select>
              </label>
              <label className="row"><span className="muted small loc-label">Note</span><input value={note} onChange={(e) => setNote(e.target.value)} placeholder="On the transmittal (optional)" aria-label="Note" /></label>
            </div>
            <h4>What goes</h4>
            <div className="cde-sheets">
              {sheets.length === 0 && <p className="muted small">The project has no sheet; the model alone can go.</p>}
              {sheets.map((x) => {
                const nm = names?.sheets.find((y) => y.id === x.id);
                return (
                  <label key={x.id} className="row small">
                    <input type="checkbox" checked={chosen?.has(x.id) ?? false} onChange={() => toggle(x.id)} />
                    <span>{String(x.props.number ?? '')} {x.name}</span>
                    {nm && <code className="muted">{nm.file}</code>}
                  </label>
                );
              })}
              <label className="row small"><input type="checkbox" checked={model} onChange={(e) => setModel(e.target.checked)} /> <span>The model as IFC</span>{names && model && <code className="muted">{names.model}</code>}</label>
              {model && <label className="row small indent" title="A large model is tens of MB an issue: unticked, the IFC goes to the project folder only, and the project file keeps its name and hash"><input type="checkbox" checked={keepModel} onChange={(e) => setKeepModel(e.target.checked)} /> <span>Keep the IFC in the project file</span></label>}
              {model && !keepModel && !(folderHere && toFolder) && <p className="muted small indent">Without a project folder, an IFC not kept in the project file goes nowhere: only its name and hash are recorded.</p>}
              <label className="row small"><input type="checkbox" checked={dxf} onChange={(e) => setDxf(e.target.checked)} /> <span>Each sheet as DXF too</span></label>
              {ids.length > 1 && <label className="row small"><input type="checkbox" checked={set} onChange={(e) => setSet(e.target.checked)} /> <span>All the sheets in one PDF too</span>{names && set && <code className="muted">{names.set}</code>}</label>}
              {names && <p className="muted small">With the transmittal (PDF and CSV) and the manifest: <code>{names.transmittal}</code></p>}
            </div>
            <h4>Where it goes</h4>
            <p className="muted small">Into the project file, with the issue's record and a version named after it: open, save or download the files later from Manage → Project folder.</p>
            {folderHere
              ? <label className="row small"><input type="checkbox" checked={toFolder} onChange={(e) => setToFolder(e.target.checked)} /> <span>Also into the project folder:</span> <code className="muted">{s.folder}/{container}</code></label>
              : <p className="muted small">{canUseFolder() ? 'No project folder: the issue stays in the project file; publish it to a folder later if needed.' : 'The project folder is the desktop app\'s; here the issue stays in the project file, to download as a zip.'}</p>}
            <h4>Checks</h4>
            {check ? <FindingsList result={check} /> : <p className="muted">Checking…</p>}
            {blocked && <p className="cde-block">Fix what blocks, then come back: the issue cannot go.</p>}
            {!blocked && warned && <label className="row small"><input type="checkbox" checked={anyway} onChange={(e) => setAnyway(e.target.checked)} /> Issue anyway, with the warnings</label>}
          </div>
        )}
        <footer className="dialog-foot">
          <span className="grow" />
          {done && <button type="button" className="btn" onClick={() => void again()} title="The form again, for the next issue"><Plus size={13} /> New issue</button>}
          <button type="button" className="btn" onClick={close}>{done ? 'Done' : 'Cancel'}</button>
          {!done && <button type="button" className="btn primary" disabled={!ready} onClick={() => void go()} title="Keep the files in the project file (and the project folder, when asked), the transmittal with them, and save a version of the project named after the issue"><Send size={13} /> {busy ? 'Issuing…' : 'Issue'}</button>}
        </footer>
      </div>
    </div>
  );
}
