/**
 * The account's personal access tokens on an Atrium Server (docs/plan-cde-references.md §7, stage R6; Atrium's docs/atrium-server.md
 * § MCP): an AI client signs in to the server's MCP endpoint (`/mcp`) with one. Listed (name, access, expiry, last use), made (a name,
 * read only or read and write, an expiry of up to a year) with the secret shown once and a copy button, and revoked. Shown by the CDE
 * start screen and File → Collaborate… when the server offers `account.tokens`; loaded only when opened. When the server also offers
 * `account.apps` (R6b), the apps the account connected by signing in with the browser (OAuth: claude.ai's connectors, other MCP clients
 * that sign in by themselves) are listed below the tokens, with whether their name is verified, where they return to, their access,
 * when they were connected and last used, and a revoke button; with the server's MCP address to give such an app.
 */

import { useCallback, useEffect, useState } from 'react';
import { Copy, KeyRound, RefreshCw, Trash2 } from 'lucide-react';
import { confirmAction } from '../../shell/prompts';
import { notify } from '../../app/store';
import { when } from './format';
import { AtriumError, accessTokens, connectedApps, createAccessToken, features, revokeAccessToken, revokeConnectedApp, type AccessToken, type ConnectedApp } from '../../engine/atrium';

const message = (e: unknown) => (e instanceof Error ? e.message : String(e));

const EXPIRIES: [number, string][] = [[30, '30 days'], [90, '90 days'], [180, '180 days'], [365, '1 year']];

const copy = (text: string) => { void navigator.clipboard?.writeText(text).then(() => notify('Copied.'), () => notify('Select it and copy it by hand.')); };

/** The secret of a token just made: shown once, with a copy button and where (only) to paste it. */
function OnceToken({ token, command, onDone }: { token: string; command: string; onDone: () => void }) {
  return (
    <div className="cde-once" role="status" data-cde-token-once>
      <p className="small"><b>Your new token, shown once (it cannot be shown again):</b></p>
      <p className="row"><code data-no-i18n data-cde-token>{token}</code>
        <button type="button" className="icon" title="Copy" onClick={() => copy(token)}><Copy size={13} /></button>
        <button type="button" className="btn" onClick={onDone}>Done</button></p>
      <p className="small warn">Paste it only into the AI client's configuration, never into a chat, a document or a project: whoever has it works as you until it expires or you revoke it.</p>
      <p className="small muted">Connect Claude Code:</p>
      <p className="row"><code data-no-i18n>{command}</code>
        <button type="button" className="icon" title="Copy" onClick={() => copy(command)}><Copy size={13} /></button></p>
    </div>
  );
}

/** The command that adds the server to Claude Code, with the token or its placeholder. */
export const claudeCommand = (server: string, token = '<token>') =>
  `claude mcp add --transport http atrium ${server.replace(/\/+$/, '')}/mcp --header "Authorization: Bearer ${token}"`;

/** The server's MCP address, which an app that signs in by itself (OAuth) needs alone. */
export const mcpAddress = (server: string) => `${server.replace(/\/+$/, '')}/mcp`;

