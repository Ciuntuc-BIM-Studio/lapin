/**
 * Manage → IDS… (docs/ids.md): the project's IDS files (buildingSMART IDS 1.0), added from an .ids file and kept in the project, run
 * over the model as its IFC export writes it; each specification passes or fails with its counts, a failure names the element, the
 * requirement, what it has and what is needed, and a click shows the element in 3D. Make remarks turns each failing specification
 * into a remark with its elements selected (one undo step; Sync issues sends them to a CDE server); the results go out as a
 * workbook. The same panel is the IDS tab of Publishing when a project is open.
 */

import { backdropClose } from '../../../shell/Dialog';
import { useEffect, useRef, useState } from 'react';
import { ChevronDown, ChevronRight, FilePlus2, FileSpreadsheet, FileText, MessageSquarePlus, Play, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../../app/store';
import { useSession } from '../../../engine/session';
import { download, fileBytes, isDesktop, pickPath, safeFileName } from '../../../engine/files';
import { historyStatus, signedIn, syncIssues } from '../../../engine/remote';
import { lazyPart } from '../../../platform/lazyPart';
import {
  addIdsFile, failureText, idsFiles, idsReport, idsResults, makeIdsRemarks, moreFailures, removeIdsFile, runIds, setIdsRules, showElements, specLine, totals,
  type IdsFiles, type IdsRun, type IdsSpec,
} from './ids';

const message = (e: unknown) => (e instanceof Error ? e.message : String(e));
// W6d: Bring server IDS files into this project, when the project's server offers IDS (CdeIds.tsx, fetched then).
const IdsFromServer = lazyPart(() => import('../CdeIds').then((m) => m.IdsFromServer), { inline: true });

export function IdsDialog() {
  const close = () => useUi.setState({ dialog: null });
  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog wide" role="dialog" aria-label="IDS" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">IDS</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body cde"><IdsPanel /></div>
      </div>
    </div>
  );
}

