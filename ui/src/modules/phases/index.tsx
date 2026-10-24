/** Phases: existing, construction and execution phases; each element created in one, maybe demolished in a later one; what a view shows of each. */

import { Clock, Hammer, ListOrdered, Trash2 } from 'lucide-react';
import type { LapinModule, ToolDef } from '../../platform/module';
import { notify, useUi } from '../../app/store';
import { execute, query } from '../../engine/session';
import { PhasesDialog } from './PhasesDialog';
import { PhaseLooks } from './PhaseLooks';
import { ElementPhaseSection, ViewPhaseSection } from './PhaseSections';

/** The selection, given a phase relative to the active one. */
const onSelection = (label: string, args: (active: string) => Record<string, unknown>): ToolDef['run'] => () => {
  const ids = useUi.getState().selected;
  if (!ids.length) { notify(`Select what to mark ${label}.`); return; }
  void query<{ active?: string; phases: { id: string }[] }>('phases.settings').then((s) => {
    const active = s?.active ?? s?.phases[s.phases.length - 1]?.id;
    if (active) void execute('phases.assign', { ids, ...args(active) });
  });
};

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
