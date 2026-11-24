/** Drafting: CAD drawing on views (lines, polylines, circles, arcs, hatches on layers, and their edits), dimensions that follow the elements, texts, grid axes. */

import { promptText } from '../../shell/prompts';
import { ArrowRightToLine, Crosshair, Egg, ListPlus, ListMinus, Circle, Copy, CopyPlus, CornerUpRight, Eraser, FileInput, FileText, FlipHorizontal2, Hash, Layers, Link2, Unlink2, Move, MoveHorizontal, RectangleHorizontal, RotateCw, Scaling, Scissors, Slash, Spline, Type, Redo2, PenLine, RulerDimensionLine, Tag, Tags, Grid3x3, Settings2, SlidersHorizontal, SquareDashed, Image as ImageIcon, Ruler, Replace, Shapes, Shapes as SymbolsIcon, FolderPlus, ListOrdered, FileImage } from 'lucide-react';
import { openTempDimProperties } from '../../viewport/tempDimProps';
import { AngleIcon, ArcLengthIcon, DiameterIcon, DimensionIcon, GridAxisIcon, LevelIcon, OrdinateIcon, RadiusIcon, SlopeIcon } from '../../icons';
import { openPlaceCatalog } from '../../shell/placeCatalogStore';
import type { Grip, GripStep, GripView, LapinModule, RefLine, ToolDef, Vec2 } from '../../platform/module';
import { axisGrips, axisRef, outlineGrips, pt } from '../../viewport/gripKit';
import type { NodeDto } from '../../engine/protocol';
import { notify, select, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { cadHost, cadLog, registerCommand, startCommand, useCad, withSelection, type Step } from '../../viewport/cad/session';
import { importDxf, importTrace } from '../../engine/files';
import { DimensionSection } from './DimensionSection';
import { stackedAt, stackSpacingMm } from './dimStack';
import { DimensionOptions, isSamePoint, pickDirection, segmentLabels, setStringMode, stringMode, useDimTool, type StringMode } from './dimensionTool';
import { DIM_KINDS, SmartDimensionOptions, setDimensionExtras, startDimension, useSmartDim, type DimKind } from './smartDimension';
import { DraftSection } from './DraftSection';
import { ellipseAt, fromJson, keyPoints, toJson, type Shape } from '../../viewport/cad/shapes';
import { sketchPartOf } from '../../viewport/cad/constraints';
import { viewHasConstraints } from '../../viewport/cad/sketchStore';
import { frontView } from '../../viewport/frontView';
import { BoundarySection, boundaryGrips, viewBoundary } from './ViewBoundary';
import { xy } from '../../platform/vec';
import { GridDialog, GridSection } from './GridDialog';
import { RenumberAxesDialog } from './RenumberAxesDialog';
import { DimensionRulesDialog } from './DimensionRulesDialog';
import { drawableElement } from '../core/shopDrawing';
import { openStandards } from '../core/standardsTab';
import { formatLength, formatLevel, parseLength } from '../../engine/unitsStore';
import { SCALE_IMAGE_TOOL, scaleTarget, trueDistance } from './scaleImage';
import { ScaleImageSection, scaleImageShows } from './ScaleImageSection';
import { TagOptions, setTagSource, tagCommand, tagGrips, tagOpenings } from './tag';
import { DRAW_ORDER, drawOrder } from '../../viewport/drawOrder';
import { lazyPart } from '../../platform/lazyPart';
import { AngleLockMark } from './AngleLock';
import { GridAxisDims } from './GridAxisDims';
import '../core/symbols/symbolTools';
import { convertLayersInFront, convertSelection } from './convert/convert';
import { SymbolSection } from '../core/symbols/SymbolSection';
import { annotateSheetTool, keyGrips, leaderNote, legendTool, northArrowTool, revisionCloudTool, scaleBarTool } from './sheetAnnotations';
import { surveyPoint, useLocation } from '../../app/location';
import { TableSection, tableGrips, tablePartTool, tableTool } from './table/tableTool';

// Fetched the first time each is opened (platform/lazyPart.tsx).
const ConvertDialog = lazyPart(() => import('./convert/ConvertDialog').then((m) => m.ConvertDialog), { title: 'Convert drafting' });
const ConvertLayersDialog = lazyPart(() => import('./convert/ConvertLayersDialog').then((m) => m.ConvertLayersDialog), { title: 'Convert layers' });
const ImportDxfDialog = lazyPart(() => import('./ImportDxfDialog').then((m) => m.ImportDxfDialog), { title: 'Import DXF' });
const ImportTraceDialog = lazyPart(() => import('./ImportTraceDialog').then((m) => m.ImportTraceDialog), { title: 'Import PDF or image' });
const LayersDialog = lazyPart(() => import('./LayersDialog').then((m) => m.LayersDialog), { title: 'Drafting layers' });
const TableEditor = lazyPart(() => import('./table/TableEditor').then((m) => m.TableEditor), { title: 'Table' });

/** An aligned dimension: its two points, then where its line goes; then on as a string, one object. Points on an element's cut edge, a grid axis or a wall's centre line follow it. */
const DRAFTED = new Set(['DRAFT', 'TEXT', 'DIMENSION', 'TAG', 'SYMBOL', 'SHEETLEGEND', 'SCALEBAR', 'NORTHARROW', 'TABLE', 'TABLEPART']);
const draftsFields = (node: NodeDto) => DRAFTED.has(String(node.props.predefinedType));

/**
 * The one Dimension tool (S9): smart (the kind from what is clicked), with every kind, a string's mode and the generated
 * dimensions in its bar. It was Aligned; DIMALIGNED stays a typed command.
 */
const dimension: ToolDef = { id: 'drafting.dimension', label: 'Dimension', icon: DimensionIcon, shortcut: 'DI', run: () => startDimension('smart') };
const text: ToolDef = {
  id: 'drafting.text', label: 'Text', icon: Type, shortcut: 'TX',
  gesture: { kind: 'point' },
  complete: async ({ points: [p], rotation }, ctx) => {
    const body = promptText('Text');
    const angle = rotation ? -rotation : 0;
    if (body?.trim()) await ctx.execute('drafting.addText', { viewId: ctx.viewId, at: [p.x, p.y], text: body.trim(), ...(angle ? { angle } : {}) });
  },
};
const textBox: ToolDef = {
  id: 'drafting.textBox', label: 'Text box', icon: Type,
  gesture: { kind: 'rectangle' },
  complete: async ({ points: [a, b], rotation }, ctx) => {
    const width = Math.abs(b.x - a.x);
    if (width < 1e-3) return;
    const body = promptText('Text');
    const angle = rotation ? -rotation : 0;
    const at = [Math.min(a.x, b.x), Math.max(a.y, b.y)];
    if (body?.trim()) await ctx.execute('drafting.addText', { viewId: ctx.viewId, at, width, text: body.trim(), ...(angle ? { angle } : {}) });
  },
};
/** A grid axis: its two ends; it takes the next label of its direction. Its bar: the whole grid's dialog and the axis types (report #68). */
const axis: ToolDef = {
  id: 'drafting.axis', label: 'Grid axis', icon: GridAxisIcon, shortcut: 'GR',
  gesture: { kind: 'twoPoints' },
  complete: async ({ points: [a, b] }, ctx) => { await ctx.execute('drafting.addAxis', { x1: a.x, y1: a.y, x2: b.x, y2: b.y }); },
  options: AxisOptions,
};
/** A whole grid from its spacings (report #68). */
const gridTool: ToolDef = { id: 'drafting.grid', label: 'Axis grid…', icon: Grid3x3, run: () => useUi.setState({ dialog: 'grid' }) };
/** The axes renumbered in order across them (forum #75). */
const renumberAxesTool: ToolDef = { id: 'drafting.renumberAxes', label: 'Renumber axes…', icon: ListOrdered, run: () => useUi.setState({ dialog: 'renumberAxes' }) };
/** How axes look: Drafting standards → Grid axes. */
/** Several levels at once from their heights (issue #83): core's New levels… dialog. */
const newLevelsTool: ToolDef = { id: 'drafting.newLevels', label: 'New levels…', icon: ListPlus, run: () => useUi.setState({ dialog: 'newLevels' }) };
const axisStylesTool: ToolDef = { id: 'drafting.axisStyles', label: 'Axis styles…', icon: Settings2, run: () => openStandards('axis') };
function AxisOptions() {
  return (
    <>
      <button type="button" className="cad-toggle" data-axis-grid title="A whole grid from its spacings" onClick={() => useUi.setState({ dialog: 'grid' })}>AXIS GRID…</button>
      <button type="button" className="cad-toggle" title="The axes renumbered in order across them" onClick={() => useUi.setState({ dialog: 'renumberAxes' })}>RENUMBER…</button>
      <button type="button" className="cad-toggle" title="How axes look: bubbles, ends, size, line type, the first label" onClick={() => openStandards('axis')}>AXIS STYLES…</button>
    </>
  );
}

/**
 * DIM (aligned) and DLI (linear: horizontal or vertical, by where its line goes): two points, then the line. One
 * dimension at a time by default, and the command asks for the next one (Esc leaves); with Continuous on (the bar's box,
 * or C) each next point goes on the same line, measured from the last, previewed with its values until Enter, a
 * right-click, the Finish button or a click on the last point again makes the whole string one dimension (one object,
 * one undo step; U takes the last point back). Esc, or another tool or command, keeps the string drawn so far and says
 * so. A linear dimension's direction holds once chosen (Tab flips it). Points on elements, grid axes or wall centre
 * lines follow them.
 */
function dimCommand(linear: boolean): Step {
  const first = (): Step => {
    useDimTool.setState({ orient: null, forced: false, count: 0 });
    return {
      prompt: `First point [Chain/Baseline/Single: ${stringMode()}]`, want: 'point',
      keywords: modeWords(first),
      point: (a) => dimFrom(a, linear, first),
    };
  };
  return first();
}

/** The string modes typed at a dimension's first point: C a chain (again: single), B a baseline, S single. */
const modeWords = (again: () => Step): Record<string, () => Step> => {
  const to = (m: StringMode) => () => { setStringMode(m); return again(); };
  return {
    C: () => { setStringMode(stringMode() === 'chain' ? 'single' : 'chain'); return again(); },
    CONTINUOUS: to('chain'), CHAIN: to('chain'), B: to('baseline'), BASELINE: to('baseline'), S: to('single'), SINGLE: to('single'),
  };
};

/** A linear dimension's direction from where its line is (null: aligned), told to the bar after the draw. */
function dirOf(linear: boolean, a: Vec2, b: Vec2, at: Vec2): Vec2 | null {
  if (!linear) return null;
  const s = useDimTool.getState();
  const o = pickDirection(a, b, at, s.orient, s.forced);
  // Drawn from a preview, so the bar hears of a change after the draw.
  if (o !== s.orient) queueMicrotask(() => useDimTool.setState({ orient: o }));
  return o === 'h' ? { x: 1, y: 0 } : { x: 0, y: 1 };
}
const clickTol = () => 6 / (cadHost()?.pxPerMetre?.() || 1e6);

/** After a dimension's first point: its second, then its line (`enter`: what Enter does there, the smart tool's level). */
function dimFrom(a: Vec2, linear: boolean, again: () => Step, enter?: () => Step): Step {
  return {
    prompt: enter ? 'Second point (Enter: the level of the first)' : 'Second point', want: 'point', base: a, preview: (c) => [{ type: 'line', a, b: c }],
    labels: (c) => [{ at: { x: (a.x + c.x) / 2, y: (a.y + c.y) / 2 }, text: formatLength(Math.hypot(c.x - a.x, c.y - a.y)) }],
    point: (b) => dimLine(a, b, linear, again),
    ...(enter ? { enter } : {}),
  };
}

/**
 * A dimension's two points chosen: where its line goes. Single, it is made and the command asks for the next one; as a chain
 * (Continuous) each next point goes on the same line, measured from the last, until Enter makes the string one dimension; as a
 * baseline each next point is measured from the first, a dimension of its own on a line one step further out.
 */
function dimLine(a: Vec2, b: Vec2, linear: boolean, again: () => Step): Step {
  const tool = useDimTool;
  return {
    prompt: 'Where the dimension line goes', want: 'point', base: b,
    preview: (c) => stringPreview([a, b], c, dirOf(linear, a, b, c) ?? unit(a, b)),
    labels: (c) => segmentLabels([a, b], c, dirOf(linear, a, b, c) ?? unit(a, b)),
    point: async (at) => {
      const dir = dirOf(linear, a, b, at);
      tool.setState({ orient: dir ? (dir.x ? 'h' : 'v') : null, forced: true });
      if (tool.getState().baseline) { await addString([a, b], at, dir); return baselineNext(a, b, at, dir ?? unit(a, b), again); }
      if (!tool.getState().continuous) { await addString([a, b], at, dir); return again(); }
      const pts = [a, b];
      tool.setState({ count: 2 });
      // An aligned string measures along its first segment.
      const along = dir ?? unit(a, b);
      const make = () => addString(pts, at, dir ?? (collinear(pts) ? null : along));
      const finish = async () => { tool.setState({ count: 0 }); await make(); return null; };
      const next = (): Step => ({
        prompt: 'Next point [Undo] (Enter or right-click to finish)', want: 'point', base: pts[pts.length - 1],
        preview: (c) => stringPreview([...pts, c], at, along),
        labels: (c) => segmentLabels([...pts, c], at, along),
        point: (p) => {
          if (isSamePoint(p, pts[pts.length - 1], clickTol())) return finish();
          pts.push(p);
          tool.setState({ count: pts.length });
          return next();
        },
        keywords: { U: () => { if (pts.length > 2) pts.pop(); tool.setState({ count: pts.length }); return next(); } },
        enter: finish,
        // Esc (or another tool, or another command) keeps what is drawn, and says so.
        escape: async () => {
          tool.setState({ count: 0 });
          await make();
          notify(`Dimension string kept (${pts.length} points) · Undo removes it`);
        },
      });
      return next();
    },
  };
}

/** A baseline's step: 7 mm on the paper at the view's scale. */
function baselineGap(): number {
  const h = cadHost();
  const scale = Number(h ? useSession.getState().nodes.get(h.viewId)?.props.scale : NaN);
  return 0.007 * (scale > 0 ? scale : 100);
}

/**
 * A baseline (S9): after its first dimension, each point clicked is measured from the first point, a dimension of its own
 * (one undo step each) along the same direction, its line a step further out than the last; Enter or Esc ends it.
 */
function baselineNext(a: Vec2, b: Vec2, at: Vec2, u: Vec2, again: () => Step): Step {
  // Out: from the points towards the first dimension's line.
  const n0 = { x: -u.y, y: u.x };
  const side = Math.sign((at.x - a.x) * n0.x + (at.y - a.y) * n0.y) || 1;
  const out = { x: n0.x * side, y: n0.y * side };
  let k = 0;
  const lineAt = (i: number): Vec2 => ({ x: at.x + out.x * baselineGap() * i, y: at.y + out.y * baselineGap() * i });
  const next = (): Step => ({
    prompt: 'Next point, measured from the first (Enter to finish)', want: 'point', base: a,
    preview: (c) => stringPreview([a, c], lineAt(k + 1), u),
    labels: (c) => segmentLabels([a, c], lineAt(k + 1), u),
    point: async (p) => {
      if (isSamePoint(p, a, clickTol()) || isSamePoint(p, b, clickTol())) return next();
      k++;
      await addString([a, p], lineAt(k), u);
      return next();
    },
    enter: () => again(),
  });
  return next();
}

const unit = (a: Vec2, b: Vec2): Vec2 => { const l = Math.hypot(b.x - a.x, b.y - a.y) || 1; return { x: (b.x - a.x) / l, y: (b.y - a.y) / l }; };
/** Whether the points lie on the line through the first two. */
const collinear = (pts: Vec2[]): boolean => {
  const u = unit(pts[0], pts[1]);
  return pts.every((p) => Math.abs((p.x - pts[0].x) * u.y - (p.y - pts[0].y) * u.x) < 1e-6);
};

/** A string of points made as one dimension (one engine command). */
async function addString(pts: Vec2[], at: Vec2, dir: Vec2 | null): Promise<void> {
  const h = cadHost();
  if (!h) return;
  await execute('drafting.addDimension', { viewId: h.viewId, pts: pts.map(xy), at: xy(at), ...(dir ? { dir: xy(dir) } : {}) });
}

/** How a string will look: its dimension line through `at` along `u`, and each point's extension line onto it. */
function stringPreview(pts: Vec2[], at: Vec2, u: Vec2): Shape[] {
  const n = { x: -u.y, y: u.x };
  const onLine = pts.map((p) => { const o = (at.x - p.x) * n.x + (at.y - p.y) * n.y; return { x: p.x + n.x * o, y: p.y + n.y * o }; });
  const t = onLine.map((q) => (q.x - onLine[0].x) * u.x + (q.y - onLine[0].y) * u.y);
  const lo = Math.min(...t), hi = Math.max(...t);
  const shapes: Shape[] = [{ type: 'line', a: { x: onLine[0].x + u.x * lo, y: onLine[0].y + u.y * lo }, b: { x: onLine[0].x + u.x * hi, y: onLine[0].y + u.y * hi } }];
  pts.forEach((p, i) => shapes.push({ type: 'line', a: p, b: onLine[i] }));
  return shapes;
}
/** Whether an id is a dimension string (a linear or aligned dimension, not an angle, a radius…). */
const isString = (id: string | null | undefined): id is string => {
  const n = id ? useSession.getState().nodes.get(id) : undefined;
  return n?.props.predefinedType === 'DIMENSION' && !n.props.dimKind;
};

/**
 * DIMADD, as ArchiCAD inserts a dimension point: a dimension string (the one
 * selected, else one clicked), then each point clicked goes into it where it
 * falls along its line (inside it, or beyond an end as the new end), holding
 * on the element, axis or wall centre line it lands on; one undo step each,
 * Enter or Esc to finish.
 */
function dimAddCommand(): Step {
  const add = (id: string): Step => ({
    prompt: 'Point to add to the dimension string (Enter to finish)', want: 'point',
    point: async (p) => { await execute('drafting.insertDimensionPoint', { id, point: [p.x, p.y] }); return add(id); },
    enter: () => null,
  });
  const sel = useUi.getState().selected.filter(isString);
  if (sel.length === 1) return add(sel[0]);
  const pick = (): Step => ({
    prompt: 'Select the dimension string', want: 'select',
    point: (p) => { const id = cadHost()?.pickAny(p); if (!isString(id)) return pick(); select(id); return add(id); },
    enter: () => null,
  });
  return pick();
}
registerCommand({ names: ['DIMADD', 'DAP'], label: 'Add dimension points', start: dimAddCommand });

/**
 * DIMDEL: a dimension string (the one selected, else one clicked), then each
 * click takes out the string's point nearest it (the segments either side
 * become one); a string keeps two points. Enter or Esc to finish.
 */
function dimDelCommand(): Step {
  const del = (id: string): Step => ({
    prompt: 'Click near a point to take it out of the string (Enter to finish)', want: 'point',
    point: async (p) => {
      const n = useSession.getState().nodes.get(id);
      const pts = (Array.isArray(n?.props.pts) ? (n!.props.pts as unknown[]) : []).map(pt).filter((q): q is Vec2 => !!q);
      if (pts.length <= 2) { notify('A dimension keeps two points at least.'); return null; }
      let best = 0;
      pts.forEach((q, i) => { if (Math.hypot(q.x - p.x, q.y - p.y) < Math.hypot(pts[best].x - p.x, pts[best].y - p.y)) best = i; });
      await execute('drafting.removeDimensionPoint', { id, index: best });
      return del(id);
    },
    enter: () => null,
  });
  const sel = useUi.getState().selected.filter(isString);
  if (sel.length === 1) return del(sel[0]);
  const pick = (): Step => ({
    prompt: 'Select the dimension string', want: 'select',
    point: (p) => { const id = cadHost()?.pickAny(p); if (!isString(id)) return pick(); select(id); return del(id); },
    enter: () => null,
  });
  return pick();
}
registerCommand({ names: ['DIMDEL', 'DRP'], label: 'Remove dimension points', start: dimDelCommand });

registerCommand({ names: ['TAG', 'TG'], label: 'Tag', start: tagCommand, options: TagOptions });
/**
 * Rooms' interior dimensions (decision 249): on a plan, an aligned string inside each room along each of its sides, its
 * points at the corners and at the doors' and windows' jambs, held on the walls. The selected rooms, or every room of the
 * plan's storey; a room done again has its strings made anew.
 */
const roomDimensions: ToolDef = {
  id: 'drafting.roomDimensions', label: 'Room dimensions', icon: RulerDimensionLine, shortcut: 'ID',
  run: () => void (async () => {
    if (useUi.getState().view !== 'plan') { notify('Rooms are dimensioned on a plan: open one.'); return; }
    const viewId = await frontView();
    if (!viewId) return;
    const nodes = useSession.getState().nodes;
    const ids = useUi.getState().selected.filter((id) => nodes.get(id)?.type === 'IfcSpace');
    const r = await execute('drafting.dimensionRooms', { viewId, ...(ids.length ? { ids } : {}) }) as { rooms?: number; strings?: number; views?: string[] } | null;
    if (!r) return;
    // Asked on a site plan: the strings went to the storey's floor plan.
    if (r.rooms && r.views?.length) notify(`${r.strings} dimension strings in ${r.rooms} rooms, on the floor plan: ${r.views.join(', ')}.`);
    else notify(r.rooms ? `${r.strings} dimension strings in ${r.rooms} rooms.` : 'No room here to dimension: place rooms first (Architecture → Room).');
  })(),
};
/**
 * Exterior dimensions (forum #78): on a plan, parallel dimension strings outside the building along each straight exterior
 * facade, in the order of the dimension rules (Annotate → Dimension rules…). The selected walls, or every exterior wall of
 * the plan's storey; done again, the strings it made are replaced.
 */
const exteriorDimensions: ToolDef = {
  id: 'drafting.exteriorDimensions', label: 'Exterior dimensions', icon: SquareDashed, shortcut: 'ED',
  run: () => void (async () => {
    if (useUi.getState().view !== 'plan') { notify('Exteriors are dimensioned on a plan: open one.'); return; }
    const viewId = await frontView();
    if (!viewId) return;
    const nodes = useSession.getState().nodes;
    const wallIds = useUi.getState().selected.filter((id) => { const t = nodes.get(id)?.type; return t === 'IfcWall' || t === 'IfcWallStandardCase'; });
    const r = await execute('drafting.exteriorDimensions', { viewId, ...(wallIds.length ? { wallIds } : {}) }) as { facades?: number; strings?: number; views?: string[] } | null;
    if (!r) return;
    if (r.facades && r.views?.length) notify(`${r.strings} dimension strings on ${r.facades} facades, on the floor plan: ${r.views.join(', ')}.`);
    else notify(r.facades ? `${r.strings} dimension strings on ${r.facades} facades.` : 'No exterior wall found on this plan.');
  })(),
};
registerCommand({ names: ['EXTDIM', 'DEX'], label: 'Exterior dimensions', start: () => { void exteriorDimensions.run?.(); } });
const dimensionRulesTool: ToolDef = { id: 'drafting.dimensionRules', label: 'Dimension rules…', icon: SlidersHorizontal, run: () => useUi.setState({ dialog: 'dimensionRules' }) };
registerCommand({ names: ['DIMALIGNED', 'DAL'], label: 'Aligned dimension', start: () => dimCommand(false), options: DimensionOptions });
registerCommand({ names: ['DIMLINEAR', 'DLI'], label: 'Linear dimension', start: () => dimCommand(true), options: DimensionOptions });

// ─── The other dimensions: angle, radius, diameter, arc length, slope, level, ordinate ───

const pair = (p: Vec2) => [p.x, p.y];
/** One dimension of a kind made (one undo step); then `again` (the one Dimension tool asks for the next) or the command ends. */
async function addKind(kind: string, args: Record<string, unknown>, again?: () => Step | null): Promise<Step | null> {
  const h = cadHost();
  if (!h) return null;
  await execute('drafting.addDimension', { viewId: h.viewId, dimKind: kind, ...args });
  return again?.() ?? null;
}
type Again = (() => Step | null) | undefined;
/** DIMANGULAR: the vertex, a point on each side, then where the arc goes (the side it lies on picks the angle or its reflex). */
function angularCommand(again?: Again): Step {
  return {
    prompt: 'The angle\'s vertex', want: 'point',
    point: (c) => angleFrom(c, again),
  };
}
/** An angle's vertex given: a point on each side, then the arc. */
function angleFrom(c: Vec2, again?: Again): Step {
  return ({
      prompt: 'A point on the first side', want: 'point', base: c, preview: (q) => [{ type: 'line', a: c, b: q }],
      point: (a) => ({
        prompt: 'A point on the second side', want: 'point', base: c, preview: (q) => [{ type: 'line', a: c, b: a }, { type: 'line', a: c, b: q }],
        point: (b) => angleArc(c, a, b, again),
      }),
  });
}
/** An angle's vertex and a point on each side given: where its arc goes (the side it lies on picks the angle or its reflex). */
function angleArc(c: Vec2, a: Vec2, b: Vec2, again?: Again): Step {
  return {
    prompt: 'Where the arc goes', want: 'point', base: c,
    preview: (q) => [{ type: 'line', a: c, b: a }, { type: 'line', a: c, b }, { type: 'circle', c, r: Math.max(1e-3, Math.hypot(q.x - c.x, q.y - c.y)) }],
    point: (at) => addKind('angular', { c: pair(c), a: pair(a), b: pair(b), at: pair(at) }, again),
  };
}
/** DIMRADIUS / DIMDIAMETER: a drafted circle or arc (it follows and drives it), or its centre and a point on it [Points]; then where the text goes. */
/** A radius's or a diameter's circle given (a drafted one, or its centre and a point): where the text goes. */
function roundPlace(kind: 'radial' | 'diameter', args: Record<string, unknown>, centre: Vec2, again?: Again): Step {
  return {
    prompt: 'Where the text goes', want: 'point', base: centre,
    preview: (q) => [{ type: 'line', a: centre, b: q }],
    point: (at) => addKind(kind, { ...args, at: pair(at) }, again),
  };
}
function roundCommand(kind: 'radial' | 'diameter', again?: Again): Step {
  const place = (args: Record<string, unknown>, centre: Vec2): Step => roundPlace(kind, args, centre, again);
  return {
    prompt: `Pick a drafted circle or arc [Points]`, want: 'pick',
    pick: (hit) => (hit.shape.type === 'circle' || hit.shape.type === 'arc' ? place({ shape: hit.id }, hit.shape.c) : (cadLog('Pick a circle or an arc.'), roundCommand(kind, again))),
    keywords: {
      P: () => ({
        prompt: 'The centre', want: 'point',
        point: (c) => ({ prompt: 'A point on the circle', want: 'point', base: c, preview: (q) => [{ type: 'circle', c, r: Math.max(1e-3, Math.hypot(q.x - c.x, q.y - c.y)) }], point: (a) => place({ c: pair(c), a: pair(a) }, c) }),
      }),
    },
  };
}
/** DIMARC: a drafted arc, then where the dimension arc goes. */
function arcLengthCommand(again?: Again): Step {
  return {
    prompt: 'Pick a drafted arc', want: 'pick',
    pick: (hit) => (hit.shape.type === 'arc' ? arcLengthPlace(hit.id, hit.shape.c, again) : (cadLog('Pick an arc.'), arcLengthCommand(again))),
  };
}
/** An arc's length: where the dimension arc goes. */
function arcLengthPlace(id: string, centre: Vec2, again?: Again): Step {
  return { prompt: 'Where the dimension goes', want: 'point', base: centre, point: (at: Vec2) => addKind('arc', { shape: id, at: pair(at) }, again) };
}
/** DIMSLOPE: two points on the sloped line [Percent/Degrees/Ratio/Auto], then a point beside it. */
function slopeCommand(again?: Again): Step {
  let format = 'auto';
  const kw = (next: () => Step) => ({ P: () => { format = '%'; return next(); }, D: () => { format = 'deg'; return next(); }, R: () => { format = 'ratio'; return next(); }, A: () => { format = 'auto'; return next(); } });
  const first = (): Step => ({
    prompt: `First point on the slope [Percent/Degrees/Ratio/Auto: ${format}]`, want: 'point', keywords: kw(() => first()),
    point: (a) => ({
      prompt: 'Second point on it', want: 'point', base: a, preview: (q) => [{ type: 'line', a, b: q }],
      point: (b) => addKind('slope', { a: pair(a), b: pair(b), at: pair({ x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 }), format }, again),
    }),
  });
  return first();
}
/** DIMLEVEL: the point, then the side the text goes; a typed value is the level itself (in a plan: else the storey's). */
function levelCommand(again?: Again): Step {
  return { prompt: 'The point whose level is written', want: 'point', point: (a) => levelFrom(a, again) };
}
/** A level's point given: which side its text goes (a typed value is the level itself). */
function levelFrom(a: Vec2, again?: Again): Step {
  let z: number | null = null;
  const side = (): Step => ({
    prompt: `Which side the text goes${z != null ? ` (level ${formatLevel(z)})` : ' (type a level to write it)'}`, want: 'point', base: a,
    value: (v) => { z = v; return side(); },
    point: (at) => addKind('level', { a: pair(a), at: pair(at), ...(z != null ? { z } : {}) }, again),
  });
  return side();
}
/** DIMORDINATE: the point, then where its leader ends [X/Y: which coordinate; Origin: set it]. */
const ordinate = { origin: { x: 0, y: 0 } as Vec2 };
function ordinateCommand(again?: Again): Step {
  let axis: 'x' | 'y' = 'x';
  const step = (): Step => ({
    prompt: `The point [X/Y: ${axis}; Origin at ${formatLength(ordinate.origin.x)}, ${formatLength(ordinate.origin.y)}]`, want: 'point',
    keywords: { X: () => { axis = 'x'; return step(); }, Y: () => { axis = 'y'; return step(); }, O: () => ({ prompt: 'The origin', want: 'point', point: (o) => { ordinate.origin = o; return step(); } }) },
    point: (a) => ({ prompt: 'Where the leader ends', want: 'point', base: a, preview: (q) => [{ type: 'line', a, b: q }], point: (at) => addKind('ordinate', { a: pair(a), at: pair(at), axis, o: pair(ordinate.origin) }, again) }),
  });
  return step();
}
registerCommand({ names: ['DIMANGULAR', 'DAN'], label: 'Angular dimension', start: () => angularCommand() });
registerCommand({ names: ['DIMRADIUS', 'DRA'], label: 'Radius', start: () => roundCommand('radial') });
registerCommand({ names: ['DIMDIAMETER', 'DDI'], label: 'Diameter', start: () => roundCommand('diameter') });
registerCommand({ names: ['DIMARC', 'DAR'], label: 'Arc length', start: () => arcLengthCommand() });
registerCommand({ names: ['DIMSLOPE', 'DSL'], label: 'Slope', start: () => slopeCommand() });
registerCommand({ names: ['DIMLEVEL', 'DLV'], label: 'Level', start: () => levelCommand() });
registerCommand({ names: ['DIMORDINATE', 'DOR'], label: 'Ordinate', start: () => ordinateCommand() });

/**
 * DIMCOORD (decision 447): a spot coordinate on a plan, the point then where its leader ends [Map/Project/Internal:
 * what it reads]. Map by default when the project is tied to the map (eastings and northings through the survey
 * point, turned to true north), else the project base point's X and Y; worked out again whenever the points move.
 */
function coordinateCommand(again?: Again): Step | null {
  let system: 'map' | 'project' | 'internal' = surveyPoint(useLocation.getState().loc) ? 'map' : 'project';
  const names = { map: 'map E, N', project: 'base point X, Y', internal: 'plan origin X, Y' };
  const step = (): Step => ({
    prompt: `The point [Map/Project/Internal: ${names[system]}]`, want: 'point',
    keywords: {
      M: () => { if (surveyPoint(useLocation.getState().loc)) system = 'map'; else cadLog('The project has no survey point: Manage → Location ties it to the map.'); return step(); },
      P: () => { system = 'project'; return step(); },
      I: () => { system = 'internal'; return step(); },
    },
    point: (a) => ({ prompt: 'Where the leader ends', want: 'point', base: a, preview: (q) => [{ type: 'line', a, b: q }], point: (at) => addKind('coordinate', { a: pair(a), at: pair(at), system }, again) }),
  });
  if (useUi.getState().view !== 'plan') { cadLog('A spot coordinate is written on a plan: open one.'); return null; }
  return step();
}
registerCommand({ names: ['DIMCOORD', 'DCO'], label: 'Spot coordinate', start: () => coordinateCommand() });

// ─── The one Dimension tool (S9): the kind from what is clicked, the rest in its bar ───

/** A drafted shape's straight piece under a point: a line, or a polyline's side nearest it. */
export function segmentAt(shape: Shape, p: Vec2): { a: Vec2; b: Vec2 } | null {
  if (shape.type === 'line') return { a: shape.a, b: shape.b };
  if (shape.type !== 'polyline' || shape.pts.length < 2) return null;
  const n = shape.pts.length, sides = shape.closed ? n : n - 1;
  let best: { a: Vec2; b: Vec2 } | null = null, bd = Infinity;
  for (let i = 0; i < sides; i++) {
    const a = shape.pts[i], b = shape.pts[(i + 1) % n];
    const dx = b.x - a.x, dy = b.y - a.y, l2 = dx * dx + dy * dy || 1;
    const t = Math.max(0, Math.min(1, ((p.x - a.x) * dx + (p.y - a.y) * dy) / l2));
    const d = Math.hypot(a.x + dx * t - p.x, a.y + dy * t - p.y);
    if (d < bd) { bd = d; best = { a, b }; }
  }
  return best;
}

/** Where two lines (each through two points) cross; null when they are parallel. */
export function crossing(a1: Vec2, b1: Vec2, a2: Vec2, b2: Vec2): Vec2 | null {
  const d1 = { x: b1.x - a1.x, y: b1.y - a1.y }, d2 = { x: b2.x - a2.x, y: b2.y - a2.y };
  const den = d1.x * d2.y - d1.y * d2.x;
  if (Math.abs(den) < 1e-9 * (Math.hypot(d1.x, d1.y) * Math.hypot(d2.x, d2.y) || 1)) return null;
  const t = ((a2.x - a1.x) * d2.y - (a2.y - a1.y) * d2.x) / den;
  return { x: a1.x + d1.x * t, y: a1.y + d1.y * t };
}

/** A point on a side of an angle at `c`: the one clicked on it, or, clicked on the vertex, the side's end farther from it. */
export function sidePoint(c: Vec2, seg: { a: Vec2; b: Vec2 }, clicked: Vec2, tol: number): Vec2 {
  if (Math.hypot(clicked.x - c.x, clicked.y - c.y) > tol) return clicked;
  return Math.hypot(seg.a.x - c.x, seg.a.y - c.y) >= Math.hypot(seg.b.x - c.x, seg.b.y - c.y) ? seg.a : seg.b;
}

/** Whether the point clicked came from an object snap (an end, a middle, a centre, a crossing…), not from a bare place on a shape. */
const snappedClick = (): boolean => { const aim = useCad.getState().aim; return aim?.kind === 'point' && !!aim.mode && aim.mode !== 'nearest'; };

/**
 * A circle or an arc the click snapped to (its centre, a quadrant, a point on it): the smart tool takes the curve itself, as a
 * hover over a circle does in CAD (a dimension from a centre is the Aligned kind's).
 */
function roundSnapped(): { id: string; shape: Shape } | null {
  const aim = useCad.getState().aim;
  if (!aim?.shapeId || aim.kind !== 'point' || !aim.mode || !['center', 'quadrant', 'nearest', 'perpendicular'].includes(aim.mode)) return null;
  const shape = cadHost()?.shapeOf(aim.shapeId);
  return shape && (shape.type === 'circle' || shape.type === 'arc') ? { id: aim.shapeId, shape } : null;
}

/**
 * DIMENSION, smart: a point (snapped, or on nothing drafted) starts an aligned or linear dimension, as the bar says; a drafted line
 * (or a polyline's side) clicked away from its snaps is measured end to end, and a second line clicked next makes the angle
 * between them instead; a circle or an arc its radius, diameter or arc length, as the bar says; a level's line clicked in a
 * section or an elevation its spot elevation (audit §17 leftover), as Enter after the first point writes that point's level.
 * Each dimension made, it asks for the next one.
 */
function smartFirst(): Step {
  useDimTool.setState({ orient: null, forced: false, count: 0 });
  const linear = () => useSmartDim.getState().linear;
  return {
    prompt: `A point, a drafted line, circle or arc [Chain/Baseline/Single: ${stringMode()}]`, want: 'point',
    keywords: { ...modeWords(smartFirst), ...kindWords() },
    point: (p) => {
      const hit = roundSnapped() ?? (snappedClick() ? null : cadHost()?.pickDraft(p) ?? null);
      if (hit && (hit.shape.type === 'circle' || hit.shape.type === 'arc')) {
        const as = useSmartDim.getState().roundAs;
        if (as === 'arc' && hit.shape.type === 'arc') return arcLengthPlace(hit.id, hit.shape.c, smartFirst);
        return roundPlace(as === 'diameter' ? 'diameter' : 'radial', { shape: hit.id }, hit.shape.c, smartFirst);
      }
      const seg = hit ? segmentAt(hit.shape, p) : null;
      if (seg) return smartLine(seg, p, linear());
      // A level's line clicked in a section or an elevation (no object snap, nothing drafted there): its spot elevation.
      const level = !hit && !snappedClick() && cadHost()?.isPlan?.() === false ? cadHost()?.pickLevel?.(p) ?? null : null;
      if (level) return levelFrom({ x: p.x, y: level.y }, smartFirst);
      return dimFrom(p, linear(), smartFirst, () => levelFrom(p, smartFirst));
    },
  };
}

/** A line picked: where its dimension line goes, or a second line for the angle between them. */
function smartLine(seg: { a: Vec2; b: Vec2 }, clicked: Vec2, linear: boolean): Step {
  const plain = dimLine(seg.a, seg.b, linear, smartFirst);
  return {
    ...plain,
    prompt: 'Where the dimension line goes, or a second line for the angle',
    point: (q) => {
      const hit = snappedClick() ? null : cadHost()?.pickDraft(q) ?? null;
      const other = hit ? segmentAt(hit.shape, q) : null;
      const c = other && !(isSamePoint(other.a, seg.a, 1e-9) && isSamePoint(other.b, seg.b, 1e-9)) ? crossing(seg.a, seg.b, other.a, other.b) : null;
      if (other && c) return angleArc(c, sidePoint(c, seg, clicked, clickTol()), sidePoint(c, other, q, clickTol()), smartFirst);
      return plain.point!(q);
    },
  };
}

/** The kinds typed at the first point: the tool goes on in that kind. */
function kindWords(): Record<string, () => Step | null> {
  const words: Record<string, DimKind> = {
    AL: 'aligned', ALIGNED: 'aligned', LI: 'linear', LINEAR: 'linear', AN: 'angular', ANGLE: 'angular', RA: 'radial', RADIUS: 'radial',
    DI: 'diameter', DIAMETER: 'diameter', AR: 'arc', ARC: 'arc', OR: 'ordinate', ORDINATE: 'ordinate', SL: 'slope', SLOPE: 'slope',
    LE: 'level', LEVEL: 'level', CO: 'coordinate', COORDINATE: 'coordinate', SM: 'smart', SMART: 'smart',
  };
  return Object.fromEntries(Object.entries(words).map(([w, k]) => [w, () => { useSmartDim.setState({ kind: k }); return dimensionStart(); }]));
}

/** DIMENSION at the bar's kind: smart, or one kind's own steps, each asking for the next once made. */
function dimensionStart(): Step | null {
  const kind = useSmartDim.getState().kind;
  const again = () => dimensionStart();
  const words = { ...kindWords() };
  const withWords = (st: Step | null): Step | null => (st ? { ...st, keywords: { ...words, ...(st.keywords ?? {}) } } : st);
  switch (kind) {
    case 'smart': return smartFirst();
    case 'aligned': case 'linear': {
      const first = (): Step => {
        useDimTool.setState({ orient: null, forced: false, count: 0 });
        return { prompt: `First point [Chain/Baseline/Single: ${stringMode()}]`, want: 'point', keywords: { ...words, ...modeWords(first) }, point: (a) => dimFrom(a, kind === 'linear', first) };
      };
      return first();
    }
    case 'angular': return withWords(angularCommand(again));
    case 'radial': case 'diameter': return withWords(roundCommand(kind, again));
    case 'arc': return withWords(arcLengthCommand(again));
    case 'slope': return withWords(slopeCommand(again));
    case 'level': return withWords(levelCommand(again));
    case 'ordinate': return withWords(ordinateCommand(again));
    case 'coordinate': return withWords(coordinateCommand(again));
  }
}
registerCommand({ names: ['DIMENSION', 'DIM'], label: 'Dimension', start: dimensionStart, options: SmartDimensionOptions });

// ─── Linework (LW…): the view's generated lines hidden, shown or restyled, joined at corners ───

type LwMode = { action: 'hide' | 'show' | 'style'; style?: { pen?: number; color?: string; pattern?: string } };
const LW_STYLES: Record<string, LwMode> = {
  INVISIBLE: { action: 'hide' }, SHOW: { action: 'show' },
  THIN: { action: 'style', style: { pen: 1, pattern: 'solid' } }, MEDIUM: { action: 'style', style: { pen: 3, pattern: 'solid' } }, WIDE: { action: 'style', style: { pen: 5, pattern: 'solid' } },
  HIDDEN: { action: 'style', style: { pattern: 'hidden' } }, OVERHEAD: { action: 'style', style: { pattern: 'dashed' } },
  BEYOND: { action: 'style', style: { pen: 1, color: '#9ca3af' } }, CENTER: { action: 'style', style: { pattern: 'center' } }, DOTTED: { action: 'style', style: { pattern: 'dotted' } },
};
let lwMode = 'INVISIBLE';
let lwPen: number | null = null;

function lineworkStep(): Step {
  const mode = (): LwMode => (lwPen != null ? { action: 'style', style: { pen: lwPen } } : LW_STYLES[lwMode]);
  const step = (): Step => ({
    prompt: `Click the edges [${lwPen != null ? `pen ${lwPen}` : lwMode}: ${Object.keys(LW_STYLES).join('/')}; a number: a pen]`, want: 'select',
    keywords: Object.fromEntries(Object.keys(LW_STYLES).map((k) => [k, () => { lwMode = k; lwPen = null; return step(); }])),
    value: (n) => { if (n >= 1 && n <= 16) lwPen = Math.round(n); return step(); },
    point: async (p) => {
      const id = await frontView();
      if (id && id !== '3d') await execute('view.linework', { id, at: [p.x, p.y], ...mode() });
      return step();
    },
    enter: () => null,
  });
  return step();
}

function lineworkCorner(mode: 'join' | 'trim' | 'extend'): Step {
  const first = (): Step => ({
    prompt: mode === 'join' ? 'First edge' : mode === 'trim' ? 'The edge to cut at' : 'The edge to extend to', want: 'select',
    point: (a) => ({
      prompt: mode === 'join' ? 'Second edge' : mode === 'trim' ? 'The edge to cut (the side clicked goes)' : 'The edge to extend', want: 'select',
      point: async (b) => {
        const id = await frontView();
        if (id && id !== '3d') await execute('view.lineworkCorner', { id, first: [a.x, a.y], second: [b.x, b.y], mode });
        return first();
      },
    }),
    enter: () => null,
  });
  return first();
}

function lineworkReset(): Step {
  const step = (): Step => ({
    prompt: 'Click an edge to give it back its own lines [ALL: the whole view; ORPHANS: the edits that lost their edge]', want: 'select',
    keywords: {
      ALL: async () => { const id = await frontView(); if (id && id !== '3d') await execute('view.lineworkReset', { id, all: true }); return null; },
      ORPHANS: async () => { const id = await frontView(); if (id && id !== '3d') await execute('view.lineworkReset', { id, orphans: true }); return null; },
    },
    point: async (p) => { const id = await frontView(); if (id && id !== '3d') await execute('view.lineworkReset', { id, at: [p.x, p.y] }); return step(); },
    enter: () => null,
  });
  return step();
}

/** JOIN: lines and open polylines welded end to end into polylines (ends within 0.5 mm meet); a chain that comes back closes. */
registerCommand({ names: ['JOIN', 'J'], label: 'Join', start: () => withSelection(async (ids) => {
  const r = await execute('drafting.join', { ids }) as { joined?: number; pieces?: number } | null;
  cadLog(r?.joined ? `${r.joined} polyline${r.joined === 1 ? '' : 's'} from ${r.pieces} pieces.` : 'Nothing met end to end.');
  return null;
}) });

/** EXPLODE: polylines and rectangles split into lines, the way back from JOIN; circles, arcs, hatches and dimensions stay (report #121). */
registerCommand({ names: ['EXPLODE', 'X'], label: 'Explode', start: () => withSelection(async (ids) => {
  const r = await execute('drafting.explode', { ids }) as { exploded?: number; pieces?: number; kept?: number } | null;
  if (r) cadLog(`${r.exploded} shape${r.exploded === 1 ? '' : 's'} into ${r.pieces} lines${r.kept ? ` (${r.kept} left as they are)` : ''}.`);
  return null;
}) });

registerCommand({ names: ['LINEWORK', 'LW'], label: 'Linework', start: lineworkStep });
registerCommand({ names: ['LWJOIN', 'LWJ'], label: 'Linework join', start: () => lineworkCorner('join') });
registerCommand({ names: ['LWTRIM', 'LWTR'], label: 'Linework trim', start: () => lineworkCorner('trim') });
registerCommand({ names: ['LWEXTEND', 'LWEX'], label: 'Linework extend', start: () => lineworkCorner('extend') });
registerCommand({ names: ['LWRESET', 'LWR'], label: 'Linework reset', start: lineworkReset });

/** A CAD command started from the ribbon, as if typed. */
const cad = (id: string, label: string, icon: ToolDef['icon'], command: string): ToolDef => ({ id: `cad.${id}`, label, icon, run: () => void startCommand(command) });

/**
 * A drafted shape's grips, drawn in the view it is on: a line's ends and
 * middle; a polyline's vertices and edge middles (Ctrl/⌘ inserts or removes a
 * vertex); a circle's centre and quadrants; an arc's centre, ends and middle.
 */
function draftGrips(node: NodeDto): Grip[] {
  if (node.props.predefinedType !== 'DRAFT' || !node.props.shape) return [];
  const s = fromJson(node.props.shape as Record<string, unknown>);
  if (!s) return [];
  const view = typeof node.props.viewId === 'string' ? node.props.viewId : undefined;
  // A component's detail drawn here: its points as this view draws them, which the engine takes back to its own (forum #224).
  const save = (next: Shape): GripStep => ({ command: 'drafting.updateDraft', args: { id: node.id, shape: toJson(next), ...(typeof node.props.detailSet === 'string' && view ? { viewId: view } : {}) } });
  const on = (r: number, t: number): Vec2 => (s.type === 'arc' || s.type === 'circle' ? { x: s.c.x + Math.cos(t) * r, y: s.c.y + Math.sin(t) * r } : { x: 0, y: 0 });
  const grips: Grip[] = (() => {
    switch (s.type) {
      case 'line': return axisGrips(s.a, s.b, (a, b) => save({ ...s, a, b }));
      case 'polyline': {
        if (s.closed && s.pts.length >= 3) return outlineGrips({ pts: s.pts }, (o) => save({ ...s, pts: o.pts }));
        const out: Grip[] = s.pts.map((q, i) => ({
          key: `p${i}`, kind: 'vertex' as const, at: q,
          step: (p: Vec2) => save({ ...s, pts: s.pts.map((x, j) => (j === i ? p : x)) }),
          remove: () => (s.pts.length > 2 ? save({ ...s, pts: s.pts.filter((_, j) => j !== i) }) : null),
        }));
        for (let i = 0; i + 1 < s.pts.length; i++) {
          const a = s.pts[i], b = s.pts[i + 1], m = { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
          out.push({
            key: `e${i}`, kind: 'edgeMid', at: m,
            // The segment moved parallel to itself.
            step: (p: Vec2) => {
              const len = Math.hypot(b.x - a.x, b.y - a.y) || 1, n = { x: -(b.y - a.y) / len, y: (b.x - a.x) / len };
              const d = (p.x - m.x) * n.x + (p.y - m.y) * n.y;
              return save({ ...s, pts: s.pts.map((x, j) => (j === i || j === i + 1 ? { x: x.x + n.x * d, y: x.y + n.y * d } : x)) });
            },
            insert: () => save({ ...s, pts: [...s.pts.slice(0, i + 1), m, ...s.pts.slice(i + 1)] }),
          });
        }
        return out;
      }
      case 'circle': return [
        { key: 'c', kind: 'centre', at: s.c, step: (p: Vec2) => save({ ...s, c: p }) },
        ...keyPoints(s).quadrants.map((q, i) => ({ key: `q${i}`, kind: 'radius' as const, at: q, step: (p: Vec2) => save({ ...s, r: Math.max(1e-3, Math.hypot(p.x - s.c.x, p.y - s.c.y)) }) })),
      ];
      case 'arc': {
        const mid = s.start + (((s.end - s.start) % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI) / 2;
        const angle = (p: Vec2) => Math.atan2(p.y - s.c.y, p.x - s.c.x);
        return [
          { key: 'c', kind: 'centre', at: s.c, step: (p: Vec2) => save({ ...s, c: p }) },
          { key: 's', kind: 'end', at: on(s.r, s.start), step: (p: Vec2) => save({ ...s, start: angle(p) }) },
          { key: 'e', kind: 'end', at: on(s.r, s.end), step: (p: Vec2) => save({ ...s, end: angle(p) }) },
          { key: 'm', kind: 'radius', at: on(s.r, mid), step: (p: Vec2) => save({ ...s, r: Math.max(1e-3, Math.hypot(p.x - s.c.x, p.y - s.c.y)) }) },
        ];
      }
      case 'ellipse': {
        // Its centre moves it; the major axis's ends turn and stretch it; the minor axis's ends set its other semi-axis (audit S4).
        const a = Math.hypot(s.m.x, s.m.y);
        const major = (p: Vec2, sign: number) => ({ ...s, m: { x: (p.x - s.c.x) * sign, y: (p.y - s.c.y) * sign }, ratio: Math.min(1, (s.ratio * a) / Math.max(1e-3, Math.hypot(p.x - s.c.x, p.y - s.c.y))) });
        const minor = (p: Vec2) => {
          const n = { x: -s.m.y / a, y: s.m.x / a };
          const b = Math.abs((p.x - s.c.x) * n.x + (p.y - s.c.y) * n.y);
          return b > a ? { ...s, m: { x: n.x * b, y: n.y * b }, ratio: a / b, start: 0, end: 0 } : { ...s, ratio: Math.max(1e-3, b / a) };
        };
        const q = [0, 1, 2, 3].map((k) => ellipseAt(s, (k * Math.PI) / 2));
        return [
          { key: 'c', kind: 'centre', at: s.c, step: (p: Vec2) => save({ ...s, c: p }) },
          { key: 'q0', kind: 'radius', at: q[0], step: (p: Vec2) => save(major(p, 1)) },
          { key: 'q2', kind: 'radius', at: q[2], step: (p: Vec2) => save(major(p, -1)) },
          { key: 'q1', kind: 'radius', at: q[1], step: (p: Vec2) => save(minor(p)) },
          { key: 'q3', kind: 'radius', at: q[3], step: (p: Vec2) => save(minor(p)) },
        ];
      }
    }
  })();
  return grips.map((g) => withSketch(g, node.id, view, s));
}

/**
 * A point grip of a shape in a constrained sketch: dragging it asks the sketch's solver (`studio.dragSketch`, previewed live),
 * so what the constraints tie to the point follows, or the drag is refused with its reason. The constraints are read when the
 * drag drops, not when the grips are made.
 */
function withSketch(g: Grip, id: string, view: string | undefined, s: Shape): Grip {
  const part = sketchPartOf(s, g.key);
  if (!view || !part || !g.step) return { ...g, view };
  const plain = g.step;
  return {
    ...g, view, sketch: { viewId: view, shape: id, part },
    step: (to) => (viewHasConstraints(view) ? { command: 'studio.dragSketch', args: { viewId: view, shape: id, part, to: [to.x, to.y] } } : plain(to)),
  };
}

/**
 * A dimension string's grips, like a polyline's: a grip on each measured
 * point (dragged: the point goes there and holds on what it lands on; Ctrl/⌘:
 * taken out, two stay), one in each segment's middle on the dimension line
 * (dragged: the line goes through the point, as at its ends; Ctrl/⌘-drag: a new
 * point where it is dropped; Ctrl/⌘-click: one halfway), and the dimension
 * line's ends (dragged: the line goes through the point). They stand
 * where the view draws the points (on the elements they hold on), else where
 * they were placed.
 */
function dimensionGrips(node: NodeDto, view?: GripView): Grip[] {
  if (node.props.predefinedType !== 'DIMENSION' || node.props.dimKind) return [];
  const data = view?.mark?.data as { points?: unknown[]; line?: unknown[] } | undefined;
  const list = (raw: unknown): Vec2[] => (Array.isArray(raw) ? raw.map(pt).filter((p): p is Vec2 => !!p) : []);
  const stored = Array.isArray(node.props.pts) ? list(node.props.pts) : list([node.props.a, node.props.b]);
  const pts = data?.points && list(data.points).length === stored.length ? list(data.points) : stored;
  if (pts.length < 2) return [];
  const at = pt(node.props.at) ?? pts[0];
  const dir = pt(node.props.dir);
  const u = dir ? unit({ x: 0, y: 0 }, dir) : unit(pts[0], pts[pts.length - 1]);
  const n = { x: -u.y, y: u.x };
  const line = data?.line && list(data.line).length === pts.length ? list(data.line)
    : pts.map((p) => { const o = (at.x - p.x) * n.x + (at.y - p.y) * n.y; return { x: p.x + n.x * o, y: p.y + n.y * o }; });
  const id = node.id;
  // Its line dragged near a parallel dimension's snaps at the stacking distance from it (forum topic 236); Shift: free.
  const stack = {
    constrain: (to: Vec2, free: boolean): Vec2 => (free ? to : stackedAt(useSession.getState().nodes, id, to)?.at ?? to),
    guide: (at: Vec2) => {
      const nodes = useSession.getState().nodes;
      const s = stackedAt(nodes, id, at);
      return s && Math.hypot(s.at.x - at.x, s.at.y - at.y) < 1e-9 ? { ...s.guide, label: `${stackSpacingMm(nodes)} mm` } : null;
    },
  };
  const grips: Grip[] = pts.map((p, i) => ({
    key: `p${i}`, kind: 'vertex' as const, at: p, title: 'Drag: the point moves (and holds on what it lands on); Alt-click or Ctrl/⌘-click: take it out',
    altRemove: true,
    step: (to: Vec2) => ({ command: 'drafting.moveDimensionPoint', args: { id, index: i, point: [to.x, to.y] } }),
    remove: () => (pts.length > 2 ? { command: 'drafting.removeDimensionPoint', args: { id, index: i } } : null),
  }));
  for (let i = 0; i + 1 < pts.length; i++) {
    const m = { x: (line[i].x + line[i + 1].x) / 2, y: (line[i].y + line[i + 1].y) / 2 };
    const half = { x: (pts[i].x + pts[i + 1].x) / 2, y: (pts[i].y + pts[i + 1].y) / 2 };
    grips.push({
      key: `e${i}`, kind: 'edgeMid', at: m, title: 'Drag: the dimension line goes through the point · Ctrl/⌘-drag: a new point where it is dropped · Ctrl/⌘-click: one halfway',
      ...stack,
      step: (to: Vec2) => ({ command: 'drafting.moveDimension', args: { id, at: [to.x, to.y] } }),
      insertAt: (to: Vec2) => ({ command: 'drafting.insertDimensionPoint', args: { id, point: [to.x, to.y] } }),
      insert: () => ({ command: 'drafting.insertDimensionPoint', args: { id, index: i + 1, point: [half.x, half.y] } }),
    });
  }
  // Each segment's value: dragged, it moves off its place (to stand clear of a neighbour's); Ctrl/⌘-click: back on its line.
  const texts = Array.isArray((data as { texts?: unknown[] } | undefined)?.texts) ? ((data as { texts: unknown[] }).texts) : [];
  // A value the engine moved clear of its neighbours (forum #78): dragged, it goes where it is dropped, from where it is shown.
  const autoBy = Array.isArray((data as { textAuto?: unknown[] } | undefined)?.textAuto) ? ((data as { textAuto: unknown[] }).textAuto) : [];
  texts.forEach((raw, i) => {
    const at = pt(raw);
    if (!at) return;
    const auto = pt(autoBy[i]) ?? { x: 0, y: 0 };
    grips.push({
      key: `t${i}`, kind: 'move', at, title: 'Drag: move the value · Ctrl/⌘-click: back on its line',
      step: (to: Vec2) => ({ command: 'drafting.moveDimensionText', args: { id, segment: i, by: [to.x - at.x + auto.x, to.y - at.y + auto.y] } }),
      remove: () => ({ command: 'drafting.moveDimensionText', args: { id, segment: i, reset: true } }),
    });
  });
  // The dimension line's ends: dragged, the line goes through the point.
  const t = line.map((q) => (q.x - line[0].x) * u.x + (q.y - line[0].y) * u.y);
  for (const [key, s] of [['l0', Math.min(...t)], ['l1', Math.max(...t)]] as const) {
    grips.push({
      key, kind: 'end', at: { x: line[0].x + u.x * s, y: line[0].y + u.y * s }, title: 'Drag: the dimension line goes through the point · Ctrl/⌘-click: go on, the next clicks add points beyond it',
      ...stack,
      step: (to: Vec2) => ({ command: 'drafting.moveDimension', args: { id, at: [to.x, to.y] } }),
      extend: () => { select(id); void startCommand('DIMADD'); },
    });
  }
  const viewId = typeof node.props.viewId === 'string' ? node.props.viewId : undefined;
  return grips.map((g) => ({ ...g, view: viewId }));
}

/** An annotation's grips: a drafted shape's, a dimension string's, a tag's. */
/**
 * A note's leader (audit §16 leftovers): a grip on its arrow's tip and on each bend, drawn in the note's view (a sheet's paper
 * too); dragged, the point goes there (`drafting.moveLeaderPoint`, one undo step) and the note stays where it is.
 */
function leaderGrips(node: NodeDto): Grip[] {
  if (node.props.predefinedType !== 'TEXT' || !Array.isArray(node.props.leader)) return [];
  const view = typeof node.props.viewId === 'string' ? node.props.viewId : undefined;
  return (node.props.leader as unknown[]).flatMap((p, index) => {
    if (!Array.isArray(p) || typeof p[0] !== 'number' || typeof p[1] !== 'number') return [];
    const grip: Grip = {
      key: `leader${index}`, kind: index === 0 ? 'end' : 'vertex', at: { x: p[0], y: p[1] }, view,
      title: index === 0 ? "Drag to move the leader's arrow" : 'Drag to move the bend',
      step: (to: Vec2) => ({ command: 'drafting.moveLeaderPoint', args: { id: node.id, index, at: [to.x, to.y] } }),
    };
    return [grip];
  });
}

const annotationGrips = (node: NodeDto, view?: GripView): Grip[] => [...draftGrips(node), ...leaderGrips(node), ...dimensionGrips(node, view), ...tagGrips(node, view), ...tableGrips(node, view), ...keyGrips(node, view)];

/** A grid's axes: each axis's ends and middle (keys "<tag>:start"…), shown when that axis is selected (grid#tag). */
function gridGrips(node: NodeDto): Grip[] {
  const axes = (node.props.axes as { tag: string; a: number[]; b: number[] }[] | undefined) ?? [];
  return axes.flatMap((ax) => {
    const a = pt(ax.a), b = pt(ax.b);
    if (!a || !b) return [];
    return axisGrips(a, b, (s, e) => ({ command: 'drafting.updateAxis', args: { gridId: node.id, tag: ax.tag, x1: s.x, y1: s.y, x2: e.x, y2: e.y } })).map((g) => ({ ...g, key: `${ax.tag}:${g.key}` }));
  });
}
const gridRefs = (node: NodeDto): RefLine[] => ((node.props.axes as { tag: string; a: number[]; b: number[] }[] | undefined) ?? [])
  .flatMap((ax) => { const a = pt(ax.a), b = pt(ax.b); return a && b ? [{ ...axisRef(a, b), tag: ax.tag }] : []; });

/**
 * The one Tag tool (S9): TAG with its source first in its bar (an element, a room, a parameter, a material or layers, a keynote).
 * Tag a parameter (report #64) is its alias set to the parameter source; Door & window tags stays a button (all of a plan at once).
 */
const tagTool = cad('tag', 'Tag', Tag, 'TAG');
const tagParameter: ToolDef = { id: 'drafting.tagParameter', label: 'Tag a parameter', icon: Tag, run: () => { setTagSource('parameter'); void startCommand('TAG'); } };
const tagAliases: ToolDef[] = [tagParameter];
const tagOpeningsTool: ToolDef = { id: 'tagOpenings', label: 'Door & window tags', icon: Tags, run: () => void tagOpenings() };

/** The draw order of the selected drafting (report #44, decision 339): to the front and back as tools, a step either way and behind the model small. */
const orderTools: ToolDef[] = DRAW_ORDER.map((d) => ({ id: `drafting.order.${d.move}`, label: d.label, icon: d.icon, shortcut: d.shortcut, run: () => void drawOrder(d.move) }));
const orderGroup = { label: 'Order', tools: [orderTools[0], orderTools[3]], small: [orderTools[1], orderTools[2], orderTools[4]] };

/**
 * An image to trace, or an imported PDF's group, scaled true (decision 371; forum 143): with it selected, two points on it,
 * the distance between them shown as the second is picked and their true distance typed there (or asked for after a click);
 * it grows or shrinks about the first point (scaleImage.ts).
 */
const scaleImage: ToolDef = {
  id: SCALE_IMAGE_TOOL, label: 'Scale image', icon: Ruler,
  gesture: { kind: 'twoPoints', measure: true },
  complete: async ({ points: [a, b], typed }, ctx) => {
    const { nodes, relations } = useSession.getState();
    const id = scaleTarget(useUi.getState().selected, nodes, relations);
    if (!id) { notify('Select the image to scale first.'); return; }
    const length = trueDistance(typed, () => promptText('Their true distance apart (m)', (Math.hypot(b.x - a.x, b.y - a.y)).toFixed(3)), (s) => parseLength(s, 'm'));
    if (length == null) return;
    await ctx.execute('drafting.calibrateImage', { id, a: [a.x, a.y], b: [b.x, b.y], length });
    useUi.setState({ tool: null });
  },
};

/** The Dimension tool's kinds as the tools they were (S9): each starts it at its kind; ids, letters, quick slots and My tools keep working. */
const KIND_ICONS: Partial<Record<DimKind, ToolDef['icon']>> = {
  linear: MoveHorizontal, angular: AngleIcon, radial: RadiusIcon, diameter: DiameterIcon, arc: ArcLengthIcon, slope: SlopeIcon, ordinate: OrdinateIcon, coordinate: Crosshair, level: LevelIcon,
};
const kindTool = (kind: DimKind): ToolDef => {
  const k = DIM_KINDS.find((x) => x.kind === kind)!;
  return { id: k.tool!, label: k.label, icon: KIND_ICONS[kind] ?? DimensionIcon, ...(kind === 'level' ? { shortcut: 'LL' } : {}), run: () => startDimension(kind) };
};
/** A level mark (Datum → Level): the Dimension tool at its Level kind. */
const levelTool = kindTool('level');
const dimensionAliases: ToolDef[] = DIM_KINDS.filter((k) => k.tool && k.kind !== 'level').map((k) => kindTool(k.kind));
const dimAddTool = cad('dimAdd', 'Add points', ListPlus, 'DIMADD');
const dimDelTool = cad('dimDel', 'Remove points', ListMinus, 'DIMDEL');
// The Dimension tool's bar: the generated strings and a string's points, beside the kinds.
setDimensionExtras([
  { testId: 'rooms', label: 'Rooms', title: 'Room dimensions: strings inside the selected rooms, or every room of the plan (ID)', run: () => void roomDimensions.run?.() },
  { testId: 'exterior', label: 'Exterior', title: 'Exterior dimensions: strings outside the selected walls, or every facade of the plan (ED)', run: () => void exteriorDimensions.run?.() },
  { testId: 'add', label: 'Add points', title: 'Points clicked into a dimension string (DIMADD)', run: () => void startCommand('DIMADD') },
  { testId: 'remove', label: 'Remove points', title: 'Points taken out of a dimension string (DIMDEL)', run: () => void startCommand('DIMDEL') },
  { testId: 'rules', label: 'Rules…', title: 'Dimension rules: the strings of exterior and room dimensions', run: () => useUi.setState({ dialog: 'dimensionRules' }) },
]);

const drafting: LapinModule = {
  id: 'drafting',
  name: 'Drafting',
  summary: 'CAD drawing on views, annotations, grid axes, levels, hatches and sheets.',
  tabs: [
    {
      id: 'draft', label: 'Draft', order: 10,
      // Core's Modify group is this tab's Modify: its Move, Copy… are drawn once, Scale, Array and Erase stay, and the rest of core's (Pin, Match properties, groups) join them.
      include: ['modify'],
      groups: [
        { label: 'Draw', tools: [cad('line', 'Line', Slash, 'LINE'), cad('pline', 'Polyline', Spline, 'PLINE'), cad('rect', 'Rectangle', RectangleHorizontal, 'RECTANGLE'), cad('circle', 'Circle', Circle, 'CIRCLE'), cad('arc', 'Arc', Redo2, 'ARC'), cad('ellipse', 'Ellipse', Egg, 'ELLIPSE')] },
        { label: 'Modify', tools: [cad('move', 'Move', Move, 'MOVE'), cad('copy', 'Copy', Copy, 'COPY'), cad('rotate', 'Rotate', RotateCw, 'ROTATEREF'), cad('mirror', 'Mirror', FlipHorizontal2, 'MIRROR')], small: [cad('scale', 'Scale', Scaling, 'SCALE'), cad('array', 'Array', Grid3x3, 'ARRAY'), cad('erase', 'Erase', Eraser, 'ERASE')] },
        { label: 'Edit', tools: [cad('offset', 'Offset', CopyPlus, 'OFFSET'), cad('trim', 'Trim', Scissors, 'TRIM'), cad('extend', 'Extend', ArrowRightToLine, 'EXTEND'), cad('fillet', 'Fillet', CornerUpRight, 'FILLET')], small: [cad('chamfer', 'Chamfer', CornerUpRight, 'CHAMFER'), cad('join', 'Join', Link2, 'JOIN'), cad('explode', 'Explode', Unlink2, 'EXPLODE')] },
        { label: 'Fill', tools: [cad('hatch', 'Hatch', Hash, 'HATCH')], small: [revisionCloudTool] },
        { label: 'Symbol', tools: [cad('saveSymbol', 'Save as symbol', FolderPlus, 'SAVESYMBOL')], small: [cad('symbol', 'Symbol', Shapes, 'SYMBOL'), cad('saveHatch', 'Save as hatch pattern', Grid3x3, 'SAVEHATCH')] },
        orderGroup,
        { label: 'Linework', tools: [cad('linework', 'Linework', PenLine, 'LINEWORK')], small: [cad('lwJoin', 'Join linework', CornerUpRight, 'LWJOIN'), cad('lwTrim', 'Trim linework', Scissors, 'LWTRIM'), cad('lwExtend', 'Extend linework', ArrowRightToLine, 'LWEXTEND'), cad('lwReset', 'Reset linework', Eraser, 'LWRESET')] },
        // Drafted lines and shapes made into elements by the modules' converters (docs/plan-user-extensibility.md §7.4–7.5).
        { label: 'Convert', tools: [{ id: 'drafting.convert', label: 'Convert to…', icon: Replace, run: () => void convertSelection() }], small: [{ id: 'drafting.convertLayers', label: 'Convert layers…', icon: Layers, run: () => void convertLayersInFront() }] },
        { label: 'Layers', tools: [{ id: 'drafting.layers', label: 'Layers', icon: Layers, run: () => useUi.setState({ dialog: 'draftLayers' }) }] },
        { label: 'Import', tools: [{ id: 'drafting.importDxf', label: 'Import DXF', icon: FileInput, run: () => void importDxf() }],
          small: [{ id: 'drafting.importTrace', label: 'PDF or image', icon: ImageIcon, run: () => void importTrace() }, scaleImage] },
      ],
    },
    {
      id: 'annotate', label: 'Annotate', order: 60,
      groups: [
        // One Dimension tool (S9): the kinds are in its bar, the old buttons its aliases (legacyTools); the generated strings stay beside it.
        { label: 'Dimension', tools: [dimension, roomDimensions, exteriorDimensions],
          // Points clicked into a selected string, as ArchiCAD's Insert dimension point.
          small: [dimensionRulesTool, dimAddTool, dimDelTool, { id: 'drafting.tempDimensions', label: 'Temporary dimensions…', icon: Ruler, run: openTempDimProperties }] },
        // A drafting table (issue #71): cells, formulas, merges, on a view or a sheet.
        { label: 'Text', small: [textBox, leaderNote, tagOpeningsTool], tools: [text, tagTool, tableTool] },
        { label: 'Datum', small: [gridTool, renumberAxesTool, axisStylesTool, newLevelsTool], tools: [
          axis,
          levelTool,
        ] },
        // Symbols of the user's own (docs/plan-user-extensibility.md §3): placed with a ghost (R/Space turns, X and Y flip), made from drafting, managed and edited in Manage → Symbols….
        { label: 'Symbols', tools: [cad('symbol', 'Symbol', Shapes, 'SYMBOL'), cad('saveSymbol', 'Save as symbol', FolderPlus, 'SAVESYMBOL')], small: [cad('saveHatch', 'Save as hatch pattern', Grid3x3, 'SAVEHATCH'), { id: 'drafting.symbols', label: 'Symbols…', icon: SymbolsIcon, run: () => openPlaceCatalog('symbols') }] },
        orderGroup,
        // An element's own sheet (decision 581): its views from its own axes, chosen in a dialog with the engine's live preview.
        { label: 'Shop drawings', tools: [{
          id: 'shopDrawing', label: 'Shop drawing', icon: FileImage,
          run: () => {
            if (!drawableElement(useUi.getState().selected, useSession.getState().nodes)) { notify('Select one element of the model to make its shop drawing.'); return; }
            useUi.setState({ dialog: 'shopDrawing' });
          },
        }], small: [{
          // Identical parts share a mark (decision 583): the project's steel parts and every drawn part numbered, marks kept as they were.
          id: 'marks.number', label: 'Number parts', icon: ListOrdered,
          run: () => void execute('marks.number', {}).then(() => notify('The parts are numbered: identical parts share a mark.')),
        }] },
        // Drawing on a sheet itself (decision 620): its paper, a legend of its views, a scale bar, a revision cloud.
        { label: 'Sheets', small: [annotateSheetTool, legendTool, scaleBarTool, northArrowTool, revisionCloudTool], tools: [{ id: 'sheet', label: 'Sheet', icon: FileText, run: () => void execute('sheet.create', { name: 'Sheet', paper: 'A3' }).then((r) => { const id = (r as { id?: string } | null)?.id; if (id) useUi.setState({ view: 'sheet', sheetId: id, selection: id, selected: [id] }); }) }, viewBoundary] },
      ],
    },
  ],
  hotbar: [dimension],
  legacyTools: [...dimensionAliases, ...tagAliases, tablePartTool],
  dialogs: { convertDrafting: ConvertDialog, convertLayers: ConvertLayersDialog, draftLayers: LayersDialog, importDxf: ImportDxfDialog, importTrace: ImportTraceDialog, grid: GridDialog, renumberAxes: RenumberAxesDialog, dimensionRules: DimensionRulesDialog, tableEditor: TableEditor },
  propertySections: [
    // The drafted kinds show their own fields (a line its ends, a circle its radius), so the class's generic ones stay out.
    { id: 'draft', label: 'Drafting', classes: ['IfcAnnotation'], component: DraftSection, replacesFields: draftsFields },
    { id: 'dimension', label: 'Dimension', classes: ['IfcAnnotation'], component: DimensionSection },
    { id: 'symbol', label: 'Symbol', classes: ['IfcAnnotation'], component: SymbolSection },
    { id: 'table', label: 'Table', classes: ['IfcAnnotation'], component: TableSection },
    { id: 'viewBoundary', label: 'View boundary', classes: ['LapinViewBoundary'], component: BoundarySection },
    { id: 'grid', label: 'Axes', classes: ['IfcGrid'], component: GridSection },
    // An image or an imported PDF's group: scaled to a known distance from its properties too (forum 143).
    { id: 'scaleImage', label: 'Scale', classes: ['LapinViewImage', 'IfcGroup'], shows: scaleImageShows, component: ScaleImageSection },
  ],
  grips: { IfcAnnotation: annotationGrips, IfcGrid: gridGrips, LapinViewBoundary: boundaryGrips },
  // A selected grid axis's temporary dimensions to its neighbours (forum #78).
  planOverlays: [GridAxisDims, AngleLockMark],
  references: { IfcGrid: gridRefs },
};

export default drafting;
