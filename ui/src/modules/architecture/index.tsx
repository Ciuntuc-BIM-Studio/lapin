/** Architecture: layered walls and their types, doors and windows, floors, roofs, rooms and their colours. */

import { ArrowLeftRight, CornerDownRight, Layers3, Link2, Settings2, Slash } from 'lucide-react';
import { AttachRoofIcon, CurtainWallIcon, CurvedWallIcon, DetachRoofIcon, DoorIcon, DormerIcon, RoofIcon, RoofWindowIcon, SlabIcon, SlabOpeningIcon, StairIcon, WallIcon, WallTypesIcon, WindowIcon } from '../../icons';
import type { Grip, LapinModule, RefLine, ToolDef, Vec2 } from '../../platform/module';
import { flipAcross, frameAt, outlineGrips, outlineRef, pathGrips, pathRef, toPairs } from '../../viewport/gripKit';
import { arcOf, project } from '../../viewport/arc';
import { fromPairs } from '../../viewport/contour';
import type { NodeDto } from '../../engine/protocol';
import { notify, useUi } from '../../app/store';
import { activeStorey, execute, query, useSession } from '../../engine/session';
import { registerCommand, startCommand, type Step } from '../../viewport/cad/session';
import type { Shape } from '../../viewport/cad/shapes';
import { num, relatingOf, typeOf, usageOf } from '../../engine/model';
import { activate } from '../../platform/modules';
import { setJoinStyle, useWallPrefs } from './prefs';
import { WallLayers } from './WallLayers';
import { WallTypesDialog } from './WallTypesDialog';
import { JoineryDialog } from './JoineryDialog';
import { JoinerySection } from './JoinerySection';
import { SlabSection } from './SlabSection';
import { SlabTypesDialog } from './SlabTypesDialog';
import { RoofSection } from './RoofSection';
import { StairSection, type StairNumbers } from './StairSection';
import { StairTypesDialog } from './StairTypesDialog';
import { dirOf, footFromTop } from './stairPlacement';
import { CurtainWallDialog } from './CurtainWallDialog';
import { CurtainWallSection } from './CurtainWallSection';
import { RoomColorsDialog } from './RoomColorsDialog';
import { RoomSection, RoomViewSection } from './RoomSection';
import { roomGrips, roomRefs } from './rooms';
import { CeilingSection, CeilingTypesDialog, ceiling, ceilingDiffuser, ceilingInRoom, ceilingLight, ceilingTypes } from './Ceilings';
import { colorLegend, room, roomColors, roomOutline, updateRooms } from './roomTools';

const wall: ToolDef = {
  id: 'architecture.wall', label: 'Wall', icon: WallIcon, shortcut: 'WA',
  gesture: { kind: 'twoPoints', repeat: true },
  complete: async ({ points: [a, b] }, ctx) => {
    const typeId = useUi.getState().wallTypeId;
    await ctx.execute('architecture.addWall', { storeyId: ctx.storeyId, x1: a.x, y1: a.y, x2: b.x, y2: b.y, joinStyle: useWallPrefs.getState().joinStyle, ...(typeId ? { typeId } : {}) });
  },
};

/** A curved wall: its start, its end, then a point the arc passes through. */
const curvedWall: ToolDef = {
  id: 'architecture.curvedWall', label: 'Curved wall', icon: CurvedWallIcon, shortcut: 'WC',
  gesture: { kind: 'threePoints', arc: true },
  complete: async ({ points: [a, b, p] }, ctx) => {
    const typeId = useUi.getState().wallTypeId;
    await ctx.execute('architecture.addWall', { storeyId: ctx.storeyId, x1: a.x, y1: a.y, x2: b.x, y2: b.y, x3: p.x, y3: p.y, joinStyle: useWallPrefs.getState().joinStyle, ...(typeId ? { typeId } : {}) });
  },
};
/** A curtain wall: two points (chained), of the type picked in the configurator, else the project's first. */
const curtainWall: ToolDef = {
  id: 'architecture.curtainWall', label: 'Curtain wall', icon: CurtainWallIcon, shortcut: 'CW',
  gesture: { kind: 'twoPoints', repeat: true },
  complete: async ({ points: [a, b] }, ctx) => {
    const typeId = useUi.getState().curtainTypeId;
    await ctx.execute('architecture.addCurtainWall', { storeyId: ctx.storeyId, x1: a.x, y1: a.y, x2: b.x, y2: b.y, ...(typeId ? { typeId } : {}) });
  },
};
const curtainTypes: ToolDef = { id: 'architecture.curtainTypes', label: 'Curtain walls', icon: Settings2, run: () => useUi.setState({ dialog: 'curtainWalls' }) };

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

