/**
 * The disciplines a view or sheet may belong to: the browser groups by them
 * (its sets), and a view style may be a discipline's. The known ones are the
 * three defaults, those the project's browser sets and view styles name, and
 * any a view or sheet already carries; another can always be typed.
 */

import type { NodeDto } from '../../engine/protocol';
import { useSession } from '../../engine/session';
import { useUi } from '../../app/store';
import { DISCIPLINES, setsOf } from '../../shell/browserSets';

/** The discipline of the view in front (its own plan view, the section or elevation): what a view made from it takes. */
export const frontDiscipline = (): string | undefined => {
  const s = useUi.getState();
  const id = s.view === 'plan' ? s.planViewId : s.view === 'drawing' ? s.drawingViewId : null;
  const d = id ? useSession.getState().nodes.get(id)?.props.discipline : undefined;
  return typeof d === 'string' && d.trim() ? d : undefined;
};

export function knownDisciplines(nodes: Map<string, NodeDto>, styles: { discipline: string | null }[] = []): string[] {
  const seen = new Map<string, string>();
  const add = (d: unknown) => { if (typeof d === 'string' && d.trim() && !seen.has(d.trim().toLowerCase())) seen.set(d.trim().toLowerCase(), d.trim()); };
  DISCIPLINES.forEach(add);
  for (const s of setsOf(nodes)) (s.filter.disciplines ?? []).forEach(add);
  for (const s of styles) add(s.discipline);
  for (const n of nodes.values()) if (n.type === 'LapinView' || n.type === 'LapinSheet' || n.type === 'LapinSchedule') add(n.props.discipline);
  const all = [...seen.values()];
  return [...all.filter((d) => DISCIPLINES.includes(d)), ...all.filter((d) => !DISCIPLINES.includes(d)).sort((a, b) => a.localeCompare(b))];
}
