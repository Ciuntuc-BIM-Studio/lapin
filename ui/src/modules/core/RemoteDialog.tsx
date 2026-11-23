/**
 * File → Collaborate…: the project's history and its remote (docs/remote.md; lapin-remote 1.0, docs/spec-lapin-remote.md; decision
 * 793, stage S1). A remote is a folder (a disk, a NAS, a synced folder) or a server (Atrium Server) signed in to with the account's
 * e-mail and password. From here the project is committed (with a message: the commit stays in the project's file), fetched, pulled
 * (fast-forward only for now) and pushed; ahead/behind as the last fetch saw it. A push the remote refused because someone pushed
 * first, or a pull whose histories went different ways, offers to keep my work as a branch on the remote or to discard my commits
 * (they stay in the file). A project can also be cloned from a remote into a new file.
 */

import { useCallback, useEffect, useRef, useState } from 'react';
import { ArrowDown, ArrowUp, FolderOpen, GitCommitHorizontal, LogIn, LogOut, RefreshCw, Trash2, X } from 'lucide-react';
import { backdropClose } from '../../shell/Dialog';
import { confirmAction } from '../../shell/prompts';
import { notify, useUi } from '../../app/store';
import { useSession } from '../../engine/session';
import { enter, isDesktop, pickPath, projectTitle } from '../../engine/files';
import { pickFolder } from './cde/cde';
import {
  authorFor, historyLog, historyStatus, remoteCall, serverOf, setAuthorName, authorName, signedIn, signIn, signOut, writingCall,
  type CommitInfo, type HistoryStatus,
} from '../../engine/remote';
import { when } from './format';

type Kind = 'folder' | 'server';
interface Outcome { pushed?: boolean; pulled?: boolean; kind?: string; message?: string; notes?: string[]; branch?: string; uploaded?: number; status?: HistoryStatus }

const short = (id: string | null | undefined) => (id ? id.slice(0, 8) : '—');