/**
 * A wall's ends, to drag (a joined corner moves with it), its arc's middle on
 * a curved one, and a flip arrow on its exterior face (its right one, the left
 * once flipped) that swaps its faces.
 */
function wallGrips(node: NodeDto): Grip[] {
  const c = arcOf(node.props);
  if (!c) return [];
  const flipped = usageOf(useSession.getState(), node.id)?.negative ?? false;
  return [
    ...pathGrips(c, (s, e, rise) => ({ command: 'architecture.updateWall', args: { id: node.id, x1: s.x, y1: s.y, x2: e.x, y2: e.y, arc: rise, detachJoinedEnds: true } })),
    flipAcross(c, flipped ? 1 : -1, 'Flip: swap the wall\'s faces (its exterior goes to the other side)', () => ({ command: 'architecture.flipWall', args: { id: node.id } })),
  ];
}
const pathRefs = (node: NodeDto): RefLine[] => { const c = arcOf(node.props); return c ? [pathRef(c)] : []; };

/**
 * A door or window: dragged along its wall by its middle, and four flip
 * arrows round it: along the wall the hand (hinges on the other side), across
 * it the side (opening to the other face).
 */
function joineryGrips(node: NodeDto): Grip[] {
  const s = useSession.getState();
  const opening = relatingOf(s, node.id, 'IfcRelFillsElement');
  const wall = opening ? relatingOf(s, opening.id, 'IfcRelVoidsElement') : null;
  const c = wall ? arcOf(wall.props) : null;
  if (!opening || !c) return [];
  const f = frameAt(c, num(opening, 'along'));
  const hand = () => ({ command: 'architecture.flipJoinery', args: { id: node.id, axis: 'x' } });
  const side = () => ({ command: 'architecture.flipJoinery', args: { id: node.id, axis: 'y' } });
  const arrow = (key: string, d: { x: number; y: number }, title: string, click: () => { command: string; args: Record<string, unknown> }): Grip =>
    ({ key, kind: 'flip', at: f.at, dir: d, title, click });
  return [
    { key: 'along', kind: 'centre', at: f.at, title: 'Drag along the wall', step: (to) => ({ command: 'architecture.moveOpening', args: { id: opening.id, along: project(c, to) } }) },
    arrow('hand+', f.along, 'Flip the hand: hinges on the other side', hand),
    arrow('hand-', { x: -f.along.x, y: -f.along.y }, 'Flip the hand: hinges on the other side', hand),
    arrow('side+', f.left, 'Flip the side: opens to the other face', side),
    arrow('side-', { x: -f.left.x, y: -f.left.y }, 'Flip the side: opens to the other face', side),
  ];
}

/** A curtain wall: its ends, its middle, its arc. */
function curtainGrips(node: NodeDto): Grip[] {
  const c = arcOf(node.props);
  return c ? pathGrips(c, (s, e, rise) => ({ command: 'architecture.updateCurtainWall', args: { id: node.id, x1: s.x, y1: s.y, x2: e.x, y2: e.y, arc: rise } })) : [];
}
/** A slab, floor opening or roof: its outline's vertices and edges (a roof's edge rules follow its edges). */
function contourGrips(command: string, withEdges = false) {
  return (node: NodeDto): Grip[] => {
    const pts = fromPairs(node.props.contour);
    if (pts.length < 3) return [];
    const edges = withEdges && Array.isArray(node.props.edges) && node.props.edges.length === pts.length ? (node.props.edges as unknown[]) : undefined;
    return outlineGrips({ pts, edges }, (o) => ({ command, args: { id: node.id, points: toPairs(o.pts), ...(o.edges ? { edges: o.edges } : {}) } }));
  };
}
const contourRefs = (node: NodeDto): RefLine[] => { const pts = fromPairs(node.props.contour); return pts.length >= 3 ? [outlineRef(pts)] : []; };

