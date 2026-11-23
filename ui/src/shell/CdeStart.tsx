/**
 * The start screen of Lapin on the web in front of a CDE server (engine/cdeServer.ts; Atrium docs/plan-cde-web.md, W1 and W2): sign
 * in with the account the server's administrator made, the projects the account sees, and a project's page: its history on main
 * (author, date, message, its versions and issues), the branches in progress, activity and members. A project is cloned to work on
 * (W2: the browser's engine holds it with its history; File → Save to server sends the work, Get latest brings the others'), a
 * commit opens read-only (its package: nothing goes back to the server) or downloads as a .lapin or as its IFC copy. "Work offline"
 * goes to the ordinary start screen; File → Server projects… comes back. W3: a project's activity (saves and issue changes), its
 * rules (owners change them), and for the server's administrators its accounts and new projects (modules/core/CdeAdmin.tsx).
 */

import { useCallback, useEffect, useRef, useState } from 'react';
import { ArrowLeft, ArrowUp, CloudDownload, Download, FolderOpen, KeyRound, LogIn, LogOut, RefreshCw, ShieldCheck, Trash2, UserPlus, WifiOff } from 'lucide-react';
import { BrandName } from './BrandMark';
import { confirmAction } from './prompts';
import { confirmDiscard, download, enter, openProjectBytes, safeFileName } from '../engine/files';
import { remoteCall } from '../engine/remote';
import { useSession } from '../engine/session';
import { notify, useUi } from '../app/store';
import { when } from '../modules/core/format';
import { ActivityList, AdminPanel, RulesEditor } from '../modules/core/CdeAdmin';
import { account } from '../engine/atrium';
import { REFUSALS } from '../engine/remote';
import {
  CdeError, changePassword, chipsByCommit, commitPackage, getObjectJson, listCommits, listMembers, listProjects, listRefs, objectBytes,
  readRefs, refLabel, removeMember, setMember, signIn, signOut, useCde, viewerOf,
  type Commit, type Member, type RefsRead, type Role, type ServerProject, type TagObject,
} from '../engine/cdeServer';

const ROLES: { id: Role; label: string }[] = [{ id: 'viewer', label: 'Viewer' }, { id: 'editor', label: 'Editor' }, { id: 'owner', label: 'Owner' }];
const roleLabel = (r: string) => ROLES.find((x) => x.id === r)?.label ?? r;
const short = (id: string) => id.slice(0, 8);
/** The server's refusals the interface says in the reader's language; any other as the server words it. */
const SAID: Record<string, string> = {
  ...REFUSALS,
  invalid_grant: 'The e-mail address or the password is wrong.',
  no_account: "Nobody has an account with that e-mail address. The server's administrator adds accounts.",
  last_owner: 'The project needs at least one owner: make someone else owner first.',
  wrong_password: 'The current password is wrong.',
  too_many_attempts: 'Too many wrong passwords: try again later.',
  weak_password: 'The new password needs at least 8 characters.',
};
const message = (e: unknown) => (e instanceof CdeError && SAID[e.code]) || (e instanceof Error ? e.message : String(e));

/** A commit opened read-only: its package in the browser's engine, with no file of its own (Save keeps a copy). */
async function openCommit(project: ServerProject, commit: string): Promise<void> {
  const data = await commitPackage(project.id, commit);
  if (!await openProjectBytes(safeFileName(`${project.name} ${short(commit)}.lapin`), data)) return;
  useCde.setState({ opened: { project: project.name, commit } });
  useUi.setState({ notice: 'Opened from the server, read-only: changes stay in this browser and never reach the server. Save keeps a copy on this computer.', noticeStays: true });
}

/**
 * The project cloned to work on (W2): the engine's remote.clone against this server (the same client as the desktop's), the
 * project opened from the server's latest with its history, kept in the browser's engine; File → Save to server sends the work.
 */
async function cloneProject(project: ServerProject): Promise<void> {
  if (!(await confirmDiscard('The project has unsaved changes. Open another anyway?'))) return;
  const server = useCde.getState().server;
  if (!server) return;
  await remoteCall('remote.clone', { kind: 'server', url: server, project: project.id }, server);
  enter();
  useCde.setState({ cloned: { project: project.id, name: project.name }, linked: true });
  useUi.setState({
    notice: project.role === 'viewer'
      ? 'Cloned from the server. Your role is Viewer: you can look and keep a copy, but not save to the server.'
      : 'Cloned from the server to work on. This browser holds the copy: File → Save to server sends your work (closing the tab loses what is not saved there); Get latest brings the others\' work.',
    noticeStays: true,
  });
}

