/** Architecture: layered walls and their types, doors and windows, floors, roofs, rooms and their colours. */

import { Armchair, ArrowLeftRight, CornerDownRight, Layers3, Link2, Settings2, Slash, Table2 } from 'lucide-react';
import { AttachRoofIcon, CurtainWallIcon, CurvedWallIcon, DetachRoofIcon, DoorIcon, DormerIcon, RoofIcon, RampIcon, RoofWindowIcon, SlabIcon, SlabOpeningIcon, StairIcon, WallIcon, WallTypesIcon, WindowIcon } from '../../icons';
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
import { setJoinStyle, setWallLine, useWallPrefs, type WallLine } from './prefs';
import { WallLayers } from './WallLayers';
import { WallTypesDialog } from './WallTypesDialog';
import { JoineryDialog } from './JoineryDialog';
import { JoinerySection } from './JoinerySection';
import { SlabSection } from './SlabSection';
import { SlabTypesDialog } from './SlabTypesDialog';
import { RoofSection } from './RoofSection';
import { RoofDialog } from './RoofDialog';
import { roofOpeningGrips, roofOpeningRefs } from './roofOpeningGrips';
import { DownpipeSection, downpipe, downpipeGrips } from './Downpipe';
import { railing, railingGrips } from './Railing';
import { BALCONY, BalconySection, balcony, balconyConfigurator, balconyGrips, balconyOutline, balconySchedule, galleryEdge, stackBalconies, styleOf } from './Balconies';
import { BalconyDialog } from './BalconyDialog';
import { StairSection, type StairNumbers } from './StairSection';
import { StairTypesDialog } from './StairTypesDialog';
import { UnderStairDialog, openUnderStair } from './UnderStairDialog';
import { UnderStairSection } from './UnderStairSection';
import { dirOf, footFromTop } from './stairPlacement';
import { CurtainWallDialog } from './CurtainWallDialog';
import { CurtainWallSection } from './CurtainWallSection';
import { RoomColorsDialog } from './RoomColorsDialog';
import { RoomSection, RoomViewSection } from './RoomSection';
import { roomGrips, roomRefs } from './rooms';
import { newElement, typedBy } from './NewElement';
import { WallThumb, SlabThumb, JoineryThumb } from './Thumbs';
import { isWallOpening, wallOpening, WallOpeningSection } from './WallOpening';
import { wallOpeningGrips } from './wallOpeningGrips';
import { CeilingSection, CeilingTypesDialog, ceiling, ceilingDiffuser, ceilingInRoom, ceilingLight, ceilingTypes } from './Ceilings';
import { RoomFinishesDialog } from './RoomFinishes';
import { finishRoom, roomFinishSchedule, roomFinishes } from './roomFinishTools';
import { WallSweepSection, WallSweepsOnWall, hasSweeps, revealSelected, sweepSelected, wallReveal, wallSweep, wallSweepSchedule } from './WallSweeps';
import { SkirtingSection, SkirtingTypesDialog, skirting, skirtingGrips, skirtingInRoom, skirtingSchedule, skirtingTypes } from './Skirtings';
import { colorLegend, room, roomColors, roomOutline, roomTag, updateRooms } from './roomTools';
import { RoomTagDialog } from './RoomTagDialog';
import { sameKind } from '../../shell/params';

const wall: ToolDef = {
  id: 'architecture.wall', label: 'Wall', icon: WallIcon, shortcut: 'WA', properties: newElement('IfcWallType', 'wallTypeId', 'wallTypes'), typed: typedBy('IfcWallType', 'wallTypeId', 'wallTypes', WallThumb),
  gesture: { kind: 'twoPoints', repeat: true },
  complete: async ({ points: [a, b] }, ctx) => {
    const typeId = useUi.getState().wallTypeId;
    const { joinStyle, line } = useWallPrefs.getState();
    await ctx.execute('architecture.addWall', { storeyId: ctx.storeyId, x1: a.x, y1: a.y, x2: b.x, y2: b.y, joinStyle, ...(line !== 'core' ? { reference: line } : {}), ...(typeId ? { typeId } : {}) });
  },
  options: WallLineOption,
};

/** The line a wall is drawn on, chosen in the command line's bar while the Wall tool is on (decision 237). */
function WallLineOption() {
  const line = useWallPrefs((s) => s.line);
  return (
    <select className={`cad-layer${line !== 'core' ? ' on' : ''}`} value={line} data-testid="wall-line" title="The line you click: the core's centre (the axis), a face of the core, or a finish face; the exterior is the wall's right as you draw"
      onChange={(e) => setWallLine(e.target.value as WallLine)}>
      <option value="core">Core centre</option>
      <option value="coreExterior">Core, exterior face</option>
      <option value="coreInterior">Core, interior face</option>
      <option value="exterior">Exterior face</option>
      <option value="interior">Interior face</option>
    </select>
  );
}