/**
 * The selected walls rise to the underside of the element clicked, from any
 * storey, cut along it: a roof by its slopes, and any other body (a Studio
 * solid, a slab, a beam, an element imported from IFC) by its underside.
 */
const attachRoof: ToolDef = {
  id: 'architecture.attachRoof', label: 'Attach to roof', icon: AttachRoofIcon, shortcut: 'AR',
  gesture: { kind: 'pick', types: ['IfcRoof', 'IfcSlab', 'IfcBeam', 'IfcBuildingElementProxy', 'IfcCovering', 'IfcPlate', 'IfcMember', 'IfcBuildingElementPart', 'IfcStair', 'IfcFooting', 'IfcColumn', 'IfcRailing'] },
  complete: async ({ entity }) => {
    const ids = useUi.getState().selected.filter((id) => useSession.getState().nodes.get(id)?.type === 'IfcWall');
    if (!entity) return;
    if (!ids.length) { notify('Select the walls first, then click the roof, or any body over them, they go up to.'); return; }
    await execute('architecture.attachToRoof', { ids, roofIds: [entity.id] });
    notify(entity.type === 'IfcRoof' ? `${ids.length} wall(s) attached to the roof.` : `${ids.length} wall(s) rise to the underside of ${entity.name}.`);
  },
};
/** The selected walls keep their own height, whatever roof is over them. */
const detachRoof: ToolDef = {
  id: 'architecture.detachRoof', label: 'Detach from roof', icon: DetachRoofIcon,
  run: () => {
    const ids = useUi.getState().selected.filter((id) => useSession.getState().nodes.get(id)?.type === 'IfcWall');
    if (!ids.length) { notify('Select the walls to detach from the roof.'); return; }
    void execute('architecture.detachFromRoof', { ids }).then(() => notify(`${ids.length} wall(s) keep their own height.`));
  },
};
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
/**
 * STAIR: the foot (the centre of the first riser), the direction of the first
 * flight, and, for an L or a U, which way it turns; the type the configurator
 * picked (else the project's first). The footprint the engine works out
 * follows the cursor.
 */