async function downloadLapin(project: ServerProject, commit: string): Promise<void> {
  download(await commitPackage(project.id, commit), safeFileName(`${project.name} ${short(commit)}.lapin`), 'application/zip');
}

async function downloadIfc(project: ServerProject, commit: string): Promise<void> {
  const viewer = await viewerOf(project.id, commit);
  if (!viewer) { notify('This commit has no IFC copy on the server: open it, then export the IFC from the File menu.'); return; }
  download(await objectBytes(project.id, viewer.ifc), safeFileName(`${project.name} ${short(commit)}.ifc`), 'application/x-step');
}

export function CdeStart() {
  const user = useCde((s) => s.user);
  const connected = useSession((s) => s.connected);
  const doc = useSession((s) => s.doc);
  const [project, setProject] = useState<ServerProject | null>(null);
  const [changing, setChanging] = useState(false);
  const [isAdmin, setIsAdmin] = useState(false);
  const [admin, setAdmin] = useState(false);
  const [projectsShown, setProjectsShown] = useState(0);
  const host = window.location.host;
  const server = useCde((s) => s.server) || window.location.origin;
  useEffect(() => {
    setIsAdmin(false); setAdmin(false);
    if (user) account(server).then((a) => setIsAdmin(a.isAdmin)).catch(() => setIsAdmin(false));
  }, [user, server]);
  return (
    <div className="start cde-start">
      <section className="start-left">
        <h1 className="start-brand"><BrandName big /></h1>
        <p className="muted"><span>Common data environment</span> · <span data-no-i18n>{host}</span></p>
        <CdeNotice />
        {!user && <SignIn />}
        {user && <>
          <p className="eyebrow">Account</p>
          <p className="small"><span>Signed in as</span> <strong data-no-i18n>{user.name}</strong> <span className="muted" data-no-i18n>{user.id}</span></p>
          <div className="cde-actions">
            <button type="button" className="btn" onClick={() => setChanging(!changing)}><KeyRound size={14} />Change password…</button>
            <button type="button" className="btn" onClick={() => { signOut(); setProject(null); }}><LogOut size={14} />Sign out</button>
            {isAdmin && <button type="button" className={`btn${admin ? ' primary' : ''}`} data-cde="admin" onClick={() => { setAdmin(!admin); setProject(null); }}><ShieldCheck size={14} />Administration</button>}
          </div>
          {changing && <ChangePassword onDone={() => setChanging(false)} />}
        </>}
        <ClonedNote />
        {doc && (doc.path || doc.canUndo) && <button type="button" className="btn big" onClick={() => useUi.setState({ screen: 'workspace' })}>Back to the open project</button>}
        <button type="button" className="btn big" data-start="cde-offline" onClick={() => useCde.setState({ offline: true })}><WifiOff size={18} />Work offline…</button>
        <p className="muted small">New projects and projects from this computer, on the usual start screen. File → Server projects… comes back here.</p>
        {!connected && <p className="muted small">Starting the engine…</p>}
        <p className="eyebrow">In the browser</p>
        <p className="muted small">Clone a project to work on it here: the browser holds the copy and its history until you save to the server (File → Save to server); Get latest brings the others' work. Open read-only to look at a commit: nothing goes back to the server.</p>
      </section>
      <section className="start-right">
        {!user && <p className="muted small">Sign in with the account the server's administrator made for you.</p>}
        {user && admin && <AdminPanel server={server} onProject={() => setProjectsShown((n) => n + 1)} />}
        {user && !project && !admin && <Projects key={projectsShown} onOpen={setProject} />}
        {user && project && <ProjectPage key={project.id} project={project} back={() => setProject(null)} connected={connected} />}
      </section>
    </div>
  );
}

/** The notices (a refused sign-in, a member added…): the workspace shows them, the start screen has none of its own. */
function CdeNotice() {
  const notice = useUi((s) => s.notice);
  useEffect(() => {
    if (!notice) return;
    const t = setTimeout(() => useUi.setState({ notice: null }), 6000);
    return () => clearTimeout(t);
  }, [notice]);
  return notice ? <p className="cde-notice small" role="status">{notice}</p> : null;
}

function SignIn() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const go = async () => {
    if (busy || !email || !password) return;
    setBusy(true);
    try { await signIn(email, password); } catch (e) { notify(message(e)); } finally { setPassword(''); setBusy(false); }
  };
  return (
    <form className="cde-signin" onSubmit={(e) => { e.preventDefault(); void go(); }}>
      <p className="eyebrow">Sign in</p>
      <input placeholder="E-mail address" value={email} onChange={(e) => setEmail(e.target.value)} aria-label="E-mail address" autoComplete="username" autoFocus />
      <input placeholder="Password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} aria-label="Password" autoComplete="current-password" />
      <button type="submit" className="btn primary big" disabled={busy || !email || !password}><LogIn size={18} />Sign in</button>
    </form>
  );
}

