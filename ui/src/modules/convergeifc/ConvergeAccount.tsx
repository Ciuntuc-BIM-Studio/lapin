/**
 * The Converge IFC account as Send… and Get… both ask for it (docs/plan-converge-ifc.md): the server's address (the firm's
 * own: there is no default) and a personal API token kept in the system's secure store; signed in, who and where, with Sign out.
 */

import { useEffect, useState } from 'react';
import { LogOut } from 'lucide-react';
import { canRemember, listProjects, restore, signIn, signOut, useConverge, type ConvergeAccount, type ConvergeProject } from './convergeifc';

/** A text field across its column (`.fields input` is narrow, for numbers). */
export const WIDE = { width: '100%', textAlign: 'left' } as const;
export const message = (e: unknown) => (e instanceof Error ? e.message : String(e));

/**
 * What both dialogs start with: the account (the engine's, or the one whose token the secure store keeps) and, signed in,
 * its projects, the last one used chosen again.
 */
export function useAccountProjects(setError: (e: string | null) => void) {
  const account = useConverge((s) => s.account);
  const [checking, setChecking] = useState(true);
  const [projects, setProjects] = useState<ConvergeProject[]>([]);
  const [project, setProject] = useState('');

  useEffect(() => { void restore().finally(() => setChecking(false)); }, []);

  useEffect(() => {
    if (!account) { setProjects([]); setProject(''); return; }
    let live = true;
    listProjects().then((list) => {
      if (!live) return;
      setProjects(list);
      const last = useConverge.getState().project;
      setProject(list.some((p) => p.id === last) ? last! : list[0]?.id ?? '');
    }).catch((e) => setError(message(e)));
    return () => { live = false; };
  }, [account, setError]);

  return { account, checking, projects, project, setProject };
}

/** The sign-in form's state: the server, the token, whether to remember it; `submit` signs in. */
export function useSignInForm(setError: (e: string | null) => void) {
  const [server, setServer] = useState(() => useConverge.getState().server);
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
        <dt title="The address of your firm's Converge IFC server">Server</dt>
        <dd><input className="converge-server" style={WIDE} value={form.server} onChange={(e) => form.setServer(e.target.value)} placeholder="https://cde.example.com" /></dd>
        <dt title="On the server: the account menu → API tokens">Token</dt>
        <dd><input className="converge-token" style={WIDE} type="password" autoComplete="off" value={form.token} onChange={(e) => form.setToken(e.target.value)} placeholder="Personal access token" /></dd>
        {canRemember() && <>
          <dt title="In the system's secure store (Keychain, Credential Manager, Secret Service), never in a file">Remember</dt>
          <dd><label><input type="checkbox" style={{ width: 'auto', marginRight: 6 }} checked={form.remember} onChange={(e) => form.setRemember(e.target.checked)} />on this computer, in its secure store</label></dd>
        </>}
      </dl>
      <p className="muted small">Make a token on the server, in the account menu under API tokens. Lapin never writes it into the project, its settings or a report.</p>
    </>
  );
}

export function SignInButton({ form }: { form: SignInForm }) {
  return <button type="button" className="btn primary" data-action="converge-sign-in" disabled={form.busy || !form.server.trim() || !form.token.trim()} onClick={() => void form.submit()}>{form.busy ? 'Signing in…' : 'Sign in'}</button>;
}

export function SignedIn({ account }: { account: ConvergeAccount }) {
  return (
    <div className="row" style={{ alignItems: 'center', gap: 8, marginBottom: 8 }}>
      <span className="grow" data-no-i18n>{account.name ?? account.email ?? ''} · {account.server}</span>
      <button type="button" className="btn" data-action="converge-sign-out" onClick={() => void signOut()} title="Forget this account and its token on this computer"><LogOut size={13} /> Sign out</button>
    </div>
  );
}
