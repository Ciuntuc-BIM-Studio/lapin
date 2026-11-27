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
 * With a remote (W4, CdePublish.tsx IssueOnServer) the issue then goes on: submitted for approval on a CDE server, or its issue tag
 * made directly where the rules allow.
 * The same documents issued again are a new revision of their issue (ISO 19650: issue 1 at P01, then P02, C01…): its number and
 * GlobalId kept, the revision after its last; "New issue" stays a choice, and any earlier issue can be picked.
 * Include reference documents (stage R5, docs/plan-cde-references.md §6): the project's received documents, picked one by one or by
 * folder, go with the issue as copies of their files; a file the project does not hold is fetched from its server first, else
 * the engine leaves it out and says so.
 */

import { backdropClose } from '../../../shell/Dialog';
import { useEffect, useMemo, useReducer, useState } from 'react';
import { FileText, FolderOpen, Plus, Send, X } from 'lucide-react';
import { notify, useUi } from '../../../app/store';
import { engineInBrowser, useSession } from '../../../engine/session';
import { keepProjectFile, saveProject } from '../../../engine/files';
import { FindingsList } from './CheckDialog';
import {
  canIssue, canUseFolder, cdeCheck, cdeNames, cdeSettings, containerFor, isRevision, issue, issuedSummary, issuePhase, openFolder, openIssued, revisionFits, revisionFor,
  type CdeSettings, type CheckResult, type Names, type Series,
} from './cde';
import { PendingDialog } from '../PendingDialog';
import { documentPictures } from '../documents/documents';
import { IssueOnServer } from '../CdePublish';
import { t } from '../../../i18n';
import { publishSets, type PublishSet } from '../publish/publish';
import { fileOf, flattenFolders, folderIds, folderTree, listRefs, type RefDoc, type RefFolder } from '../references/references';
import { canSend, chosenRefs, folderState, notHeld, toggleFolder } from './issueReferences';

