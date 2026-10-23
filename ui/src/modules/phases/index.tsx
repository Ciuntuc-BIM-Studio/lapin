/** Phases: existing, construction and execution phases; what a view shows of each. */

import { Clock, Hammer, ListOrdered, Trash2 } from 'lucide-react';
import type { LapinModule } from '../../platform/module';

const phases: LapinModule = {
  id: 'phases',
  name: 'Phases',
  summary: 'Project phases, phase filters and graphic overrides.',
  tabs: [{
    id: 'phases', label: 'Phases', order: 40,
    groups: [
      { label: 'Project', tools: [{ id: 'phases.manage', label: 'Phases', icon: ListOrdered }] },
      { label: 'Selection', tools: [
        { id: 'phases.created', label: 'Created here', icon: Hammer },
        { id: 'phases.demolish', label: 'Demolish', icon: Trash2 },
        { id: 'phases.temporary', label: 'Temporary', icon: Clock },
      ] },
    ],
  }],
};

export default phases;
