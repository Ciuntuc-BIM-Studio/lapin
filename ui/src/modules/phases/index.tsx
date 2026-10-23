/** Phases: existing, construction and execution phases; what a view shows of each. */

import { Clock, Hammer, ListOrdered, Trash2 } from 'lucide-react';
import type { LapinModule } from '../../platform/module';

function PhaseSection() {
  return (
    <dl className="fields">
      <dt>Created in</dt><dd><select defaultValue="new"><option value="existing">Existing</option><option value="new">New construction</option></select></dd>
      <dt>Demolished in</dt><dd><select defaultValue=""><option value="">Not demolished</option><option>Demolition</option></select></dd>
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
      { label: 'Project', tools: [{ id: 'phases.manage', label: 'Phases', icon: ListOrdered }] },
      { label: 'Selection', tools: [
        { id: 'phases.created', label: 'Created here', icon: Hammer },
        { id: 'phases.demolish', label: 'Demolish', icon: Trash2 },
        { id: 'phases.temporary', label: 'Temporary', icon: Clock },
      ] },
    ],
  }],
  propertySections: [{ id: 'phase', label: 'Phase', kinds: ['wall', 'slab', 'door', 'window'], component: PhaseSection }],
};

export default phases;
