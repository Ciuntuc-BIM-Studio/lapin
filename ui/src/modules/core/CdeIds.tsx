/**
 * IDS on the CDE server (Atrium docs/plan-cde-web.md W6c; engine/serverIds.ts): the project's IDS files under Publishing (owners add
 * and delete them, members read and download them and open their specifications), the `ids` group of a submission's checks (each
 * specification per IDS file and model, a failing one with its requirements, failures and GlobalIds; the notices apart), and Check
 * on the server in Manage → Issue… (the dry run on the commit just saved). One lazy chunk: a session that never shows IDS on a
 * server fetches none of it.
 */

import { useEffect, useRef, useState } from 'react';
import { AlertTriangle, Copy, Download, Info, ListTree, ShieldCheck, Trash2, Upload } from 'lucide-react';
import { notify } from '../../app/store';
import { confirmAction } from '../../shell/prompts';
import { when } from './format';
import { AtriumError } from '../../engine/atrium';
import { historyStatus } from '../../engine/remote';
import { deleteIds, describeIds, dryRunIds, idsGroups, idsPath, listIds, problemLines, putIds, viewIds, type IdsFileInfo, type IdsReport, type IdsSpecView } from '../../engine/serverIds';
import { specLine } from './ids/ids';
import { saveFromServer, saveForIssue } from './CdePublish';

const message = (e: unknown) => (e instanceof Error ? e.message : String(e));
const STATUS = { pass: 'Pass', fail: 'Fail', skipped: 'Skipped' } as const;

