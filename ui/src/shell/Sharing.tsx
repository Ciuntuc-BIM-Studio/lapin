/**
 * Working together (docs/plan-multiplayer.md), in the interface: the host's
 * Share session dialog (the invitation's link and code, the guests and their
 * roles), Join session (a link or a code, and one's name), the screen a link
 * opens on, the participants in the top bar, and a guest's bar with Leave.
 */

import { useEffect, useState } from 'react';
import { Copy, LogOut, Users, X } from 'lucide-react';
import { useUi, notify } from '../app/store';
import { engine, useSession } from '../engine/session';
import { invitation, isGuest, join, leave, parseInvitation, reportPresence, savedName, saveName, useShare, type Participant, type ShareStatus } from '../engine/share';
import { activeStorey } from '../engine/session';

const initials = (name: string) => name.split(/\s+/).filter(Boolean).slice(0, 2).map((w) => w[0]!.toUpperCase()).join('') || '?';

const copy = (text: string, what: string) => void navigator.clipboard.writeText(text).then(() => notify(`${what} copied.`), () => notify(text));

/** File → Share session…: the open project shared through the relay, with a link for the browser and a code for the desktop. */
export function ShareDialog() {
  const status = useShare((s) => s.status);
  const participants = useShare((s) => s.participants);
  const path = useSession((s) => s.doc?.path);
  const [name, setName] = useState(savedName());
  const [role, setRole] = useState<'editor' | 'viewer'>('editor');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const close = () => useUi.setState({ dialog: null });
  useEffect(() => { void engine.call<ShareStatus>('share.status').then((s) => useShare.setState({ status: s })).catch(() => undefined); }, []);
  const call = async (method: string, params: Record<string, unknown> = {}) => {
    setBusy(true); setError(null);
    try { useShare.setState({ status: await engine.call<ShareStatus>(method, params) }); }
    catch (e) { setError(e instanceof Error ? e.message : String(e)); }
    finally { setBusy(false); }
  };
  const start = () => { saveName(name); void call('share.start', { name, role }); };
  const guests = participants.filter((p) => !p.host);
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog share-dialog" role="dialog" aria-label="Share session" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Share session</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          {!status?.active ? (
            <>
              <p className="muted small">Others join this project live: from Lapin on their computer (Join session…) or from a browser with the link. The project stays on this computer and this Lapin decides; each undoes only their own edits. Everything goes through Lapin's relay encrypted with the invitation's key, which the relay never sees.</p>
              <dl className="fields">
                <dt>Your name</dt><dd><input value={name} placeholder="as the others see you" onChange={(e) => setName(e.target.value)} /></dd>
                <dt>Guests join as</dt>
                <dd><select value={role} onChange={(e) => setRole(e.target.value as 'editor' | 'viewer')}>
                  <option value="editor">editors (they model, as you do)</option>
                  <option value="viewer">viewers (they look, they do not edit)</option>
                </select></dd>
              </dl>
              {!path && <p className="muted small">The project has no file yet: save it, and while it is shared it is saved every 5 minutes.</p>}
            </>
          ) : (
            <>
              <p className="muted small">Send the link to whoever joins from a browser, or the code to whoever joins from Lapin (Join session…). Anyone with them can join until you stop sharing.</p>
              <dl className="fields">
                <dt>Link</dt>
                <dd className="share-secret"><input readOnly value={status.link ?? ''} onFocus={(e) => e.target.select()} /><button type="button" className="icon" title="Copy the link" onClick={() => copy(status.link ?? '', 'The link')}><Copy size={14} /></button></dd>
                <dt>Code</dt>
                <dd className="share-secret"><input readOnly value={status.code ?? ''} onFocus={(e) => e.target.select()} /><button type="button" className="icon" title="Copy the code" onClick={() => copy(status.code ?? '', 'The code')}><Copy size={14} /></button></dd>
              </dl>
              <p className="eyebrow">In the session</p>
              <ul className="share-people">
                {participants.filter((p) => p.host).map((p) => <li key={p.id}><Avatar p={p} /> {p.name} <span className="muted small">host</span></li>)}
                {guests.map((p) => (
                  <li key={p.id}>
                    <Avatar p={p} /> {p.name}
                    <select value={p.role} onChange={(e) => void call('share.setRole', { id: p.id, role: e.target.value })}>
                      <option value="editor">editor</option><option value="viewer">viewer</option>
                    </select>
                    <button type="button" className="link small" onClick={() => void call('share.kick', { id: p.id })}>ask to leave</button>
                  </li>
                ))}
                {guests.length === 0 && <li className="muted small">No one has joined yet.</li>}
              </ul>
            </>
          )}
          {error && <p className="error-text" role="alert">{error}</p>}
        </div>
        <footer className="dialog-foot">
          {status?.active
            ? <button type="button" className="btn" disabled={busy} onClick={() => void call('share.stop')}>Stop sharing</button>
            : <button type="button" className="btn primary" disabled={busy || !name.trim()} onClick={start}>{busy ? 'Opening…' : 'Share'}</button>}
          <button type="button" className="btn" onClick={close}>Close</button>
        </footer>
      </div>
    </div>
  );
}