/** A curved wall: its start, its end, then a point the arc passes through. */
const curvedWall: ToolDef = {
  id: 'architecture.curvedWall', label: 'Curved wall', icon: CurvedWallIcon, shortcut: 'WC', properties: newElement('IfcWallType', 'wallTypeId', 'wallTypes'), typed: typedBy('IfcWallType', 'wallTypeId', 'wallTypes', WallThumb),
  gesture: { kind: 'threePoints', arc: true },
  complete: async ({ points: [a, b, p] }, ctx) => {
    const typeId = useUi.getState().wallTypeId;
    await ctx.execute('architecture.addWall', { storeyId: ctx.storeyId, x1: a.x, y1: a.y, x2: b.x, y2: b.y, x3: p.x, y3: p.y, joinStyle: useWallPrefs.getState().joinStyle, ...(typeId ? { typeId } : {}) });
  },
};
/** A curtain wall: two points (chained), of the type picked in the configurator, else the project's first. */
const curtainWall: ToolDef = {
  id: 'architecture.curtainWall', label: 'Curtain wall', icon: CurtainWallIcon, shortcut: 'CW', properties: newElement('IfcCurtainWallType', 'curtainTypeId', 'curtainWalls'), typed: typedBy('IfcCurtainWallType', 'curtainTypeId', 'curtainWalls'),
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
    flipAcross(c, flipped ? 1 : -1, 'Flip: swap the wall\'s faces (its exterior goes to the other side)', () => ({ command: 'architecture.flipWall', args: { ids: sameKind(node) } })),
  ];
}
const pathRefs = (node: NodeDto): RefLine[] => { const c = arcOf(node.props); return c ? [pathRef(c)] : []; };
/** A wall's axis with the way it was drawn and its faces, R and L, as the Wall types dialog's legend shows them (report #79). */
const wallRefs = (node: NodeDto): RefLine[] => pathRefs(node).map((r) => ({ ...r, faces: true }));

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
  // With several selected, an arrow flips them all (#74).
  const hand = () => ({ command: 'architecture.flipJoinery', args: { ids: sameKind(node, ['IfcDoor', 'IfcWindow']), axis: 'x' } });
  const side = () => ({ command: 'architecture.flipJoinery', args: { ids: sameKind(node, ['IfcDoor', 'IfcWindow']), axis: 'y' } });
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