export function IdsPanel({ server, project }: { server?: string; project?: string }) {
  const nodes = useSession((s) => s.nodes);
  const [files, setFiles] = useState<IdsFiles | null>(null);
  const [run, setRun] = useState<IdsRun | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const input = useRef<HTMLInputElement>(null);
  useEffect(() => { let live = true; void idsFiles().then((f) => { if (live) setFiles(f); }); return () => { live = false; }; }, [nodes]);
  // The last run, kept by the engine, shown again when the panel opens (marked when the model has changed since).
  useEffect(() => { let live = true; void idsResults().then((r) => { if (live && !r.none) setRun(r); }).catch(() => undefined); return () => { live = false; }; }, []);

  const work = async (what: string, f: () => Promise<void>) => {
    if (busy) return;
    setBusy(what); setError(null);
    try { await f(); } catch (e) { setError(message(e)); } finally { setBusy(null); }
  };
  const add = (list: FileList | null) => void work('add', async () => {
    for (const file of Array.from(list ?? [])) {
      const r = await addIdsFile(file);
      notify(`IDS „${r.title || r.file}” added: ${r.specifications} specifications.`);
    }
    if (input.current) input.current.value = '';
  });
  const check = () => void work('run', async () => { setRun(await runIds()); });
  const remarks = () => void work('remarks', async () => {
    const r = await makeIdsRemarks();
    notify(r.made + r.updated === 0 ? 'No failing specification: no remark to make.' : `IDS remarks: ${r.made} made, ${r.updated} brought up to date.`);
    // On a CDE server the remarks go there with the next sync: at once when signed in.
    const remote = (await historyStatus().catch(() => null))?.remotes[0];
    if (remote?.kind === 'server' && r.made + r.updated > 0 && await signedIn(remote.url)) await syncIssues(remote.url, true);
    setRun(await idsResults());
  });
  const report = () => void work('report', async () => {
    const name = safeFileName(`IDS report ${new Date().toISOString().slice(0, 10)}.xlsx`);
    if (isDesktop()) {
      const to = await pickPath('save', name);
      if (!to) return;
      await idsReport(to);
    } else {
      const r = await idsReport();
      const bytes = fileBytes(r.data);
      if (!bytes) throw new Error('The engine sent back no file.');
      download(bytes, name, 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');
    }
    notify('IDS report made.');
  });

  const sum = totals(run);
  return (
    <div className="ids-panel" data-ids-panel>
      <p className="muted small">An IDS (Information Delivery Specification, buildingSMART IDS 1.0) says what the model must hold: classes and types, attributes, property sets, classifications, materials, what is part of what. It is checked on the model as its IFC export writes it.</p>
      <div className="cde-actions">
        <button type="button" className="btn" disabled={Boolean(busy)} onClick={() => input.current?.click()} data-ids="add"><FilePlus2 size={13} /> Add IDS file…</button>
        <input ref={input} type="file" accept=".ids,.xml" multiple hidden onChange={(e) => add(e.target.files)} data-ids="file" />
        <button type="button" className="btn primary" disabled={Boolean(busy) || !files?.files.length} onClick={check} data-ids="run"><Play size={13} /> {busy === 'run' ? 'Checking…' : 'Run the check'}</button>
      </div>
      <IdsFromServer server={server} project={project} />
      {files && files.files.length === 0 && <p className="muted small">The project has no IDS file yet.</p>}
      {files && files.files.length > 0 && (
        <ul className="ids-files">
          {files.files.map((f) => (
            <li key={f.id} className="row small" data-ids-file={f.id}>
              <FileText size={12} /> <b data-no-i18n>{f.title || f.name}</b> <code className="muted" data-no-i18n>{f.file}</code>
              <span className="muted">{f.specifications} <span>specifications</span></span>
              <span className="grow" />
              <button type="button" className="icon" title="Remove this IDS file from the project" disabled={Boolean(busy)} onClick={() => void removeIdsFile(f.id)}><Trash2 size={12} /></button>
            </li>
          ))}
        </ul>
      )}
      {files && (
        <div className="ids-rules">
          <label className="row small"><input type="checkbox" checked={files.onSubmit} onChange={(e) => void setIdsRules({ onSubmit: e.target.checked })} data-ids="on-submit" /> <span>Run before Submit for approval (Manage → Issue…)</span></label>
          <label className="row small indent"><input type="checkbox" checked={files.blocks} disabled={!files.onSubmit} onChange={(e) => void setIdsRules({ blocks: e.target.checked })} data-ids="blocks" /> <span>A failing specification stops the submission (unless overridden with a note)</span></label>
        </div>
      )}
      {error && <p className="error-text" role="alert">{error}</p>}
      {run && run.files.length > 0 && (
        <div className="ids-results" data-ids-results>
          <p className="small">
            <span className={sum.fail ? 'cde-block' : 'cde-ok'}>{sum.fail ? <><span data-no-i18n>{sum.fail}</span> <span>failing</span></> : <span>Every specification passes</span>}</span>
            {' · '}<span data-no-i18n>{sum.pass}</span> <span>passing</span>
            {sum.skipped > 0 && <>{' · '}<span data-no-i18n>{sum.skipped}</span> <span>not for this IFC version</span></>}
            {run.source !== 'model' && <> · <span>checked on</span> <code data-no-i18n>{run.source}</code></>}
            {run.stale && <> · <span className="cde-warn">The model changed since: run again</span></>}
          </p>
          {run.files.map((f) => (
            <div key={f.id} className="version-group">
              <p className="eyebrow" data-no-i18n>{f.title || f.name} · {f.schema}</p>
              {f.specifications.map((s) => <SpecRow key={s.index} file={f.id} spec={s} />)}
            </div>
          ))}
          <div className="cde-actions">
            <button type="button" className="btn" disabled={Boolean(busy) || sum.fail === 0 || run.stale || run.source !== 'model'} onClick={remarks} data-ids="remarks"
              title="A remark for each failing specification, its elements selected; on a CDE server they go there with Sync issues"><MessageSquarePlus size={13} /> Make remarks</button>
            <button type="button" className="btn" disabled={Boolean(busy)} onClick={report} data-ids="xlsx"><FileSpreadsheet size={13} /> Report (XLSX)</button>
          </div>
        </div>
      )}
    </div>
  );
}

function SpecRow({ file, spec }: { file: string; spec: IdsSpec }) {
  const [open, setOpen] = useState(false);
  const [failures, setFailures] = useState(spec.failures);
  const [more, setMore] = useState(spec.more);
  useEffect(() => { setFailures(spec.failures); setMore(spec.more); }, [spec]);
  const loadMore = async () => {
    try {
      const r = await moreFailures(file, spec.index, failures.length);
      setFailures([...failures, ...r.failures]);
      setMore(Math.max(0, r.total - failures.length - r.failures.length) + (r.more ?? 0));
    } catch (e) { notify(message(e)); }
  };
  const chip = spec.status === 'pass' ? 'ok' : spec.status === 'fail' ? 'block' : 'muted';
  return (
    <div className="ids-spec" data-ids-spec={spec.index} data-ids-status={spec.status}>
      <button type="button" className="link row small" onClick={() => setOpen(!open)} aria-expanded={open}>
        {open ? <ChevronDown size={12} /> : <ChevronRight size={12} />}
        <span className={`chip ${chip}`}>{spec.status === 'pass' ? 'Pass' : spec.status === 'fail' ? 'Fail' : 'Skipped'}</span>
        <b data-no-i18n>{spec.name}</b>{spec.identifier && <code className="muted" data-no-i18n>{spec.identifier}</code>}
        <span className="muted">{specLine(spec)}</span>
      </button>
      {open && (
        <div className="ids-spec-body indent small">
          {spec.description && <p className="muted" data-no-i18n>{spec.description}</p>}
          {spec.instructions && <p data-no-i18n><span>Instructions:</span> {spec.instructions}</p>}
          {spec.requirements.length > 0 && <ul className="muted">{spec.requirements.map((r, i) => <li key={i} data-no-i18n>{r}</li>)}</ul>}
          {spec.message && <p className="cde-warn" data-no-i18n>{spec.message}</p>}
          {spec.elements.length > 1 && <button type="button" className="link" onClick={() => void showElements(spec.elements)} data-ids="show-all"><span>Select the failing elements</span> <span data-no-i18n>({spec.elements.length})</span></button>}
          {failures.length > 0 && (
            <ul className="ids-failures">
              {failures.map((f, i) => (
                <li key={`${f.globalId}-${f.facet}-${i}`}>
                  <button type="button" className="link" disabled={!f.elementId} title={f.elementId ? 'Select it and show it in 3D' : 'Not an element of the model (a part Lapin writes for it)'} onClick={() => void showElements([f.elementId!])} data-ids-failure={f.globalId}>
                    <span data-no-i18n>{failureText(f)}</span>
                  </button>
                  <span className="muted" data-no-i18n> {f.requirement} · {f.globalId}</span>
                </li>
              ))}
            </ul>
          )}
          {more > 0 && <button type="button" className="link" onClick={() => void loadMore()}><span>More</span> <span data-no-i18n>({more})</span></button>}
        </div>
      )}
    </div>
  );
}