/** The project's IDS files on the server: owners add (or replace, by name) and delete them; members read and download them. */
export function IdsServerPanel({ server, project, owner }: { server: string; project: string; owner: boolean }) {
  const [files, setFiles] = useState<IdsFileInfo[] | null>(null);
  const [failed, setFailed] = useState<string | null>(null);
  const [problems, setProblems] = useState<string[] | null>(null);
  const [open, setOpen] = useState<{ name: string; specs: IdsSpecView[] | null } | null>(null);
  const [busy, setBusy] = useState(false);
  const input = useRef<HTMLInputElement>(null);
  const load = () => { setFailed(null); listIds(server, project).then((x) => setFiles(x.files)).catch((e) => { setFailed(message(e)); setFiles([]); }); };
  useEffect(load, [server, project]);
  const work = async (f: () => Promise<unknown>) => { setBusy(true); try { await f(); } catch (e) { notify(message(e)); } finally { setBusy(false); } };
  const upload = (file: File) => work(async () => {
    setProblems(null);
    try {
      await putIds(server, project, file.name, new Uint8Array(await file.arrayBuffer()));
      notify('IDS file uploaded: pending submissions are checked against it when their checks run again.');
      load();
    } catch (e) {
      if (e instanceof AtriumError && e.code === 'bad_ids') setProblems((e.body?.problems as string[] | undefined) ?? []); else throw e;
    } finally { if (input.current) input.current.value = ''; }
  });
  const remove = (name: string) => confirmAction('Delete this IDS file? Submissions are no longer checked against it.')
    && void work(async () => { await deleteIds(server, project, name); notify('IDS file deleted.'); load(); });
  const specs = (name: string) => {
    if (open?.name === name) { setOpen(null); return; }
    setOpen({ name, specs: null });
    void viewIds(server, project, name).then((v) => setOpen((o) => (o?.name === name ? { name, specs: v.specifications } : o))).catch((e) => { notify(message(e)); setOpen(null); });
  };
  return (
    <div className="cde-register" data-cde-ids-server={files ? files.length : 'loading'}>
      {owner && <div className="cde-actions">
        <input ref={input} type="file" accept=".ids,application/xml,text/xml" hidden data-cde-ids-file onChange={(e) => { const f = e.target.files?.[0]; if (f) void upload(f); }} />
        <button type="button" className="btn" disabled={busy} onClick={() => input.current?.click()} data-cde-ids-upload><Upload size={13} />Add an IDS file…</button>
      </div>}
      {problems && (
        <div className="cde-block small" data-cde-ids-problems={problems.length}>
          <p>The server refused the file: it is not IDS 1.0.</p>
          <ul>{problemLines(problems).map((p, i) => <li key={i} data-no-i18n>{p.line != null && <b>{p.line}: </b>}{p.text}</li>)}</ul>
        </div>
      )}
      {files === null && <p className="muted small">Loading…</p>}
      {failed && <p className="muted small">{failed}</p>}
      {files?.length === 0 && !failed && <p className="muted small">This project has no IDS file on the server yet.{owner ? '' : ' The project\'s owners add IDS files.'}</p>}
      <ul className="cde-list">
        {files?.map((f) => (
          <li key={f.name} className="cde-row" data-cde-ids-row={f.name}>
            <span className="cde-row-body">
              <span className="recent-name"><span data-no-i18n>{f.name}</span>{f.version && <span className="chip" data-no-i18n>{f.version}</span>}</span>
              <span className="muted small"><span data-no-i18n>{[f.title, f.ifcVersions.join(' '), `${f.specifications}`].filter(Boolean).join(' · ')}</span> <span>specifications</span> <span data-no-i18n>· {f.uploadedBy?.name ?? ''} · {when(f.uploaded)}</span></span>
            </span>
            <span className="cde-row-actions">
              <button type="button" className="icon" title="Its specifications" onClick={() => specs(f.name)} data-cde-ids-view={f.name}><ListTree size={13} /></button>
              <button type="button" className="icon" disabled={busy} title="Download the IDS file" onClick={() => void work(() => saveFromServer(server, idsPath(project, f.name), f.name))}><Download size={13} /></button>
              {owner && <button type="button" className="icon" disabled={busy} title="Delete" onClick={() => remove(f.name)} data-cde-ids-delete={f.name}><Trash2 size={13} /></button>}
            </span>
            {open?.name === f.name && (open.specs
              ? <ol className="small" data-cde-ids-specs={open.specs.length}>{open.specs.map((s) => (
                <li key={s.index}><b data-no-i18n>{s.name}</b>{s.identifier && <code data-no-i18n> {s.identifier}</code>}{s.usage !== 'required' && <span className="chip">{s.usage}</span>} <span className="muted" data-no-i18n>{s.ifcVersion.join(' ')}</span>
                  {s.description && <div className="muted" data-no-i18n>{s.description}</div>}
                  <div><span className="muted">Applies to</span> <span data-no-i18n>{s.applicability.map((x) => x.text).join(' · ')}</span></div>
                  <div><span className="muted">Requires</span> <span data-no-i18n>{s.requirements.map((x) => (x.cardinality === 'required' ? x.text : `${x.cardinality}: ${x.text}`)).join(' · ')}</span></div>
                </li>))}</ol>
              : <p className="muted small">Loading…</p>)}
          </li>
        ))}
      </ul>
    </div>
  );
}

