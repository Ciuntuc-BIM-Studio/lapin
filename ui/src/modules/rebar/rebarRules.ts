/** The active rule pack's reinforcement rules (docs/plan-user-extensibility.md §5.4, §6.5): the steels and diameters offered by the pickers instead of the static catalog's. */

import { useEffect, useState } from 'react';
import { query, useSession } from '../../engine/session';

export interface RebarRules { steels: string[]; defaultSteel: string; diameters: number[] }

/** The project's pack's rules for bars, asked again as the model changes; null until it answers (the catalog's lists serve until then). */
export function useRebarRules(): RebarRules | null {
  const version = useSession((s) => s.version);
  const [rules, setRules] = useState<RebarRules | null>(null);
  useEffect(() => {
    let live = true;
    void query<RebarRules>('rebar.rules').then((r) => { if (live && r) setRules((was) => (was && JSON.stringify(was) === JSON.stringify(r) ? was : r)); });
    return () => { live = false; };
  }, [version]);
  return rules;
}

/** The diameters a picker lists (metres): the rules' (they are in mm), else the catalog's; the one a bar has already is kept in the list so it shows. */
export function diameterChoices(rules: Pick<RebarRules, 'diameters'> | null, catalog: number[], current?: number | null): number[] {
  const list = rules ? rules.diameters.map((d) => Math.round(d * 1000) / 1e6) : catalog;
  return current != null && current > 0 && !list.some((d) => Math.abs(d - current) < 1e-9) ? [...list, current].sort((a, b) => a - b) : list;
}

/** The steels a picker lists: the rules', else the catalog's; the current one kept in the list. */
export function steelChoices(rules: Pick<RebarRules, 'steels'> | null, catalog: string[], current?: string | null): string[] {
  const list = rules ? rules.steels : catalog;
  return current && !list.includes(current) ? [...list, current] : list;
}
