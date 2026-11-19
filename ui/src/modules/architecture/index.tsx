/** Architecture: layered walls and their types, doors and windows, floors, roofs, rooms and their colours. */

import { Armchair, PenTool, ArrowLeftRight, ArrowRightToLine, CornerDownRight, CornerUpRight, Layers3, Link2, Scissors, Settings2, Slash, Spline, Table2 } from 'lucide-react';
import { AttachRoofIcon, CurtainWallIcon, CurvedWallIcon, DetachRoofIcon, DoorIcon, DormerIcon, RoofIcon, RampIcon, RoofWindowIcon, SlabIcon, SlabOpeningIcon, StairIcon, WallIcon, WallTypesIcon, WindowIcon } from '../../icons';
import type { Grip, GripView, LapinModule, RefLine, ToolDef, Vec2 } from '../../platform/module';
import { flipAcross, frameAt, holeGrips, holeRefs, outlineGrips, outlineRef, pathGrips, pathRef, toPairs, withEllipses } from '../../viewport/gripKit';
import { architectureGrips3d } from './grips3d';
import { arcOf, project, type Arc } from '../../viewport/arc';
import { arcsOf, fromPairs } from '../../viewport/contour';
import { FloorShapeBar, EllipseShapeBar, loopOf, toggleOutlineArcs, type EllipseEdgeJson } from '../../viewport/outlineShape';
import type { NodeDto } from '../../engine/protocol';
import { notify, useUi } from '../../app/store';
import { createScheduleOf } from '../../shell/fieldSchedule';
import { activeStorey, execute, query, useSession } from '../../engine/session';
import { registerCommand, startCommand, type Step } from '../../viewport/cad/session';
import type { Shape } from '../../viewport/cad/shapes';
import { num, ofClass, relatingOf, typeLayers, typeOf, usageOf } from '../../engine/model';
import { fillFlips, fillGhost, flipFillHand, useFillHand, type PanelLike } from './fillPlacing';
import { activate, useToolTick } from '../../platform/modules';
import { gateGroup } from './gates/tools';
import { GateSection } from './gates/GateSection';
import { CLASS as GATE_CLASS, TYPE_CLASS as GATE_TYPE, gateGrips, overallOf, useGateTypesDialog } from './gates/gates';
import { setJoinStyle, setRememberLine, setWallLine, setWallLineOffset, useWallPrefs, wallLineArgs, type WallLine } from './prefs';
import { LengthField } from '../../shell/cells';
import { axisFromReference, referenceArc } from '../../viewport/wallReference';
import { WallLayers } from './WallLayers';
import { mirrorLine, wallGhost } from './wallLine';
import { newWallSection } from './newWallSection';
import { WallDims } from './WallTempDims';
import { DimRefChoice } from '../../viewport/LineTempDims';
import { WallTopOption, wallTopArgs } from './WallTop';
import { JoinerySection } from './JoinerySection';
import { SlabSection } from './SlabSection';
import { LayerExtentsSection, hasLayerExtents } from './LayerExtentsSection';
import { editLayers, editingLayersOf, layerGrips } from './layerEdit';
import { LayerEditDrawing, LayerEditPlan, LayerEditWatcher } from './LayerEditOverlays';
import { RoofSection } from './RoofSection';
import { roofSlopeGrips } from './roofEdges';
import { roofOpeningGrips, roofOpeningRefs } from './roofOpeningGrips';
import { DownpipeSection, downpipe, downpipeGrips } from './Downpipe';
import { railing, railingGrips, railingRefs } from './Railing';
import { BALCONY, BalconySection, balcony, balconyConfigurator, balconyGrips, balconyOutline, balconySchedule, galleryEdge, stackBalconies, styleOf } from './Balconies';
import { StairSection, type StairNumbers } from './StairSection';
import { UnderStairDialog, openUnderStair } from './UnderStairDialog';
import { UnderStairSection } from './UnderStairSection';
import { dirOf, footFromTop } from './stairPlacement';
import { CurtainWallSection } from './CurtainWallSection';
import { RoomSection, RoomViewSection } from './RoomSection';
import { roomGrips, roomRefs } from './rooms';
import { newElement, typedBy } from './NewElement';
import { WallThumb, SlabThumb, JoineryThumb } from './Thumbs';
import { isWallOpening, wallOpening, WallOpeningSection } from './WallOpening';
import { openingTool } from './openingTool';
import { OpeningDims, OpeningDimsInView } from './TempDimensions';
import { joineryJambGrips, wallOpeningGrips } from './wallOpeningGrips';
import { joinerySize } from './joinerySize';
import { CeilingSection, CeilingTypesDialog, ceiling, ceilingDiffuser, ceilingInRoom, ceilingLight, ceilingTypes } from './Ceilings';
import { finishRoom, roomFinishSchedule, roomFinishes } from './roomFinishTools';
import { finishPainter, paintBrush, paintClear, paintEyedropper, splitLine, splitRect, surfaceFinishSchedule } from './finishPainter';
import { FinishPainterPanel } from './FinishPainterPanel';
import { hasPaintedFinishes, PaintedFinishesSection } from './PaintedFinishesSection';
import { WallSweepSection, WallSweepsOnWall, hasSweeps, revealSelected, sweepSelected, wallReveal, wallSweep, wallSweepSchedule } from './WallSweeps';
import { SkirtingSection, SkirtingTypesDialog, skirting, skirtingGrips, skirtingInRoom, skirtingSchedule, skirtingTypes } from './Skirtings';
import { colorLegend, room, roomColors, roomDivider, roomDividerArc, roomDividerCircle, roomDividerPolyline, roomDividerRectangle, roomDividers, roomOutline, roomTag, updateRooms } from './roomTools';
import { sameKind } from '../../shell/params';
import { withAutoJoinMenus } from '../core/autoJoin';
import { arcAlias, chainTool, setChainMode } from '../core/chainTool';
import { SHADE, SHADE_TYPE, editShadeType, SunshadeSection, SunshadeTypesDialog, sunshade, sunshadeAlong, sunshadeGrips, sunshadeSchedule, sunshadeTypes } from './Sunshades';
import { lazyPart } from '../../platform/lazyPart';
import { StairRailingPanel, railingPick } from './StairRailingPanel';
import './joineryBoard';
import { editStairSketch, newStairSketch, sketchStairs } from './stairSketchBoard';
const FieldScheduleDialog = lazyPart(() => import('./FieldScheduleDialog').then((m) => m.FieldScheduleDialog), { title: 'Field schedule' });
const FieldTemplatesDialog = lazyPart(() => import('./FieldTemplatesDialog').then((m) => m.FieldTemplatesDialog), { title: 'New schedule from a template' });

