/** Architecture: layered walls and their types, doors and windows, floors, roofs; later rooms. */

import { ArrowLeftRight, CornerDownRight, Layers3, Link2, Settings2, Slash } from 'lucide-react';
import { DoorIcon, DormerIcon, RoofIcon, RoofWindowIcon, RoomIcon, SlabIcon, SlabOpeningIcon, StairIcon, WallIcon, WallTypesIcon, WindowIcon } from '../../icons';
import type { Grip, LapinModule, ToolDef } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { typeOf } from '../../engine/model';
import { activate } from '../../platform/modules';
import { setJoinStyle, useWallPrefs } from './prefs';
import { WallLayers } from './WallLayers';
import { WallTypesDialog } from './WallTypesDialog';
import { JoineryDialog } from './JoineryDialog';
import { JoinerySection } from './JoinerySection';
import { SlabSection } from './SlabSection';
import { SlabTypesDialog } from './SlabTypesDialog';
import { RoofSection } from './RoofSection';

const wall: ToolDef = {
  id: 'architecture.wall', label: 'Wall', icon: WallIcon, shortcut: 'WA',
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
const wallTypes: ToolDef = { id: 'architecture.wallTypes', label: 'Wall types', icon: WallTypesIcon, run: () => useUi.setState({ dialog: 'wallTypes' }) };
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
const door = fillTool('door', 'Door', DoorIcon, 'DR');
const window_ = fillTool('window', 'Window', WindowIcon, 'WN');
const joineryTypes: ToolDef = { id: 'architecture.joineryTypes', label: 'Doors & windows', icon: Settings2, run: () => useUi.setState({ dialog: 'joinery' }) };
/** A floor: click its corners, then the first one again (or Enter). */
const slab: ToolDef = {
  id: 'architecture.slab', label: 'Floor', icon: SlabIcon, shortcut: 'SB',
  gesture: { kind: 'polygon' },
  complete: async ({ points }, ctx) => {
    const typeId = useUi.getState().slabTypeId;
    await ctx.execute('architecture.addSlab', { storeyId: ctx.storeyId, points: points.map((p) => [p.x, p.y]), ...(typeId ? { typeId } : {}) });
  },
};
/** A roof: click the wall line's corners, then the first one again (or Enter); it sits on the storey's top, in the style picked. */
const roof: ToolDef = {
  id: 'architecture.roof', label: 'Roof', icon: RoofIcon, shortcut: 'RF',
  gesture: { kind: 'polygon' },
  complete: async ({ points }, ctx) => {
    const style = useUi.getState().roofStyle;
    await ctx.execute('architecture.addRoof', { storeyId: ctx.storeyId, points: points.map((p) => [p.x, p.y]), ...(style ? { style } : {}) });
  },
};
/** An opening through a floor (a stairwell, a shaft): its outline, inside the floor. */
const slabOpening: ToolDef = {
  id: 'architecture.slabOpening', label: 'Floor opening', icon: SlabOpeningIcon, shortcut: 'SH',
  gesture: { kind: 'polygon' },
  complete: async ({ points }, ctx) => {
    await ctx.execute('architecture.addSlabOpening', { storeyId: ctx.storeyId, points: points.map((p) => [p.x, p.y]) });
  },
};
/** A roof window, or a dormer: a click on a slope of a roof (refused where it does not fit). */
const roofOpening = (kind: 'window' | 'dormer'): ToolDef => ({
  id: `architecture.roof${kind === 'window' ? 'Window' : 'Dormer'}`, label: kind === 'window' ? 'Roof window' : 'Dormer', icon: kind === 'window' ? RoofWindowIcon : DormerIcon,
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => { await ctx.execute('architecture.addRoofOpening', { kind, x: p.x, y: p.y }); },
});
const slabTypes: ToolDef = { id: 'architecture.slabTypes', label: 'Floor types', icon: Layers3, run: () => useUi.setState({ dialog: 'slabTypes' }) };

const architecture: LapinModule = {
  id: 'architecture',
  name: 'Architecture',
  summary: 'Layered walls and their joins, doors and windows, floors, roofs and rooms.',
  tabs: [{
    id: 'architecture', label: 'Architecture', order: 10,
    groups: [
      { label: 'Build', tools: [wall, door, window_, slab, roof], small: [slabOpening, roofOpening('window'), roofOpening('dormer')] },
      { label: 'Types', tools: [wallTypes, slabTypes, joineryTypes] },
      { label: 'Joins', tools: [join, mitre, butt, swap, mitreNew] },
      { label: 'Room', tools: [{ id: 'architecture.room', label: 'Room', icon: RoomIcon, shortcut: 'RM' }] },
      { label: 'Circulation', tools: [{ id: 'architecture.stair', label: 'Stair', icon: StairIcon }] },
    ],
  }],
  hotbar: [wall, door, window_, slab, roof],
  propertySections: [
    { id: 'wall', label: 'Wall', classes: ['IfcWall'], component: WallLayers },
    { id: 'joinery', label: 'Door or window', classes: ['IfcDoor', 'IfcWindow'], component: JoinerySection },
    { id: 'slab', label: 'Floor', classes: ['IfcSlab'], component: SlabSection },
    { id: 'roof', label: 'Roof', classes: ['IfcRoof'], component: RoofSection },
  ],
  dialogs: { wallTypes: WallTypesDialog, slabTypes: SlabTypesDialog, joinery: JoineryDialog },
  grips: { IfcWall: wallGrips },
  similar: {
    IfcWall: (n) => { useUi.setState({ wallTypeId: typeOf(useSession.getState(), n.id)?.id ?? null }); activate(wall); },
    IfcSlab: (n) => { useUi.setState({ slabTypeId: typeOf(useSession.getState(), n.id)?.id ?? null }); activate(slab); },
    IfcDoor: (n) => { useUi.setState({ doorTypeId: typeOf(useSession.getState(), n.id)?.id ?? null }); activate(door); },
    IfcWindow: (n) => { useUi.setState({ windowTypeId: typeOf(useSession.getState(), n.id)?.id ?? null }); activate(window_); },
    IfcRoof: (n) => { useUi.setState({ roofStyle: typeof n.props.style === 'string' ? n.props.style : null }); activate(roof); },
  },
  setType: {
    IfcWall: (ids, typeId) => execute('architecture.setWallType', { ids, typeId }),
    IfcSlab: (ids, typeId) => execute('architecture.setSlabType', { ids, typeId }),
    IfcDoor: (ids, typeId) => execute('architecture.setJoineryType', { ids, typeId }),
    IfcWindow: (ids, typeId) => execute('architecture.setJoineryType', { ids, typeId }),
  },
};

export default architecture;