export function IssueDialog() {
  const nodes = useSession((s) => s.nodes);
  const path = useSession((s) => s.doc?.path ?? null);
  const sheets = useMemo(() => [...nodes.values()].filter((n) => n.type === 'LapinSheet'), [nodes]);
  const docs = useMemo(() => [...nodes.values()].filter((n) => n.type === 'LapinDocument'), [nodes]);
  const [s, setS] = useState<CdeSettings | null>(null);
  const [chosen, setChosen] = useState<Set<string> | null>(null);
  const [chosenDocs, setChosenDocs] = useState<Set<string> | null>(null);
  const [docx, setDocx] = useState(false);
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
  const [check, setCheck] = useState<{ key: string; result: CheckResult | null } | null>(null);
  const [names, setNames] = useState<Names | null>(null);
  const [anyway, setAnyway] = useState(false);
  // What the issue is: '' a new issue, else a new revision of the issue with this GlobalId; proposed from what is chosen until the user picks.
  const [target, setTarget] = useState('');
  const [picked, setPicked] = useState(false);
  // Publish sets (forum topic 225): one picked ticks its sheets; its views stay out (an issue sends sheets).
  const [publish, setPublish] = useState<PublishSet[]>([]);
  const [fromSet, setFromSet] = useState('');
  // The reference documents that go with it (stage R5): off until asked, then the project's register to pick from.
  const [withRefs, setWithRefs] = useState(false);
  const [refDocs, setRefDocs] = useState<RefDoc[]>([]);
  const [refTree, setRefTree] = useState<RefFolder[]>([]);
  const [chosenRefIds, setChosenRefIds] = useState<Set<string>>(new Set());
  const [phase, dispatch] = useReducer(issuePhase, { kind: 'form' });
  const done = phase.kind === 'done' ? phase.result : null;
  const busy = phase.kind === 'issuing';
  const close = () => useUi.setState({ dialog: null });

  // The settings follow the project, but the form's fields are not touched while an issue is shown: it stays as issued.
  useEffect(() => {
    let live = true;
    void cdeSettings().then((r) => {
      if (!r || !live) return;
      setS(r);
      setRevision((cur) => cur || r.nextRevision);
      setName((cur) => cur || `Issue ${r.nextNumber}`);
    });
    return () => { live = false; };
  }, [nodes]);
  useEffect(() => { setChosen((c) => c ?? new Set(sheets.map((x) => x.id))); }, [sheets]);
  useEffect(() => { let live = true; void publishSets().then((r) => { if (live) setPublish(r); }); return () => { live = false; }; }, [nodes]);
  const takeSet = (id: string) => {
    setFromSet(id);
    const ps = publish.find((x) => x.id === id);
    if (ps) setChosen(new Set(ps.sheets));
  };
  const fromSetViews = publish.find((x) => x.id === fromSet)?.views.length ?? 0;
  const ids = useMemo(() => sheets.filter((x) => chosen?.has(x.id)).map((x) => x.id), [sheets, chosen]);
  // The written documents go too, all ticked at first (docs/plan-documents.md).
  useEffect(() => { setChosenDocs((c) => c ?? (docs.length ? new Set(docs.map((x) => x.id)) : null)); }, [docs]);
  const docIds = useMemo(() => docs.filter((x) => chosenDocs?.has(x.id)).map((x) => x.id), [docs, chosenDocs]);
  useEffect(() => {
    if (!withRefs) return;
    let live = true;
    void Promise.all([listRefs(), folderTree()]).then(([list, tree]) => {
      if (!live) return;
      setRefDocs(list);
      setRefTree(tree?.folders ?? []);
    });
    return () => { live = false; };
  }, [withRefs, nodes]);
  const sendRefs = useMemo(() => (withRefs ? chosenRefs(refDocs, chosenRefIds) : []), [withRefs, refDocs, chosenRefIds]);
  const refsNotHeld = useMemo(() => notHeld(sendRefs), [sendRefs]);
  const toggleRef = (id: string) => setChosenRefIds((c) => { const n = new Set(c); if (n.has(id)) n.delete(id); else n.add(id); return n; });

  // The checks and the names follow what is chosen (not while the issue made is shown), a moment after the typing stops;
  // each answer is kept with what it was asked for, so an issue goes only on the checks of what is chosen now.
  const showing = done != null;
  const asked = JSON.stringify([ids, revision, status]);
  const refIds = sendRefs.map((d) => d.id);
  const namesFor = JSON.stringify([ids, docIds, model, target, refIds, dxf, set, docx]);
  useEffect(() => {
    if (showing || !chosen || !isRevision(revision)) return;
    let live = true;
    const t = setTimeout(() => {
      void (async () => {
        const c = await cdeCheck(ids, revision);
        const n = await cdeNames({ sheets: ids, documents: docIds, model, revision, status, ...(target ? { of: target } : {}), ...(refIds.length ? { references: refIds, dxf, set, documentsDocx: docx } : {}) });
        if (live) { setCheck({ key: asked, result: c }); setNames(n); }
      })();
    }, 300);
    return () => { live = false; clearTimeout(t); };
    // namesFor stands for the documents, the model and the target, whose names follow them too.
  }, [showing, chosen, ids, revision, status, asked, namesFor, nodes]);
  const current = check?.key === asked ? check.result : undefined;
  const series: Series[] = names?.series ?? [];
  const targetSeries = target ? series.find((x) => x.issueId === target) ?? null : null;

  // A new revision of an issue, or a new issue: its revision proposed after that issue's last (or the project's), its name the issue's.
  const aim = (id: string, c = contractual) => {
    const sr = id ? series.find((x) => x.issueId === id) ?? null : null;
    setTarget(id);
    setRevision(revisionFor(sr, c, s?.lastRevision ?? null));
    setName(sr ? sr.name : `Issue ${s?.nextNumber ?? 1}`);
  };
  // Until the user picks, the issue whose last revision sent exactly what is chosen is the default: a new revision of it.
  const matchId = names?.match?.issueId ?? '';
  useEffect(() => {
    if (picked || showing || !names || matchId === target) return;
    aim(matchId);
    // aim reads the series of these names; it runs when the match changes.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [matchId, picked, showing, names]);

  const toggle = (id: string) => { setFromSet(''); setChosen((c) => { const n = new Set(c); if (n.has(id)) n.delete(id); else n.add(id); return n; }); };
  const toggleDoc = (id: string) => setChosenDocs((c) => { const n = new Set(c); if (n.has(id)) n.delete(id); else n.add(id); return n; });
  const chooseStatus = (code: string) => { setStatus(code); setContainer(containerFor(code)); };
  const chooseKind = (c: boolean) => { setContractual(c); setRevision(revisionFor(targetSeries, c, s?.lastRevision ?? null)); };

  const folderHere = canUseFolder() && Boolean(s?.folder);
  const blocked = (current?.blocking ?? 0) > 0;
  const warned = (current?.warnings ?? 0) > 0;
  const ready = canIssue() && current != null && chosen && (ids.length > 0 || model || docIds.length > 0 || sendRefs.length > 0) && revisionFits(revision, targetSeries) && !blocked && (!warned || anyway) && !busy && !done;

  const go = async () => {
    if (!ready) return;
    // An issue is a version in the project's file: on the desktop the project needs its file first (as Manage → Versions asks).
    if (!engineInBrowser && !path) {
      notify('Save the project first: an issue is kept as a version in its file.');
      if (!(await saveProject(true))) return;
    }
    dispatch({ type: 'start' });
    try {
      // A DOCX carries its pictures as the interface draws them (an SVG and a PNG each), as File → Export makes them.
      const withDocx = docIds.length > 0 && docx;
      const documentImages = withDocx ? Object.fromEntries(await Promise.all(docIds.map(async (id) => [id, await documentPictures(id)] as const))) : undefined;
      // The reference documents' files above the project's keep limit, fetched from its server when it has one (the engine checks their hashes).
      const referenceData: Record<string, string> = {};
      for (const f of refsNotHeld) {
        const got = await fileOf(f.id, f.name);
        if (got?.data) referenceData[f.sha256] = got.data;
      }
      const r = await issue({
        name: name.trim(), revision, status, container, sheets: ids, model, keepModel: model && keepModel, dxf, set, note: note.trim(), documents: docIds, documentsDocx: withDocx,
        ...(documentImages ? { documentImages } : {}),
        toFolder: folderHere && toFolder, ...(engineInBrowser ? { bytes: true } : {}), ...(target ? { of: target } : {}),
        ...(sendRefs.length ? { references: sendRefs.map((d) => d.id) } : {}), ...(Object.keys(referenceData).length ? { referenceData } : {}),
      });
      if (r.file) await keepProjectFile(r.file);
      dispatch({ type: 'issued', result: r });
      if (r.folderProblem) notify(r.folderProblem);
      if (r.versionError) notify(r.versionError);
      for (const why of r.skipped ?? []) notify(why);
      notify(`Issued ${r.name}: ${r.files.length} files${r.path ? ` in ${r.container}` : ' in the project file'}.`);
    } catch (e) {
      dispatch({ type: 'failed' });
      notify(e instanceof Error ? e.message : String(e));
    }
  };

  // A new issue: the form back, its name and revision proposed after the one just made.
  const again = async () => {
    const r = await cdeSettings();
    if (r) { setS(r); setName(`Issue ${r.nextNumber}`); setRevision(revisionFor(null, contractual, r.lastRevision)); }
    setAnyway(false);
    setTarget('');
    setPicked(false);
    dispatch({ type: 'new' });
  };

  if (!s) return <PendingDialog title="Issue" />;
  const summary = done ? issuedSummary(done) : null;
  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
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
            {(done.skipped ?? []).length > 0 && <ul className="cde-warn small" data-field="issue-skipped">{done.skipped!.map((x) => <li key={x} data-no-i18n="">{x}</li>)}</ul>}
            {done.path && canUseFolder() && <button type="button" className="link" onClick={() => void openFolder(done.path!)}><FolderOpen size={12} /> Open folder</button>}
            <IssueOnServer issue={done} />
          </div>
        ) : (
          <div className="dialog-body cde">
            {!canIssue() && <p className="cde-warn">Only the host of a shared session can issue.</p>}
            <div className="cde-issue-grid">
              {series.length > 0 && (
                <label className="row"><span className="muted small loc-label">Issue as</span>
                  <select value={target} onChange={(e) => { setPicked(true); aim(e.target.value); }} aria-label="Issue as" data-field="issue-target">
                    <option value="" data-no-i18n>{`${t('New issue')} #${s.nextNumber}`}</option>
                    {series.map((x) => <option key={x.issueId} value={x.issueId} data-no-i18n>{`${t('New revision of issue')} #${x.number} · ${x.name} (${x.revisions.join(', ')})`}</option>)}
                  </select>
                  {names?.match && target === names.match.issueId && <span className="muted small">Same documents as its last revision</span>}
                </label>
              )}
              <label className="row"><span className="muted small loc-label">Issue</span><input value={name} onChange={(e) => setName(e.target.value)} aria-label="Issue name" /></label>
              <label className="row"><span className="muted small loc-label">Revision</span>
                <input value={revision} onChange={(e) => setRevision(e.target.value.toUpperCase())} aria-label="Revision" className={revisionFits(revision, targetSeries) ? undefined : 'invalid'} style={{ width: 60 }} />
                <label className="small"><input type="radio" checked={!contractual} onChange={() => chooseKind(false)} /> preliminary (P)</label>
                <label className="small"><input type="radio" checked={contractual} onChange={() => chooseKind(true)} /> contractual (C)</label>
                {targetSeries ? <span className="muted small">last <span data-no-i18n>{`#${targetSeries.number} ${targetSeries.lastRevision}`}</span></span> : s.lastRevision && <span className="muted small">last {s.lastRevision}</span>}
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
            {publish.length > 0 && (
              <label className="row small"><span className="muted">The sheets of a publish set</span>
                <select value={fromSet} onChange={(e) => takeSet(e.target.value)} aria-label="Publish set" data-field="issue-publish-set">
                  <option value="">—</option>
                  {publish.map((x) => <option key={x.id} value={x.id} data-no-i18n>{x.name}</option>)}
                </select>
                {fromSetViews > 0 && <span className="muted"><span data-no-i18n>{fromSetViews}</span> <span>views stay out: an issue sends sheets</span></span>}
              </label>
            )}
            <div className="cde-sheets">
              {sheets.length === 0 && docs.length === 0 && <p className="muted small">The project has no sheet; the model alone can go.</p>}
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
              {docs.length > 0 && (
                <div className="cde-documents" data-field="issue-documents">
                  <p className="muted small">Documents, each as a PDF:</p>
                  {docs.map((x) => {
                    const nm = names?.documents?.find((y) => y.id === x.id);
                    return (
                      <label key={x.id} className="row small" title={nm?.problems.length ? nm.problems.join('\n') : undefined}>
                        <input type="checkbox" data-field={`issue-document-${x.id}`} checked={chosenDocs?.has(x.id) ?? false} onChange={() => toggleDoc(x.id)} />
                        <span data-no-i18n="">{x.name}</span>
                        {nm && <code className={nm.problems.length ? 'cde-warn' : 'muted'}>{nm.file}</code>}
                      </label>
                    );
                  })}
                  {docIds.length > 0 && <label className="row small indent"><input type="checkbox" data-field="issue-documents-docx" checked={docx} onChange={(e) => setDocx(e.target.checked)} /> <span>Also as DOCX</span></label>}
                </div>
              )}
              <label className="row small"><input type="checkbox" data-field="issue-references" checked={withRefs} onChange={(e) => setWithRefs(e.target.checked)} /> <span>Include reference documents</span></label>
              {withRefs && (
                <div className="cde-documents indent" data-field="issue-references-list">
                  <p className="muted small">The documents the project received, as copies of their files under their own names. A folder ticks its documents and its subfolders'.</p>
                  {refDocs.length === 0 && <p className="muted small">The project has no reference documents yet (Manage → Reference documents…).</p>}
                  {refDocs.length > 0 && flattenFolders(refTree).map((fo) => {
                    const state = folderState(chosenRefIds, refDocs, refTree, fo.id);
                    const own = refDocs.filter((d) => d.folder === fo.id);
                    // A folder with nothing in it or below it is left out of the list.
                    if (own.length === 0 && !refDocs.some((d) => folderIds(refTree, fo.id).has(d.folder ?? ''))) return null;
                    return (
                      <div key={fo.id} style={{ paddingLeft: fo.depth * 14 }}>
                        <label className="row small">
                          <input type="checkbox" data-field={`issue-ref-folder-${fo.id}`} checked={state === 'on'} ref={(el) => { if (el) el.indeterminate = state === 'mixed'; }}
                            onChange={(e) => setChosenRefIds((c) => toggleFolder(c, refDocs, refTree, fo.id, e.target.checked))} />
                          <b data-no-i18n="">{fo.text}</b>
                        </label>
                        {own.map((d) => (
                          <label key={d.id} className="row small indent" title={canSend(d) ? undefined : t('Requested: it has no file yet')}>
                            <input type="checkbox" data-field={`issue-ref-${d.id}`} disabled={!canSend(d)} checked={chosenRefIds.has(d.id) && canSend(d)} onChange={() => toggleRef(d.id)} />
                            <span data-no-i18n="">{d.title}{d.number ? ` · ${d.number}` : ''}</span>
                            <code className="muted" data-no-i18n="">{d.files.map((f) => f.name).join(', ')}</code>
                            {d.status !== 'received' && <span className="muted">{d.status === 'expired' ? 'Expired' : d.status === 'requested' ? 'Requested' : d.status === 'superseded' ? 'Superseded' : d.status === 'withdrawn' ? 'Withdrawn' : ''}</span>}
                          </label>
                        ))}
                      </div>
                    );
                  })}
                  {sendRefs.length > 0 && <p className="muted small"><span data-no-i18n="">{sendRefs.length}</span> <span>reference documents go with the issue</span></p>}
                  {names?.references && names.references.length > 0 && (
                    <div className="small" data-field="issue-references-names">
                      <p className="muted">Their files in the issue, under these names:</p>
                      {names.references.flatMap((r) => r.files.map((x) => (
                        <div key={`${r.id}:${x.name}:${x.file}`} className="indent" style={{ display: 'flex', gap: 8, alignItems: 'baseline' }} title={x.file !== x.name ? t('Another file of the issue has this name: a number is put after it') : undefined}>
                          <code className={x.file !== x.name ? 'cde-warn' : 'muted'} data-no-i18n="">{x.file}</code>
                          <span className="muted" data-no-i18n="">{r.title}{r.number ? ` · ${r.number}` : ''}</span>
                        </div>
                      )))}
                    </div>
                  )}
                  {refsNotHeld.length > 0 && <p className="cde-warn small">Some files are larger than the project keeps: they are fetched from the project's server, or left out when it has none.</p>}
                </div>
              )}
              {names && <p className="muted small">With the transmittal (PDF and CSV) and the manifest: <code>{names.transmittal}</code></p>}
            </div>
            <h4>Where it goes</h4>
            <p className="muted small">Into the project file, with the issue's record and a version named after it: open, save or download the files later from Manage → Project folder.</p>
            {folderHere
              ? <label className="row small"><input type="checkbox" checked={toFolder} onChange={(e) => setToFolder(e.target.checked)} /> <span>Also into the project folder:</span> <code className="muted">{s.folder}/{container}</code></label>
              : <p className="muted small">{canUseFolder() ? 'No project folder: the issue stays in the project file; publish it to a folder later if needed.' : 'The project folder is the desktop app\'s; here the issue stays in the project file, to download as a zip.'}</p>}
            <h4>Checks</h4>
            {current ? <FindingsList result={current} /> : current === null ? <p className="error-text" role="alert">The checks could not run: an issue waits for them.</p> : <p className="muted">Checking…</p>}
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
