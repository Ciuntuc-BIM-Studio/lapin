/**
 * The status bar's word on Archicad's model (docs/plan-archicad.md A2): "Archicad model changed (N)" when the Archicad the project
 * received from changed since, at a click Get latest from Archicad. Asked once when the project opens, then every 10 s only while
 * the project is linked to an Archicad model (it was received with a state kept) and the window is visible, never while a receive
 * or an update runs. The engine's `archicad.status` is cheap: Archicad's changes counted, no element read. Fetched with the
 * Archicad module, the first time the status bar draws it (index.tsx).
 */

import { RefreshCw } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useSession } from '../../engine/session';
import { archicadRunning, onArchicadDone, status, statusText, type ArchicadStatus as Status } from './archicad';
import { getLatest } from './getLatest';

export const POLL_MS = 10_000;

export function ArchicadStatus() {
  const connected = useSession((s) => s.connected);
  const path = useSession((s) => s.doc?.path ?? null);
  const [st, setSt] = useState<Status | null>(null);
  const [visible, setVisible] = useState(() => typeof document === 'undefined' || document.visibilityState !== 'hidden');

  useEffect(() => {
    const on = () => setVisible(document.visibilityState !== 'hidden');
    document.addEventListener('visibilitychange', on);
    return () => document.removeEventListener('visibilitychange', on);
  }, []);

  // Asked when the project opens or a receive or an update ends (whether it is linked now), then polled while linked and visible.
  useEffect(() => {
    if (!connected) { setSt(null); return; }
    let live = true;
    const ask = () => {
      if (archicadRunning()) return;
      status().then((s) => { if (live) setSt(s); }).catch(() => { if (live) setSt(null); });
    };
    ask();
    const off = onArchicadDone(ask);
    return () => { live = false; off(); };
  }, [connected, path]);

  const linked = !!st?.linked;
  useEffect(() => {
    if (!connected || !linked || !visible) return;
    let live = true;
    const id = setInterval(() => {
      if (archicadRunning()) return;
      status().then((s) => { if (live) setSt(s); }).catch(() => undefined);
    }, POLL_MS);
    return () => { live = false; clearInterval(id); };
  }, [connected, linked, visible]);

  const text = statusText(st);
  if (!text) return null;
  return (
    <button type="button" className="status-link status-warn" data-testid="archicad-changed" title="Archicad's model changed since it was received: click to get the latest"
      onClick={() => void getLatest()}>
      <RefreshCw size={11} /> <span>{text.label}</span>{text.count != null && <span data-no-i18n> ({text.count})</span>}
    </button>
  );
}
