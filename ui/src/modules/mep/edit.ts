/**
 * The system's edit mode (the platform's edit scope): a system isolated from
 * the rest of the model — its nodes and runs picked and edited, everything
 * else faded but still snapped to. Done counts the fittings its runs make
 * and what is left open; with mep-pro, it resolves the system (sizes,
 * laying rules, the checks).
 */

import { notify, useUi } from '../../app/store';
import { useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { enterScope, exitScope, useScope } from '../../platform/scope';
import { mepHooks } from './hooks';
import { fittingsQuery, openPortsQuery } from './network';
import { setMep } from './prefs';

const KIND = 'System';

/** The system being edited, if any. */
export const editedSystem = (): string | null => {
  const s = useScope.getState().scope;
  return s && s.kind === KIND ? s.id : null;
};

/** The system an element belongs to (a system is its own). */
export const systemOfNode = (n: NodeDto | null | undefined): string | null =>
  !n ? null : n.type === 'IfcDistributionSystem' ? n.id : typeof n.props.systemId === 'string' ? n.props.systemId : null;

export function editSystem(systemId: string): void {
  const system = useSession.getState().nodes.get(systemId);
  if (!system) return;
  setMep({ systemId });
  enterScope({
    id: systemId, kind: KIND, label: system.name,
    includes: (n) => n.id === systemId || n.props.systemId === systemId,
    onDone: async () => {
      if (mepHooks.resolve) { await mepHooks.resolve(systemId); return; }
      const [fittings, open] = await Promise.all([fittingsQuery(systemId), openPortsQuery(systemId)]);
      const parts = [`${fittings?.bends ?? 0} bend(s), ${fittings?.tees ?? 0} tee(s)`];
      if (open?.length) parts.push(`${open.length} open end(s)`);
      notify(`${system.name}: ${parts.join(', ')}.`);
    },
  });
}

/** Edit: the selected element's system, else the current one; again: Done. */
export function toggleEdit(fallback: string | null): void {
  if (editedSystem()) { void exitScope(); return; }
  const { selection } = useUi.getState();
  const id = systemOfNode(selection ? useSession.getState().nodes.get(selection) : null) ?? fallback;
  if (id) editSystem(id);
  else notify('Place a system first (New system), or choose one in Systems….');
}