/** The apps the account connected over OAuth (R6b), with revoke. */
function ConnectedApps({ server }: { server: string }) {
  const [apps, setApps] = useState<ConnectedApp[] | null>(null);
  const [busy, setBusy] = useState(false);
  const load = useCallback(() => { connectedApps(server).then(setApps).catch((e) => { notify(message(e)); setApps([]); }); }, [server]);
  useEffect(load, [load]);
  const revoke = async (a: ConnectedApp) => {
    if (busy || !confirmAction('Revoke {name}? It stops at once, and works again only if you connect it again.', { name: a.name })) return;
    setBusy(true);
    try { await revokeConnectedApp(server, a.id); notify('App revoked.'); load(); } catch (e) { notify(message(e)); } finally { setBusy(false); }
  };
  const address = mcpAddress(server);
  return (
    <div className="cde-apps" data-cde-apps>
      <p className="eyebrow">Connected apps <button type="button" className="icon" title="Refresh" onClick={load}><RefreshCw size={12} /></button></p>
      <p className="muted small">Apps you connected by signing in with the browser (claude.ai's connectors, other MCP clients that sign in by themselves). Each works as you, with the access you allowed it. To connect one, give it this server's MCP address:</p>
      <p className="row"><code data-no-i18n data-cde-mcp-address>{address}</code>
        <button type="button" className="icon" title="Copy" onClick={() => copy(address)}><Copy size={13} /></button></p>
      {apps === null && <p className="muted small">Loading…</p>}
      {apps && apps.length === 0 && <p className="muted small">No connected apps.</p>}
      {apps && apps.length > 0 && (
        <table className="cde-members">
          <thead><tr><th>App</th><th>Returns to</th><th>Scope</th><th>Connected on</th><th>Last used</th><th /></tr></thead>
          <tbody>
            {apps.map((a) => (
              <tr key={a.id} data-cde-app-row={a.name}>
                <td><span data-no-i18n>{a.name}</span>{!a.verified && <>{' '}<span className="muted small" title="The app registered itself with the server and chose this name: nobody checked it.">(unverified)</span></>}</td>
                <td data-no-i18n>{a.redirectHost}</td>
                <td>{a.scope === 'read+write' ? 'Read and write' : 'Read only'}</td>
                <td><span data-no-i18n>{when(a.created)}</span></td>
                <td>{a.lastUsed ? <span data-no-i18n>{when(a.lastUsed)}</span> : <span>Never</span>}</td>
                <td><button type="button" className="icon" title="Revoke" disabled={busy} onClick={() => void revoke(a)}><Trash2 size={13} /></button></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

export default function AccessTokensPanel({ server }: { server: string }) {
  const [tokens, setTokens] = useState<AccessToken[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [name, setName] = useState('');
  const [write, setWrite] = useState(false);
  const [days, setDays] = useState(90);
  const [shown, setShown] = useState<string | null>(null);
  const [apps, setApps] = useState(false);
  const load = useCallback(() => { accessTokens(server).then(setTokens).catch((e) => { notify(message(e)); setTokens([]); }); }, [server]);
  useEffect(load, [load]);
  useEffect(() => {
    let live = true;
    void features(server).then((f) => { if (live) setApps(f.includes('account.apps')); });
    return () => { live = false; };
  }, [server]);
  const make = async () => {
    if (busy) return;
    if (!name.trim()) { notify('Give the token a name.'); return; }
    setBusy(true);
    try {
      const made = await createAccessToken(server, { name, scope: write ? 'read+write' : 'read', expiresInDays: days });
      setShown(made.token);
      setName(''); setWrite(false); setDays(90);
      load();
    } catch (e) {
      // The refusals' codes are shared with other screens (exists: an account; too_many: IDS files): said here for tokens
      if (e instanceof AtriumError && e.code === 'exists') notify('You have a token of that name: choose another, or revoke it first.');
      else if (e instanceof AtriumError && e.code === 'too_many') notify('An account has at most 50 tokens: revoke some first.');
      else notify(message(e));
    } finally { setBusy(false); }
  };
  const revoke = async (t: AccessToken) => {
    if (busy || !confirmAction('Revoke the token {name}? Clients using it stop at once.', { name: t.name })) return;
    setBusy(true);
    try { await revokeAccessToken(server, t.id); notify('Token revoked.'); load(); } catch (e) { notify(message(e)); } finally { setBusy(false); }
  };
  return (
    <div className="cde-tokens" data-cde-tokens>
      <p className="eyebrow">Access tokens <button type="button" className="icon" title="Refresh" onClick={load}><RefreshCw size={12} /></button></p>
      <p className="muted small">A personal access token lets an AI client (Claude Code, Claude Desktop or another MCP client) work on this server's projects as you: it reads them, and with Read and write it also adds and fills reference documents and comments on issues. Approvals, deletions, rules and members stay with people.</p>
      {shown && <OnceToken token={shown} command={claudeCommand(server, shown)} onDone={() => setShown(null)} />}
      {tokens === null && <p className="muted small">Loading…</p>}
      {tokens && tokens.length === 0 && <p className="muted small">No access tokens yet.</p>}
      {tokens && tokens.length > 0 && (
        <table className="cde-members">
          <thead><tr><th>Name</th><th>Scope</th><th>Expires</th><th>Last used</th><th /></tr></thead>
          <tbody>
            {tokens.map((t) => (
              <tr key={t.id} className={t.expired ? 'muted' : undefined} data-cde-token-row={t.name}>
                <td data-no-i18n>{t.name}</td>
                <td>{t.scope === 'read+write' ? 'Read and write' : 'Read only'}</td>
                <td>{t.expired ? <span>Expired</span> : <span data-no-i18n>{when(t.expires)}</span>}</td>
                <td>{t.lastUsed ? <span data-no-i18n>{when(t.lastUsed)}</span> : <span>Never</span>}</td>
                <td><button type="button" className="icon" title="Revoke" disabled={busy} onClick={() => void revoke(t)}><Trash2 size={13} /></button></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <div className="row cde-token-new">
        <input placeholder="A name for the client (Claude Code on the office laptop)" aria-label="A name for the client (Claude Code on the office laptop)" value={name} maxLength={100}
          onChange={(e) => setName(e.target.value)} onKeyDown={(e) => { if (e.key === 'Enter') void make(); }} />
        <select aria-label="Scope" value={write ? 'read+write' : 'read'} onChange={(e) => setWrite(e.target.value === 'read+write')}>
          <option value="read">Read only</option>
          <option value="read+write">Read and write</option>
        </select>
        <select aria-label="Expiry" value={days} onChange={(e) => setDays(Number(e.target.value))}>
          {EXPIRIES.map(([d, label]) => <option key={d} value={d}>{label}</option>)}
        </select>
        <button type="button" className="btn primary" disabled={busy || !name.trim()} onClick={() => void make()}><KeyRound size={13} />Make a token</button>
      </div>
      <p className="muted small">What a client reads (the documents' text, the people named on certificates and land-book extracts) is sent to its model provider.</p>
      {apps && <ConnectedApps server={server} />}
    </div>
  );
}
