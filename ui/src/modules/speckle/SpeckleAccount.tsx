/**
 * The Speckle account as Send… and Receive… both ask for it (docs/plan-speckle.md): the server (Speckle's own by default)
 * and a personal access token kept in the system's secure store; signed in, who and where, with Sign out.
 */

import { useState } from 'react';
import { LogOut } from 'lucide-react';
import { canRemember, DEFAULT_SERVER, signIn, signOut, useSpeckle, type SpeckleAccount } from './speckle';

/** A text field across its column (`.fields input` is narrow, for numbers). */
export const WIDE = { width: '100%', textAlign: 'left' } as const;
export const message = (e: unknown) => (e instanceof Error ? e.message : String(e));

/** The sign-in form's state: the server, the token, whether to remember it; `submit` signs in. */
export function useSignInForm(setError: (e: string | null) => void) {
  const [server, setServer] = useState(() => useSpeckle.getState().server || DEFAULT_SERVER);
  const [token, setToken] = useState('');
  const [remember, setRemember] = useState(true);
  const [busy, setBusy] = useState(false);
  const submit = async () => {
    setBusy(true); setError(null);
    try { await signIn(server, token, remember && canRemember()); setToken(''); } catch (e) { setError(message(e)); } finally { setBusy(false); }
  };
  return { server, setServer, token, setToken, remember, setRemember, busy, submit };
}

export type SignInForm = ReturnType<typeof useSignInForm>;

export function SignInFields({ form }: { form: SignInForm }) {
  return (
    <>
      <dl className="fields">
        <dt title="Speckle's own server, or the one your firm hosts">Server</dt>
        <dd><input className="speckle-server" style={WIDE} value={form.server} onChange={(e) => form.setServer(e.target.value)} placeholder={DEFAULT_SERVER} /></dd>
        <dt title="In Speckle: your profile → Developer settings → Access tokens → New token, with streams:read, streams:write and profile:read">Token</dt>
        <dd><input className="speckle-token" style={WIDE} type="password" autoComplete="off" value={form.token} onChange={(e) => form.setToken(e.target.value)} placeholder="Personal access token" /></dd>
        {canRemember() && <>
          <dt title="In the system's secure store (Keychain, Credential Manager, Secret Service), never in a file">Remember</dt>
          <dd><label><input type="checkbox" style={{ width: 'auto', marginRight: 6 }} checked={form.remember} onChange={(e) => form.setRemember(e.target.checked)} />on this computer, in its secure store</label></dd>
        </>}
      </dl>
      <p className="muted small">Make a token in Speckle under your profile: Developer settings → Access tokens, with the scopes streams:read, streams:write and profile:read. Lapin never writes it into the project, its settings or a report.</p>
    </>
  );
}

export function SignInButton({ form }: { form: SignInForm }) {
  return <button type="button" className="btn primary" data-action="speckle-sign-in" disabled={form.busy || !form.token.trim()} onClick={() => void form.submit()}>{form.busy ? 'Signing in…' : 'Sign in'}</button>;
}

export function SignedIn({ account }: { account: SpeckleAccount }) {
  return (
    <div className="row" style={{ alignItems: 'center', gap: 8, marginBottom: 8 }}>
      <span className="grow" data-no-i18n>{account.name ?? account.email ?? ''} · {account.server}</span>
      <button type="button" className="btn" data-action="speckle-sign-out" onClick={() => void signOut()} title="Forget this account and its token on this computer"><LogOut size={13} /> Sign out</button>
    </div>
  );
}
