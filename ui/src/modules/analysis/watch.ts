/** Keeps the analysis in step with the model: a change to it makes the results stale (they are derived, never kept). */

import { useEffect } from 'react';
import { useSession } from '../../engine/session';
import { markStale } from './useResults';

export function useWatchNodes(): void {
  const nodes = useSession((s) => s.nodes);
  useEffect(() => { markStale(nodes); }, [nodes]);
}

/** Mounted while the workspace is open (the module's background). */
export function AnalysisWatch() {
  useWatchNodes();
  return null;
}
