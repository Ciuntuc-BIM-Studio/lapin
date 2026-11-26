/**
 * Where the account's access tokens (AccessTokens.tsx, R6) are opened: a button shown when the server offers `account.tokens` (its
 * `GET /atrium/1.0/` features), and the panel, loaded only when it is first opened.
 */

import { lazy, Suspense, useEffect, useState } from 'react';
import { KeyRound } from 'lucide-react';
import { features } from '../../engine/atrium';

const Panel = lazy(() => import('./AccessTokens'));

/** Whether the server lets an account make access tokens (false until it says so). */
export function useTokensOffered(server: string | null): boolean {
  const [offered, setOffered] = useState(false);
  useEffect(() => {
    setOffered(false);
    if (!server) return;
    let live = true;
    void features(server).then((f) => { if (live) setOffered(f.includes('account.tokens')); });
    return () => { live = false; };
  }, [server]);
  return offered;
}

/** The button that opens and closes the access tokens. */
export function AccessTokensButton({ open, onToggle, className = 'btn' }: { open: boolean; onToggle: () => void; className?: string }) {
  return (
    <button type="button" className={`${className}${open ? ' primary' : ''}`} data-cde="tokens" onClick={onToggle}
      title="Personal access tokens for AI clients (MCP)"><KeyRound size={14} />Access tokens…</button>
  );
}

/** The access tokens' panel, loaded on first use. */
export function AccessTokensSection({ server }: { server: string }) {
  return (
    <Suspense fallback={<p className="muted small">Loading…</p>}>
      <Panel server={server} />
    </Suspense>
  );
}
