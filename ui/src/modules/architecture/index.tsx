/** Architecture: layered walls and their types; later doors and windows, slabs, roofs, rooms. */

import { ArrowLeftRight, BrickWall, CornerDownRight, DoorOpen, Home, LayoutGrid, Layers3, Link2, PanelTop, Slash, Square, StretchHorizontal } from 'lucide-react';
import type { Grip, LapinModule, ToolDef } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { setJoinStyle, useWallPrefs } from './prefs';
import { WallLayers } from './WallLayers';
import { WallTypesDialog } from './WallTypesDialog';

const wall: ToolDef = {
  id: 'architecture.wall', label: 'Wall', icon: BrickWall, shortcut: 'WA',
  gesture: { kind: 'twoPoints', repeat: true },
  complete: async ({ points: [a, b] }, ctx) => {
    const typeId = useUi.getState().wallTypeId;
    await ctx.execute('architecture.addWall', { storeyId: ctx.storeyId, x1: a.x, y1: a.y, x2: b.x, y2: b.y, joinStyle: useWallPrefs.getState().joinStyle, ...(typeId ? { typeId } : {}) });
  },
};

/** The selected walls. */
const selectedWalls = () => {
  const nodes = useSession.getState().nodes;
  return useUi.getState().selected.filter((id) => nodes.get(id)?.type === 'IfcWall');
};

const join: ToolDef = {
  id: 'architecture.join', label: 'Join', icon: Link2, shortcut: 'JW',
  run: () => {
    const walls = selectedWalls();
    if (walls.length !== 2) { notify('Select the two walls to join (Shift-click the second).'); return; }
    void execute('architecture.joinWalls', { a: walls[0], b: walls[1], style: useWallPrefs.getState().joinStyle });
  },
};
const restyle = (label: string, id: string, icon: ToolDef['icon'], args: Record<string, unknown>): ToolDef => ({
  id, label, icon,
  run: () => {
    const ids = selectedWalls();
    if (!ids.length) { notify('Select the walls whose corners to change.'); return; }
    void execute('architecture.restyleJoins', { ids, ...args });
  },
});
const mitre = restyle('Mitre', 'architecture.mitre', Slash, { style: 'mitre' });
const butt = restyle('Butt', 'architecture.butt', CornerDownRight, { style: 'butt' });
const swap = restyle('Swap', 'architecture.swap', ArrowLeftRight, { swap: true });
const mitreNew: ToolDef = {
  id: 'architecture.mitreNew', label: 'Mitre new corners', icon: Slash,
  run: () => {
    const next = useWallPrefs.getState().joinStyle === 'mitre' ? 'butt' : 'mitre';
    setJoinStyle(next);
    notify(next === 'mitre' ? 'New corners are mitred.' : 'New corners are butt: one wall runs through.');
  },
};

/** A wall's ends, to drag: a joined corner moves with it. */
function wallGrips(node: NodeDto): Grip[] {
  const a = node.props.start as number[] | undefined, b = node.props.end as number[] | undefined;
  if (!a || !b) return [];
  return [
    { key: `${node.id}:start`, at: { x: a[0], y: a[1] }, move: (p) => execute('architecture.updateWall', { id: node.id, x1: p.x, y1: p.y }) },
    { key: `${node.id}:end`, at: { x: b[0], y: b[1] }, move: (p) => execute('architecture.updateWall', { id: node.id, x2: p.x, y2: p.y }) },
  ];
}
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
      { label: 'Joins', tools: [join, mitre, butt, swap, mitreNew] },
      { label: 'Room', tools: [{ id: 'architecture.room', label: 'Room', icon: LayoutGrid, shortcut: 'RM' }] },
      { label: 'Circulation', tools: [{ id: 'architecture.stair', label: 'Stair', icon: StretchHorizontal }] },
    ],
  }],
  hotbar: [wall, door, window_, slab],
  propertySections: [{ id: 'wall', label: 'Wall', classes: ['IfcWall'], component: WallLayers }],
  dialogs: { wallTypes: WallTypesDialog },
  grips: { IfcWall: wallGrips },
};

export default architecture;
