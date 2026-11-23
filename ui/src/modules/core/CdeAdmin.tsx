/**
 * Atrium Server's own screens (Atrium docs/plan-cde-web.md W2 and W3), shared by the CDE start screen in the browser and File →
 * Collaborate… on the desktop (engine/atrium.ts reaches the server either way): a project's activity (pushes and issue changes
 * together, newest first), a project's rules (owners change them), and the server's administration for its administrators (accounts:
 * make one with a password shown once, disable and enable, reset a password, make administrator; a new project).
 */

import { useCallback, useEffect, useState } from 'react';
import { Copy, FolderPlus, KeyRound, RefreshCw, ShieldCheck, UserPlus, X } from 'lucide-react';
import { backdropClose } from '../../shell/Dialog';
import { confirmAction } from '../../shell/prompts';
import { notify, useUi } from '../../app/store';
import { when } from './format';
import { refLabel } from '../../engine/cdeServer';
import {
  activity, adminUsers, changeUser, createProject, createUser, getRules, issueWords, putRules, resetPassword,
  type ActivityItem, type AdminUser, type Rules,
} from '../../engine/atrium';

const message = (e: unknown) => (e instanceof Error ? e.message : String(e));

/** A password the server made, shown once with a copy button. */
function OncePassword({ email, password, onDone }: { email: string; password: string; onDone: () => void }) {
  const copy = () => { void navigator.clipboard?.writeText(password).then(() => notify('Copied.'), () => notify('Select it and copy it by hand.')); };
  return (
    <div className="cde-once" role="status" data-cde-once>
      <p className="small"><span>The password for</span> <b data-no-i18n>{email}</b> <span>(shown once: give it to the person; they change it after signing in):</span></p>
      <p className="row"><code data-no-i18n data-cde-password>{password}</code>
        <button type="button" className="icon" title="Copy" onClick={copy}><Copy size={13} /></button>
        <button type="button" className="btn" onClick={onDone}>Done</button></p>
    </div>
  );
}