// Fetched the first time each is opened (platform/lazyPart.tsx).
const CurtainMembersDialog = lazyPart(() => import('./CurtainMembersDialog').then((m) => m.CurtainMembersDialog), { title: 'Curtain wall members' });
const CurtainCellsDialog = lazyPart(() => import('./CurtainCellsDialog').then((m) => m.CurtainCellsDialog), { title: 'Curtain wall panels and doors' });
const WallProfileDialog = lazyPart(() => import('./WallProfileDialog').then((m) => m.WallProfileDialog), { title: 'Wall profile' });
const GateTypesDialog = lazyPart(() => import('./gates/GateTypesDialog').then((m) => m.GateTypesDialog), { title: 'Gate types' });
const WallTypesDialog = lazyPart(() => import('./WallTypesDialog').then((m) => m.WallTypesDialog), { title: 'Wall types' });
const JoineryDialog = lazyPart(() => import('./JoineryDialog').then((m) => m.JoineryDialog), { title: 'Doors and windows' });
const SlabTypesDialog = lazyPart(() => import('./SlabTypesDialog').then((m) => m.SlabTypesDialog), { title: 'Floor types' });
const RoofDialog = lazyPart(() => import('./RoofDialog').then((m) => m.RoofDialog), { title: 'Roof' });
const BalconyDialog = lazyPart(() => import('./BalconyDialog').then((m) => m.BalconyDialog), { title: 'Balcony' });
const StairTypesDialog = lazyPart(() => import('./StairTypesDialog').then((m) => m.StairTypesDialog), { title: 'Stair types' });
const CurtainWallDialog = lazyPart(() => import('./CurtainWallDialog').then((m) => m.CurtainWallDialog), { title: 'Curtain walls' });
const RoomColorsDialog = lazyPart(() => import('./RoomColorsDialog').then((m) => m.RoomColorsDialog), { title: 'Room colours' });
const OpeningDialog = lazyPart(() => import('./OpeningDialog').then((m) => m.OpeningDialog), { title: 'Wall opening' });
const RoomFinishesDialog = lazyPart(() => import('./RoomFinishes').then((m) => m.RoomFinishesDialog), { title: 'Room finishes' });
const RoomTagDialog = lazyPart(() => import('./RoomTagDialog').then((m) => m.RoomTagDialog), { title: 'Room tag' });
const WallFromSolidsDialog = lazyPart(() => import('./WallFromSolidsDialog').then((m) => m.WallFromSolidsDialog), { title: 'Make a layered wall from solids' });

/**
 * Make a layered wall from solids (forum topic 101): the selected solids, one per layer, made one wall of a type with
 * those layers (architecture.wallFromSolids, its preview first). WFS on the command line; the right-click offers it too.
 */
const wallFromSolids: ToolDef = {
  id: 'architecture.wallFromSolids', label: 'Wall from solids', icon: Layers3, shortcut: 'WFS',
  run: () => {
    if (!useUi.getState().selected.length) { notify('Select the solids that make the wall (its layers), then Wall from solids.'); return; }
    useUi.setState({ dialog: 'wallFromSolids' });
  },
};

