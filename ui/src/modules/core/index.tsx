/** Core: always there — selecting, measuring, the views and how they look, materials and drafting standards. */

import { Box, Eye, Hash, Layers3, MousePointer2, Palette, Ruler, Scissors, Square, SquareStack } from 'lucide-react';
import { execute } from '../../engine/session';
import type { LapinModule } from '../../platform/module';
import { setTool, useUi } from '../../app/store';
import { MaterialsDialog } from './MaterialsDialog';
import { StandardsDialog } from './StandardsDialog';
import { SheetSection } from './SheetSection';

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
        { id: 'view.section', label: 'Section', icon: Scissors, shortcut: 'SE',
          gesture: { kind: 'twoPoints' },
          complete: async ({ points: [a, b] }, ctx) => {
            const r = await ctx.execute('view.create', { kind: 'section', start: [a.x, a.y], end: [b.x, b.y] }) as { id?: string } | null;
            if (r?.id) useUi.setState({ view: 'drawing', drawingViewId: r.id, tool: null });
          } },
        { id: 'view.elevations', label: 'Elevations', icon: SquareStack, run: () => void (async () => {
          for (const direction of ['south', 'east', 'north', 'west']) await execute('view.create', { kind: 'elevation', direction });
        })() },
      ] },
      { label: 'Graphics', tools: [
        { id: 'view.style', label: 'Visual style', icon: Eye },
        { id: 'view.layers', label: 'Visibility', icon: Layers3 },
      ] },
    ],
  }],
  dialogs: { materials: MaterialsDialog, standards: StandardsDialog },
  propertySections: [{ id: 'titleBlock', label: 'Title block', classes: ['LapinSheet'], component: SheetSection }],
  hotbar: [
    { id: 'select', label: 'Select', icon: MousePointer2, shortcut: 'Esc', run: () => setTool(null) },
    { id: 'measure', label: 'Measure', icon: Ruler, shortcut: 'ME' },
  ],
};

export default core;
