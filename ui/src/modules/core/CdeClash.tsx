/**
 * Clash detection on the CDE server (Atrium docs/atrium-server.md § Clash detection on the server; engine/serverAnalysis.ts): the `clash`
 * group of a submission's checks, the project's own clash tests (Manage → Clash detection) run by the server with Lapin's engine. The
 * counts, each test, then on demand the clashes themselves, the new ones first, with both elements (name, class, storey, GlobalId), and
 * the tests run again on the commit (the dry run). One lazy chunk: a session that never shows a server's clash report fetches none of it.
 */

import { useState } from 'react';
import { AlertCircle, AlertTriangle, Copy, Info, Play } from 'lucide-react';
import { notify } from '../../app/store';
import { clashDistance, clashOrder, clashReport, runClash, type ClashElement, type ClashReport, type ClashStatus } from '../../engine/serverAnalysis';

const message = (e: unknown) => (e instanceof Error ? e.message : String(e));
const STATUS: Record<ClashStatus, string> = { new: 'New', active: 'Active', reviewed: 'Reviewed', approved: 'Approved' };

/** What a finding of the server's clash run says, in the interface's words (the server's message is English). */
function Notice({ code, severity }: { code: string; severity: string }) {
  const text = code === 'CLASH-01' ? 'New hard clashes are more than the project\'s rule allows: the publish gate stops this submission.'
    : code === 'CLASH-02' ? 'None of the project\'s clash tests runs: set them up in Lapin (Manage → Clash detection).'
      : code === 'CLASH-06' ? 'The server\'s Lapin lacks modules this project uses: their elements take part as they are stored.'
        : 'The clash tests could not run on the server.';
  return (
    <li className={`cde-finding sev-${severity}`} data-cde-finding={code} data-cde-severity={severity}>
      {severity === 'error' ? <AlertCircle size={12} /> : severity === 'warning' ? <AlertTriangle size={12} /> : <Info size={12} />}
      <span className="cde-finding-body"><span><code data-no-i18n>{code}</code> <span>{text}</span></span></span>
    </li>
  );
}

function ElementCell({ e }: { e: ClashElement }) {
  return (
    <td>
      <span data-no-i18n>{e.name ?? ''}</span> <span className="muted" data-no-i18n>{[e.class, e.storey, e.link].filter(Boolean).join(' · ')}</span>
      <br /><code className="mono small" data-no-i18n>{e.globalId}</code>
    </td>
  );
}

/** A submission's (or a dry run's) clash group: what changed since the previous run, each test, the clashes on demand. */
export function ClashReportView({ server, project, clash }: { server: string; project: string; clash: ClashReport }) {
  const [full, setFull] = useState<ClashReport | null>(clash.clashes ? clash : null);
  const [shown, setShown] = useState(false);
  const [busy, setBusy] = useState(false);
  const [report, setReport] = useState<ClashReport>(clash);
  const n = report.summary;
  const notices = report.findings.filter((f) => f.code !== 'CLASH-01' || f.severity === 'error');
  const open = async () => {
    if (shown) { setShown(false); return; }
    setShown(true);
    if (full) return;
    try { setFull(await clashReport(server, project, report.commit)); } catch (e) { notify(message(e)); setShown(false); }
  };
  const again = async () => {
    setBusy(true);
    try {
      const r = await runClash(server, project, report.commit);
      setReport(r); setFull(r);
      notify('The clash tests ran again on the server.');
    } catch (e) { notify(message(e)); } finally { setBusy(false); }
  };
  const clashes = full?.clashes ? clashOrder(full.clashes) : [];
  return (
    <section className="cde-checked-file" data-cde-clash-report={report.status} data-cde-clash-new={n?.new ?? 0}>
      <p className="small cde-checked-name"><b>Clash tests on the server</b>
        {n && <> <span className={n.open ? 'cde-warn' : 'cde-ok'} data-no-i18n>{n.open}</span> <span>open clashes</span>
          {' · '}<span className={n.newHard ? 'cde-block' : ''} data-no-i18n>{n.new}</span> <span>new since the previous run</span>
          {' · '}<span data-no-i18n>{n.resolved}</span> <span>resolved since the previous run</span></>}
        <button type="button" className="icon" disabled={busy} title="Run the clash tests again on the server" onClick={() => void again()} data-cde-clash-run><Play size={12} /></button>
      </p>
      {n?.firstRun && n.open > 0 && <p className="muted small">The project's first run on the server: every clash is new.</p>}
      {notices.length > 0 && <ul className="cde-findings-list">{notices.map((f, i) => <Notice key={i} code={f.code} severity={f.severity} />)}</ul>}
      {report.tests.length > 0 && (
        <table className="cde-members small" data-cde-clash-tests={report.tests.length}>
          <thead><tr><th>Clash test</th><th>Open clashes</th><th>New</th><th>Resolved</th></tr></thead>
          <tbody>{report.tests.map((t) => (
            <tr key={t.id} data-cde-clash-test={t.name} className={t.ran ? '' : 'muted'}>
              <td><span data-no-i18n>{t.name}</span>{!t.ran && <span className="chip">did not run</span>}</td>
              <td data-no-i18n>{t.ran ? t.open ?? 0 : ''}</td>
              <td data-no-i18n className={t.newHard ? 'cde-block' : ''}>{t.ran ? t.new ?? 0 : ''}</td>
              <td data-no-i18n>{t.ran ? t.resolved ?? 0 : ''}</td>
            </tr>
          ))}</tbody>
        </table>
      )}
      {report.status === 'done' && (n?.open ?? 0) > 0 && (
        <button type="button" className="link small" onClick={() => void open()} data-cde-clash-show>{shown ? 'Hide the clashes' : 'Show the clashes'}</button>
      )}
      {shown && !full && <p className="muted small">Loading…</p>}
      {shown && full && (
        <>
          <table className="cde-members small" data-cde-clashes={clashes.length}>
            <thead><tr><th>Clash test</th><th>Status</th><th>Element A</th><th>Element B</th><th>Depth (mm)</th></tr></thead>
            <tbody>{clashes.map((c) => (
              <tr key={`${c.test} ${c.key}`} data-cde-clash={c.since}>
                <td><span data-no-i18n>{c.testName} {c.number}</span>{c.since === 'new' && <> <span className="chip warn">New</span></>}</td>
                <td>{STATUS[c.status]}</td>
                <ElementCell e={c.a} /><ElementCell e={c.b} />
                <td data-no-i18n>{clashDistance(c)}</td>
              </tr>
            ))}</tbody>
          </table>
          {(full.clashesTotal ?? 0) > clashes.length && <p className="muted small"><span data-no-i18n>+{(full.clashesTotal ?? 0) - clashes.length}</span> <span>clashes not listed</span></p>}
          <button type="button" className="link small" title="Copy" onClick={() => void navigator.clipboard?.writeText(clashes.map((c) => `${c.testName}\t${c.number}\t${c.status}\t${c.a.globalId}\t${c.b.globalId}\t${clashDistance(c)}`).join('\n')).then(() => notify('Copied.'))}><Copy size={11} /> GlobalIds</button>
        </>
      )}
    </section>
  );
}
