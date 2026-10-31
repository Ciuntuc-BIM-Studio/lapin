/**
 * Convert to native (decision 207, docs/spec-ifc.md): the references selected (an imported element drawn from its
 * triangles; a group selected being its members) made Lapin elements where a module recognises them from their
 * triangles — a wall with its doors and windows, a slab, a column — and the rest Studio solids, editable at once.
 */

import { notify, useUi } from '../../app/store';
import { engine, useSession } from '../../engine/session';
import { GROUP, membersOf } from '../../platform/groups';

interface Converted { recognition: { recognized: number; byClass: Record<string, number>; skipped: Record<string, number> }; editable: { made: number; kept: Record<string, number> } }

export async function convertToNative(): Promise<void> {
  const nodes = useSession.getState().nodes;
  const ids = [...new Set(useUi.getState().selected.flatMap((id) => (nodes.get(id)?.type === GROUP ? membersOf(id) : [id])))];
  if (!ids.length) { notify('Select the imported elements (or their group) to convert.'); return; }
  try {
    const r = await engine.call<Converted>('project.convertToNative', { ids });
    const recognised = Object.entries(r.recognition.byClass).map(([cls, n]) => `${n} ${cls.replace(/^Ifc/, '').toLowerCase()}`).join(', ');
    const kept = Object.values(r.editable.kept).reduce((a, n) => a + n, 0);
    const parts = [
      r.recognition.recognized ? `${r.recognition.recognized} recognised (${recognised})` : '',
      r.editable.made ? `${r.editable.made} made Studio solids` : '',
      kept ? `${kept} left as references (${Object.entries(r.editable.kept).map(([why, n]) => `${why}: ${n}`).join(', ')})` : '',
    ].filter(Boolean);
    notify(parts.length ? `Converted: ${parts.join('; ')}.` : 'Nothing to convert: select imported references.');
  } catch (e) {
    notify(e instanceof Error ? e.message : String(e));
  }
}
