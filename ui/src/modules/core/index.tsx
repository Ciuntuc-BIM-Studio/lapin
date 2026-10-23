/** Core: always there — selecting, measuring, the views and how they look, materials and drafting standards. */

import { Box, Eye, Hash, Layers3, MousePointer2, Palette, Ruler, Scissors, Square } from 'lucide-react';
import type { LapinModule } from '../../platform/module';
import { setTool, useUi } from '../../app/store';
import { MaterialsDialog } from './MaterialsDialog';
import { StandardsDialog } from './StandardsDialog';

const core: LapinModule = {
  id: 'core',
  name: 'Core',
  summary: 'Project, views, selection and measuring.',
  tabs: [{
    id: 'manage', label: 'Manage', order: 80,
    groups: [
      { label: 'Settings', tools: [
        { id: 'manage.materials', label: 'Materials', icon: Palette, run: () => useUi.setState({ dialog: 'materials' }) },
        { id: 'manage.standards', label: 'Drafting standards', icon: Hash, run: () => useUi.setState({ dialog: 'standards' }) },
      ] },
    ],
  }, {
    id: 'view', label: 'View', order: 90,
    groups: [
      { label: 'Views', tools: [
        { id: 'view.3d', label: '3D', icon: Box, run: () => useUi.setState({ view: '3d' }) },
        { id: 'view.plan', label: 'Plan', icon: Square, run: () => useUi.setState({ view: 'plan' }) },
        { id: 'view.section', label: 'Section', icon: Scissors },
      ] },
      { label: 'Graphics', tools: [
        { id: 'view.style', label: 'Visual style', icon: Eye },
        { id: 'view.layers', label: 'Visibility', icon: Layers3 },
      ] },
    ],
  }],
  dialogs: { materials: MaterialsDialog, standards: StandardsDialog },
  hotbar: [
    { id: 'select', label: 'Select', icon: MousePointer2, shortcut: 'Esc', run: () => setTool(null) },
    { id: 'measure', label: 'Measure', icon: Ruler, shortcut: 'ME' },
  ],
};

export default core;
