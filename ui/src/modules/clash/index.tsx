/** Clash detection (docs/plan-visualisation.md, stage 2): search sets, hard and clearance tests, run when asked, their clashes followed, shown and sent out. */

import { Merge } from 'lucide-react';
import type { LapinModule } from '../../platform/module';
import { useUi } from '../../app/store';
import { ClashDialog } from './ClashDialog';

const clash: LapinModule = {
  id: 'clash',
  name: 'Clash detection',
  summary: 'Search sets, hard and clearance clash tests between them, the clashes followed from run to run, BCF.',
  tabs: [{
    // No tab of its own: its group goes into Manage, beside the openings' clash check.
    id: 'manage', label: 'Manage', order: 80,
    groups: [{ label: 'Clashes', tools: [{ id: 'clash.manage', label: 'Clash detection', icon: Merge, run: () => useUi.setState({ dialog: 'clashes' }) }] }],
  }],
  dialogs: { clashes: ClashDialog },
};

export default clash;