/** The account's password changed; the passwords live in this form's state only and are cleared after each try. */
function ChangePassword({ onDone }: { onDone: () => void }) {
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [again, setAgain] = useState('');
  const [busy, setBusy] = useState(false);
  const go = async () => {
    if (busy) return;
    if (next.length < 8) { notify('The new password needs at least 8 characters.'); return; }
    if (next !== again) { notify('The two new passwords differ.'); return; }
    setBusy(true);
    try {
      await changePassword(current, next);
      setNext(''); setAgain('');
      notify('Password changed. Your other sign-ins to this server have ended.');
      onDone();
    } catch (e) { notify(message(e)); } finally { setCurrent(''); setBusy(false); }
  };
  return (
    <form className="cde-signin" onSubmit={(e) => { e.preventDefault(); void go(); }}>
      <input type="password" autoFocus placeholder="Current password" value={current} onChange={(e) => setCurrent(e.target.value)} aria-label="Current password" autoComplete="current-password" />
      <input type="password" placeholder="New password (at least 8 characters)" value={next} onChange={(e) => setNext(e.target.value)} aria-label="New password (at least 8 characters)" autoComplete="new-password" />
      <input type="password" placeholder="The new password again" value={again} onChange={(e) => setAgain(e.target.value)} aria-label="The new password again" autoComplete="new-password" />
      <div className="cde-actions">
        <button type="submit" className="btn primary" disabled={busy || !current || !next || !again}><KeyRound size={14} />Change password</button>
        <button type="button" className="btn" disabled={busy} onClick={onDone}>Cancel</button>
      </div>
    </form>
  );
}

