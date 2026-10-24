/** Phases: existing, construction and execution phases; each element created in one, maybe demolished in a later one; what a view shows of each. */

import { useEffect } from 'react';
import { Clock, Hammer, ListOrdered, Trash2 } from 'lucide-react';
import type { LapinModule, ToolDef } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { useLooks, type Look } from '../../platform/looks';
import { PhasesDialog, usePhaseSettings } from './PhasesDialog';

/** The selection, given a phase relative to the active one. */
const onSelection = (label: string, args: (active: string) => Record<string, unknown>): ToolDef['run'] => () => {
  const ids = useUi.getState().selected;
  if (!ids.length) { notify(`Select what to mark ${label}.`); return; }
  void query<{ active?: string; phases: { id: string }[] }>('phases.settings').then((s) => {
    const active = s?.active ?? s?.phases[s.phases.length - 1]?.id;
    if (active) void execute('phases.assign', { ids, ...args(active) });
  });
};

/** The 3D follows the active phase and filter: hidden or tinted by state. */
function PhaseLooks() {
  const nodes = useSession((s) => s.nodes);
  useEffect(() => {
    let live = true;
    void query<Record<string, { presentation: string; color?: string; opacity?: number }>>('phases.look').then((r) => {
      if (!live || !r) return;
      const looks: Record<string, Look> = {};
      for (const [id, x] of Object.entries(r)) looks[id] = x.presentation === 'hide' ? { hidden: true } : { color: x.color, opacity: x.opacity };
      useLooks.setState({ looks });
    });
    return () => { live = false; };
  }, [nodes]);
  return null;
}

/** A view's phase and filter in its inspector. */
function ViewPhaseSection({ node }: { node: NodeDto }) {
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
function ElementPhaseSection({ node }: { node: NodeDto }) {
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

const phases: LapinModule = {
  id: 'phases',
  name: 'Phases',
  summary: 'Project phases, phase filters and graphic overrides.',
  tabs: [{
    id: 'phases', label: 'Phases', order: 40,
    groups: [
      { label: 'Project', tools: [{ id: 'phases.manage', label: 'Phases', icon: ListOrdered, run: () => useUi.setState({ dialog: 'phases' }) }] },
      { label: 'Selection', tools: [
        { id: 'phases.created', label: 'Created here', icon: Hammer, run: onSelection('created in the active phase', (a) => ({ created: a })) },
        { id: 'phases.demolish', label: 'Demolish', icon: Trash2, run: onSelection('demolished', (a) => ({ demolished: a })) },
        { id: 'phases.temporary', label: 'Temporary', icon: Clock, run: onSelection('temporary', (a) => ({ created: a, demolished: a })) },
      ] },
    ],
  }],
  dialogs: { phases: PhasesDialog },
  background: PhaseLooks,
  propertySections: [
    { id: 'viewPhase', label: 'Phase', classes: ['LapinView'], component: ViewPhaseSection },
    { id: 'elementPhase', label: 'Phases', classes: ['IfcWall', 'IfcSlab', 'IfcDoor', 'IfcWindow', 'IfcRoof', 'IfcColumn', 'IfcBeam', 'IfcFooting'], component: ElementPhaseSection },
  ],
};

export default phases;
