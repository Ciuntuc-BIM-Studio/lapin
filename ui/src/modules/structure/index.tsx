/** Structure: columns, beams, foundations, ring beams and lintels. */

import { Columns3, Minus, RectangleHorizontal, Rows3, SquareStack } from 'lucide-react';
import type { LapinModule } from '../../platform/module';

const structure: LapinModule = {
  id: 'structure',
  name: 'Structure',
  summary: 'Columns, beams, strip and pad foundations, ring beams, lintels.',
  tabs: [{
    id: 'structure', label: 'Structure', order: 20,
    groups: [
      { label: 'Frame', tools: [
        { id: 'column', label: 'Column', icon: Columns3, shortcut: 'CL' },
        { id: 'beam', label: 'Beam', icon: Minus, shortcut: 'BM' },
      ] },
      { label: 'Foundation', tools: [
        { id: 'strip', label: 'Strip footing', icon: Rows3 },
        { id: 'pad', label: 'Pad footing', icon: SquareStack },
      ] },
      { label: 'Masonry', tools: [{ id: 'ringbeam', label: 'Ring beam', icon: RectangleHorizontal }] },
    ],
  }],
  hotbar: [{ id: 'column', label: 'Column', icon: Columns3, shortcut: 'CL' }],
};

export default structure;
