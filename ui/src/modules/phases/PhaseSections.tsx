/** The phases in the inspector: a view's phase and filter, an element's phases. */

import type { NodeDto } from '../../engine/protocol';
import { execute, useSession } from '../../engine/session';
import { useUi } from '../../app/store';
import { selectionWith, sharedValue } from '../../shell/params';
import { usePhaseSettings } from './settings';

/** The selected elements a phase goes to: those on a storey (a forum user's report, topic #29: only the first of 23 beams took it). */
const onStorey = (n: NodeDto) => !!n.storeyId;
/** The choice shown while the selected differ. */
const VARIES = '~varies';

/** A view's phase and filter in its inspector. */
export function ViewPhaseSection({ node }: { node: NodeDto }) {
  const s = usePhaseSettings();
  if (!s) return null;
  return (
    <dl className="fields">
      <dt>Phase</dt>
      <dd><select value={String(node.props.phaseId ?? '')} onChange={(e) => void execute('phases.viewPhase', { id: node.id, phase: e.target.value || null })}>
        <option value="">the active one</option>{s.phases.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
      </select></dd>
      <dt>Phase filter</dt>
      <dd><select value={String(node.props.phaseFilterId ?? '')} onChange={(e) => void execute('phases.viewPhase', { id: node.id, filter: e.target.value || null })}>
        <option value="">none</option>{s.filters.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
      </select></dd>
    </dl>
  );
}

/** An element's phases in its inspector: with several selected, theirs (a value they share, else "varies"), changed for all of them. */
export function ElementPhaseSection({ node }: { node: NodeDto }) {
  const s = usePhaseSettings();
  const nodes = useSession((st) => st.nodes);
  useUi((st) => st.selected);
  if (!s || !node.storeyId) return null;
  const ids = selectionWith(node, onStorey);
  const props = (id: string) => nodes.get(id)?.props;
  const created = sharedValue(ids, 'phaseCreated', props), demolished = sharedValue(ids, 'phaseDemolished', props);
  const assign = (args: Record<string, unknown>) => void execute('phases.assign', { ids: selectionWith(node, onStorey), ...args });
  return (
    <dl className="fields">
      <dt>Created in</dt>
      <dd><select data-field="phase-created" value={created.varies ? VARIES : String(created.value ?? '')} onChange={(e) => assign({ created: e.target.value || null })}>
        {created.varies && <option value={VARIES} disabled>varies</option>}
        <option value="">(no phase)</option>{s.phases.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
      </select></dd>
      <dt>Demolished in</dt>
      <dd><select data-field="phase-demolished" value={demolished.varies ? VARIES : String(demolished.value ?? '')} onChange={(e) => assign({ demolished: e.target.value || null })}>
        {demolished.varies && <option value={VARIES} disabled>varies</option>}
        <option value="">(stays)</option>{s.phases.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
      </select></dd>
    </dl>
  );
}
