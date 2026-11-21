/** The engine's analysis reports, read again after every diff. */

import { useEffect, useState } from 'react';
import { query, useSession } from '../../engine/session';

export function useAnalysis<T>(name: string, enabled = true, args: Record<string, unknown> = {}): T | null {
  const key = JSON.stringify(args);
  const nodes = useSession((s) => s.nodes);
  const [r, setR] = useState<T | null>(null);
  useEffect(() => {
    if (!enabled) return;
    let live = true;
    void query<T>(name, JSON.parse(key) as Record<string, unknown>).then((x) => { if (live && x) setR(x); });
    return () => { live = false; };
  }, [nodes, name, enabled, key]);
  return r;
}