/** Join session…: someone's invitation (the link or the code) and one's name; the window becomes their guest. */
export function JoinDialog() {
  const [text, setText] = useState('');
  const [name, setName] = useState(savedName());
  const inv = parseInvitation(text);
  const close = () => useUi.setState({ dialog: null });
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog" role="dialog" aria-label="Join session" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Join session</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          <p className="muted small">Paste the link or the code the host sent you. You work in their project, on their computer: what you draw is theirs, and you undo only your own edits.</p>
          <dl className="fields">
            <dt>Link or code</dt><dd><input value={text} autoFocus placeholder="https://…#join=… or the code" onChange={(e) => setText(e.target.value)} /></dd>
            <dt>Your name</dt><dd><input value={name} placeholder="as the others see you" onChange={(e) => setName(e.target.value)} /></dd>
          </dl>
          {text && !inv && <p className="error-text">That is not an invitation: ask the host for the link again.</p>}
        </div>
        <footer className="dialog-foot">
          <button type="button" className="btn primary" disabled={!inv || !name.trim()} onClick={() => inv && join(inv, name)} data-i18n-ctx="session">Join</button>
          <button type="button" className="btn" onClick={close}>Cancel</button>
        </footer>
      </div>
    </div>
  );
}

/** A link opened before one's name is known: asked once, then the session. */
export function JoinGate({ onJoin }: { onJoin: () => void }) {
  const [name, setName] = useState(savedName());
  const go = () => { saveName(name); useUi.setState({ screen: 'workspace' }); onJoin(); };
  return (
    <div className="start join-gate">
      <section className="start-left">
        <h1 className="start-brand">Lapin</h1>
        <p className="muted">You are invited to work in a project together.</p>
        <label className="field-line"><span className="eyebrow">Your name</span><input value={name} autoFocus placeholder="as the others see you" onChange={(e) => setName(e.target.value)} onKeyDown={(e) => { if (e.key === 'Enter' && name.trim()) go(); }} /></label>
        <button type="button" className="btn primary big" disabled={!name.trim()} onClick={go} data-i18n-ctx="session"><Users size={18} />Join</button>
        <p className="muted small">The project stays on the host's computer; what you see comes from it, encrypted on the way.</p>
        <button type="button" className="link small" onClick={leave}>Open Lapin on its own instead</button>
      </section>
    </div>
  );
}

export function Avatar({ p, size = 22 }: { p: Participant; size?: number }) {
  return <span className="avatar" style={{ background: p.color, width: size, height: size, fontSize: size * 0.45 }} title={`${p.name}${p.host ? ' · host' : p.role === 'viewer' ? ' · viewer' : ''}`}>{initials(p.name)}</span>;
}

/** The top bar's participants: while shared or joined, each with their colour; a click goes to the view they look at. */
export function Participants() {
  const participants = useShare((s) => s.participants);
  const welcome = useShare((s) => s.welcome);
  const sharing = useShare((s) => s.status?.active);
  if (participants.length < 2 && !isGuest() && !sharing) return null;
  const me = welcome?.id ?? 'me';
  const follow = (p: Participant) => {
    const at = p.presence;
    if (at.view === 'plan' && at.storeyId) useUi.setState({ view: 'plan', storeyId: at.storeyId, planViewId: at.planViewId ?? null });
    else if (at.view === '3d') useUi.setState({ view: '3d' });
    notify(`${p.name} is ${at.view === 'plan' ? 'in this plan' : at.view === '3d' ? 'in 3D' : 'here'}.`);
  };
  return (
    <span className="participants" aria-label="In the session">
      {participants.map((p) => (
        <button type="button" key={p.id} className={`participant${p.id === me ? ' me' : ''}`} onClick={() => p.id !== me && follow(p)} title={`${p.name}${p.id === me ? ' (you)' : ''}${p.host ? ' · host' : ` · ${p.role}`}`}>
          <Avatar p={p} />
        </button>
      ))}
      {!isGuest() && <button type="button" className="icon mini" title="Share session" onClick={() => useUi.setState({ dialog: 'share' })}><Users size={14} /></button>}
    </span>
  );
}

/** A guest's bar: whose session this is, their role, Leave; and why it ended, when it has. */
export function GuestBar() {
  const welcome = useShare((s) => s.welcome);
  const ended = useShare((s) => s.ended);
  const connected = useSession((s) => s.connected);
  if (!isGuest()) return null;
  if (ended) {
    return (
      <div className="dialog-backdrop">
        <div className="dialog" role="alertdialog" aria-label="Session ended">
          <header className="dialog-head"><span className="panel-title">The session ended</span></header>
          <div className="dialog-body"><p>{ended}</p></div>
          <footer className="dialog-foot"><button type="button" className="btn primary" onClick={leave}>Back to Lapin</button></footer>
        </div>
      </div>
    );
  }
  return (
    <div className="guest-bar" role="status">
      <Users size={13} />
      {welcome ? <span>In {welcome.host}'s session{welcome.role === 'viewer' ? ', to look' : ''}</span> : <span>Joining {invitation() ? 'the session' : ''}…</span>}
      {!connected && welcome && <span className="muted">· reconnecting…</span>}
      <button type="button" className="link small" onClick={leave}><LogOut size={12} /> Leave</button>
    </div>
  );
}

/** What this window shows the others as it changes: the view it is in, the storey and plan, the selection (the cursor comes from the plan itself). */
export function PresenceReporter() {
  useEffect(() => {
    const send = () => {
      const s = useUi.getState();
      reportPresence({ view: s.view, storeyId: activeStorey()?.id ?? null, planViewId: s.planViewId ?? null, selection: s.selected.slice(0, 500), ...(s.view === 'plan' ? {} : { cursor: null }) });
    };
    send();
    const off = [useUi.subscribe(send), useShare.subscribe((s, prev) => { if (s.participants.length !== prev.participants.length) send(); })];
    return () => off.forEach((f) => f());
  }, []);
  return null;
}