function stairCommand(library?: string): Step {
  // The plan's storey: the one chosen, else the one a new project's plan shows (the ground floor).
  const storeyId = useUi.getState().storeyId ?? activeStorey()?.id ?? null;
  if (!storeyId) { notify('Open a plan to place a stair.'); return { prompt: '', want: 'none' }; }
  // Entrance steps come from the library (the project gets its copy of the type with the first one placed); a stair, from the type picked.
  const typeId = library ? undefined : useUi.getState().stairTypeId ?? undefined;
  const which = library ? { library } : typeId ? { typeId } : {};
  const plans: Record<string, StairNumbers | null> = { left: null, right: null };
  for (const turn of ['left', 'right']) void query<StairNumbers>('architecture.stairPlan', { storeyId, ...which, turn }).then((p) => { plans[turn] = p; });
  const footprint = (turn: string, at: Vec2, dir: Vec2): Shape[] => {
    const p = plans[turn] ?? plans.left;
    if (!p) return [];
    const px = (x: number, y: number): Vec2 => ({ x: at.x + dir.x * x - dir.y * y, y: at.y + dir.y * x + dir.x * y });
    const shapes: Shape[] = p.outlines.map((o) => ({ type: 'polyline', pts: Array.from({ length: o.points.length / 2 }, (_, i) => px(o.points[2 * i], o.points[2 * i + 1])), closed: true }));
    for (const l of p.risersLines) shapes.push({ type: 'line', a: px(l[0], l[1]), b: px(l[2], l[3]) });
    return shapes;
  };
  const place = async (at: Vec2, dir: Vec2, turn: string) => {
    const r = await execute('architecture.addStair', { storeyId, ...which, x: at.x, y: at.y, x2: at.x + dir.x, y2: at.y + dir.y, turn }) as { id?: string } | null;
    if (r?.id) useUi.setState({ selection: r.id, selected: [r.id] });
    return null;
  };
  if (library) {
    // By the top: the middle of the porch's edge, then the way down; the foot is a run away.
    const run = () => (plans.left ?? plans.right)?.run ?? 0;
    return {
      prompt: 'Top of the steps: click the middle of the porch edge (or the door sill)', want: 'point',
      point: (top) => ({
        prompt: 'The way down: click on the side the steps go out to', want: 'point', base: top,
        preview: (c) => { const { foot, up } = footFromTop(top, c, run()); return footprint('left', foot, up); },
        point: (c) => { const { foot, up } = footFromTop(top, c, run()); return place(foot, up, 'left'); },
      }),
    };
  }
  return {
    prompt: 'Foot of the stair: click the centre of the first riser', want: 'point',
    point: (a) => ({
      prompt: 'Direction of the first flight', want: 'point', base: a,
      preview: (c) => footprint('left', a, dirOf(a, c)),
      point: (b) => {
        const dir = dirOf(a, b);
        const turns = (plans.left ?? plans.right)?.shape === 'l' || (plans.left ?? plans.right)?.shape === 'u';
        if (!turns) return place(a, dir, 'left');
        const side = (c: Vec2) => ((c.x - a.x) * dir.y - (c.y - a.y) * dir.x) > 0 ? 'right' : 'left';
        return {
          prompt: 'Which way does it turn? Click left or right of the flight', want: 'point', base: a,
          preview: (c) => footprint(side(c), a, dir),
          point: (c) => place(a, dir, side(c)),
        };
      },
    }),
  };
}
registerCommand({ names: ['STAIR', 'ST'], label: 'Stair', start: () => stairCommand() });
registerCommand({ names: ['STEPS'], label: 'Entrance steps', start: () => stairCommand('entrance') });
const stair: ToolDef = { id: 'architecture.stair', label: 'Stair', icon: StairIcon, shortcut: 'ST', run: () => void startCommand('STAIR') };
const entranceSteps: ToolDef = { id: 'architecture.entranceSteps', label: 'Entrance steps', icon: StairIcon, run: () => void startCommand('STEPS') };
const stairTypes: ToolDef = { id: 'architecture.stairTypes', label: 'Stair types', icon: Settings2, run: () => useUi.setState({ dialog: 'stairTypes' }) };
const slabTypes: ToolDef = { id: 'architecture.slabTypes', label: 'Floor types', icon: Layers3, run: () => useUi.setState({ dialog: 'slabTypes' }) };

