/** The engine's analysis reports, read again after every diff. */

import { useEffect, useState } from 'react';
import { query, useSession } from '../../engine/session';

export function useAnalysis<T>(name: string, enabled = true, args: Record<string, unknown> = {}): T | null {
  const key = JSON.stringify(args);
  const nodes = useSession((s) => s.nodes);
  const [r, setR] = useState<T | null>(null);
  // What another report (or a switched-off one) answered is never shown for this one.
  useEffect(() => { setR(null); }, [name, enabled, key]);
  useEffect(() => {
    if (!enabled) return;
    let live = true;
    void query<T>(name, JSON.parse(key) as Record<string, unknown>).then((x) => { if (live) setR(x ?? null); });
    return () => { live = false; };
  }, [nodes, name, enabled, key]);
  return enabled ? r : null;
}