/** The server's accounts and a new project, for its administrators. `onProject` follows a project made (the list to reload). */
export function AdminPanel({ server, onProject }: { server: string; onProject?: () => void }) {
  const [users, setUsers] = useState<AdminUser[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [email, setEmail] = useState('');
  const [name, setName] = useState('');
  const [admin, setAdmin] = useState(false);
  const [shown, setShown] = useState<{ email: string; password: string } | null>(null);
  const [project, setProject] = useState('');
  const load = useCallback(() => { adminUsers(server).then(setUsers).catch((e) => { notify(message(e)); setUsers([]); }); }, [server]);
  useEffect(load, [load]);
  const run = async (what: () => Promise<unknown>, done?: string) => {
    if (busy) return;
    setBusy(true);
    try { await what(); if (done) notify(done); load(); } catch (e) { notify(message(e)); } finally { setBusy(false); }
  };
  const create = () => run(async () => {
    const r = await createUser(server, { email, name, isAdmin: admin });
    if (r.password) setShown({ email: r.user.email, password: r.password });
    setEmail(''); setName(''); setAdmin(false);
  }, 'Account created.');
  const reset = (u: AdminUser) => confirmAction('A new password for {who}? Their sign-ins end.', { who: u.name || u.email })
    && void run(async () => setShown({ email: u.email, password: await resetPassword(server, u.email) }), 'Password reset.');
  const newProject = () => run(async () => {
    const p = await createProject(server, project);
    setProject('');
    notify('Project created: you are its owner. Add its members on its page.');
    void p;
    onProject?.();
  });
  return (
    <div className="cde-admin" data-cde-admin>
      <p className="eyebrow">Accounts <button type="button" className="icon" title="Refresh" onClick={load}><RefreshCw size={12} /></button></p>
      {shown && <OncePassword email={shown.email} password={shown.password} onDone={() => setShown(null)} />}
      {users === null && <p className="muted small">Loading…</p>}
      {users && users.length > 0 && (
        <table className="cde-members cde-users">
          <thead><tr><th>Name</th><th>E-mail</th><th>Member of</th><th>Administrator</th><th /></tr></thead>
          <tbody>
            {users.map((u) => (
              <tr key={u.email} className={u.disabled ? 'muted' : undefined} data-cde-user={u.email}>
                <td data-no-i18n>{u.name}</td>
                <td className="small" data-no-i18n>{u.email}</td>
                <td className="small">{u.projects.length}</td>
                <td><input type="checkbox" aria-label="Administrator" checked={u.isAdmin} disabled={busy} onChange={(e) => void run(() => changeUser(server, u.email, { isAdmin: e.target.checked }), e.target.checked ? 'Now an administrator.' : 'No longer an administrator.')} /></td>
                <td className="cde-row-actions">
                  <button type="button" className="btn" disabled={busy} data-cde-disable={u.email} onClick={() => {
                    if (u.disabled || confirmAction('Disable {who}? Their sign-ins end; their projects, commits and issues stay.', { who: u.name || u.email }))
                      void run(() => changeUser(server, u.email, { disabled: !u.disabled }), u.disabled ? 'Account enabled.' : 'Account disabled.');
                  }}>{u.disabled ? 'Enable' : 'Disable'}</button>
                  <button type="button" className="icon" title="Reset the password" disabled={busy} onClick={() => reset(u)}><KeyRound size={13} /></button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <form className="cde-add" onSubmit={(e) => { e.preventDefault(); if (email.trim()) void create(); }}>
        <input placeholder="E-mail address" aria-label="E-mail address" value={email} onChange={(e) => setEmail(e.target.value)} data-cde-new-email />
        <input placeholder="Name" aria-label="Name" value={name} onChange={(e) => setName(e.target.value)} data-cde-new-name />
        <label className="row small"><input type="checkbox" checked={admin} onChange={(e) => setAdmin(e.target.checked)} /> Administrator</label>
        <button type="submit" className="btn" disabled={busy || !email.trim()} data-cde-create-user><UserPlus size={13} />Create account</button>
      </form>
      <p className="muted small">The server makes the first password and shows it once. There is no sign-up and no reset by e-mail.</p>
      <p className="eyebrow">New project</p>
      <form className="cde-add" onSubmit={(e) => { e.preventDefault(); if (project.trim()) void newProject(); }}>
        <input placeholder="Project name" aria-label="Project name" value={project} onChange={(e) => setProject(e.target.value)} data-cde-new-project />
        <button type="submit" className="btn" disabled={busy || !project.trim()} data-cde-create-project><FolderPlus size={13} />Create project</button>
      </form>
    </div>
  );
}

let adminServer: string | null = null;

/** Administration… for a server (File → Collaborate… on the desktop): the dialog opened on it. */
export function openAdmin(server: string): void {
  adminServer = server;
  useUi.setState({ dialog: 'cdeAdmin' });
}

/** File → Collaborate… → Administration… on the desktop: the same screens in a dialog, for the project's server. */
export function CdeAdminDialog() {
  const server = adminServer;
  const close = () => useUi.setState({ dialog: 'remote' });
  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog wide" role="dialog" aria-label="Administration" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title"><ShieldCheck size={14} /> Administration · <span data-no-i18n>{server}</span></span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">{server ? <AdminPanel server={server} /> : <p className="muted">No server.</p>}</div>
        <footer className="dialog-foot"><span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}

/** A project's rules: owners change them; the others read them. */
export function RulesEditor({ server, project }: { server: string; project: string }) {
  const [rules, setRules] = useState<Rules | null>(null);
  const [busy, setBusy] = useState(false);
  useEffect(() => { getRules(server, project).then(setRules).catch(() => setRules(null)); }, [server, project]);
  if (!rules) return null;
  const owner = rules.role === 'owner';
  const set = async (change: Partial<Omit<Rules, 'role'>>) => {
    setBusy(true);
    try { setRules(await putRules(server, project, change)); notify('Rules changed.'); } catch (e) { notify(message(e)); } finally { setBusy(false); }
  };
  const off = busy || !owner;
  return (
    <div className="cde-rules" data-cde-rules>
      <p className="eyebrow">Rules</p>
      <table className="cde-members">
        <tbody>
          <tr><td>Who saves to main</td><td><select aria-label="Who saves to main" value={rules.main} disabled={off} onChange={(e) => void set({ main: e.target.value as Rules['main'] })}>
            <option value="editor">Editors and owners</option><option value="owner">Owners only</option></select></td></tr>
          <tr><td>Who makes issue tags</td><td><select aria-label="Who makes issue tags" value={rules.issueTags} disabled={off} onChange={(e) => void set({ issueTags: e.target.value as Rules['issueTags'] })}>
            <option value="owner">Owners only</option><option value="editor">Editors and owners</option></select></td></tr>
          <tr><td>Who comments on issues</td><td><select aria-label="Who comments on issues" value={rules.comments} disabled={off} onChange={(e) => void set({ comments: e.target.value as Rules['comments'] })}>
            <option value="viewer">Everyone, viewers too</option><option value="editor">Editors and owners</option></select></td></tr>
          <tr><td>Who closes an issue</td><td><select aria-label="Who closes an issue" value={rules.close} disabled={off} onChange={(e) => void set({ close: e.target.value as Rules['close'] })}>
            <option value="author">Its author, and owners</option><option value="editor">Anyone who may change it</option></select></td></tr>
          <tr><td>Resolved needs a comment</td><td><input type="checkbox" aria-label="Resolved needs a comment" checked={rules.resolveNeedsComment} disabled={off} onChange={(e) => void set({ resolveNeedsComment: e.target.checked })} /></td></tr>
        </tbody>
      </table>
      {!owner && <p className="muted small">The project's owners change its rules.</p>}
    </div>
  );
}

/** One activity line: a ref moved (a save to the server, a version, an issue tag) or an issue changed. */
function ActivityLine({ item, nameOf }: { item: ActivityItem; nameOf: (email: string) => string }) {
  if (item.kind === 'ref') {
    return (
      <li className="small" data-cde-activity="ref">
        <span data-no-i18n>{when(item.at)} · {item.by.name} · {refLabel(item.name)}</span>{' '}
        <span className="muted" data-i18n-ctx="cde">{item.old === null ? 'created' : item.new === null ? 'deleted' : item.force ? 'moved by force' : 'moved'}</span>
      </li>
    );
  }
  return (
    <li className="small" data-cde-activity="issue">
      <span data-no-i18n>{when(item.at)} · {item.by.name} · „{item.title ?? item.topicGuid.slice(0, 8)}”</span>{' '}
      {issueWords(item, nameOf).map((w, i) => <span key={w} className="muted">{i > 0 ? ', ' : ''}<span data-i18n-ctx="cde">{w}</span></span>)}
    </li>
  );
}

/** A project's activity: who saved, tagged, and changed which issue, newest first; More reads the next page. */
export function ActivityList({ server, project, nameOf = (x) => x }: { server: string; project: string; nameOf?: (email: string) => string }) {
  const [items, setItems] = useState<ActivityItem[] | null>(null);
  const [cursor, setCursor] = useState<string | undefined>();
  const [failed, setFailed] = useState<string | null>(null);
  const load = useCallback(async (from?: string) => {
    try {
      const r = await activity(server, project, from);
      setItems((old) => (from ? [...(old ?? []), ...r.items] : r.items));
      setCursor(r.more ? r.cursor : undefined);
      setFailed(null);
    } catch (e) { setFailed(message(e)); if (!from) setItems([]); }
  }, [server, project]);
  useEffect(() => { setItems(null); void load(); }, [load]);
  return (
    <div className="cde-activity-block" data-cde-activity-list>
      <p className="eyebrow">Activity <button type="button" className="icon" title="Refresh" onClick={() => { setItems(null); void load(); }}><RefreshCw size={12} /></button></p>
      {items === null && <p className="muted small">Loading…</p>}
      {failed && <p className="muted small">{failed}</p>}
      {items?.length === 0 && !failed && <p className="muted small">Nothing yet.</p>}
      <ul className="cde-list cde-activity">
        {items?.map((item, i) => <ActivityLine key={i} item={item} nameOf={nameOf} />)}
      </ul>
      {cursor && <button type="button" className="link small" onClick={() => void load(cursor)}>More</button>}
    </div>
  );
}
