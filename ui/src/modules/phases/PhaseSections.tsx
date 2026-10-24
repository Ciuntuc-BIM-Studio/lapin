/** The phases in the inspector: a view's phase and filter, an element's phases. */

import type { NodeDto } from '../../engine/protocol';
import { execute } from '../../engine/session';
import { usePhaseSettings } from './settings';

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

/** An element's phases in its inspector. */
export function ElementPhaseSection({ node }: { node: NodeDto }) {
  const s = usePhaseSettings();
  if (!s || !node.storeyId) return null;
  return (
    <dl className="fields">
      <dt>Created in</dt>
      <dd><select value={String(node.props.phaseCreated ?? '')} onChange={(e) => void execute('phases.assign', { id: node.id, created: e.target.value || null })}>
        <option value="">(no phase)</option>{s.phases.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
      </select></dd>
      <dt>Demolished in</dt>
      <dd><select value={String(node.props.phaseDemolished ?? '')} onChange={(e) => void execute('phases.assign', { id: node.id, demolished: e.target.value || null })}>
        <option value="">(stays)</option>{s.phases.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
      </select></dd>
    </dl>
  );
}
