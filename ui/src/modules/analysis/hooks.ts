/** The engine's analysis reports, read again after every diff. */

import { useEffect, useState } from 'react';
import { query, useSession } from '../../engine/session';

export function useAnalysis<T>(name: string, enabled = true): T | null {
  const nodes = useSession((s) => s.nodes);
  const [r, setR] = useState<T | null>(null);
  useEffect(() => {
    if (!enabled) return;
    let live = true;
    void query<T>(name).then((x) => { if (live && x) setR(x); });
    return () => { live = false; };
  }, [nodes, name, enabled]);
  return r;
}
