/** Architecture: layered walls and their types; later doors and windows, slabs, roofs, rooms. */

import { ArrowLeftRight, BrickWall, CornerDownRight, DoorOpen, Home, LayoutGrid, Layers3, Link2, PanelTop, Settings2, Slash, Square, StretchHorizontal } from 'lucide-react';
import type { Grip, LapinModule, ToolDef } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { setJoinStyle, useWallPrefs } from './prefs';
import { WallLayers } from './WallLayers';
import { WallTypesDialog } from './WallTypesDialog';
import { JoineryDialog } from './JoineryDialog';
import { JoinerySection } from './JoinerySection';
import { SlabSection } from './SlabSection';
import { SlabTypesDialog } from './SlabTypesDialog';

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
/** A door or window: click a wall where it goes (the type: the one picked in the configurator, else the project's first). */
const fillTool = (kind: 'door' | 'window', label: string, icon: ToolDef['icon'], shortcut: string): ToolDef => ({
  id: `architecture.${kind}`, label, icon, shortcut,
  gesture: { kind: 'pick', types: ['IfcWall'] },
  complete: async ({ points: [p], entity }, ctx) => {
    if (!entity) return;
    const typeId = kind === 'door' ? useUi.getState().doorTypeId : useUi.getState().windowTypeId;
    await ctx.execute(kind === 'door' ? 'architecture.addDoor' : 'architecture.addWindow', { wallId: entity.id, x: p.x, y: p.y, ...(typeId ? { typeId } : {}) });
  },
});
const door = fillTool('door', 'Door', DoorOpen, 'DR');
const window_ = fillTool('window', 'Window', PanelTop, 'WN');
const joineryTypes: ToolDef = { id: 'architecture.joineryTypes', label: 'Doors & windows', icon: Settings2, run: () => useUi.setState({ dialog: 'joinery' }) };
/** A floor: click its corners, then the first one again (or Enter). */
const slab: ToolDef = {
  id: 'architecture.slab', label: 'Floor', icon: Square, shortcut: 'SB',
  gesture: { kind: 'polygon' },
  complete: async ({ points }, ctx) => {
    const typeId = useUi.getState().slabTypeId;
    await ctx.execute('architecture.addSlab', { storeyId: ctx.storeyId, points: points.map((p) => [p.x, p.y]), ...(typeId ? { typeId } : {}) });
  },
};
const slabTypes: ToolDef = { id: 'architecture.slabTypes', label: 'Floor types', icon: Layers3, run: () => useUi.setState({ dialog: 'slabTypes' }) };

const architecture: LapinModule = {
  id: 'architecture',
  name: 'Architecture',
  summary: 'Layered walls and their joins, doors and windows, floors, roofs and rooms.',
  tabs: [{
    id: 'architecture', label: 'Architecture', order: 10,
    groups: [
      { label: 'Build', tools: [wall, door, window_, slab, { id: 'architecture.roof', label: 'Roof', icon: Home, shortcut: 'RF' }] },
      { label: 'Types', tools: [wallTypes, slabTypes, joineryTypes] },
      { label: 'Joins', tools: [join, mitre, butt, swap, mitreNew] },
      { label: 'Room', tools: [{ id: 'architecture.room', label: 'Room', icon: LayoutGrid, shortcut: 'RM' }] },
      { label: 'Circulation', tools: [{ id: 'architecture.stair', label: 'Stair', icon: StretchHorizontal }] },
    ],
  }],
  hotbar: [wall, door, window_, slab],
  propertySections: [
    { id: 'wall', label: 'Wall', classes: ['IfcWall'], component: WallLayers },
    { id: 'joinery', label: 'Door or window', classes: ['IfcDoor', 'IfcWindow'], component: JoinerySection },
    { id: 'slab', label: 'Floor', classes: ['IfcSlab'], component: SlabSection },
  ],
  dialogs: { wallTypes: WallTypesDialog, slabTypes: SlabTypesDialog, joinery: JoineryDialog },
  grips: { IfcWall: wallGrips },
};

export default architecture;
