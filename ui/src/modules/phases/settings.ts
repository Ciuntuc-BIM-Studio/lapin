/** The project's phases and filters, as the engine reports them (read again after every diff). */

import { useEffect, useState } from 'react';
import { query, useSession } from '../../engine/session';

export interface PhaseSettingsDto {
  phases: { id: string; name: string; kind: string }[];
  filters: { id: string; name: string; states: Record<string, string> }[];
  overrides: Record<string, { color: string; opacity: number }>;
  active?: string; activeFilter?: string;
}

export function usePhaseSettings(): PhaseSettingsDto | null {
  const nodes = useSession((s) => s.nodes);
  const [s, setS] = useState<PhaseSettingsDto | null>(null);
  useEffect(() => { void query<PhaseSettingsDto>('phases.settings').then((r) => r && setS(r)); }, [nodes]);
  return s;
}