/** An opening: a floor's by its outline; a bare wall opening along its wall and by its jambs, as a window (decision 312). */
function openingGrips(node: NodeDto): Grip[] {
  if (node.props.contour) return contourGrips('architecture.updateSlabOpening')(node);
  if (!isWallOpening(node)) return [];
  const wall = relatingOf(useSession.getState(), node.id, 'IfcRelVoidsElement');
  const c = wall ? arcOf(wall.props) : null;
  return c ? wallOpeningGrips(node.id, c, num(node, 'along'), num(node, 'width')) : [];
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
/**
 * A roof: its outline, and each roof window's and dormer's grips (decisions 251 and 280): the middle moves it, a window's
 * edges and corners and a dormer's sides resize it on its slope (refused where it would not fit); Ctrl/⌘-click, Delete
 * over a grip or its right-click menu takes it out.
 */
const roofGrips = (node: NodeDto): Grip[] => [...contourGrips('architecture.updateRoof', true)(node), ...roofOpeningGrips(node)];
const contourRefs = (node: NodeDto): RefLine[] => { const pts = fromPairs(node.props.contour); return pts.length >= 3 ? [outlineRef(pts)] : []; };

/**
 * The selected walls rise to the underside of the element clicked, from any
 * storey, cut along it: a roof by its slopes, and any other body (a Studio
 * solid, a slab, a beam, an element imported from IFC) by its underside.
 */
const attachRoof: ToolDef = {
  id: 'architecture.attachRoof', label: 'Attach to roof', icon: AttachRoofIcon, shortcut: 'AR',
  gesture: { kind: 'pick', types: ['IfcRoof', 'IfcSlab', 'IfcBeam', 'IfcBuildingElementProxy', 'IfcCovering', 'IfcPlate', 'IfcMember', 'IfcBuildingElementPart', 'IfcStair', 'IfcRamp', 'IfcFooting', 'IfcColumn', 'IfcRailing'] },
  complete: async ({ entity }) => {
    const ids = useUi.getState().selected.filter((id) => useSession.getState().nodes.get(id)?.type === 'IfcWall');
    if (!entity) return;
    if (!ids.length) { notify('Select the walls first, then click the roof, or any body over them, they go up to.'); return; }
    await execute('architecture.attachToRoof', { ids, roofIds: [entity.id] });
    if (useSession.getState().error) return;
    notify(entity.type === 'IfcRoof' ? `${ids.length} wall(s) attached to the roof.` : `${ids.length} wall(s) rise to the underside of ${entity.name}.`);
  },
};
/** The selected walls keep their own height, whatever roof is over them. */
const detachRoof: ToolDef = {
  id: 'architecture.detachRoof', label: 'Detach from roof', icon: DetachRoofIcon,
  run: () => {
    const ids = useUi.getState().selected.filter((id) => useSession.getState().nodes.get(id)?.type === 'IfcWall');
    if (!ids.length) { notify('Select the walls to detach from the roof.'); return; }
    void execute('architecture.detachFromRoof', { ids }).then(() => { if (!useSession.getState().error) notify(`${ids.length} wall(s) keep their own height.`); });
  },
};
const wallTypes: ToolDef = { id: 'architecture.wallTypes', label: 'Wall types', icon: WallTypesIcon, run: () => useUi.setState({ dialog: 'wallTypes' }) };
/** A door or window: click a wall where it goes (the type: the one picked in the configurator, else the project's first). */
const fillTool = (kind: 'door' | 'window', label: string, icon: ToolDef['icon'], shortcut: string): ToolDef => ({
  id: `architecture.${kind}`, label, icon, shortcut,
  properties: kind === 'door' ? newElement('IfcDoorType', 'doorTypeId', 'joinery') : newElement('IfcWindowType', 'windowTypeId', 'joinery'),
  typed: kind === 'door' ? typedBy('IfcDoorType', 'doorTypeId', 'joinery', JoineryThumb) : typedBy('IfcWindowType', 'windowTypeId', 'joinery', JoineryThumb),
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
  id: 'architecture.slab', label: 'Floor', icon: SlabIcon, shortcut: 'SB', properties: newElement('IfcSlabType', 'slabTypeId', 'slabTypes'), typed: typedBy('IfcSlabType', 'slabTypeId', 'slabTypes', SlabThumb),
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
/** The roof configurator, on the roof selected (decision 231). */
const roofConfigurator: ToolDef = {
  id: 'architecture.roofConfigurator', label: 'Roof configurator', icon: Settings2,
  run: () => {
    const { selected } = useUi.getState();
    const roof = selected.map((id) => useSession.getState().nodes.get(id)).find((n) => n?.type === 'IfcRoof');
    if (!roof) { notify('Select a roof first, then open the roof configurator.'); return; }
    useUi.setState({ dialog: 'roof', roofId: roof.id });
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
function stairCommand(library?: string, ramp = false): Step {
  // The plan's storey: the one chosen, else the one a new project's plan shows (the ground floor).
  const storeyId = useUi.getState().storeyId ?? activeStorey()?.id ?? null;
  if (!storeyId) { notify(ramp ? 'Open a plan to place a ramp.' : 'Open a plan to place a stair.'); return { prompt: '', want: 'none' }; }
  // Entrance steps come from the library (the project gets its copy of the type with the first one placed); a stair, from the type picked;
  // a ramp from the ramp type picked, else the library's ramp.
  const typeId = library ? undefined : ramp ? useUi.getState().rampTypeId ?? undefined : useUi.getState().stairTypeId ?? undefined;
  if (ramp && !typeId) library = 'ramp';
  const which = typeId ? { typeId } : library ? { library } : {};
  const byTop = library === 'entrance' || library === 'entrance-ramp';
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
  if (byTop) {
    // By the top: the middle of the porch's edge, then the way down; the foot is a run away.
    const run = () => (plans.left ?? plans.right)?.run ?? 0;
    return {
      prompt: ramp ? 'Top of the ramp: click the middle of the porch edge (or the door sill)' : 'Top of the steps: click the middle of the porch edge (or the door sill)', want: 'point',
      point: (top) => ({
        prompt: ramp ? 'The way down: click on the side the ramp goes out to' : 'The way down: click on the side the steps go out to', want: 'point', base: top,
        preview: (c) => { const { foot, up } = footFromTop(top, c, run()); return footprint('left', foot, up); },
        point: (c) => { const { foot, up } = footFromTop(top, c, run()); return place(foot, up, 'left'); },
      }),
    };
  }
  return {
    prompt: ramp ? 'Foot of the ramp: click the middle of its lower end' : 'Foot of the stair: click the centre of the first riser', want: 'point',
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
registerCommand({ names: ['RAMP', 'RU'], label: 'Ramp', start: () => stairCommand(undefined, true) });
registerCommand({ names: ['ENTRANCERAMP'], label: 'Entrance ramp', start: () => stairCommand('entrance-ramp', true) });
const stair: ToolDef = { id: 'architecture.stair', label: 'Stair', icon: StairIcon, shortcut: 'ST', typed: typedBy('IfcStairType', 'stairTypeId', 'stairTypes'), run: () => void startCommand('STAIR') };
const entranceSteps: ToolDef = { id: 'architecture.entranceSteps', label: 'Entrance steps', icon: StairIcon, run: () => void startCommand('STEPS') };
const ramp: ToolDef = { id: 'architecture.ramp', label: 'Ramp', icon: RampIcon, shortcut: 'RU', typed: typedBy('IfcRampType', 'rampTypeId', 'stairTypes'), run: () => void startCommand('RAMP') };
const entranceRamp: ToolDef = { id: 'architecture.entranceRamp', label: 'Entrance ramp', icon: RampIcon, run: () => void startCommand('ENTRANCERAMP') };
const stairTypes: ToolDef = { id: 'architecture.stairTypes', label: 'Stair types', icon: Settings2, run: () => useUi.setState({ dialog: 'stairTypes' }) };
/** The space under a stair (a tester's report, #71): the configurator on the space or the stair selected. */
const underStair: ToolDef = {
  id: 'architecture.underStair', label: 'Under-stair space', icon: Armchair,
  run: () => {
    const nodes = useSession.getState().nodes;
    const picked = useUi.getState().selected.map((id) => nodes.get(id)).find((n) => n?.type === 'IfcStair' || n?.type === 'LapinUnderStair');
    if (!picked) { notify('Select a stair first, then furnish the space under it.'); return; }
    if (picked.type === 'LapinUnderStair') openUnderStair(String(picked.props.host), picked.id);
    else openUnderStair(picked.id);
  },
};
const underStairSchedule: ToolDef = {
  id: 'architecture.underStairSchedule', label: 'Under-stair schedule', icon: Table2,
  run: () => void execute('architecture.createSchedule', { kind: 'underStairs' }).then((r) => { if (r) notify('Under-stair schedule made: it is in the project browser, under Schedules.'); }),
};
const slabTypes: ToolDef = { id: 'architecture.slabTypes', label: 'Floor types', icon: Layers3, run: () => useUi.setState({ dialog: 'slabTypes' }) };

const architecture: LapinModule = {
  id: 'architecture',
  name: 'Architecture',
  summary: 'Layered walls and their joins, doors and windows, floors, roofs and rooms.',
  tabs: [{
    id: 'architecture', label: 'Architecture', order: 20, include: ['modify'],
    groups: [
      { label: 'Build', tools: [wall, door, window_, slab, roof], small: [curvedWall, curtainWall, wallOpening, slabOpening, roofOpening('window'), roofOpening('dormer'), roofConfigurator, downpipe, railing] },
      { label: 'Types', tools: [wallTypes, slabTypes, joineryTypes], small: [curtainTypes] },
      // Profiles swept along a wall's face: added (a cornice, a band) or taken out (a reveal), decision 297.
      { label: 'Sweeps', tools: [wallSweep, wallReveal], small: [sweepSelected, revealSelected, wallSweepSchedule] },
      { label: 'Joins', tools: [join, mitre, butt, swap, mitreNew], small: [attachRoof, detachRoof] },
      { label: 'Room', tools: [room, roomColors], small: [roomOutline, updateRooms, roomTag] },
      { label: 'Circulation', tools: [stair, ramp, stairTypes], small: [entranceSteps, entranceRamp, underStair, underStairSchedule] },
      // Balconies (a tester's report, #96): by a wall's face in a style, by outline, a gallery's edge; the configurator, stacking, the schedule.
      { label: 'Balconies', tools: [balcony, balconyConfigurator], small: [balconyOutline, galleryEdge, stackBalconies, balconySchedule] },
    ],
  }, {
    // Finishes, after Architecture: what lines a room (its floor, walls and ceiling by a finish of its own, false ceilings, skirtings). Other modules add their groups by this id.
    id: 'finishes', label: 'Finishes', order: 25, include: ['modify'],
    groups: [
      { label: 'Room', tools: [finishRoom], small: [roomFinishes, roomFinishSchedule] },
      { label: 'Ceiling', tools: [ceiling, ceilingInRoom], small: [ceilingLight, ceilingDiffuser, ceilingTypes] },
      { label: 'Skirtings', tools: [skirtingInRoom, skirting], small: [skirtingTypes, skirtingSchedule] },
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
    { id: 'wallSweeps', label: 'Sweeps and reveals', classes: ['IfcWall'], shows: hasSweeps, component: WallSweepsOnWall },
    { id: 'wallSweep', label: 'Wall sweep', classes: ['LapinWallSweep'], component: WallSweepSection },
    { id: 'joinery', label: 'Door or window', classes: ['IfcDoor', 'IfcWindow'], component: JoinerySection },
    { id: 'wallOpening', label: 'Opening', classes: ['IfcOpeningElement'], shows: isWallOpening, component: WallOpeningSection },
    { id: 'slab', label: 'Floor', classes: ['IfcSlab'], component: SlabSection },
    { id: 'downpipe', label: 'Downpipe', classes: ['LapinDownpipe'], component: DownpipeSection },
    { id: 'roof', label: 'Roof', classes: ['IfcRoof'], component: RoofSection },
    { id: 'stair', label: 'Stair', classes: ['IfcStair', 'IfcRamp'], component: StairSection },
    { id: 'balcony', label: 'Balcony', classes: [BALCONY], component: BalconySection },
    { id: 'underStair', label: 'Under-stair space', classes: ['LapinUnderStair'], component: UnderStairSection },
    { id: 'ceiling', label: 'Ceiling', classes: ['LapinCeiling'], component: CeilingSection },
    { id: 'skirting', label: 'Skirting', classes: ['LapinSkirting'], component: SkirtingSection },
    { id: 'curtainWall', label: 'Curtain wall', classes: ['IfcCurtainWall'], component: CurtainWallSection },
    { id: 'room', label: 'Room', classes: ['IfcSpace'], component: RoomSection, replacesFields: () => true },
    { id: 'roomColors', label: 'Room colours', classes: ['LapinView'], component: RoomViewSection },
  ],
  dialogs: { roof: RoofDialog, wallTypes: WallTypesDialog, slabTypes: SlabTypesDialog, joinery: JoineryDialog, stairTypes: StairTypesDialog, ceilingTypes: CeilingTypesDialog, skirtingTypes: SkirtingTypesDialog, roomFinishes: RoomFinishesDialog, curtainWalls: CurtainWallDialog, roomColors: RoomColorsDialog, roomTag: RoomTagDialog, underStair: UnderStairDialog, balcony: BalconyDialog },
  grips: { IfcWall: wallGrips, IfcDoor: joineryGrips, IfcWindow: joineryGrips, IfcCurtainWall: curtainGrips, IfcSlab: contourGrips('architecture.updateSlab'), LapinCeiling: contourGrips('architecture.updateCeiling'), IfcOpeningElement: openingGrips, IfcRoof: roofGrips, IfcSpace: roomGrips, LapinDownpipe: downpipeGrips, LapinRailing: railingGrips, LapinSkirting: skirtingGrips, [BALCONY]: balconyGrips },
  references: { IfcWall: wallRefs, IfcCurtainWall: pathRefs, IfcSlab: contourRefs, IfcOpeningElement: contourRefs, IfcRoof: (n) => [...contourRefs(n), ...roofOpeningRefs(n)], IfcSpace: roomRefs },
  similar: {
    IfcWall: (n) => { useUi.setState({ wallTypeId: typeOf(useSession.getState(), n.id)?.id ?? null }); activate(wall); },
    IfcSlab: (n) => { useUi.setState({ slabTypeId: typeOf(useSession.getState(), n.id)?.id ?? null }); activate(slab); },
    IfcDoor: (n) => { useUi.setState({ doorTypeId: typeOf(useSession.getState(), n.id)?.id ?? null }); activate(door); },
    IfcWindow: (n) => { useUi.setState({ windowTypeId: typeOf(useSession.getState(), n.id)?.id ?? null }); activate(window_); },
    IfcRoof: (n) => { useUi.setState({ roofStyle: typeof n.props.style === 'string' ? n.props.style : null }); activate(roof); },
    IfcCurtainWall: (n) => { useUi.setState({ curtainTypeId: typeOf(useSession.getState(), n.id)?.id ?? null }); activate(curtainWall); },
    IfcSpace: (n) => activate(n.props.auto === false ? roomOutline : room),
    [BALCONY]: (n) => { useUi.setState({ balconyStyle: styleOf(n) || null }); activate(n.props.kind === 'mezzanine' ? galleryEdge : n.props.contour ? balconyOutline : balcony); },
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
