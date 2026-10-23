/** Drafting: dimensions, text, grid axes, levels, hatches, sheets. */

import { FileText, Grid3x3, Hash, MoveHorizontal, Type, TrendingUp } from 'lucide-react';
import type { LapinModule } from '../../platform/module';

const drafting: LapinModule = {
  id: 'drafting',
  name: 'Drafting',
  summary: 'Annotations, grid axes, levels, hatches and sheets.',
  tabs: [{
    id: 'annotate', label: 'Annotate', order: 30,
    groups: [
      { label: 'Dimension', tools: [{ id: 'dim', label: 'Aligned', icon: MoveHorizontal, shortcut: 'DI' }] },
      { label: 'Text', tools: [{ id: 'text', label: 'Text', icon: Type, shortcut: 'TX' }] },
      { label: 'Datum', tools: [
        { id: 'axis', label: 'Grid axis', icon: Grid3x3, shortcut: 'GR' },
        { id: 'level', label: 'Level', icon: TrendingUp, shortcut: 'LL' },
      ] },
      { label: 'Detail', tools: [{ id: 'hatch', label: 'Hatch', icon: Hash }] },
      { label: 'Sheets', tools: [{ id: 'sheet', label: 'Sheet', icon: FileText }] },
    ],
  }],
  hotbar: [{ id: 'dim', label: 'Dimension', icon: MoveHorizontal, shortcut: 'DI' }],
};

export default drafting;