export function RemoteDialog() {
  const path = useSession((s) => s.doc?.path ?? null);
  const [status, setStatus] = useState<HistoryStatus | null>(null);
  const [log, setLog] = useState<CommitInfo[]>([]);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [diverged, setDiverged] = useState<string | null>(null);
  const [account, setAccount] = useState(false);
  const close = () => useUi.setState({ dialog: null });
  const gone = useRef(false);
  useEffect(() => () => { gone.current = true; }, []);

  const server = serverOf(status);
  const refresh = useCallback(async () => {
    try {
      const s = await historyStatus();
      if (gone.current) return;
      setStatus(s);
      setLog(s.head ? await historyLog(30) : []);
      const url = serverOf(s);
      setAccount(url ? await signedIn(url) : false);
    } catch (e) { notify(e instanceof Error ? e.message : String(e)); }
  }, []);
  useEffect(() => { void refresh(); }, [refresh, path]);

  const run = async (what: () => Promise<unknown>) => {
    if (busy) return;
    setBusy(true);
    try { await what(); } catch (e) { notify(e instanceof Error ? e.message : String(e)); } finally { setBusy(false); await refresh(); }
  };

  const tell = (o: Outcome | null, done: string) => {
    if (!o) return;
    for (const n of o.notes ?? []) notify(n);
    if (o.pushed === false || o.pulled === false) {
      if (o.message) notify(o.message);
      if (o.kind === 'diverged' || (o.pushed === false && o.message?.includes('pull first'))) setDiverged(o.message ?? null);
      return;
    }
    setDiverged(null);
    notify(done);
    if (o.branch) notify('Your work is kept as a branch on the remote.');
  };

  const commit = () => run(async () => {
    const r = await writingCall<{ commit: CommitInfo }>('history.commit', { message: message.trim() }, server);
    if (r) { setMessage(''); notify('Committed: the commit is in the project\'s file.'); }
  });
  const fetchIt = () => run(async () => {
    const r = await remoteCall<{ unchanged: boolean; commits: number; notes: string[] }>('remote.fetch', {}, server);
    for (const n of r.notes) notify(n);
    notify(r.unchanged ? 'The remote has nothing new.' : 'Fetched the remote\'s new commits.');
  });
  const pull = (resolve?: 'branch' | 'discard') => run(async () => tell(await writingCall<Outcome>('remote.pull', resolve ? { resolve } : {}, server), 'Pulled: the project is the remote\'s.'));
  const push = () => run(async () => tell(await remoteCall<Outcome>('remote.push', {}, server), 'Pushed.'));
  const remove = () => { if (confirmAction('Remove the remote from this project? Nothing on the remote is deleted.')) void run(() => writingCall('remote.remove', {}, null)); };

  const remote = status?.remotes[0] ?? null;
  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog wide" role="dialog" aria-label="Collaborate" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Collaborate · {projectTitle(path)}</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          {status === null && <p className="muted">Reading the history…</p>}
          {status?.unreadable && <p className="error-text">The project's history could not be read: it is kept as it is.</p>}
          {status && status.problems.length > 0 && <p className="muted small">The history's check found problems; they are repaired at the next save.</p>}
          {status && (
            <section className="remote-status">
              <p>
                <span className="muted">Branch</span> <b data-no-i18n>{status.branch.replace('refs/heads/', '')}</b>
                {' · '}<span className="muted">Last commit</span> <span data-no-i18n>{status.commit ? `${short(status.commit.id)} ${status.commit.message || '—'} (${status.commit.author.name}, ${when(status.commit.at)})` : '—'}</span>
                {status.uncommitted && <span className="badge">Changes not committed</span>}
              </p>
              {remote && (
                <p>
                  <span className="muted">{remote.kind === 'server' ? 'Server' : 'Folder'}</span> <span data-no-i18n>{remote.url}</span>
                  {' · '}<span className="muted">Project</span> <span data-no-i18n>{remote.project}</span>
                  {' · '}<span title="Commits to push / to pull, as the last fetch saw the remote" data-no-i18n>↑{remote.ahead} ↓{remote.behind}</span>
                  {remote.fetchedAt && <span className="muted small"> · <span>Fetched</span> <span data-no-i18n>{when(remote.fetchedAt)}</span></span>}
                </p>
              )}
            </section>
          )}
          {status && !remote && <AddRemote busy={busy} onAdded={refresh} setBusy={setBusy} />}
          {remote?.kind === 'server' && <Account server={remote.url} signedIn={account} onChange={refresh} />}
          <div className="version-save">
            <input placeholder="Commit message" value={message} onChange={(e) => setMessage(e.target.value)} aria-label="Commit message" onKeyDown={(e) => { if (e.key === 'Enter') void commit(); }} />
            <button type="button" className="btn primary" disabled={busy || !status?.uncommitted} onClick={() => void commit()} title="The project as it is now, committed into its file"><GitCommitHorizontal size={13} /> Commit</button>
          </div>
          {remote && (
            <div className="version-save">
              <button type="button" disabled={busy} onClick={() => void fetchIt()} title="What the remote has, without changing the project"><RefreshCw size={13} /> Fetch</button>
              <button type="button" disabled={busy || status?.uncommitted} onClick={() => void pull()} title="The remote's commits brought into the project (commit first)"><ArrowDown size={13} /> Pull</button>
              <button type="button" disabled={busy || !status?.head} onClick={() => void push()} title="The project's commits sent to the remote, with a viewer IFC of the last one"><ArrowUp size={13} /> Push</button>
              <span className="grow" />
              <button type="button" className="icon" disabled={busy} onClick={remove} title="Remove the remote from this project"><Trash2 size={13} /></button>
            </div>
          )}
          {diverged && (
            <div className="remote-diverged">
              <p>Someone pushed before you. Pull cannot merge yet: keep your work as a branch on the remote, or discard your commits (they stay in the project's file). Either way the project becomes the remote's.</p>
              <button type="button" className="btn primary" disabled={busy} onClick={() => void pull('branch')}>Keep my work as a branch</button>
              <button type="button" disabled={busy} onClick={() => { if (confirmAction('Discard your commits? They stay in the file as a local branch.')) void pull('discard'); }}>Discard my commits</button>
            </div>
          )}
          {log.length > 0 && (
            <table className="layer-table versions">
              <thead><tr><th>Commit</th><th>Message</th><th>Author</th><th>When</th><th title="On the remote">Pushed</th></tr></thead>
              <tbody>
                {log.map((c) => (
                  <tr key={c.id}>
                    <td data-no-i18n><code>{short(c.id)}</code></td>
                    <td data-no-i18n>{c.message || '—'}{c.tags.length > 0 && <span className="muted small"> {c.tags.map((t) => t.replace('refs/tags/', '')).join(', ')}</span>}</td>
                    <td data-no-i18n>{c.author.name}</td>
                    <td>{when(c.at)}</td>
                    <td>{c.pushed ? '✓' : ''}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          {isDesktop() && <p><button type="button" className="link" onClick={() => useUi.setState({ dialog: 'remoteClone' })}>Open a project from a server…</button></p>}
          <p className="muted small">A commit stays in the project's file. Push sends the commits to the remote; pull brings the remote's in, in one undo step. Sign-ins are kept by this computer, never in the project.</p>
        </div>
        <footer className="dialog-foot"><span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}

/** The account on the project's server: sign in (e-mail and password) or out. */
function Account({ server, signedIn: inNow, onChange }: { server: string; signedIn: boolean; onChange: () => void }) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const go = async () => {
    setBusy(true);
    try {
      const who = await signIn(server, email.trim(), password);
      setPassword('');
      notify('Signed in.');
      void who;
      onChange();
    } catch (e) { notify(e instanceof Error ? e.message : String(e)); } finally { setBusy(false); }
  };
  if (inNow) {
    return (
      <div className="version-save">
        <span className="muted">Signed in as</span> <span data-no-i18n>{authorFor(server).name}</span>
        <button type="button" onClick={() => void signOut(server).then(onChange)}><LogOut size={13} /> Sign out</button>
      </div>
    );
  }
  return (
    <div className="version-save">
      <input placeholder="E-mail" value={email} onChange={(e) => setEmail(e.target.value)} aria-label="E-mail" autoComplete="username" />
      <input placeholder="Password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} aria-label="Password" autoComplete="current-password" onKeyDown={(e) => { if (e.key === 'Enter') void go(); }} />
      <button type="button" className="btn primary" disabled={busy || !email || !password} onClick={() => void go()}><LogIn size={13} /> Sign in</button>
    </div>
  );
}

/** A remote added: a folder (a new project made in it, or one it holds) or a server (signed in, its project picked or made). */
function AddRemote({ busy, setBusy, onAdded }: { busy: boolean; setBusy: (b: boolean) => void; onAdded: () => void }) {
  const [kind, setKind] = useState<Kind>(isDesktop() ? 'folder' : 'server');
  const [url, setUrl] = useState('');
  const [projects, setProjects] = useState<{ id: string; name: string; role?: string }[] | null>(null);
  const [project, setProject] = useState('');
  const [name, setName] = useState(authorName() === 'Lapin' ? '' : authorName());
  const server = kind === 'server' ? url.trim().replace(/\/+$/, '') : null;
  const [account, setAccount] = useState(false);
  useEffect(() => { if (server) void signedIn(server).then(setAccount); else setAccount(false); }, [server]);

  const list = async () => {
    setBusy(true);
    try {
      const r = await remoteCall<{ projects: { id: string; name: string; role?: string }[] }>('remote.projects', { kind, url: kind === 'folder' ? url : server }, server);
      setProjects(r.projects);
      setProject(r.projects[0]?.id ?? '');
    } catch (e) { notify(e instanceof Error ? e.message : String(e)); } finally { setBusy(false); }
  };
  const add = async (create: boolean) => {
    setBusy(true);
    try {
      if (name.trim()) setAuthorName(name);
      const args: Record<string, unknown> = { kind, url: kind === 'folder' ? url : server };
      if (create) args.create = true; else if (project) args.project = project;
      if (await writingCall('remote.add', args, server)) notify('Remote added.');
      onAdded();
    } catch (e) { notify(e instanceof Error ? e.message : String(e)); } finally { setBusy(false); }
  };
  return (
    <section className="remote-add">
      <div className="version-save">
        <select value={kind} onChange={(e) => { setKind(e.target.value as Kind); setProjects(null); setProject(''); }} aria-label="Kind of remote">
          {isDesktop() && <option value="folder">Folder</option>}
          <option value="server">Server</option>
        </select>
        <input placeholder={kind === 'folder' ? 'Folder (full path)' : 'Server address (https://…, or http://127.0.0.1:5280)'} value={url} onChange={(e) => { setUrl(e.target.value); setProjects(null); }} aria-label="Address" />
        {kind === 'folder' && isDesktop() && <button type="button" className="icon" title="Choose the folder" onClick={() => void pickFolder(url || null).then((p) => { if (p) { setUrl(p); setProjects(null); } })}><FolderOpen size={13} /></button>}
        <input placeholder="Your name" value={name} onChange={(e) => setName(e.target.value)} aria-label="Your name" title="The name your commits carry (a server's account name when signed in)" />
      </div>
      {server && <Account server={server} signedIn={account} onChange={() => void signedIn(server).then(setAccount)} />}
      <div className="version-save">
        <button type="button" disabled={busy || !url.trim() || (kind === 'server' && !account)} onClick={() => void list()}>Projects on the remote</button>
        {projects && projects.length > 0 && (
          <select value={project} onChange={(e) => setProject(e.target.value)} aria-label="Project">
            {projects.map((p) => <option key={p.id} value={p.id}>{p.name}{p.role ? ` (${p.role})` : ''}</option>)}
          </select>
        )}
        {projects && projects.length === 0 && <span className="muted small">No project there yet.</span>}
        <button type="button" className="btn primary" disabled={busy || !url.trim() || !project} onClick={() => void add(false)}>Use this project</button>
        <button type="button" disabled={busy || !url.trim() || (kind === 'server' && !account)} onClick={() => void add(true)} title="A new project on the remote, for this one">Make a new project there</button>
      </div>
    </section>
  );
}

/** File → Open from server…: a project cloned from a remote into a new file (the desktop), opened in place of this one. */
export function OpenFromServerDialog() {
  const [busy, setBusy] = useState(false);
  const close = () => useUi.setState({ dialog: null });
  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog wide" role="dialog" aria-label="Open from server" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Open from server</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <p className="muted small">The project is copied from the server or folder into a new file on this computer, and opened.</p>
          <CloneForm busy={busy} setBusy={setBusy} />
        </div>
        <footer className="dialog-foot"><span className="grow" /><button type="button" onClick={close}>Cancel</button></footer>
      </div>
    </div>
  );
}

/** File → Save to server…: the project's changes committed with a message and pushed, in one step. Without a remote, the Collaborate dialog opens to connect one. */
export function SaveToServerDialog() {
  const [status, setStatus] = useState<HistoryStatus | null>(null);
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  const close = () => useUi.setState({ dialog: null });
  const gone = useRef(false);
  useEffect(() => () => { gone.current = true; }, []);
  useEffect(() => {
    void historyStatus().then((s) => {
      if (gone.current) return;
      if (!s.remotes[0]) { notify('Connect a server or folder first.'); useUi.setState({ dialog: 'remote' }); return; }
      setStatus(s);
    }).catch((e) => { notify(e instanceof Error ? e.message : String(e)); close(); });
  }, []);
  const remote = status?.remotes[0] ?? null;
  const server = serverOf(status);
  const go = async () => {
    if (busy || !status) return;
    setBusy(true);
    try {
      if (server && !await signedIn(server)) { notify('Sign in to the server first.'); useUi.setState({ dialog: 'remote' }); return; }
      if (status.uncommitted && !await writingCall('history.commit', { message: message.trim() }, server)) return;
      const o = await remoteCall<Outcome>('remote.push', {}, server);
      for (const n of o.notes ?? []) notify(n);
      if (o.pushed === false) { notify(o.message ?? 'The server did not take it.'); useUi.setState({ dialog: 'remote' }); return; }
      notify('Saved to the server.');
      close();
    } catch (e) { notify(e instanceof Error ? e.message : String(e)); } finally { setBusy(false); }
  };
  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog" role="dialog" aria-label="Save to server" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Save to server</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          {!status && <p className="muted">Reading the history…</p>}
          {remote && <p><span className="muted">{remote.kind === 'server' ? 'Server' : 'Folder'}</span> <span data-no-i18n>{remote.url}</span>{' · '}<span data-no-i18n>{remote.project}</span></p>}
          {status && !status.uncommitted && <p className="muted">Nothing new since the last save: the saved version is sent again if the server lacks it.</p>}
          {status?.uncommitted && (
            <input autoFocus placeholder="What changed? (a short note)" value={message} onChange={(e) => setMessage(e.target.value)} aria-label="Note for this save" onKeyDown={(e) => { if (e.key === 'Enter' && message.trim()) void go(); }} />
          )}
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" onClick={close}>Cancel</button>
          <button type="button" className="btn primary" disabled={busy || !status || (status.uncommitted && !message.trim())} onClick={() => void go()}><ArrowUp size={13} /> Save to server</button>
        </footer>
      </div>
    </div>
  );
}

function CloneForm({ busy, setBusy }: { busy: boolean; setBusy: (b: boolean) => void }) {
  const [kind, setKind] = useState<Kind>('folder');
  const [url, setUrl] = useState('');
  const [project, setProject] = useState('');
  const [account, setAccount] = useState(false);
  const target = kind === 'server' ? url.trim().replace(/\/+$/, '') : null;
  useEffect(() => { if (target) void signedIn(target).then(setAccount); else setAccount(false); }, [target]);
  const server = kind === 'server' ? url.trim().replace(/\/+$/, '') : null;
  const go = async () => {
    if (useSession.getState().doc?.dirty && !confirmAction('The project open now has changes not saved. Clone anyway (they are lost)?')) return;
    const file = await pickPath('save', 'clone.lapin');
    if (!file) return;
    setBusy(true);
    try {
      await remoteCall('remote.clone', { kind, url: kind === 'folder' ? url : server, project: project.trim(), path: file }, server);
      notify('Cloned: the project is the remote\'s, in a new file.');
      useUi.setState({ dialog: null });
      enter();
    } catch (e) { notify(e instanceof Error ? e.message : String(e)); } finally { setBusy(false); }
  };
  return (
    <section className="remote-add">
    {server && <Account server={server} signedIn={account} onChange={() => void signedIn(server).then(setAccount)} />}
    <div className="version-save">
      <select value={kind} onChange={(e) => setKind(e.target.value as Kind)} aria-label="Kind of remote">
        <option value="folder">Folder</option>
        <option value="server">Server</option>
      </select>
      <input placeholder={kind === 'folder' ? 'Folder (full path)' : 'Server address'} value={url} onChange={(e) => setUrl(e.target.value)} aria-label="Address" />
      <input placeholder="Project id" value={project} onChange={(e) => setProject(e.target.value)} aria-label="Project id" />
      <button type="button" className="btn primary" disabled={busy || !url.trim() || !project.trim() || (kind === 'server' && !account)} onClick={() => void go()}>Open</button>
    </div>
    </section>
  );
}