/** A submission's (or a dry run's) IDS group: the counts, the notices, then each IDS file over each model, specification by specification. */
export function IdsReportView({ ids }: { ids: IdsReport }) {
  const g = idsGroups(ids);
  const n = ids.summary;
  return (
    <section className="cde-checked-file" data-cde-ids-report={n.failed}>
      <p className="small cde-checked-name"><b>IDS</b> <span className={n.failed ? 'cde-block' : 'cde-ok'} data-no-i18n>{n.passed}</span> <span>passing</span> · <span data-no-i18n>{n.failed}</span> <span>failing</span>
        {n.skipped > 0 && <> · <span data-no-i18n>{n.skipped}</span> <span>not for this IFC version</span></>}</p>
      {g.notices.length > 0 && <ul className="cde-findings-list">{g.notices.map((f, i) => {
        const w = describeIds(f);
        return (
          <li key={i} className={`cde-finding sev-${f.severity}`} data-cde-finding={f.code} data-cde-severity={f.severity}>
            {f.severity === 'info' ? <Info size={12} /> : <AlertTriangle size={12} />}
            <span className="cde-finding-body"><span><code data-no-i18n>{f.code}</code> <span>{w.text}</span></span>
              <span className="cde-facts small">{w.facts.map((x, j) => <span key={j} className="cde-fact"><span className="muted">{x.label}</span> {x.translate ? <span>{x.value}</span> : <span data-no-i18n>{x.value}</span>}</span>)}</span></span>
          </li>
        );
      })}</ul>}
      {g.results.map((r) => {
        const model = describeIds({ code: '', message: '', details: { model: r.model, name: r.name } }).facts[0];
        return (
          <div key={`${r.ids} ${r.model} ${r.name}`} className="cde-severity-group" data-cde-ids-result={r.ids}>
            <p className="eyebrow small"><span data-no-i18n>{r.ids}</span> · {model.translate ? <span>{model.value}</span> : <span data-no-i18n>{model.value}</span>}</p>
            <ul className="cde-findings-list">
              {r.specs.map((s) => (
                <li key={s.index} className={`cde-finding${s.fail ? ` sev-${s.fail.finding.severity}` : ''}`} data-cde-ids-spec={s.status} data-cde-finding={s.fail ? 'IDS-01' : undefined}>
                  <span className={`chip${s.status === 'fail' ? ' warn' : ''}`}>{STATUS[s.status]}</span>
                  <span className="cde-finding-body">
                    <span><span data-no-i18n>{s.name}{s.identifier ? ` (${s.identifier})` : ''}</span> · <span className="muted">{specLine(s as Parameters<typeof specLine>[0])}</span></span>
                    {s.fail && <>
                      <span className="small"><span className="muted">Requires</span> <span data-no-i18n>{s.fail.requirements.join(' · ')}</span></span>
                      {s.fail.failures.length > 0 && <table className="cde-members small"><thead><tr><th>Element</th><th>Requirement</th><th>Actual</th><th>Expected</th></tr></thead><tbody>
                        {s.fail.failures.map((x, i) => <tr key={i} data-no-i18n><td><code>{x.globalId}</code> {x.class} {x.name ?? ''}</td><td>{x.requirement}</td><td>{x.actual}</td><td>{x.expected}</td></tr>)}
                      </tbody></table>}
                      {s.fail.more > 0 && <span className="muted small"><span data-no-i18n>+{s.fail.more}</span> <span>failures not listed</span></span>}
                      {s.fail.globalIds.length > 0 && <details className="small" data-cde-ids-globalids={s.fail.globalIdsTotal}>
                        <summary><span>Failing GlobalIds</span> <span data-no-i18n>{s.fail.globalIds.length < s.fail.globalIdsTotal ? `${s.fail.globalIds.length} / ` : ''}{s.fail.globalIdsTotal}</span>
                          <button type="button" className="icon" title="Copy" onClick={() => void navigator.clipboard?.writeText(s.fail!.globalIds.join('\n')).then(() => notify('Copied.'))}><Copy size={11} /></button></summary>
                        <code className="mono" data-no-i18n>{s.fail.globalIds.join(' ')}</code>
                      </details>}
                    </>}
                  </span>
                </li>
              ))}
            </ul>
          </div>
        );
      })}
    </section>
  );
}

/** Check on the server (Manage → Issue…, editors and owners): the project saved to the server, then the dry run of its IDS files on that commit. */
export function IdsCheckOnServer({ server, project, label }: { server: string; project: string; label: string }) {
  const [busy, setBusy] = useState(false);
  const [report, setReport] = useState<IdsReport | null>(null);
  const go = async () => {
    setBusy(true);
    try {
      if (!await saveForIssue(server, label)) return;
      const head = (await historyStatus()).head;
      if (head) setReport((await dryRunIds(server, project, head)).ids);
    } catch (e) { notify(message(e)); } finally { setBusy(false); }
  };
  return (
    <div data-cde-ids-dry-run={report ? report.summary.failed : busy ? 'busy' : 'none'}>
      <button type="button" className="btn" disabled={busy} onClick={() => void go()} title="Save to the server, then run its IDS files there on this commit; nothing is submitted" data-cde-ids-check><ShieldCheck size={13} />Check on the server</button>
      {busy && <span className="muted small"> Checking on the server…</span>}
      {report && <IdsReportView ids={report} />}
    </div>
  );
}
