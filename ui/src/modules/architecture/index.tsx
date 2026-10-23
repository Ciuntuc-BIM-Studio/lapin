/** Architecture: walls, doors and windows, slabs, roofs, rooms. */

import { BrickWall, DoorOpen, Home, LayoutGrid, PanelTop, Square, StretchHorizontal } from 'lucide-react';
import type { LapinModule } from '../../platform/module';
import { toggleTool } from '../../app/store';
import { WallLayers } from './WallLayers';

const wall = { id: 'wall', label: 'Wall', icon: BrickWall, shortcut: 'WA', run: () => toggleTool('wall') };
const door = { id: 'door', label: 'Door', icon: DoorOpen, shortcut: 'DR' };
const window_ = { id: 'window', label: 'Window', icon: PanelTop, shortcut: 'WN' };
const slab = { id: 'slab', label: 'Floor', icon: Square, shortcut: 'SB' };

const architecture: LapinModule = {
  id: 'architecture',
  name: 'Architecture',
  summary: 'Layered walls and their joins, doors and windows, floors, roofs and rooms.',
  tabs: [{
    id: 'architecture', label: 'Architecture', order: 10,
    groups: [
      { label: 'Build', tools: [wall, door, window_, slab, { id: 'roof', label: 'Roof', icon: Home, shortcut: 'RF' }] },
      { label: 'Room', tools: [{ id: 'room', label: 'Room', icon: LayoutGrid, shortcut: 'RM' }] },
      { label: 'Circulation', tools: [{ id: 'stair', label: 'Stair', icon: StretchHorizontal }] },
    ],
  }],
  hotbar: [wall, door, window_, slab],
  propertySections: [{ id: 'wall-layers', label: 'Layers', kinds: ['wall'], component: WallLayers }],
};

export default architecture;
