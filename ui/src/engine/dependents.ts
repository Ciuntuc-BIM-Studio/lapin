/**
 * Deleting elements, and saying what was built from them (core.dependents,
 * docs/dependencies.md): a wall a deleted wall was joined to, a wall attached
 * to a deleted roof. What stays in the model is named in the notice, so the
 * change to it is never a surprise; Ctrl+Z brings everything back.
 */

import { notify } from '../app/store';
import { execute, query, useSession } from './session';
import type { NodeDto } from './protocol';
import { ask, choiceField } from '../shell/AskBox';

/** At most this many names in the notice; the rest counted. */
const NAMED = 5;

/** The notice for the elements left changed by a deletion; null when none. */
export function dependentsNotice(names: string[]): string | null {
  if (names.length === 0) return null;
  const listed = names.slice(0, NAMED).join(', ') + (names.length > NAMED ? ` +${names.length - NAMED}` : '');
  return `Deleted. Rebuilt because they depended on it: ${listed}.`;
}

/** The session once a diff newer than `version` has come in (or after a while, if none comes). */
function afterDiff(version: number, ms = 2000): Promise<void> {
  if (useSession.getState().version > version) return Promise.resolve();
  return new Promise((resolve) => {
    const done = () => { clearTimeout(timer); stop(); resolve(); };
    const timer = setTimeout(done, ms);
    const stop = useSession.subscribe((s) => { if (s.version > version) done(); });
  });
}

/** The detail views the callouts being deleted made (forum topic 107), those not already going: asked whether they go too. */
export function detailsOf(ids: string[], nodes: Map<string, NodeDto>): string[] {
  const going = new Set(ids);
  return [...new Set(ids.map((id) => nodes.get(id)).filter((n): n is NodeDto => n?.type === 'LapinCallout' && n.props.made === true)
    .map((n) => String(n.props.target ?? '')).filter((t) => t && nodes.has(t) && !going.has(t)))];
}

/** Delete `ids` (one undo step), then name the elements that depended on them and are still there. */
export async function deleteElements(ids: string[]): Promise<void> {
  if (ids.length === 0) return;
  // A callout's detail view: kept, or deleted with it in the same step, as the user says.
  const details = detailsOf(ids, useSession.getState().nodes);
  if (details.length > 0) {
    const r = await ask('Delete callout', [choiceField('views', 'Its detail view', 'keep', [{ value: 'keep', label: 'Keep it' }, { value: 'delete', label: 'Delete it too' }])]);
    if (!r) return;
    if (r.views === 'delete') ids = [...ids, ...details];
  }
  const before = await query<{ dependents: string[] }>('core.dependents', { ids });
  const version = useSession.getState().version;
  await execute('element.delete', { ids });
  const dependents = before?.dependents ?? [];
  if (dependents.length === 0 || useSession.getState().error != null) return;
  await afterDiff(version);
  const { nodes, info } = useSession.getState();
  const element = (n: NodeDto | undefined): n is NodeDto => !!n && info?.nodeTypes.find((t) => t.type === n.type)?.category === 'element';
  const notice = dependentsNotice(dependents.map((id) => nodes.get(id)).filter(element).map((n) => n.name || n.type));
  if (notice) notify(notice);
}
