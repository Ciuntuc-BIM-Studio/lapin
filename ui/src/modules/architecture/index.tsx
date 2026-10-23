/** Architecture: layered walls and their types; later doors and windows, slabs, roofs, rooms. */

import { BrickWall, DoorOpen, Home, LayoutGrid, Layers3, PanelTop, Square, StretchHorizontal } from 'lucide-react';
import type { LapinModule, ToolDef } from '../../platform/module';
import { useUi } from '../../app/store';
import { WallLayers } from './WallLayers';
import { WallTypesDialog } from './WallTypesDialog';

const wall: ToolDef = {
  id: 'architecture.wall', label: 'Wall', icon: BrickWall, shortcut: 'WA',
  gesture: { kind: 'twoPoints', repeat: true },
  complete: async ({ points: [a, b] }, ctx) => {
    const typeId = useUi.getState().wallTypeId;
    await ctx.execute('architecture.addWall', { storeyId: ctx.storeyId, x1: a.x, y1: a.y, x2: b.x, y2: b.y, ...(typeId ? { typeId } : {}) });
  },
};
const wallTypes: ToolDef = { id: 'architecture.wallTypes', label: 'Wall types', icon: Layers3, run: () => useUi.setState({ dialog: 'wallTypes' }) };
const door: ToolDef = { id: 'architecture.door', label: 'Door', icon: DoorOpen, shortcut: 'DR' };
const window_: ToolDef = { id: 'architecture.window', label: 'Window', icon: PanelTop, shortcut: 'WN' };
const slab: ToolDef = { id: 'architecture.slab', label: 'Floor', icon: Square, shortcut: 'SB' };

const architecture: LapinModule = {
  id: 'architecture',
  name: 'Architecture',
  summary: 'Layered walls and their joins, doors and windows, floors, roofs and rooms.',
  tabs: [{
    id: 'architecture', label: 'Architecture', order: 10,
    groups: [
      { label: 'Build', tools: [wall, door, window_, slab, { id: 'architecture.roof', label: 'Roof', icon: Home, shortcut: 'RF' }] },
      { label: 'Types', tools: [wallTypes] },
      { label: 'Room', tools: [{ id: 'architecture.room', label: 'Room', icon: LayoutGrid, shortcut: 'RM' }] },
      { label: 'Circulation', tools: [{ id: 'architecture.stair', label: 'Stair', icon: StretchHorizontal }] },
    ],
  }],
  hotbar: [wall, door, window_, slab],
  propertySections: [{ id: 'wall', label: 'Wall', classes: ['IfcWall'], component: WallLayers }],
  dialogs: { wallTypes: WallTypesDialog },
};

export default architecture;
