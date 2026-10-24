/** The phases' looks in the 3D: what the active phase and filter hide or tint. */

import { useEffect } from 'react';
import { query, useSession } from '../../engine/session';
import { setLooks, type Look } from '../../platform/looks';

/** The 3D follows the active phase and filter: hidden or tinted by state. */
export function PhaseLooks() {
  const nodes = useSession((s) => s.nodes);
  useEffect(() => {
    let live = true;
    void query<Record<string, { presentation: string; color?: string; opacity?: number }>>('phases.look').then((r) => {
      if (!live || !r) return;
      const looks: Record<string, Look> = {};
      for (const [id, x] of Object.entries(r)) looks[id] = x.presentation === 'hide' ? { hidden: true } : { color: x.color, opacity: x.opacity };
      setLooks('phases', looks);
    });
    return () => { live = false; };
  }, [nodes]);
  return null;
}
