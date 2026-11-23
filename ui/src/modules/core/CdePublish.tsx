/**
 * Publishing on a CDE server (Atrium docs/plan-cde-web.md, stage W4), shared by the CDE start screen in the browser and File →
 * Collaborate… on the desktop (engine/atrium.ts reaches the server either way): the ISO 19650 containers (WIP, Shared, Published,
 * Archived) with each revision's date and author, opened read-only or downloaded; the publish queue (submissions with their gate,
 * approved, rejected with a reason, withdrawn, as the role and the project's rules allow); the handover diff between two issued
 * revisions; the handover report (XLSX, written by the engine); and the server's e-mail outbox for its administrators.
 */

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Check, Download, FileSpreadsheet, FolderOpen, GitCompare, RefreshCw, Undo2, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { download, isDesktop, openProjectBytes, pickPath, safeFileName, fileBytes } from '../../engine/files';
import { historyStatus, refusalText, remoteCall, signedIn, syncIssues, writingCall } from '../../engine/remote';
import { useCde, type Role } from '../../engine/cdeServer';
import { when } from './format';
import { openRemark } from './remarks/RemarksDialog';
import {
  account, approveSubmission, features, commitViewer, containers as getContainers, getRules, handoverDiff, members as listMembers, listSubmissions, outbox, rejectSubmission, serverBytes, withdrawSubmission,
  type Containers, type HandoverDiff, type OutboxItem, type Rules, type ServerFile, type Submission, type SubmissionStatus,
} from '../../engine/atrium';
import { CONTAINERS, atLeast, comparable, containerRows, issueLabel, mayTagDirectly, proposedPair, purposeOf, submissionRights, SUBMISSION_STATUS, tagShort, type ContainerId, type Me } from '../../engine/publishing';

const message = (e: unknown) => (e instanceof Error ? e.message : String(e));
const short = (id: string) => id.slice(0, 8);
const P = (project: string) => `lapin-remote/1.0/projects/${encodeURIComponent(project)}`;

// ------------------------------------------------------------ files from the server

/** Bytes of the server saved where the user says: a download in the browser, the save dialog on the desktop (the engine writes them). */
async function saveFromServer(server: string, path: string, name: string): Promise<void> {
  if (isDesktop()) {
    const to = await pickPath('save', name);
    if (!to) return;
    await remoteCall('remote.atrium', { url: server, method: 'GET', path, bytes: true, saveTo: to }, server);
    notify('Saved.');
    return;
  }
  download(await serverBytes(server, path), name);
}

/** A revision opened read-only: the commit's package, opened with no file of its own (Save keeps a copy; nothing goes back). */
export async function openRevision(server: string, project: string, name: string, commit: string): Promise<void> {
  const bytes = await serverBytes(server, `${P(project)}/commits/${commit}/package?viewer=false`);
  if (!await openProjectBytes(safeFileName(`${name} ${short(commit)}.lapin`), bytes)) return;
  useCde.setState({ opened: { project: name, commit } });
  useUi.setState({ notice: 'Opened from the server, read-only: changes stay here and never reach the server. Save keeps a copy on this computer.', noticeStays: true });
}

async function downloadIfc(server: string, project: string, name: string, commit: string): Promise<void> {
  const v = await commitViewer(server, project, commit);
  if (!v) { notify('This commit has no IFC copy on the server: open it, then export the IFC from the File menu.'); return; }
  await saveFromServer(server, `${P(project)}/objects/${v.ifc}`, safeFileName(`${name} ${short(commit)}.ifc`));
}

/**
 * The handover report (remote.handoverReport): the open project's issues, comments and the server's journal, with the handover diff
 * when one is given. Saved where the user says (the desktop) or downloaded (the browser).
 */