function Projects({ onOpen }: { onOpen: (p: ServerProject) => void }) {
  const [list, setList] = useState<ServerProject[] | null>(null);
  const load = useCallback(() => { setList(null); listProjects().then(setList).catch((e) => { notify(message(e)); setList([]); }); }, []);
  useEffect(load, [load]);
  return (
    <>
      <p className="eyebrow">Projects <button type="button" className="icon" title="Refresh" onClick={load}><RefreshCw size={12} /></button></p>
      {list === null && <p className="muted small">Loading…</p>}
      {list?.length === 0 && <p className="muted small">No project yet: the server's administrator adds you to one.</p>}
      {list?.map((p) => (
        <button type="button" key={p.id} className="recent" data-cde-project={p.id} onClick={() => onOpen(p)}>
          <span className="thumb" aria-hidden="true" />
          <span className="recent-body">
            <span className="recent-name" data-no-i18n>{p.name}</span>
            <span className="muted small">{roleLabel(p.role)}</span>
          </span>
        </button>
      ))}
    </>
  );
}

interface ProjectData { refs: RefsRead; commits: Commit[]; chips: Map<string, string[]> }

function ProjectPage({ project, back, connected }: { project: ServerProject; back: () => void; connected: boolean }) {
  const [data, setData] = useState<ProjectData | null>(null);
  const [busy, setBusy] = useState(false);
  const gone = useRef(false);
  useEffect(() => () => { gone.current = true; }, []);
  const load = useCallback(async () => {
    try {
      const refs = readRefs(await listRefs(project.id));
      const [commits, tags] = await Promise.all([
        refs.main ? listCommits(project.id, refs.main.target, 200) : Promise.resolve([]),
        Promise.all([...refs.versions, ...refs.issues].map((r) => getObjectJson<TagObject>(project.id, r.target).catch(() => null))),
      ]);
      if (!gone.current) setData({ refs, commits, chips: chipsByCommit(tags.filter((t): t is TagObject => !!t)) });
    } catch (e) { notify(message(e)); if (!gone.current) setData({ refs: readRefs([]), commits: [], chips: new Map() }); }
  }, [project.id]);
  useEffect(() => { void load(); }, [load]);
  const run = async (what: () => Promise<void>) => {
    if (busy) return;
    setBusy(true);
    try { await what(); } catch (e) { notify(message(e)); } finally { if (!gone.current) setBusy(false); }
  };
  const head = data?.refs.main?.target ?? null;
  const cloned = useCde((s) => s.cloned);
  const actions = (commit: string) => (
    <span className="cde-row-actions">
      <button type="button" className="btn" disabled={busy || !connected} title="Open this commit, read-only" onClick={() => void run(() => openCommit(project, commit))}><FolderOpen size={13} />Open</button>
      <button type="button" className="icon" disabled={busy} title="Download the .lapin" onClick={() => void run(() => downloadLapin(project, commit))}><Download size={13} /><span>.lapin</span></button>
      <button type="button" className="icon" disabled={busy} title="Download the IFC copy" onClick={() => void run(() => downloadIfc(project, commit))}><Download size={13} /><span>IFC</span></button>
    </span>
  );
  return (
    <div className="cde-project" data-cde-page={project.id}>
      <p className="eyebrow"><button type="button" className="link" onClick={back}><ArrowLeft size={12} /> Projects</button></p>
      <h2 className="cde-title"><span data-no-i18n>{project.name}</span> <span className="chip">{roleLabel(project.role)}</span>
        <button type="button" className="icon" title="Refresh" onClick={() => { setData(null); void load(); }}><RefreshCw size={13} /></button></h2>
      {head && <div className="cde-actions">
        <button type="button" className="btn primary" data-cde="clone" disabled={busy || !connected} title="The project with its history, to work on in this browser and save to the server" onClick={() => void run(() => cloneProject(project))}><CloudDownload size={14} />Clone to work on</button>
        <button type="button" className="btn" data-cde="open-latest" disabled={busy || !connected} title="The latest commit, read-only: nothing goes back to the server" onClick={() => void run(() => openCommit(project, head))}><FolderOpen size={14} />Open latest, read-only</button>
      </div>}
      {cloned?.project === project.id && <p className="muted small"><span>Cloned in this browser.</span> <button type="button" className="link" onClick={() => useUi.setState({ screen: 'workspace' })}>Back to the open project</button></p>}
      {busy && <p className="muted small">Working…</p>}
      {!data && <p className="muted small">Loading…</p>}
      {data && <>
        <p className="eyebrow">History of main</p>
        {data.commits.length === 0 && <p className="muted small">Nothing saved to this project yet.</p>}
        <ul className="cde-list">
          {data.commits.map((c) => (
            <li key={c.id} className="cde-row" data-cde-commit={c.id}>
              <span className="cde-row-body">
                <span className="recent-name" data-no-i18n>{c.message || short(c.id)}</span>
                <span className="muted small" data-no-i18n>{c.author.name} · {when(c.at)} · {short(c.id)}{(data.chips.get(c.id) ?? []).map((chip) => <span key={chip} className="chip">{chip}</span>)}</span>
              </span>
              {actions(c.id)}
            </li>
          ))}
        </ul>
        {data.refs.wips.length > 0 && <>
          <p className="eyebrow">Branches in progress</p>
          <ul className="cde-list">
            {data.refs.wips.map((w) => (
              <li key={w.name} className="cde-row">
                <span className="cde-row-body">
                  <span className="recent-name" data-no-i18n>{w.who} / {w.label}</span>
                  <span className="muted small" data-no-i18n>{w.updatedBy.name} · {when(w.updatedAt)} · {short(w.target)}</span>
                </span>
                {actions(w.target)}
              </li>
            ))}
          </ul>
        </>}
        {(data.refs.versions.length > 0 || data.refs.issues.length > 0) && <>
          <p className="eyebrow">Versions and issues</p>
          <ul className="cde-list">
            {[...data.refs.versions, ...data.refs.issues].map((t) => (
              <li key={t.name} className="cde-row">
                <span className="cde-row-body">
                  <span className="recent-name" data-no-i18n>{refLabel(t.name)}</span>
                  <span className="muted small" data-no-i18n>{t.updatedBy.name} · {when(t.updatedAt)}</span>
                </span>
              </li>
            ))}
          </ul>
        </>}
        <ActivityList server={window.location.origin} project={project.id} />
        <Members project={project} />
        <RulesEditor server={window.location.origin} project={project.id} />
      </>}
    </div>
  );
}

/** The project's members; owners change roles, remove and add accounts by e-mail (accounts are made by the administrator). */
function Members({ project }: { project: ServerProject }) {
  const [data, setData] = useState<{ role: Role; members: Member[] } | null>(null);
  const [email, setEmail] = useState('');
  const [role, setRole] = useState<Role>('viewer');
  const [busy, setBusy] = useState(false);
  const load = useCallback(() => { listMembers(project.id).then(setData).catch(() => setData(null)); }, [project.id]);
  useEffect(load, [load]);
  const run = async (what: () => Promise<unknown>, done: string) => {
    if (busy) return;
    setBusy(true);
    try { await what(); notify(done); load(); } catch (e) { notify(message(e)); } finally { setBusy(false); }
  };
  if (!data) return null;
  const owner = data.role === 'owner';
  return (
    <>
      <p className="eyebrow">Members</p>
      <table className="cde-members">
        <tbody>
          {data.members.map((m) => (
            <tr key={m.email}>
              <td data-no-i18n>{m.name}</td>
              <td className="muted small" data-no-i18n>{m.email}</td>
              <td>{owner
                ? <select aria-label="Role" value={m.role} disabled={busy} onChange={(e) => void run(() => setMember(project.id, m.email, e.target.value as Role), 'Role changed.')}>
                    {ROLES.map((r) => <option key={r.id} value={r.id}>{r.label}</option>)}
                  </select>
                : roleLabel(m.role)}</td>
              <td>{owner && <button type="button" className="icon" title="Remove from the project" disabled={busy} onClick={() => {
                if (confirmAction('Remove {who} from the project?', { who: m.name })) void run(() => removeMember(project.id, m.email), 'Removed from the project.');
              }}><Trash2 size={13} /></button>}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {owner && (
        <form className="cde-add" onSubmit={(e) => { e.preventDefault(); if (email.trim()) void run(async () => { await setMember(project.id, email, role); setEmail(''); }, 'Member added.'); }}>
          <input placeholder="E-mail of an existing account" aria-label="E-mail of an existing account" value={email} onChange={(e) => setEmail(e.target.value)} />
          <select aria-label="Role" value={role} onChange={(e) => setRole(e.target.value as Role)}>
            {ROLES.map((r) => <option key={r.id} value={r.id}>{r.label}</option>)}
          </select>
          <button type="submit" className="btn" disabled={busy || !email.trim()}><UserPlus size={13} />Add</button>
        </form>
      )}
    </>
  );
}

/** The project cloned in this browser, on the start screen: what is not on the server yet. */
function ClonedNote() {
  const cloned = useCde((s) => s.cloned);
  const unpushed = useCde((s) => s.unpushed);
  const dirty = useSession((s) => s.doc?.dirty ?? false);
  if (!cloned) return null;
  return (
    <p className="small cde-cloned">
      <span>Working on</span> <strong data-no-i18n>{cloned.name}</strong>{' '}
      {unpushed || dirty ? <span className="badge">Not saved to the server</span> : <span className="muted">Saved to the server</span>}
    </p>
  );
}

/**
 * The status bar's word on a project whose remote is the CDE server (W2): what this browser holds that the server has not got
 * (changes, commits not pushed), and Save to server at a click. Nothing for any other project.
 */
export function CdeServerStatus() {
  const linked = useCde((s) => s.linked || Boolean(s.cloned));
  const unpushed = useCde((s) => s.unpushed);
  const dirty = useSession((s) => s.doc?.dirty ?? false);
  if (!linked && !unpushed) return null;
  const pending = dirty || Boolean(unpushed);
  return (
    <button type="button" className={`status-link${pending ? ' status-warn' : ''}`} data-cde-status={pending ? 'pending' : 'saved'}
      title={pending ? 'This browser holds work the server has not got: Save to server sends it. Closing the tab loses it.' : 'The server has everything this browser holds.'}
      onClick={() => useUi.setState({ dialog: 'remoteSave' })}>
      <ArrowUp size={11} /> {pending ? 'Not saved to the server' : 'Saved to the server'}
    </button>
  );
}
