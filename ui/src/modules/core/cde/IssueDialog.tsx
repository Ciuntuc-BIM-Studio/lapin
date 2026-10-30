/**
 * Manage → Issue…: an issue into the project folder (docs/plan-cde.md). The
 * sheets to print and whether the model goes too, the revision (proposed from
 * the last issue), the status and the container, a note; the checks run first
 * and are listed — a blocking finding stops, warnings go with "Issue anyway".
 * The engine writes the files, named by the convention, hashes them, writes
 * the transmittal and records the issue; then the project is saved as a
 * version named after the issue, through the versions' own path.
 */

import { useCallback, useEffect, useMemo, useState } from 'react';
import { FolderOpen, Send, X } from 'lucide-react';
import { notify, useUi } from '../../../app/store';
import { engine, useSession } from '../../../engine/session';
import { saveProject, writeVersions } from '../../../engine/files';
import { LONG_TIMEOUT_MS } from '../../../engine/client';
import { FindingsList } from './CheckDialog';
import { canIssue, cdeCheck, cdeNames, cdeSettings, containerFor, isRevision, issue, nextRevision, openFolder, type CdeSettings, type CheckResult, type IssueResult, type Names } from './cde';

export function IssueDialog() {
  const nodes = useSession((s) => s.nodes);
  const path = useSession((s) => s.doc?.path ?? null);
  const sheets = useMemo(() => [...nodes.values()].filter((n) => n.type === 'LapinSheet'), [nodes]);
  const [s, setS] = useState<CdeSettings | null>(null);
  const [chosen, setChosen] = useState<Set<string> | null>(null);
  const [model, setModel] = useState(true);
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
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState<IssueResult | null>(null);
  const close = () => useUi.setState({ dialog: null });

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

  // The checks and the names follow what is chosen.
  const recheck = useCallback(async () => {
    if (!chosen || !isRevision(revision)) return;
    setCheck(await cdeCheck(ids, revision));
    setNames(await cdeNames({ sheets: ids, revision, status }));
  }, [chosen, ids, revision, status]);
  useEffect(() => { void recheck(); }, [recheck, nodes]);

  const toggle = (id: string) => setChosen((c) => { const n = new Set(c); if (n.has(id)) n.delete(id); else n.add(id); return n; });
  const chooseStatus = (code: string) => { setStatus(code); setContainer(containerFor(code)); };
  const chooseKind = (c: boolean) => { setContractual(c); setRevision(nextRevision(s?.lastRevision ?? null, c)); };

  const blocked = (check?.blocking ?? 0) > 0;
  const warned = (check?.warnings ?? 0) > 0;
  const ready = canIssue() && s?.folder && chosen && (ids.length > 0 || model) && isRevision(revision) && !blocked && (!warned || anyway) && !busy && !done;

  const go = async () => {
    if (!ready) return;
    setBusy(true);
    try {
      // An issue is a version in the project's file: the project needs its file first (as Manage → Versions asks).
      if (!path) {
        notify('Save the project first: an issue is kept as a version in its file.');
        if (!(await saveProject(true))) return;
      }
      const r = await issue({ name: name.trim(), revision, status, container, sheets: ids, model, dxf, set, note: note.trim() });
      // The version named after the issue, with its number so the two are found together.
      const ok = await writeVersions('project.saveVersion', { name: r.name, note: `Issue ${r.number} · ${r.revision} ${r.status} → ${r.container}` });
      if (ok) {
        const v = await engine.call<{ number: number }[]>('project.versions', {}, { timeoutMs: LONG_TIMEOUT_MS }).catch(() => []);
        const last = v[v.length - 1]?.number;
        if (last != null) await engine.call('doc.execute', { command: 'cde.setIssueVersion', args: { id: r.id, version: last } }).catch(() => undefined);
      }
      setDone(r);
      notify(`Issued ${r.name}: ${r.files.length} files in ${r.container}.`);
    } catch (e) {
      notify(e instanceof Error ? e.message : String(e));
    } finally { setBusy(false); }
  };

  if (!s) return null;
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide" role="dialog" aria-label="Issue" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Issue</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body cde">
          {!s.folder && <p className="cde-warn">Choose the project folder first (Manage → Project folder).</p>}
          {!canIssue() && <p className="cde-warn">Issuing writes files into the project folder, so it needs the desktop app.</p>}
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
              {s.folder && <span className="muted small"><code>{s.folder}/{container}</code></span>}
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
            <label className="row small"><input type="checkbox" checked={dxf} onChange={(e) => setDxf(e.target.checked)} /> <span>Each sheet as DXF too</span></label>
            {ids.length > 1 && <label className="row small"><input type="checkbox" checked={set} onChange={(e) => setSet(e.target.checked)} /> <span>All the sheets in one PDF too</span>{names && set && <code className="muted">{names.set}</code>}</label>}
            {names && <p className="muted small">With the transmittal (PDF and CSV) and the manifest: <code>{names.transmittal}</code></p>}
          </div>
          <h4>Checks</h4>
          {check ? <FindingsList result={check} /> : <p className="muted">Checking…</p>}
          {blocked && <p className="cde-block">Fix what blocks, then come back: the issue cannot go.</p>}
          {!blocked && warned && <label className="row small"><input type="checkbox" checked={anyway} onChange={(e) => setAnyway(e.target.checked)} /> Issue anyway, with the warnings</label>}
          {done && (
            <p className="cde-ok">
              <span>Issued</span> {done.name} · {done.revision} {done.status} · {done.container} · {done.files.length} <span>files</span>
              {' '}<button type="button" className="link" onClick={() => void openFolder(done.path)}><FolderOpen size={12} /> Open folder</button>
            </p>
          )}
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" className="btn" onClick={close}>{done ? 'Done' : 'Cancel'}</button>
          {!done && <button type="button" className="btn primary" disabled={!ready} onClick={() => void go()} title="Write the files into the container, the transmittal with them, and save a version of the project named after the issue"><Send size={13} /> {busy ? 'Issuing…' : 'Issue'}</button>}
        </footer>
      </div>
    </div>
  );
}