export async function exportHandover(server: string, project: string, name: string, diff?: { from: string; to: string; body: HandoverDiff }): Promise<void> {
  const file = safeFileName(`${name} handover ${new Date().toISOString().slice(0, 10)}.xlsx`);
  const args: Record<string, unknown> = { serverProject: project, project: name, ...(diff ? { from: tagShort(diff.from), to: tagShort(diff.to), diff: diff.body } : {}) };
  let notes: string[] = [];
  if (isDesktop()) {
    const to = await pickPath('save', file);
    if (!to) return;
    notes = (await remoteCall<{ notes?: string[] }>('remote.handoverReport', { ...args, path: to }, server)).notes ?? [];
  } else {
    const r = await remoteCall<{ data: string; name: string; notes?: string[] }>('remote.handoverReport', args, server);
    const bytes = fileBytes(r.data);
    if (!bytes) throw new Error('The engine sent back no file.');
    download(bytes, file, 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');
    notes = r.notes ?? [];
  }
  for (const n of notes) notify(n);
  notify('Handover report made.');
}

// ------------------------------------------------------------ who is asking

/** The account, its role, the project's rules and how many owners it has: what the buttons' rights are worked out from. */
function useMe(server: string, project: string): { me: Me | null; rules: Rules | null; owners: number; reload: () => void } {
  const [state, setState] = useState<{ me: Me | null; rules: Rules | null; owners: number }>({ me: null, rules: null, owners: 2 });
  const [n, setN] = useState(0);
  useEffect(() => {
    let live = true;
    void (async () => {
      try {
        const [a, rules, members] = await Promise.all([account(server), getRules(server, project).catch(() => null), listMembers(server, project).catch(() => null)]);
        const role: Role = (rules?.role ?? a.projects.find((x) => x.id === project)?.role ?? 'viewer') as Role;
        const owners = members?.members.filter((m) => m.role === 'owner').length ?? 2;
        if (live) setState({ me: { email: a.email, role, isAdmin: a.isAdmin }, rules, owners });
      } catch { if (live) setState({ me: null, rules: null, owners: 2 }); }
    })();
    return () => { live = false; };
  }, [server, project, n]);
  return { ...state, reload: () => setN((x) => x + 1) };
}

// ------------------------------------------------------------ the panel

type Tab = 'containers' | 'submissions' | 'diff';

/** Containers, the publish queue and the handover diff of a server project, with the handover report. */
export function PublishingPanel({ server, project, name }: { server: string; project: string; name: string }) {
  const [offers, setOffers] = useState<boolean | null>(null);
  useEffect(() => { let live = true; void features(server).then((f) => { if (live) setOffers(f.includes('submissions')); }); return () => { live = false; }; }, [server]);
  if (offers === null) return null;
  if (!offers) return <p className="muted small" data-cde-publishing="none">This server does not offer publishing (an Atrium Server older than W4).</p>;
  return <Publishing server={server} project={project} name={name} />;
}

function Publishing({ server, project, name }: { server: string; project: string; name: string }) {
  const [tab, setTab] = useState<Tab>('containers');
  const [data, setData] = useState<Containers | null>(null);
  const [failed, setFailed] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const who = useMe(server, project);
  const load = useCallback(() => {
    setFailed(null);
    getContainers(server, project).then(setData).catch((e) => { setFailed(message(e)); setData({ wip: [], shared: [], published: [], archived: [] }); });
  }, [server, project]);
  useEffect(load, [load]);
  const run = async (what: () => Promise<unknown>) => {
    if (busy) return;
    setBusy(true);
    try { await what(); } catch (e) { notify(message(e)); } finally { setBusy(false); }
  };
  return (
    <div className="cde-publishing" data-cde-publishing>
      <p className="eyebrow">Publishing
        <button type="button" className="icon" title="Refresh" onClick={() => { setData(null); load(); who.reload(); }}><RefreshCw size={12} /></button>
        <button type="button" className="link small" data-cde="handover-report" disabled={busy} title="The issues, their comments and the journal of who changed what, as an Excel workbook" onClick={() => void run(() => exportHandover(server, project, name))}><FileSpreadsheet size={12} /> Handover report (XLSX)</button>
      </p>
      <div className="cde-tabs" role="tablist">
        {([['containers', 'Containers'], ['submissions', 'Submissions'], ['diff', 'Handover diff']] as [Tab, string][]).map(([id, label]) => (
          <button key={id} type="button" role="tab" aria-selected={tab === id} className={`tab${tab === id ? ' active' : ''}`} data-cde-tab={id} onClick={() => setTab(id)}>{label}</button>
        ))}
      </div>
      {failed && <p className="muted small">{failed}</p>}
      {tab === 'containers' && <ContainersView server={server} project={project} name={name} data={data} busy={busy} run={run} />}
      {tab === 'submissions' && <SubmissionsQueue server={server} project={project} who={who} onDecided={load} />}
      {tab === 'diff' && <HandoverDiffView server={server} project={project} name={name} data={data} />}
    </div>
  );
}

// ------------------------------------------------------------ containers

function ContainersView({ server, project, name, data, busy, run }: { server: string; project: string; name: string; data: Containers | null; busy: boolean; run: (f: () => Promise<unknown>) => Promise<void> }) {
  const [files, setFiles] = useState<string | null>(null);
  if (!data) return <p className="muted small">Loading…</p>;
  const file = (f: ServerFile) => (
    <li key={f.name} className="small row">
      <button type="button" className="link" disabled={busy} title="Download the issued file" onClick={() => void run(() => saveFromServer(server, `${P(project)}/objects/${f.sha256}`, f.name))}><Download size={11} /> <span data-no-i18n>{f.name}</span></button>
    </li>
  );
  return (
    <div className="cde-containers" data-cde-containers>
      {CONTAINERS.map(({ id, label }) => {
        const rows = containerRows(data, id as ContainerId);
        return (
          <section key={id} className="cde-container" data-cde-container={id}>
            <p className="eyebrow">{label} <span className="muted small" data-no-i18n>{rows.length}</span></p>
            {rows.length === 0 && <p className="muted small">Nothing here.</p>}
            <ul className="cde-list">
              {rows.map((r) => (
                <li key={r.key} className="cde-row" data-cde-revision={r.key}>
                  <span className="cde-row-body">
                    <span className="recent-name"><span data-no-i18n>{r.title}</span>{r.suitability && <span className="chip" data-no-i18n>{r.suitability}</span>}
                      {r.fromSubmission && <span className="chip">approved</span>}
                      {r.supersededBy && <span className="muted small"> <span>superseded by</span> <span data-no-i18n>{tagShort(r.supersededBy)}</span></span>}</span>
                    <span className="muted small" data-no-i18n>{r.by} · {when(r.at)} · {short(r.commit)}{r.note ? ` · ${r.note}` : ''}</span>
                  </span>
                  <span className="cde-row-actions">
                    <button type="button" className="btn" disabled={busy} title="Open this revision, read-only" onClick={() => void run(() => openRevision(server, project, name, r.commit))}><FolderOpen size={13} />Open</button>
                    <button type="button" className="icon" disabled={busy} title="Download the .lapin" onClick={() => void run(() => saveFromServer(server, `${P(project)}/commits/${r.commit}/package?viewer=false`, safeFileName(`${name} ${r.revision ?? short(r.commit)}.lapin`)))}><Download size={13} /><span>.lapin</span></button>
                    <button type="button" className="icon" disabled={busy} title="Download the IFC copy" onClick={() => void run(() => downloadIfc(server, project, name, r.commit))}><Download size={13} /><span>IFC</span></button>
                    {r.files > 0 && <button type="button" className="link small" onClick={() => setFiles(files === r.key ? null : r.key)} title="The files issued with this revision">{r.files} <span>files</span></button>}
                  </span>
                  {files === r.key && r.tag && <ul className="cde-issued-files">{r.tag.files.map(file)}</ul>}
                </li>
              ))}
            </ul>
          </section>
        );
      })}
      <p className="muted small">A Published revision goes to Archived when the same issue is published again at a later revision. WIP lists the branches in progress.</p>
    </div>
  );
}

// ------------------------------------------------------------ the publish queue

const FILTERS: { id: SubmissionStatus | ''; label: string }[] = [
  { id: 'pending', label: 'Waiting for approval' }, { id: '', label: 'All' }, { id: 'approved', label: 'Approved' }, { id: 'rejected', label: 'Rejected' }, { id: 'withdrawn', label: 'Withdrawn' },
];

function SubmissionsQueue({ server, project, who, onDecided }: { server: string; project: string; who: ReturnType<typeof useMe>; onDecided: () => void }) {
  const [filter, setFilter] = useState<SubmissionStatus | ''>('pending');
  const [list, setList] = useState<Submission[] | null>(null);
  const [open, setOpen] = useState<string | null>(null);
  const load = useCallback(() => {
    listSubmissions(server, project, filter || undefined).then(setList).catch((e) => { notify(message(e)); setList([]); });
  }, [server, project, filter]);
  useEffect(() => { setList(null); load(); }, [load]);
  const current = list?.find((s) => s.id === open) ?? null;
  return (
    <div className="cde-submissions" data-cde-submissions>
      <div className="cde-actions">
        <select aria-label="Show" value={filter} onChange={(e) => setFilter(e.target.value as SubmissionStatus | '')}>
          {FILTERS.map((f) => <option key={f.id} value={f.id}>{f.label}</option>)}
        </select>
        <button type="button" className="icon" title="Refresh" onClick={() => { setList(null); load(); }}><RefreshCw size={12} /></button>
      </div>
      {list === null && <p className="muted small">Loading…</p>}
      {list?.length === 0 && <p className="muted small">No submission here. Manage → Issue… submits an issue for approval when the project is on the server.</p>}
      <ul className="cde-list">
        {list?.map((s) => (
          <li key={s.id} className={`cde-row${open === s.id ? ' active' : ''}`} data-cde-submission={s.number}>
            <button type="button" className="cde-row-body link" onClick={() => setOpen(open === s.id ? null : s.id)}>
              <span className="recent-name"><span data-no-i18n>{`${s.number}. ${issueLabel(s.issue, s.revision)} · ${s.suitability}`}</span> <span className="chip">{s.purpose === 'shared' ? 'Shared' : 'Published'}</span>
                <span className={`chip${s.status === 'pending' ? ' warn' : ''}`}>{SUBMISSION_STATUS[s.status]}</span>
                {s.status === 'pending' && s.gate && !s.gate.passed && <span className="chip warn" data-cde-gate="failed">Gate: open issues</span>}</span>
              <span className="muted small" data-no-i18n>{s.author.name} · {when(s.created)}{s.note ? ` · ${s.note}` : ''}</span>
            </button>
          </li>
        ))}
      </ul>
      {current && <SubmissionDetail key={current.id} server={server} project={project} s={current} who={who} onDone={() => { load(); onDecided(); }} />}
    </div>
  );
}

function SubmissionDetail({ server, project, s, who, onDone }: { server: string; project: string; s: Submission; who: ReturnType<typeof useMe>; onDone: () => void }) {
  const [note, setNote] = useState('');
  const [reason, setReason] = useState('');
  const [override, setOverride] = useState(false);
  const [busy, setBusy] = useState(false);
  const rights = submissionRights(s, who.me, who.rules, who.owners);
  const failing = s.gate != null && !s.gate.passed;
  const act = async (what: () => Promise<Submission>, done: string) => {
    if (busy) return;
    setBusy(true);
    try { await what(); notify(done); onDone(); } catch (e) { notify(message(e)); } finally { setBusy(false); }
  };
  const canApprove = rights.approve && (!failing || (override && rights.override && note.trim().length > 0));
  return (
    <div className="cde-submission" data-cde-submission-detail={s.number}>
      <p className="small"><span className="muted">Commit</span> <code data-no-i18n>{short(s.commit)}</code>
        {s.tag && <> · <span className="muted">Issue tag</span> <span data-no-i18n>{tagShort(s.tag)}</span></>}</p>
      {s.documents.length > 0 && <p className="small"><span className="muted">Documents</span> <span data-no-i18n>{s.documents.join(', ')}</span></p>}
      {s.files.length > 0 && <p className="small"><span className="muted">Files</span> <span data-no-i18n>{s.files.map((f) => f.name).join(', ')}</span></p>}
      {s.gate && (
        <div className="cde-gate" data-cde-gate={s.gate.passed ? 'passed' : 'failed'}>
          {s.gate.passed
            ? <p className="small cde-ok"><Check size={12} /> No open issue stops this submission.</p>
            : <p className="small cde-warn">These open issues stop the approval: resolve or close them, or an owner approves with a note.</p>}
          <ul className="cde-list">
            {s.gate.topics.map((t) => (
              <li key={t.guid} className="small">
                <button type="button" className="link" title="Open the issue in Model remarks" data-cde-gate-topic={t.guid} onClick={() => openRemark(t.guid)}>
                  <span data-no-i18n>{t.serverAssignedId ? `#${t.serverAssignedId} ` : ''}{t.title}</span>
                </button>{' '}
                <span className="muted" data-no-i18n>{[t.status, t.priority, t.document].filter(Boolean).join(' · ')}</span>
              </li>
            ))}
          </ul>
        </div>
      )}
      {s.status !== 'pending' && (
        <p className="small"><span className="muted">{SUBMISSION_STATUS[s.status]}</span>{' '}
          <span data-no-i18n>{s.decidedBy?.name ?? ''}{s.decided ? ` · ${when(s.decided)}` : ''}{s.reason ? ` · ${s.reason}` : ''}{s.decisionNote ? ` · ${s.decisionNote}` : ''}</span>
          {s.override && <span className="chip warn">Gate passed by override</span>}
          {s.closedTopics.length > 0 && <span className="muted"> · <span data-no-i18n>{s.closedTopics.length}</span> <span>issues closed</span></span>}</p>
      )}
      {s.status === 'pending' && (
        <>
          <div className="cde-add">
            <input placeholder="A note with the approval (optional; needed to override the gate)" aria-label="Approval note" value={note} disabled={!rights.approve} onChange={(e) => setNote(e.target.value)} data-cde-approve-note />
            {failing && <label className="row small" title={rights.override ? undefined : 'Only owners pass a failing gate'}><input type="checkbox" checked={override} disabled={!rights.override} onChange={(e) => setOverride(e.target.checked)} data-cde-override /> Approve despite the open issues</label>}
            <button type="button" className="btn primary" disabled={busy || !canApprove} title={rights.why ?? (failing && !override ? 'The gate fails: an owner approves with a note' : 'Approve: the server makes the issue tag')} data-cde-approve
              onClick={() => void act(() => approveSubmission(server, project, s.id, note.trim(), failing && override), 'Approved: the issue tag is made.')}><Check size={13} />Approve</button>
          </div>
          <div className="cde-add">
            <input placeholder="Why it is rejected (required)" aria-label="Reason" value={reason} disabled={!rights.reject} onChange={(e) => setReason(e.target.value)} data-cde-reject-reason />
            <button type="button" className="btn" disabled={busy || !rights.reject || !reason.trim()} title={rights.why ?? 'Reject, with the reason'} data-cde-reject
              onClick={() => void act(() => rejectSubmission(server, project, s.id, reason.trim()), 'Rejected.')}><X size={13} />Reject</button>
            <button type="button" className="btn" disabled={busy || !rights.withdraw} title={rights.withdraw ? 'Take the submission back' : 'Only its author withdraws a submission'} data-cde-withdraw
              onClick={() => void act(() => withdrawSubmission(server, project, s.id), 'Withdrawn.')}><Undo2 size={13} />Withdraw</button>
          </div>
          {rights.why && <p className="muted small">{rights.why}</p>}
        </>
      )}
    </div>
  );
}

// ------------------------------------------------------------ the handover diff

function HandoverDiffView({ server, project, name, data }: { server: string; project: string; name: string; data: Containers | null }) {
  const list = useMemo(() => comparable(data), [data]);
  const pair = useMemo(() => proposedPair(list), [list]);
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [diff, setDiff] = useState<HandoverDiff | null>(null);
  const [busy, setBusy] = useState(false);
  useEffect(() => { if (pair && !from && !to) { setFrom(pair[0]); setTo(pair[1]); } }, [pair, from, to]);
  if (!data) return <p className="muted small">Loading…</p>;
  if (list.length < 2) return <p className="muted small">A handover diff needs two issued revisions (Shared, Published or Archived).</p>;
  const compare = async () => {
    setBusy(true);
    try { setDiff(await handoverDiff(server, project, tagShort(from), tagShort(to))); } catch (e) { notify(message(e)); } finally { setBusy(false); }
  };
  const report = async () => {
    if (!diff) return;
    setBusy(true);
    try { await exportHandover(server, project, name, { from, to, body: diff }); } catch (e) { notify(message(e)); } finally { setBusy(false); }
  };
  const option = (t: (typeof list)[number]) => <option key={t.name} value={t.name}>{`${issueLabel(t.issue, t.revision)} · ${t.container} · ${when(t.issuedAt || t.at)}`}</option>;
  const d = diff?.documents;
  return (
    <div className="cde-diff" data-cde-diff>
      <div className="cde-add">
        <select aria-label="From" value={from} onChange={(e) => { setFrom(e.target.value); setDiff(null); }} data-cde-diff-from>{list.map(option)}</select>
        <span className="muted">→</span>
        <select aria-label="To" value={to} onChange={(e) => { setTo(e.target.value); setDiff(null); }} data-cde-diff-to>{list.map(option)}</select>
        <button type="button" className="btn primary" disabled={busy || !from || !to || from === to} onClick={() => void compare()} data-cde-diff-compare><GitCompare size={13} />Compare</button>
        <button type="button" className="btn" disabled={busy || !diff} onClick={() => void report()} title="The handover report with this diff, as an Excel workbook" data-cde-diff-report><FileSpreadsheet size={13} />Export to XLSX</button>
      </div>
      {d && (
        <table className="cde-members cde-diff-table">
          <thead><tr><th>Change</th><th>Document</th><th>From</th><th>To</th></tr></thead>
          <tbody>
            {d.added.map((x) => <tr key={`a${x.document}`} data-cde-diff-row="added"><td>added</td><td data-no-i18n>{x.document}</td><td /><td data-no-i18n>{x.name}</td></tr>)}
            {d.removed.map((x) => <tr key={`r${x.document}`} data-cde-diff-row="removed"><td>removed</td><td data-no-i18n>{x.document}</td><td data-no-i18n>{x.name}</td><td /></tr>)}
            {d.changed.map((x) => <tr key={`c${x.document}`} data-cde-diff-row="changed"><td>changed</td><td data-no-i18n>{x.document}</td><td data-no-i18n>{x.from.name}</td><td data-no-i18n>{x.to.name}</td></tr>)}
            {d.unchanged.map((x) => <tr key={`u${x}`} className="muted" data-cde-diff-row="unchanged"><td>unchanged</td><td data-no-i18n>{x}</td><td /><td /></tr>)}
          </tbody>
        </table>
      )}
      {diff && d && d.added.length + d.removed.length + d.changed.length === 0 && <p className="muted small">The same documents in both revisions.</p>}
      {diff && <p className="muted small"><span>Model parts</span> <span data-no-i18n>+{diff.parts.added.length} −{diff.parts.removed.length} ~{diff.parts.changed.length}</span></p>}
    </div>
  );
}

// ------------------------------------------------------------ the e-mail outbox (administrators)

/** The server's e-mail outbox, read-only: what was sent, what waits, what failed and why. */
export function OutboxList({ server }: { server: string }) {
  const [status, setStatus] = useState<OutboxItem['status'] | ''>('');
  const [data, setData] = useState<{ smtp: boolean; items: OutboxItem[] } | null>(null);
  const [failed, setFailed] = useState<string | null>(null);
  const load = useCallback(() => {
    setFailed(null);
    outbox(server, status || undefined).then(setData).catch((e) => { setFailed(message(e)); setData({ smtp: false, items: [] }); });
  }, [server, status]);
  useEffect(() => { setData(null); load(); }, [load]);
  return (
    <div className="cde-outbox" data-cde-outbox>
      <p className="eyebrow">E-mail outbox <button type="button" className="icon" title="Refresh" onClick={() => { setData(null); load(); }}><RefreshCw size={12} /></button></p>
      <div className="cde-actions">
        <select aria-label="Show" value={status} onChange={(e) => setStatus(e.target.value as OutboxItem['status'] | '')}>
          <option value="">All</option><option value="pending">Waiting</option><option value="sent">Sent</option><option value="failed">Failed</option>
        </select>
      </div>
      {failed && <p className="muted small">{failed}</p>}
      {data && !data.smtp && <p className="muted small" data-cde-smtp="off">Sending is off: the server has no SMTP settings, so the mails wait here.</p>}
      {data === null && <p className="muted small">Loading…</p>}
      {data?.items.length === 0 && !failed && <p className="muted small">Nothing yet.</p>}
      {data && data.items.length > 0 && (
        <table className="cde-members cde-users">
          <thead><tr><th>When</th><th>What</th><th>To</th><th>Subject</th><th>Status</th></tr></thead>
          <tbody>
            {data.items.map((m) => (
              <tr key={String(m.id)} data-cde-mail={m.status} title={m.lastError ?? undefined}>
                <td className="small">{when(m.created)}</td>
                <td className="small" data-no-i18n>{m.kind}</td>
                <td className="small" data-no-i18n>{m.to}</td>
                <td className="small" data-no-i18n>{m.subject}</td>
                <td className="small"><span>{m.status === 'pending' ? 'Waiting' : m.status === 'sent' ? 'Sent' : 'Failed'}</span>{m.attempts > 0 && <span className="muted" data-no-i18n> ({m.attempts})</span>}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

// ------------------------------------------------------------ an issue put on the server (Manage → Issue…)

interface SubmitAnswer { ok: boolean; code?: string; message?: string; submission?: Submission; tag?: string; notes?: string[] }

/** The project saved to its remote first (an issue goes with a commit the remote holds): committed when it changed, pushed, its issues synced. */
async function saveForIssue(server: string | null, note: string): Promise<boolean> {
  const s = await historyStatus();
  if (s.uncommitted && !await writingCall('history.commit', { message: note }, server)) return false;
  const o = await remoteCall<{ pushed?: boolean; message?: string; notes?: string[] }>('remote.push', {}, server);
  for (const n of o.notes ?? []) notify(n);
  if (o.pushed === false) { notify(o.message ?? 'The remote did not take it.'); return false; }
  // The issues go along, as Save to server sends them (W3): the gate is worked out from the server's.
  if (server) await syncIssues(server, true);
  return true;
}

/**
 * After Manage → Issue… on a project whose remote is a CDE server (W4): submit the issue for approval (the server makes the issue tag
 * when an approver approves), or make the issue tag directly where the project's rules let this account (a folder remote always does).
 * Either way the project is saved to the remote first.
 */
export function IssueOnServer({ issue }: { issue: { id: string; number: number; revision: string; container: string } }) {
  const [remote, setRemote] = useState<{ kind: 'folder' | 'server'; url: string; project: string } | null | undefined>(undefined);
  const [role, setRole] = useState<Role | null>(null);
  const [rules, setRules] = useState<Rules | null>(null);
  const [busy, setBusy] = useState(false);
  const [answer, setAnswer] = useState<SubmitAnswer | null>(null);
  const [queue, setQueue] = useState(false);
  useEffect(() => {
    let live = true;
    void historyStatus().then(async (s) => {
      const r = s.remotes[0] ?? null;
      if (!live) return;
      setRemote(r);
      if (r?.kind === 'server' && await signedIn(r.url)) {
        const [got, offers] = await Promise.all([getRules(r.url, r.project).catch(() => null), features(r.url)]);
        if (live) { setRules(got); setRole(got?.role ?? null); setQueue(offers.includes('submissions')); }
      }
    }).catch(() => { if (live) setRemote(null); });
    return () => { live = false; };
  }, []);
  if (!remote) return null;
  const server = remote.kind === 'server' ? remote.url : null;
  const purpose = purposeOf(issue.container);
  const label = `Issue ${issue.number} · ${issue.revision}`;
  const go = async (method: 'remote.submitIssue' | 'remote.issueTag') => {
    if (busy) return;
    setBusy(true);
    try {
      if (server && !await signedIn(server)) { notify('Sign in to the server first.'); useUi.setState({ dialog: 'remote' }); return; }
      if (!await saveForIssue(server, label)) return;
      const a = await remoteCall<SubmitAnswer>(method, { issue: issue.id }, server);
      for (const n of a.notes ?? []) notify(n);
      setAnswer(a);
      if (a.ok) notify(method === 'remote.submitIssue' ? 'Submitted for approval.' : 'The issue tag is on the remote.');
      else notify(refusalText(a.code ?? '', a.message));
    } catch (e) { notify(message(e)); } finally { setBusy(false); }
  };
  const direct = remote.kind === 'folder' || mayTagDirectly(role, rules);
  const mayAsk = remote.kind === 'server' && queue && atLeast(role, 'editor');
  return (
    <div className="cde-on-server" data-cde-issue-server={remote.kind}>
      <h4>{remote.kind === 'server' ? 'On the CDE server' : 'On the remote folder'}</h4>
      {!purpose && <p className="muted small">A WIP issue stays in the project: issue to Shared or Published to put it on the server.</p>}
      {purpose && !answer?.ok && (
        <>
          <p className="muted small">{remote.kind === 'server'
            ? 'The project is saved to the server, then the issue goes there: submitted for approval (an approver makes it Shared or Published), or as its issue tag directly where the project\'s rules let you.'
            : 'The project is saved to the folder, then the issue goes there as its issue tag.'}</p>
          <div className="cde-actions">
            {remote.kind === 'server' && <button type="button" className="btn primary" disabled={busy || !mayAsk} data-cde-submit title={mayAsk ? 'Ask an approver to publish this issue' : queue ? 'Viewers do not submit issues' : 'This server has no publish queue (an Atrium Server older than W4)'} onClick={() => void go('remote.submitIssue')}><Check size={13} />Submit for approval</button>}
            <button type="button" className="btn" disabled={busy || !direct} data-cde-tag-direct title={direct ? 'Make the issue tag now, without approval' : 'The project\'s rules keep issue tags for its owners: submit for approval'} onClick={() => void go('remote.issueTag')}>Make the issue tag now</button>
          </div>
        </>
      )}
      {busy && <p className="muted small">Working…</p>}
      {answer?.ok && answer.submission && (
        <p className="small cde-ok" data-cde-submitted={answer.submission.number}>
          <span>Submitted for approval</span> <span data-no-i18n>{`${answer.submission.number}. ${issueLabel(answer.submission.issue, answer.submission.revision)}`}</span>
          {answer.submission.gate && !answer.submission.gate.passed && <span className="chip warn">Gate: open issues</span>}
        </p>
      )}
      {answer?.ok && !answer.submission && <p className="small cde-ok" data-cde-tagged><span>Issue tag made:</span> <span data-no-i18n>{tagShort(answer.tag ?? '')}</span></p>}
      {answer && !answer.ok && <p className="small cde-warn">{refusalText(answer.code ?? '', answer.message)}</p>}
    </div>
  );
}
