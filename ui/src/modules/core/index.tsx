/** Core: always there — selecting, measuring, the views and how they look. */

import { Box, Eye, Layers3, MousePointer2, Ruler, Scissors, Square } from 'lucide-react';
import type { LapinModule } from '../../platform/module';
import { setTool, useUi } from '../../app/store';

const core: LapinModule = {
  id: 'core',
  name: 'Core',
  summary: 'Project, views, selection and measuring.',
  tabs: [{
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
  hotbar: [
    { id: 'select', label: 'Select', icon: MousePointer2, shortcut: 'Esc', run: () => setTool(null) },
    { id: 'measure', label: 'Measure', icon: Ruler, shortcut: 'ME' },
  ],
};

export default core;