/** A wall made of solids it keeps hidden (forum topic 101): how many, and Show the original solids. */
function OriginalSolidsSection({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const count = [...nodes.values()].filter((n) => n.props.supersededBy === node.id).length;
  if (!count) return null;
  return (
    <div className="field-line">
      <span className="muted small"><span>{count}</span> <span>solid(s) it was made of, kept hidden</span></span>
      <button type="button" className="btn small" onClick={() => void execute('architecture.showOriginalSolids', { id: node.id })}>Show the original solids</button>
    </div>
  );
}
const hidesSolids = (node: NodeDto) => [...useSession.getState().nodes.values()].some((n) => n.props.supersededBy === node.id);

const wall: ToolDef = chainTool({
  id: 'architecture.wall', label: 'Wall', icon: WallIcon, shortcut: 'WA', properties: newElement('IfcWallType', 'wallTypeId', 'wallTypes'), typed: typedBy('IfcWallType', 'wallTypeId', 'wallTypes', WallThumb),
  line: async (a, b, ctx) => {
    const typeId = useUi.getState().wallTypeId;
    const prefs = useWallPrefs.getState();
    await ctx.execute('architecture.addWall', { storeyId: ctx.storeyId, x1: a.x, y1: a.y, x2: b.x, y2: b.y, joinStyle: prefs.joinStyle, ...wallLineArgs(prefs), ...(typeId ? { typeId } : {}), ...wallTopArgs() });
  },
  // An arc is drawn on the chosen line too (GitHub #63): its axis concentric with it.
  arc: async (a, b, p, ctx) => {
    const typeId = useUi.getState().wallTypeId;
    const prefs = useWallPrefs.getState();
    await ctx.execute('architecture.addWall', { storeyId: ctx.storeyId, x1: a.x, y1: a.y, x2: b.x, y2: b.y, x3: p.x, y3: p.y, joinStyle: prefs.joinStyle, ...wallLineArgs(prefs), ...(typeId ? { typeId } : {}), ...wallTopArgs() });
  },
  more: () => <><WallLineOption /><WallTopOption /><DimRefChoice /></>,
  // The wall's body beside the line drawn, on the side it will be built (decision 692).
  body: (path) => { const { bands, left } = newWallSection(); return wallGhost(path, bands, left); },
  // Space: the line on the other side (Revit's Spacebar), before the first click or between clicks.
  space: flipWallLine,
  spaceHint: () => {
    const { line, lineOffset } = useWallPrefs.getState();
    const next = mirrorLine(line, lineOffset);
    return next ? `the other side (${LINE_NAMES[next.line]})` : null;
  },
});

/** The lines' names, as the bar's list writes them. */
const LINE_NAMES: Record<WallLine, string> = { core: 'Core centre', coreExterior: 'Core, exterior face', coreInterior: 'Core, interior face', exterior: 'Exterior face', interior: 'Interior face', offset: 'Offset from the core centre' };

/** Space while the Wall tool is on (decision 692): the line drawn on goes to the other side of the wall; the core's centre has none. */
function flipWallLine(): void {
  const { line, lineOffset } = useWallPrefs.getState();
  const next = mirrorLine(line, lineOffset);
  if (!next) { notify('The core centre has no other side: choose a face or an offset to flip with Space, or flip the wall afterwards with its arrow.'); return; }
  if (next.line !== line) setWallLine(next.line);
  if (next.lineOffset !== lineOffset) setWallLineOffset(next.lineOffset);
  useToolTick.setState((t) => ({ n: t.n + 1 }));
}

/**
 * The line a wall is drawn on, chosen in the command line's bar while the Wall tool is on (decision 237): a face, or an offset
 * from the core's centre; with "Remember reference line" ticked the walls drawn keep it as their reference line, the one they
 * are snapped, moved and grown by (forum topic 159, decision 685). Unticked, the line only places them, as before.
 */
function WallLineOption() {
  const { line, lineOffset, remember } = useWallPrefs();
  return (
    <>
      <select className={`cad-layer${line !== 'core' ? ' on' : ''}`} value={line} data-testid="wall-line" title="The line you click: the core's centre (the axis), a face of the core, a finish face, or an offset from the core's centre; the exterior is the wall's right as you draw"
        onChange={(e) => { setWallLine(e.target.value as WallLine); useToolTick.setState((t) => ({ n: t.n + 1 })); }}>
        <option value="core">Core centre</option>
        <option value="coreExterior">Core, exterior face</option>
        <option value="coreInterior">Core, interior face</option>
        <option value="exterior">Exterior face</option>
        <option value="interior">Interior face</option>
        <option value="offset">Offset from the core centre</option>
      </select>
      <button type="button" className="btn small" data-action="wall-line-flip" disabled={line === 'core'} onClick={flipWallLine}
        title="Space: the same line on the other side of the wall (the exterior and interior faces swap, an offset changes sign); the wall's ghost shows where it will be built">Other side (Space)</button>
      {line === 'offset' && (
        <LengthField metres={lineOffset} testId="wall-line-offset" style={{ width: 80 }} title="How far the line you click lies from the core's centre: positive to the left as you draw, negative to the right (the exterior)"
          onCommit={(m) => { if (m != null) { setWallLineOffset(m); useToolTick.setState((t) => ({ n: t.n + 1 })); } }} />
      )}
      <label className="cad-layer" title="The walls keep this line as their reference line: they are snapped and moved by it, and grow round it when their thickness or type changes. Unticked, the line only places them."
        style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
        <input type="checkbox" data-testid="wall-remember-line" checked={remember} onChange={(e) => setRememberLine(e.target.checked)} />
        <span>Remember reference line</span>
      </label>
    </>
  );
}

/** The old Curved wall: the Wall tool, in Arc. */
const curvedWall: ToolDef = arcAlias({ id: 'architecture.curvedWall', label: 'Curved wall', icon: CurvedWallIcon, shortcut: 'WC' }, wall);
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
  id: 'architecture.join', label: 'Join walls', icon: Link2, shortcut: 'JW',
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
// Their buttons are core's Joins group now (core.mitre, butt, swap, on walls, beams and strip footings); the ids stay, off the ribbon.
const mitre = restyle('Mitre', 'architecture.mitre', Slash, { style: 'mitre' });
const butt = restyle('Butt', 'architecture.butt', CornerDownRight, { style: 'butt' });
const swap = restyle('Swap', 'architecture.swap', ArrowLeftRight, { swap: true });
/** Fillet and chamfer on walls and railings (a user's request): the command line's FILLET and CHAMFER, which take two walls, a railing's corner or two railings as well as drafted lines. */
const filletTool: ToolDef = { id: 'architecture.fillet', label: 'Fillet', icon: Spline, run: () => void startCommand('FILLET') };
const chamferTool: ToolDef = { id: 'architecture.chamfer', label: 'Chamfer', icon: CornerUpRight, run: () => void startCommand('CHAMFER') };
// The walls' Trim and Extend (forum #109) became Modify → Trim/Extend elements (audit 2026-11-15, §6): their ids stay, as aliases.
const trimTool: ToolDef = { id: 'architecture.trim', label: 'Trim/Extend elements', icon: Scissors, run: () => void startCommand('TRIMEXTEND') };
const extendTool: ToolDef = { id: 'architecture.extend', label: 'Trim/Extend elements', icon: ArrowRightToLine, run: () => void startCommand('TRIMEXTEND') };
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
/** A wall's or floor's grips: its layers' edges in "Edit layers" (forum topic 89), else its own. */
const withLayers = (own: (node: NodeDto, view?: GripView) => Grip[]) => (node: NodeDto, view?: GripView): Grip[] => {
  const data = editingLayersOf(node);
  return data ? layerGrips(node, data, view) : own(node, view);
};

function wallGrips(node: NodeDto): Grip[] {
  const c = arcOf(node.props);
  if (!c) return [];
  const flipped = usageOf(useSession.getState(), node.id)?.negative ?? false;
  // A wall that keeps a reference line (decision 685) is dragged by it: its grips on that line, the axis following at its distance.
  const ref = referenceArc(node.props);
  const update = (s: Vec2, e: Vec2, rise: number) => {
    const axis = ref ? axisFromReference({ a: s, b: e, rise }, ref.left) : { a: s, b: e, rise };
    return { command: 'architecture.updateWall', args: { id: node.id, x1: axis.a.x, y1: axis.a.y, x2: axis.b.x, y2: axis.b.y, arc: axis.rise, detachJoinedEnds: true } };
  };
  return [
    ...withWallCorners(node, ref?.arc ?? c, withAutoJoinMenus(node.id, node.props, pathGrips(ref?.arc ?? c, update))),
    flipAcross(c, flipped ? 1 : -1, 'Flip: swap the wall\'s faces (its exterior goes to the other side)', () => ({ command: 'architecture.flipWall', args: { ids: sameKind(node) } })),
  ];
}
/**
 * A wall's end joined to another wall at a corner (their ends there): its grip's right-click offers "Fillet corner…" and
 * "Chamfer corner…", the two walls trimmed to a curved (or straight) wall between them (architecture.filletWalls, one undo
 * step); offered disabled when either is curved.
 */
export function withWallCorners(node: NodeDto, c: Arc, grips: Grip[]): Grip[] {
  const s = useSession.getState();
  const joined: { id: string; a: Vec2; b: Vec2; curved: boolean }[] = [];
  for (const r of s.relations.values()) {
    if (r.class !== 'IfcRelConnectsPathElements' || (r.relating !== node.id && !r.related.includes(node.id))) continue;
    const other = s.nodes.get(r.relating === node.id ? r.related[0] : r.relating);
    const oc = other?.type === 'IfcWall' ? arcOf(other.props) : null;
    if (other && oc) joined.push({ id: other.id, a: oc.a, b: oc.b, curved: Math.abs(oc.rise) > 1e-6 });
  }
  const at = (end: Vec2) => {
    let best: { id: string; curved: boolean; d: number } | null = null;
    for (const j of joined) {
      const d = Math.min(Math.hypot(j.a.x - end.x, j.a.y - end.y), Math.hypot(j.b.x - end.x, j.b.y - end.y));
      if (d < 1 && (!best || d < best.d)) best = { id: j.id, curved: j.curved, d };
    }
    return best;
  };
  return grips.map((g) => {
    if (g.key !== 'start' && g.key !== 'end') return g;
    const other = at(g.key === 'start' ? c.a : c.b);
    if (!other) return g;
    return {
      ...g,
      corner: {
        straight: !other.curved && Math.abs(c.rise) <= 1e-6,
        fillet: (radius: number) => ({ command: 'architecture.filletWalls', args: { a: node.id, b: other.id, radius } }),
        chamfer: (distance: number) => ({ command: 'architecture.chamferWalls', args: { a: node.id, b: other.id, distance } }),
      },
    };
  });
}
const pathRefs = (node: NodeDto): RefLine[] => { const c = arcOf(node.props); return c ? [pathRef(c)] : []; };
/** A wall's axis with the way it was drawn and its faces, R and L, as the Wall types dialog's legend shows them (report #79). */
const wallRefs = (node: NodeDto): RefLine[] => {
  const ref = referenceArc(node.props);
  // And the reference line it keeps (decision 685), drawn solid beside the dashed axis.
  return [...pathRefs(node).map((r) => ({ ...r, faces: true })), ...(ref ? [{ ...pathRef(ref.arc), kind: 'reference' as const }] : [])];
};

/**
 * A door or window: dragged along its wall by its middle, widened by either
 * jamb (its own size, forum topic 140), and four flip arrows round it: along
 * the wall the hand (hinges on the other side), across it the side (opening
 * to the other face).
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
    ...joineryJambGrips(opening.id, c, num(opening, 'along'), joinerySize(node.props, typeOf(s, node.id)?.props.spec as { width?: number } | undefined).width),
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
/**
 * A slab, floor opening, ceiling or roof: its outline's vertices and edges (a roof's edge rules follow its edges). Their
 * edges may be arcs and their corners rounded or cut (forum topic 97, stage 2; forum topic 160 for roofs).
 */
function contourGrips(command: string, withEdges = false) {
  return (node: NodeDto): Grip[] => {
    const pts = fromPairs(node.props.contour);
    if (pts.length < 3) return [];
    const edges = withEdges && Array.isArray(node.props.edges) && node.props.edges.length === pts.length ? (node.props.edges as unknown[]) : undefined;
    const curved = !node.props.stairId;
    const arcs = curved ? arcsOf(node.props.arcs, pts.length) : undefined;
    const outline = outlineGrips({ pts, edges, arcs }, (o) => ({ command, args: { id: node.id, points: toPairs(o.pts), ...(o.edges ? { edges: o.edges } : {}), ...(curved ? { arcs: o.arcs ?? o.pts.map(() => 0) } : {}) } }),
      curved ? {
        fillet: (index, radius) => ({ command: 'architecture.roundCorner', args: { id: node.id, index, radius } }),
        chamfer: (index, distance) => ({ command: 'architecture.chamferCorner', args: { id: node.id, index, distance } }),
      } : undefined);
    // Elliptical edges go to "Offset outline" (audit S4): the curve at the distance from an ellipse, laid in chords by the engine.
    // A floor's or a ceiling's own holes (audit S4).
    return [...withEllipses(outline, node.props.ellipses, pts.length),
      ...holeGrips(node.id, node.props.holes, node.props.holeArcs, (holes, holeArcs) => ({ command, args: { id: node.id, holes, holeArcs } }), 'architecture', node.props.holeEllipses)];
  };
}
/**
 * A roof: its outline, and each roof window's and dormer's grips (decisions 251 and 280): the middle moves it, a window's
 * edges and corners and a dormer's sides resize it on its slope (refused where it would not fit); Ctrl/⌘-click, Delete
 * over a grip or its right-click menu takes it out.
 */
const roofGrips = (node: NodeDto): Grip[] => [...contourGrips('architecture.updateRoof', true)(node), ...roofSlopeGrips(node), ...roofOpeningGrips(node)];
const contourRefs = (node: NodeDto): RefLine[] => {
  const pts = fromPairs(node.props.contour);
  if (pts.length < 3) return [];
  // Elliptical edges (audit S4) along their ellipses.
  const ellipses = Array.isArray(node.props.ellipses) && node.props.ellipses.length === pts.length ? (node.props.ellipses as (EllipseEdgeJson | null)[]) : undefined;
  const outer: RefLine = ellipses ? { points: loopOf({ pts, arcs: arcsOf(node.props.arcs, pts.length), ellipses }), closed: true, kind: 'outline' } : outlineRef(pts, arcsOf(node.props.arcs, pts.length));
  // Holes with elliptical edges (audit §18 leftovers) along their ellipses too.
  const holeEllipses = Array.isArray(node.props.holeEllipses) ? (node.props.holeEllipses as unknown[]) : [];
  const holes = holeRefs(node.props.holes, node.props.holeArcs).map((ref, k) => {
    const el = holeEllipses[k];
    const corners = Array.isArray(node.props.holes) ? fromPairs((node.props.holes as unknown[])[k]) : [];
    return Array.isArray(el) && el.length === corners.length && corners.length >= 3
      ? { ...ref, points: loopOf({ pts: corners, arcs: arcsOf(Array.isArray(node.props.holeArcs) ? (node.props.holeArcs as unknown[])[k] : undefined, corners.length), ellipses: el as (EllipseEdgeJson | null)[] }) }
      : ref;
  });
  return [outer, ...holes];
};

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
/** The command line's bar while a door or window is being placed. */
function FillPlacingHint() {
  const flipped = useFillHand((s) => s.flipped);
  return <button type="button" className="btn small" data-action="fill-hand" aria-pressed={flipped} title="The side of the wall the cursor is on is the side it opens to" onClick={flipFillHand}>{flipped ? 'Other hand (Space)' : 'Hand (Space)'}</button>;
}
/** A door or window: click a wall where it goes (the type: the one picked in the configurator, else the project's first). */
const fillTool = (kind: 'door' | 'window', label: string, icon: ToolDef['icon'], shortcut: string): ToolDef => ({
  id: `architecture.${kind}`, label, icon, shortcut,
  properties: kind === 'door' ? newElement('IfcDoorType', 'doorTypeId', 'joinery') : newElement('IfcWindowType', 'windowTypeId', 'joinery'),
  typed: kind === 'door' ? typedBy('IfcDoorType', 'doorTypeId', 'joinery', JoineryThumb) : typedBy('IfcWindowType', 'windowTypeId', 'joinery', JoineryThumb),
  // Space: the other hand; the side of the wall the cursor is on: the side it opens to, shown before the click.
  space: flipFillHand,
  options: FillPlacingHint,
  gesture: {
    kind: 'pick', types: ['IfcWall'], reach: 40,
    // What a module that drives a wall is asked, as the cursor moves: whether this fits there.
    placing: (_wall, _at) => {
      const s = useSession.getState();
      const wanted = kind === 'door' ? useUi.getState().doorTypeId : useUi.getState().windowTypeId;
      const type = (wanted ? s.nodes.get(wanted) : undefined) ?? ofClass(s, kind === 'door' ? 'IfcDoorType' : 'IfcWindowType')[0];
      const width = (type?.props.spec as { width?: number } | undefined)?.width ?? (kind === 'door' ? 0.9 : 1.2);
      return { kind, width, typeId: type?.id ?? null };
    },
    ghost: (wall, at) => {
      const s = useSession.getState();
      const wanted = kind === 'door' ? useUi.getState().doorTypeId : useUi.getState().windowTypeId;
      const cls = kind === 'door' ? 'IfcDoorType' : 'IfcWindowType';
      const type = (wanted ? s.nodes.get(wanted) : undefined) ?? ofClass(s, cls)[0];
      const spec = type?.props.spec as { width?: number; columns?: number[]; panels?: PanelLike[] } | undefined;
      const shape = spec?.width ? { width: spec.width, columns: spec.columns, panels: spec.panels }
        : kind === 'door' ? { width: 0.9, panels: [{ operation: 'swing-left' }] } : { width: 1.2, panels: [{ operation: 'fixed' }] };
      const wallType = typeOf(s, wall.id);
      const thickness = (wallType ? typeLayers(s, wallType.id) : []).reduce((a, l) => a + l.thickness, 0) || 0.25;
      return fillGhost(wall.props, at, thickness, shape, useFillHand.getState().flipped);
    },
  },
  complete: async ({ points: [p], entity }, ctx) => {
    if (!entity) return;
    const typeId = kind === 'door' ? useUi.getState().doorTypeId : useUi.getState().windowTypeId;
    await ctx.execute(kind === 'door' ? 'architecture.addDoor' : 'architecture.addWindow', {
      wallId: entity.id, x: p.x, y: p.y, ...fillFlips(arcOf(entity.props), p, useFillHand.getState().flipped), ...(typeId ? { typeId } : {}),
    });
  },
});
const doorPart = fillTool('door', 'Door', DoorIcon, 'DR');
const windowPart = fillTool('window', 'Window', WindowIcon, 'WN');
/**
 * A bay, bow or box window (report #60): click a wall. The type picked in the configurator when it projects, else the
 * project's first that does, else one made from Lapin's first bay style (a 45° bay).
 */
const bayWindowPart: ToolDef = {
  id: 'architecture.bayWindow', label: 'Bay window', icon: WindowIcon, shortcut: 'BW',
  gesture: { kind: 'pick', types: ['IfcWall'] },
  complete: async ({ points: [p], entity }, ctx) => {
    if (!entity) return;
    const s = useSession.getState();
    const projects = (id?: string | null) => { const n = id ? s.nodes.get(id) : undefined; return n?.type === 'IfcWindowType' && Boolean((n.props.spec as { bay?: unknown } | undefined)?.bay); };
    const picked = useUi.getState().windowTypeId;
    let typeId = projects(picked) ? picked : [...s.nodes.values()].find((n) => projects(n.id))?.id ?? null;
    if (!typeId) {
      const style = (s.info?.catalogs['architecture.bayStyles'] as unknown as { spec: unknown }[] | undefined)?.[0];
      if (!style) return;
      const r = (await ctx.execute('architecture.createJoineryType', { spec: style.spec })) as { id?: string } | null;
      typeId = r?.id ?? null;
    }
    if (!typeId) return;
    useUi.setState({ windowTypeId: typeId });
    await ctx.execute('architecture.addWindow', { wallId: entity.id, x: p.x, y: p.y, typeId });
  },
};
const joineryTypes: ToolDef = { id: 'architecture.joineryTypes', label: 'Doors & windows', icon: Settings2, run: () => useUi.setState({ dialog: 'joinery' }) };
/**
 * A floor: click its corners, then the first one again (or Enter); its bar draws it as a rectangle, a circle, a polygon whose
 * edges may be arcs (Space), or inside the walls clicked in (forum topic 97).
 */
const slab: ToolDef = {
  id: 'architecture.slab', label: 'Floor', icon: SlabIcon, shortcut: 'SB', properties: newElement('IfcSlabType', 'slabTypeId', 'slabTypes'), typed: typedBy('IfcSlabType', 'slabTypeId', 'slabTypes', SlabThumb),
  gesture: { kind: 'polygon', shapes: true },
  options: FloorShapeBar,
  space: toggleOutlineArcs,
  complete: async ({ points, arcs, ellipses }, ctx) => {
    const typeId = useUi.getState().slabTypeId;
    const where = points.length === 1 ? { x: points[0].x, y: points[0].y } : { points: points.map((p) => [p.x, p.y]), ...(arcs ? { arcs } : {}), ...(ellipses ? { ellipses } : {}) };
    await ctx.execute('architecture.addSlab', { storeyId: ctx.storeyId, ...where, ...(typeId ? { typeId } : {}) });
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
const slabOpeningPart: ToolDef = {
  id: 'architecture.slabOpening', label: 'Floor opening', icon: SlabOpeningIcon, shortcut: 'SH',
  gesture: { kind: 'polygon', shapes: true },
  options: EllipseShapeBar,
  space: toggleOutlineArcs,
  complete: async ({ points, arcs, ellipses }, ctx) => {
    if (points.length < 3) return;
    await ctx.execute('architecture.addSlabOpening', { storeyId: ctx.storeyId, points: points.map((p) => [p.x, p.y]), ...(arcs ? { arcs } : {}), ...(ellipses ? { ellipses } : {}) });
  },
};
/**
 * A hole in a floor's or a ceiling's own outline (audit S4): its outline drawn inside it (the floor or ceiling selected, else the
 * floor, then the ceiling, that holds it); part of the element, edited with its grips, not an opening of its own.
 */
const outlineHolePart: ToolDef = {
  id: 'architecture.outlineHole', label: 'Hole in the outline', icon: SlabOpeningIcon,
  gesture: { kind: 'polygon', shapes: true },
  // Its edges may run along an ellipse (audit §18 leftovers): the Ellipse source too.
  options: EllipseShapeBar,
  space: toggleOutlineArcs,
  complete: async ({ points, arcs, ellipses }, ctx) => {
    if (points.length < 3) return;
    const { selected } = useUi.getState();
    const host = selected.length === 1 ? useSession.getState().nodes.get(selected[0]) : undefined;
    const id = host && (host.type === 'IfcSlab' || host.type === 'LapinCeiling') ? host.id : undefined;
    await ctx.execute('architecture.addOutlineHole', { storeyId: ctx.storeyId, points: points.map((p) => [p.x, p.y]), ...(arcs ? { arcs } : {}), ...(ellipses ? { ellipses } : {}), ...(id ? { id } : {}) });
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
 * The one Opening tool (S14): door, window, bay window or an opening of a shape in a wall, an opening in a floor, a roof window or
 * a dormer. The kinds' old tools are its aliases (legacyTools); Create similar and the quick slots start them.
 */
const { tool: opening, aliases: openingAliases } = openingTool({
  door: doorPart, window: windowPart, bayWindow: bayWindowPart, opening: wallOpening, floor: slabOpeningPart, outlineHole: outlineHolePart,
  roofWindow: roofOpening('window'), dormer: roofOpening('dormer'),
});
const aliasOf = (id: string) => openingAliases.find((t) => t.id === id)!;
const door = aliasOf('architecture.door');
const window_ = aliasOf('architecture.window');

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
registerCommand({ names: ['STAIRSKETCH', 'STS'], label: 'Stair by sketch', start: () => { void newStairSketch(); return { prompt: '', want: 'none' }; } });
/** A stair drawn by sketch (users asked for Revit's, 19.11.2026): its sides, its risers and its path on a board of its own. */
const stairBySketch: ToolDef = { id: 'architecture.stairBySketch', label: 'Stair by sketch', icon: PenTool, run: () => void newStairSketch() };
/** The selected stair's sketch opened again (or a sketch begun for a stair of its type's shape). */
const editStairSketchTool: ToolDef = {
  id: 'architecture.editStairSketch', label: 'Edit stair sketch', icon: PenTool,
  run: () => {
    const id = useUi.getState().selection;
    if (!id || useSession.getState().nodes.get(id)?.type !== 'IfcStair') { notify('Select a stair first: its sketch opens on a plan of its own.'); return; }
    void editStairSketch(id);
  },
};
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
/** The window sills' schedule (forum topic 178, decision 736): each kind of sill, how many and on which windows, for their supplier. */
const windowSillSchedule: ToolDef = {
  id: 'architecture.windowSillSchedule', label: 'Window sill schedule', icon: Table2,
  run: () => void createScheduleOf('windowSills').then((r) => { if (r) notify('Window sill schedule made: it is in the project browser, under Schedules.'); }),
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
  // The edges of the wall or floor edited in "Edit layers", kept in step with the model (forum topic 89).
  background: LayerEditWatcher,
  tabs: [{
    id: 'architecture', label: 'Architecture', order: 20, include: ['modify', 'joins'],
    groups: [
      { label: 'Build', tools: [wall, opening, slab, roof], small: [wallFromSolids, curtainWall, roofConfigurator, downpipe, railing] },
      { label: 'Types', tools: [wallTypes, slabTypes, joineryTypes], small: [curtainTypes, sunshadeTypes, windowSillSchedule] },
      // Sunshades (report #56): on a window, along a facade; their schedule.
      { label: 'Sunshades', tools: [sunshade], small: [sunshadeAlong, sunshadeSchedule] },
      // Profiles swept along a wall's face: added (a cornice, a band) or taken out (a reveal), decision 297.
      { label: 'Sweeps', tools: [wallSweep, wallReveal], small: [sweepSelected, revealSelected, wallSweepSchedule] },
      { label: 'Joins', tools: [join], small: [mitreNew, filletTool, chamferTool, attachRoof, detachRoof, editLayers] },
      { label: 'Room', tools: [room], small: [roomOutline, roomDividers, updateRooms, roomTag] },
      { label: 'Circulation', tools: [stair, ramp, stairTypes], small: [...(sketchStairs() ? [stairBySketch] : []), entranceSteps, entranceRamp, underStair, underStairSchedule] },
      // Balconies (a tester's report, #96): by a wall's face in a style, by outline, a gallery's edge; the configurator, stacking, the schedule.
      { label: 'Balconies', tools: [balcony, balconyConfigurator], small: [balconyOutline, galleryEdge, stackBalconies, balconySchedule] },
    ],
  }, {
    // Finishes, after Architecture: what lines a room (its floor, walls and ceiling by a finish of its own, false ceilings, skirtings). Other modules add their groups by this id.
    // Interior (decision 367): finishes and furniture on one tab; the furniture's "Table" is a piece of furniture there.
    id: 'interior', label: 'Interior', order: 25, include: ['modify'], i18nCtx: 'furniture',
    groups: [
      { label: 'Room', tools: [finishRoom], small: [roomFinishes, roomFinishSchedule] },
      // The finish painter (forum topic 83): faces of walls, floors and columns painted, inside and out.
      { label: 'Painter', tools: [finishPainter], small: [paintBrush, splitRect, splitLine, paintEyedropper, paintClear, surfaceFinishSchedule] },
      { label: 'Ceiling', tools: [ceiling, ceilingInRoom], small: [ceilingLight, ceilingDiffuser, ceilingTypes] },
      { label: 'Skirtings', tools: [skirtingInRoom, skirting], small: [skirtingTypes, skirtingSchedule] },
    ],
  }, {
    // Into Site: gates with their piers on a line (report #57), after the landscape.
    id: 'site', label: 'Site', order: 40,
    groups: [gateGroup],
  }, {
    // Into View: the room colours by the other graphics.
    id: 'view', label: 'View', order: 90,
    groups: [{ label: 'Rooms', tools: [roomColors] }],
  }, {
    // Into Annotate: the colour legend of a plan's rooms.
    id: 'annotate', label: 'Annotate', order: 60,
    groups: [{ label: 'Rooms', tools: [colorLegend] }],
  }],
  legacyTools: [curvedWall, roomDividerArc, mitre, butt, swap, trimTool, extendTool, ...openingAliases, editStairSketchTool],
  hotbar: [wall, door, window_, slab, roof],
  propertySections: [
    { id: 'wall', label: 'Wall', classes: ['IfcWall'], component: WallLayers },
    { id: 'originalSolids', label: 'Made from solids', classes: ['IfcWall'], shows: hidesSolids, component: OriginalSolidsSection },
    { id: 'wallSweeps', label: 'Sweeps and reveals', classes: ['IfcWall'], shows: hasSweeps, component: WallSweepsOnWall },
    { id: 'wallSweep', label: 'Wall sweep', classes: ['LapinWallSweep'], component: WallSweepSection },
    { id: 'joinery', label: 'Door or window', classes: ['IfcDoor', 'IfcWindow'], component: JoinerySection },
    { id: 'wallOpening', label: 'Opening', classes: ['IfcOpeningElement'], shows: isWallOpening, component: WallOpeningSection },
    { id: 'slab', label: 'Floor', classes: ['IfcSlab'], component: SlabSection },
    { id: 'layerExtents', label: 'Layer extents', classes: ['IfcWall', 'IfcSlab', 'IfcWallType', 'IfcSlabType'], shows: hasLayerExtents, component: LayerExtentsSection },
    { id: 'paintedFinishes', label: 'Painted finishes', classes: ['IfcWall', 'IfcSlab', 'IfcColumn', 'IfcBeam'], shows: hasPaintedFinishes, component: PaintedFinishesSection },
    { id: 'downpipe', label: 'Downpipe', classes: ['LapinDownpipe'], component: DownpipeSection },
    { id: 'roof', label: 'Roof', classes: ['IfcRoof'], component: RoofSection },
    { id: 'stair', label: 'Stair', classes: ['IfcStair', 'IfcRamp'], component: StairSection },
    { id: 'balcony', label: 'Balcony', classes: [BALCONY], component: BalconySection },
    { id: 'underStair', label: 'Under-stair space', classes: ['LapinUnderStair'], component: UnderStairSection },
    { id: 'ceiling', label: 'Ceiling', classes: ['LapinCeiling'], component: CeilingSection },
    { id: 'skirting', label: 'Skirting', classes: ['LapinSkirting'], component: SkirtingSection },
    { id: 'curtainWall', label: 'Curtain wall', classes: ['IfcCurtainWall'], component: CurtainWallSection },
    { id: 'sunshade', label: 'Sunshade', classes: [SHADE], component: SunshadeSection, replacesFields: () => true },
    { id: 'room', label: 'Room', classes: ['IfcSpace'], component: RoomSection, replacesFields: () => true },
    { id: 'roomColors', label: 'Room colours', classes: ['LapinView'], component: RoomViewSection },
    { id: 'gate', label: 'Gate', classes: [GATE_CLASS], component: GateSection },
  ],
  // A stair's railing selected on its own (forum topic 102): stairId#railing:left, by a click on it in a plan or in 3D.
  subSelectionPanels: { IfcStair: StairRailingPanel },
  subSelectionPick: { IfcStair: railingPick },
  dialogs: { wallOpening: OpeningDialog, wallProfile: WallProfileDialog, curtainMembers: CurtainMembersDialog, curtainCells: CurtainCellsDialog, roof: RoofDialog, wallTypes: WallTypesDialog, slabTypes: SlabTypesDialog, joinery: JoineryDialog, stairTypes: StairTypesDialog, ceilingTypes: CeilingTypesDialog, skirtingTypes: SkirtingTypesDialog, roomFinishes: RoomFinishesDialog, curtainWalls: CurtainWallDialog, roomColors: RoomColorsDialog, roomTag: RoomTagDialog, underStair: UnderStairDialog, sunshades: SunshadeTypesDialog, balcony: BalconyDialog, gateTypes: GateTypesDialog, fieldSchedule: FieldScheduleDialog, fieldTemplates: FieldTemplatesDialog, wallFromSolids: WallFromSolidsDialog },
  // In "Edit layers" (forum topic 89) a wall's or floor's grips are its layers' edges.
  grips: { IfcWall: withLayers(wallGrips), IfcDoor: joineryGrips, IfcWindow: joineryGrips, IfcCurtainWall: curtainGrips, IfcSlab: withLayers(contourGrips('architecture.updateSlab')), LapinCeiling: contourGrips('architecture.updateCeiling'), IfcOpeningElement: openingGrips, IfcRoof: roofGrips, IfcSpace: roomGrips, LapinDownpipe: downpipeGrips, LapinRailing: railingGrips, LapinSkirting: skirtingGrips, [SHADE]: sunshadeGrips, [BALCONY]: balconyGrips,
    [GATE_CLASS]: (n: NodeDto) => {
      const { nodes, relations } = useSession.getState();
      const kinds = (useSession.getState().info?.catalogs['architecture.gateKinds'] ?? []) as unknown as Parameters<typeof overallOf>[1];
      return gateGrips(n, overallOf(relatingOf({ nodes, relations }, n.id, 'IfcRelDefinesByType') ?? undefined, kinds));
    },
  },
  // A door's, window's or wall opening's temporary dimensions (decision 511).
  // And a wall's (decision 693), selected or being drawn.
  planOverlays: [OpeningDims, WallDims, LayerEditPlan],
  overlays: [FinishPainterPanel],
  drawingOverlays: [OpeningDimsInView, LayerEditDrawing],
  grips3d: architectureGrips3d,
  references: { IfcWall: wallRefs, IfcCurtainWall: pathRefs, IfcSlab: contourRefs, IfcOpeningElement: contourRefs, IfcRoof: (n) => [...contourRefs(n), ...roofOpeningRefs(n)], IfcSpace: roomRefs, LapinRailing: railingRefs },
  similar: {
    IfcWall: (n) => { useUi.setState({ wallTypeId: typeOf(useSession.getState(), n.id)?.id ?? null }); setChainMode(wall.id, Math.abs(arcOf(n.props)?.rise ?? 0) > 1e-6 ? 'arc' : 'line'); activate(wall); },
    IfcSlab: (n) => { useUi.setState({ slabTypeId: typeOf(useSession.getState(), n.id)?.id ?? null }); activate(slab); },
    IfcDoor: (n) => { useUi.setState({ doorTypeId: typeOf(useSession.getState(), n.id)?.id ?? null }); activate(door); },
    IfcWindow: (n) => { useUi.setState({ windowTypeId: typeOf(useSession.getState(), n.id)?.id ?? null }); activate(window_); },
    // An opening alone in a wall: the Opening tool set to an opening of its shape and size; a floor's: set to a floor opening.
    IfcOpeningElement: (n) => {
      if (isWallOpening(n)) {
        const v = (k: string, d: number) => (typeof n.props[k] === 'number' ? (n.props[k] as number) : d);
        const shape = (['segmental', 'elliptical', 'round', 'radiused', 'circle'] as const).find((k) => k === n.props.shape) ?? 'rect';
        useUi.setState({ openingSize: { width: v('width', 1), height: v('height', 2), sill: v('sill', 0), shape, rise: typeof n.props.rise === 'number' ? n.props.rise : null } });
        activate(aliasOf('architecture.wallOpening'));
      } else if (relatingOf(useSession.getState(), n.id, 'IfcRelVoidsElement')?.type === 'IfcSlab') activate(aliasOf('architecture.slabOpening'));
      else notify('A door\'s or window\'s opening: select the door or window to make more like it.');
    },
    IfcRoof: (n) => { useUi.setState({ roofStyle: typeof n.props.style === 'string' ? n.props.style : null }); activate(roof); },
    IfcCurtainWall: (n) => { useUi.setState({ curtainTypeId: typeOf(useSession.getState(), n.id)?.id ?? null }); activate(curtainWall); },
    IfcSpace: (n) => activate(n.props.auto === false ? roomOutline : room),
    // Create similar on a room separation line: the same kind of line again.
    IfcVirtualElement: (n) => {
      const t = (n.props.shape as { type?: string; closed?: boolean } | undefined);
      activate(t?.type === 'arc' ? roomDividerArc : t?.type === 'circle' ? roomDividerCircle : t?.type === 'polyline' ? (t.closed ? roomDividerRectangle : roomDividerPolyline) : roomDivider);
    },
    [BALCONY]: (n) => { useUi.setState({ balconyStyle: styleOf(n) || null }); activate(n.props.kind === 'mezzanine' ? galleryEdge : n.props.contour ? balconyOutline : balcony); },
  },
  // Edit type… (decision 735) on a sunshade or a gate: its own dialog on its type (the others: their placing tool's dialog).
  editType: { [SHADE_TYPE]: (id) => editShadeType(id), [GATE_TYPE]: (id) => { useGateTypesDialog.setState({ typeId: id }); useUi.setState({ dialog: 'gateTypes' }); } },
  setType: {
    IfcWall: (ids, typeId) => execute('architecture.setWallType', { ids, typeId }),
    IfcSlab: (ids, typeId) => execute('architecture.setSlabType', { ids, typeId }),
    IfcDoor: (ids, typeId) => execute('architecture.setJoineryType', { ids, typeId }),
    IfcWindow: (ids, typeId) => execute('architecture.setJoineryType', { ids, typeId }),
    IfcCurtainWall: (ids, typeId) => execute('architecture.setCurtainWallType', { ids, typeId }),
    [SHADE]: (ids, typeId) => execute('architecture.setSunshadeType', { ids, typeId }),
    [GATE_CLASS]: (ids, typeId) => execute('architecture.setGateType', { ids, typeId }),
  },
};

export default architecture;