const architecture: LapinModule = {
  id: 'architecture',
  name: 'Architecture',
  summary: 'Layered walls and their joins, doors and windows, floors, roofs and rooms.',
  tabs: [{
    id: 'architecture', label: 'Architecture', order: 20, include: ['modify'],
    groups: [
      { label: 'Build', tools: [wall, door, window_, slab, roof], small: [curvedWall, curtainWall, slabOpening, roofOpening('window'), roofOpening('dormer')] },
      { label: 'Types', tools: [wallTypes, slabTypes, joineryTypes], small: [curtainTypes] },
      { label: 'Joins', tools: [join, mitre, butt, swap, mitreNew], small: [attachRoof, detachRoof] },
      { label: 'Room', tools: [room, roomColors], small: [roomOutline, updateRooms] },
      { label: 'Circulation', tools: [stair, stairTypes], small: [entranceSteps] },
      { label: 'Ceiling', tools: [ceiling, ceilingInRoom], small: [ceilingLight, ceilingDiffuser, ceilingTypes] },
    ],
  }, {
    // Into View: the room colours by the other graphics.
    id: 'view', label: 'View', order: 90,
    groups: [{ label: 'Rooms', tools: [roomColors] }],
  }, {
    // Into Annotate: the colour legend of a plan's rooms.
    id: 'annotate', label: 'Annotate', order: 60,
    groups: [{ label: 'Rooms', tools: [colorLegend], small: [roomColors] }],
  }],
  hotbar: [wall, door, window_, slab, roof],
  propertySections: [
    { id: 'wall', label: 'Wall', classes: ['IfcWall'], component: WallLayers },
    { id: 'joinery', label: 'Door or window', classes: ['IfcDoor', 'IfcWindow'], component: JoinerySection },
    { id: 'slab', label: 'Floor', classes: ['IfcSlab'], component: SlabSection },
    { id: 'roof', label: 'Roof', classes: ['IfcRoof'], component: RoofSection },
    { id: 'stair', label: 'Stair', classes: ['IfcStair'], component: StairSection },
    { id: 'ceiling', label: 'Ceiling', classes: ['LapinCeiling'], component: CeilingSection },
    { id: 'curtainWall', label: 'Curtain wall', classes: ['IfcCurtainWall'], component: CurtainWallSection },
    { id: 'room', label: 'Room', classes: ['IfcSpace'], component: RoomSection, replacesFields: () => true },
    { id: 'roomColors', label: 'Room colours', classes: ['LapinView'], component: RoomViewSection },
  ],
  dialogs: { wallTypes: WallTypesDialog, slabTypes: SlabTypesDialog, joinery: JoineryDialog, stairTypes: StairTypesDialog, ceilingTypes: CeilingTypesDialog, curtainWalls: CurtainWallDialog, roomColors: RoomColorsDialog },
  grips: { IfcWall: wallGrips, IfcDoor: joineryGrips, IfcWindow: joineryGrips, IfcCurtainWall: curtainGrips, IfcSlab: contourGrips('architecture.updateSlab'), LapinCeiling: contourGrips('architecture.updateCeiling'), IfcOpeningElement: (n) => (n.props.contour ? contourGrips('architecture.updateSlabOpening')(n) : []), IfcRoof: contourGrips('architecture.updateRoof', true), IfcSpace: roomGrips },
  references: { IfcWall: pathRefs, IfcCurtainWall: pathRefs, IfcSlab: contourRefs, IfcOpeningElement: contourRefs, IfcRoof: contourRefs, IfcSpace: roomRefs },
  similar: {
    IfcWall: (n) => { useUi.setState({ wallTypeId: typeOf(useSession.getState(), n.id)?.id ?? null }); activate(wall); },
    IfcSlab: (n) => { useUi.setState({ slabTypeId: typeOf(useSession.getState(), n.id)?.id ?? null }); activate(slab); },
    IfcDoor: (n) => { useUi.setState({ doorTypeId: typeOf(useSession.getState(), n.id)?.id ?? null }); activate(door); },
    IfcWindow: (n) => { useUi.setState({ windowTypeId: typeOf(useSession.getState(), n.id)?.id ?? null }); activate(window_); },
    IfcRoof: (n) => { useUi.setState({ roofStyle: typeof n.props.style === 'string' ? n.props.style : null }); activate(roof); },
    IfcCurtainWall: (n) => { useUi.setState({ curtainTypeId: typeOf(useSession.getState(), n.id)?.id ?? null }); activate(curtainWall); },
    IfcSpace: (n) => activate(n.props.auto === false ? roomOutline : room),
  },
  setType: {
    IfcWall: (ids, typeId) => execute('architecture.setWallType', { ids, typeId }),
    IfcSlab: (ids, typeId) => execute('architecture.setSlabType', { ids, typeId }),
    IfcDoor: (ids, typeId) => execute('architecture.setJoineryType', { ids, typeId }),
    IfcWindow: (ids, typeId) => execute('architecture.setJoineryType', { ids, typeId }),
    IfcCurtainWall: (ids, typeId) => execute('architecture.setCurtainWallType', { ids, typeId }),
  },
};

export default architecture;
