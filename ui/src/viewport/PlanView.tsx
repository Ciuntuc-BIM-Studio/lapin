/**
 * The plan of one storey: the engine's plan items for it, drawn in SVG, and
 * the gestures the active tool needs (adapted from BubbleBIM Pro's plan
 * view). North is up; metres inside, mm shown. Every gesture ends in one
 * engine command. Hold Shift to square a line; Esc drops the gesture.
 */

import { membersOf, openGroupOf, pickTarget, shownSelection, useExpansion } from '../platform/groups';
import { drillInside, pickByMods, pickClick, pickRegion, useDrill, wholeOf } from '../platform/assemblies';
import { modsOf, pansIn2d, regionMode, regionSelection, wheelFactor, type Mods } from '../app/navStyle';
import { currentNavStyle } from '../app/navStyleStore';
import { orthophotos } from './textures';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Compass, FileDown, Footprints, Layers, Maximize, MousePointer2, PenLine, RotateCw } from 'lucide-react';
import { openContextMenu } from '../shell/ContextMenu';
import { selectionItems } from '../shell/selectionMenu';
import { viewHideItems } from '../shell/viewHideMenu';
import { viewSheetItems } from '../shell/browserFind';
import { dimensionItems } from '../modules/drafting/dimensionMenu';
import { noteAxisClick } from '../modules/drafting/gridDims';
import { symbolMenuItems } from '../modules/core/symbols/symbolTools';
import { drawOrderItems, isDrafted } from './drawOrder';
import { num } from '../engine/model';
import { useWalk, walkInto } from './walk';
import { notify, select, setTool, useUi } from '../app/store';
import { useViewTheme } from '../app/viewTheme';
import { usePaneActive, usePaneField } from '../app/pane';
import { activeStorey, execute, query, useSession } from '../engine/session';
import { ownPlanOf } from './storeyPlan';
import type { NodeDto, RenderItem } from '../engine/protocol';
import type { FaceHit, Vec2 } from '../platform/module';
import { askPlacement, findTool, useToolTick } from '../platform/modules';
import { gestureKey } from './gestureKeys';
import { setDrawnPath } from './drawnPath';
import { previousRef } from './angleDims';
import { placedAt, reasonLines, requestFor, resetPlacement, setPlacing, usePlacement, verdictFor } from '../platform/placement';
import { useGripEditor } from './GripLayer';
import { LiveBar, LiveBox } from './LiveBox';
import { liveBegin, liveCursor, liveEnd, livePoint, liveTaken, useLive } from './liveInput';
import { useHighlight } from '../app/highlight';
import { markSelected, nearestOf, pickAllAt, pickAt, regionIds } from './planPick';
import { clearCycle, cycledId, cycleOpen, setCycleSource, usePickCycle, withFirst, type CycleSource } from './pickCycle';
import { holdRefresh } from './tabHold';
import { autoholdOn, clickEnds, onAutoholdCancel, releaseEnds } from '../app/autohold';
import { markOwner } from './planPick';
import { boxOutcome, dragTo, regionTestOf, SelectRegion, useLassoKey, type SelectDrag } from './selectDrag';
import { pickable, useScope } from '../platform/scope';
import { pairs } from './snap';
import { formatLength, lengthText } from '../engine/unitsStore';
import { completeTool } from './tools';
import { drawsThroughInsets, insetUnder } from './insetMap';
import { DrawingLayer } from './DrawingLayer';
import { PlanOverlays } from './PlanOverlays';
import { useShallow } from 'zustand/react/shallow';
import { useDisplay } from '../app/display';
import { gridSnapStep, gridStepAt, usePrefs } from '../app/prefs';
import { exportFile } from '../engine/files';
import { basePoint, surveyPoint, fromFrame, isIdentity, setTrueNorth, toFrame, trueNorthTurn, turn, ucsOf, useLocation, useTrueNorth } from '../app/location';
import { UcsIcon } from './UcsIcon';
import { drawsOnDark, useDrawing } from './drawing';
import { setUnderlay, useUnderlay } from './underlay';
import { editDimension } from './dimensionEdit';
import { openPickedView } from './openView';
import { CadOverlay } from './cad/canvas';
import { snapShapes, useCadCanvas, type Aimed } from './cad/aim';
import { WALL_CLASSES, elementCurves, gridAxisShapes, wallAxes, wallAxisPoints } from './cad/elementSnaps';
import { clearShown, hiddenInDrawing, publishShown, shownItems, shownWholeOf } from './cad/shown';
import { CommandLine } from './cad/CommandLine';
import './cad/ucs';
import './cad/plan';
import { FOLLOW_UCS, normalizeDeg, orientationOf, screenAxes, screenCorners, setOrientation, turnOf } from './orientation';
import { ask, numberField } from '../shell/AskBox';
import { feedPoint, finishStep, startCommand, useCad } from './cad/session';
import { overridePress, spendOverride } from './cad/snapOverride';
import { tagTargets, trackTagHover, useTagHover, viewHasTags } from './tagTarget';
import { carryBegin, carryBy, carryEnd, carrying, useTrace } from './moveTrace';
import type { Shape } from './cad/shapes';
import { actualScale, boundsOf, coreBounds, framing, middleDouble, popHistory, pushHistory, recall, remember, useZoomHost, wheelGesture, wheelZoom, type Bounds, type View2, type ZoomHost } from './zoom';
import { ScaleChip, ScalePicker, StructurePicker } from './ScaleChip';
import { DASHES, planDetail, shownAt } from './planDetail';
import { useLineTypes } from '../modules/core/lineTypes/lineTypes';
import { axisLine, riseThrough } from './arc';
import { PICKED, arrivingDir, circleOutline, ellipseOutline, loopOf, pickOutline, rectangleOutline, tangentRise, useOutlineShape } from './outlineShape';
import { useLang } from '../i18n';
import { others, reportPresence, useShare } from '../engine/share';

/** The radius of the arc from a to b through p (Infinity: in line). */
function radiusOf(a: Vec2, b: Vec2, p: Vec2): number {
  const rise = riseThrough(a, b, p), c = Math.hypot(b.x - a.x, b.y - a.y);
  return rise === 0 ? Infinity : (c * c / 4 + rise * rise) / (2 * Math.abs(rise));
}
import { KeyPlan } from './KeyPlan';

type View = View2; // scale = px per metre
/** Pixels per metre: from a site of kilometres (a railway, a road) to a detail. */
const clampScale = (s: number) => Math.max(0.02, Math.min(s, 2000));
/** The grid's step for a zoom: the first of 1, 5, 25, 100, 500… m that stays 12 px apart or more on screen. */

const KIND_ORDER: Record<string, number> = { grid: 0, overhead: 1, ridge: 2, hip: 2, valley: 2, break: 2, outline: 3, cut: 4, symbol: 5, heavy: 5, label: 6 };

/** Into a room in 3D: the eye over its floor (its body's foot, else the storey's level), inside its outline (walk.ts). */
function walkIntoRoom(room: NodeDto, item: RenderItem, storeyElevation: number) {
  const outline = item.plan.find((p) => p.kind === 'room')?.points;
  if (!outline) return;
  let floor = Infinity;
  for (let k = 2; k < item.positions.length; k += 3) floor = Math.min(floor, item.positions[k]);
  const start = walkInto(room.name, outline, item.plan.filter((p) => p.kind === 'roomIsland').map((p) => p.points), Number.isFinite(floor) ? floor : storeyElevation);
  if (!start) return;
  useWalk.setState({ start });
  useUi.setState({ view: '3d' });
}

export function PlanView() {
  // The project's own line types are drawn with their dashes (they join the table the plan dashes by).
  useLineTypes();
  const svgRef = useRef<SVGSVGElement>(null);
  const [size, setSize] = useState({ w: 0, h: 0 });
  const [pts, setPts] = useState<Vec2[]>([]);
  // Reference point before placing the first point (forum #126): Space switches distance/angle and X/Y offsets.
  const [refPt, setRefPt] = useState<Vec2 | null>(null);
  const [pickRef, setPickRef] = useState(false);
  // A shaped outline's edges as drawn (forum topic 97): edge i's rise, from pts[i] to pts[i + 1].
  const rises = useRef<number[]>([]);
  useEffect(() => { rises.current = pts.length < 2 ? [] : rises.current.slice(0, pts.length - 1); }, [pts]);
  const [hover, setHover] = useState<Aimed | null>(null);
  /** What a click would select under the cursor now (the outline lit; null while a tool or a step takes the point). */
  const [hoverId, setHoverId] = useState<string | null>(null);
  const [faceHover, setFaceHover] = useState<{ at: Vec2; label: string; outline: Vec2[] } | null>(null);
  const faceReq = useRef(0);
  /** Where the cursor is, unsnapped: what a pick reads, and the side of a wall a door opens to. */
  const rawHover = useRef<Vec2 | null>(null);
  const cycleSource = useRef<CycleSource | null>(null);
  /** The pointer's last place on screen (client pixels), for what Tab let go takes. */
  const lastClient = useRef<{ x: number; y: number } | null>(null);
  // Space while a tool is on changes what its ghost shows.
  useToolTick((s) => s.n);
  usePlacement((s) => s.verdict); // a module's answer to whether a door or window fits where the cursor is
  const lang = useLang((s) => s.lang); // its reason, written by the cursor, in the language the interface speaks
  const [ortho, setOrtho] = useState(false);
  /** A press: where, the view then, whether it moved; `carry`: the selection it was pressed on (a drag moves it, or copies it with Ctrl), from `base` (the press, snapped). */
  const drag = useRef<{ x: number; y: number; view: View; moved: boolean; carry?: string[]; copy?: boolean; base?: Vec2; pan?: boolean } | null>(null);
  /** A selection box or lasso being dragged (screen px): left to right a window, right to left crossing; Space turns the box into a lasso. */
  const [box, setBox] = useState<SelectDrag | null>(null);
  useLassoKey(!!box, setBox);

  const render = useSession((s) => s.render);
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  useUi((s) => s.storeyId);
  const storey = activeStorey();
  const selected = useUi((s) => s.selected);
  // The others of a shared session (docs/plan-multiplayer.md): their selections and cursors on this storey.
  const participants = useShare((s) => s.participants);
  const welcome = useShare((s) => s.welcome);
  const peers = participants.length > 1 ? others(participants, welcome) : [];
  const toolId = useUi((s) => s.tool);
  const tool = findTool(toolId);
  useEffect(() => { if (tool?.gesture?.kind !== 'face') setFaceHover(null); }, [tool]);
  useEffect(() => { setRefPt(null); setPickRef(false); }, [tool?.id]);
  useEffect(() => { if (pts.length > 0) { setRefPt(null); setPickRef(false); } }, [pts.length]);
  const canRef = pts.length === 0 && !!tool?.gesture && (tool.gesture.kind === 'point' || tool.gesture.kind === 'twoPoints' || tool.gesture.kind === 'polyline' || tool.gesture.kind === 'polygon' || tool.gesture.kind === 'rectangle' || tool.gesture.kind === 'threePoints');
  // The generated drawing of this storey's plan (its own view, else the storey's), the modelling lines over it only where selected.
  const planViewId = usePaneField('planViewId');
  const active = usePaneActive();
  const planDrawing = useUi((s) => s.planDrawing);
  const onDark = drawsOnDark(useViewTheme());
  // The plan view open, else the level's own (its imported DXF and drafting are on it: a tester's report #113), else its default plan.
  const ownPlan = storey ? ownPlanOf(nodes, storey.id, planViewId) : null;
  const drawing = useDrawing(storey ? ownPlan ?? `plan:${storey.id}` : null, planDrawing);
  // A site plan or a location plan (docs/site-plans.md): the whole model from above, so no reference level or view range of its own.
  const siteView = ownPlan && typeof nodes.get(ownPlan)?.props.site === 'string' ? nodes.get(ownPlan)! : null;
  // Another storey beneath this one, faded: seen and snapped to, never picked.
  // The reference level: the plan's own view's (kept with the project), else the one remembered in this browser for the level.
  const remembered = useUnderlay((u) => (storey ? u.of[storey.id] : undefined));
  const ownUnder = ownPlan ? nodes.get(ownPlan)?.props.underlay : undefined;
  const underId = typeof ownUnder === 'string' ? ownUnder : ownPlan ? undefined : remembered;
  const under = underId && underId !== storey?.id && nodes.get(underId) && !(ownPlan && typeof nodes.get(ownPlan)?.props.site === 'string') ? underId : null;
  const underDrawing = useDrawing(under ? `plan:${under}` : null);
  // The view (centre, zoom) is remembered per plan for the session, so the tab comes back as it was left.
  const key = ownPlan ?? (storey ? `plan:${storey.id}` : 'plan:none');
  const [view, setView] = useState<View>(() => recall<View>(key) ?? { cx: 4, cy: 2, scale: 60 });
  // The view held is the key's only once it was recalled or framed for it: another plan's framing (a new plan view
  // opened from one zoomed in, 318 %) is never remembered as this one's (decision 399).
  const viewKey = useRef<string | null>(recall<View>(key) ? key : null);
  useEffect(() => { if (viewKey.current === key) remember(key, view); }, [key, view]);
  /** A zoom the user can come back from (ZP). */
  const go = useCallback((v: View) => { setView((old) => { pushHistory(key, old); return v; }); }, [key]);

  useEffect(() => {
    const el = svgRef.current;
    if (!el) return;
    const ro = new ResizeObserver(([e]) => setSize({ w: e.contentRect.width, h: e.contentRect.height }));
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  // A new tool starts a new gesture.
  const toolMode = tool?.mode?.();
  useEffect(() => { setPts([]); }, [toolId, toolMode]);

  // True north up (decision 447): the plan turned on screen by the angle to true north, chosen per plan; drawn,
  // snapped and picked through these two, so all of it turns together. Sheets and the PDF keep project north up.
  const northAngle = useLocation((st) => basePoint(st.loc).northAngle);
  // A plan view's own orientation (decision 526): kept with the view, printed as shown; it takes over from true north up.
  const ucsAngle = useLocation((st) => ucsOf(st.loc)?.angle ?? null);
  const orientationValue = ownPlan ? nodes.get(ownPlan)?.props.orientation : undefined;
  const orientation = orientationOf(orientationValue, ucsAngle == null ? null : { origin: { x: 0, y: 0 }, angle: ucsAngle });
  const oriented = Math.abs(orientation) > 1e-9;
  const northOn = useTrueNorth((st) => !!st.on[key]);
  const trueNorth = !oriented && northOn && Math.abs(northAngle) > 1e-9;
  const rot = oriented ? turnOf(orientation) : trueNorth ? trueNorthTurn(northAngle) : 0;
  /** The axes ortho, polar tracking and the grid follow in a turned plan with no UCS: the screen's. */
  const axes = useMemo(() => (oriented ? screenAxes(orientation) : null), [oriented, orientation]);
  const toScreen = useCallback((p: Vec2) => {
    const d = turn({ x: p.x - view.cx, y: p.y - view.cy }, rot);
    return { x: d.x * view.scale + size.w / 2, y: size.h / 2 - d.y * view.scale };
  }, [view, size, rot]);
  const toWorld = useCallback((x: number, y: number): Vec2 => {
    const d = turn({ x: (x - size.w / 2) / view.scale, y: (size.h / 2 - y) / view.scale }, -rot);
    return { x: d.x + view.cx, y: d.y + view.cy };
  }, [view, size, rot]);
  /** A screen move (pixels) as a move in the plan. */
  const screenDelta = (dx: number, dy: number, scale: number): Vec2 => turn({ x: dx / scale, y: -dy / scale }, -rot);
  /** Framing for points of the plan as the screen shows them (turned to true north or not). */
  const frameOn = useCallback((pts: Vec2[], pad: number, min: number, max: number): View => {
    const q = pts.map((p) => turn(p, rot));
    const v = framing([Math.min(...q.map((p) => p.x)), Math.min(...q.map((p) => p.y)), Math.max(...q.map((p) => p.x)), Math.max(...q.map((p) => p.y))], size, pad, min, max);
    const c = turn({ x: v.cx, y: v.cy }, -rot);
    return { ...v, cx: c.x, cy: c.y };
  }, [size, rot]);
  const corners = (b: Bounds): Vec2[] => [{ x: b[0], y: b[1] }, { x: b[2], y: b[1] }, { x: b[2], y: b[3] }, { x: b[0], y: b[3] }];

  // The shown storey only.
  // The storey's items, each with the plan items its plan's scale draws (a track: its axis at 1:2000, its rails at 1:500, every sleeper at 1:100).
  const detail = planDetail(drawing?.scale ?? 100);
  const items = useMemo(() => [...render.values()].filter((r) => r.storeyId === storey?.id)
    .map((r) => (r.plan.every((it) => shownAt(it, detail)) ? r : { ...r, plan: r.plan.filter((it) => shownAt(it, detail)) })), [render, storey?.id, detail]);
  // What the snaps and the selection take: only the elements the view's drawing shows (forum #31, decisions 491 and 498), all
  // of them over modelling lines; a part of a whole drawn as one keeps its own lines (drilling into assemblies).
  const snapItems = useMemo(() => shownItems(items, drawing, shownWholeOf(relations, (id) => wholeOf(id, nodes, relations))), [items, drawing, nodes, relations]);
  // Forum topic 151: with the Wall axis snap on, a wall's axis ends are its (named so, and taken after the drawn lines' ends and
  // crossings); off, they stay the endpoints they always were.
  const wallAxisOn = usePrefs((p) => p.snapModes.includes('wallAxis'));
  const snapPoints = useMemo(() => snapItems.flatMap((r) => {
    const own = pairs(r.snaps ?? []);
    if (!wallAxisOn || !WALL_CLASSES.has(nodes.get(r.nodeId)?.type ?? '')) return own;
    const ends = wallAxisPoints([r], nodes);
    return own.filter((q) => !ends.some((e) => Math.hypot(e.x - q.x, e.y - q.y) < 1e-9));
  }), [snapItems, wallAxisOn, nodes]);
  const snapWallAxes = useMemo(() => wallAxes(snapItems, nodes), [snapItems, nodes]);
  // The shown grid's axes (an IfcGrid's `axes`, each a line): the Grid axis snap and "Grid only" take them.
  const snapGridAxes = useMemo(() => snapItems.flatMap((r) => (nodes.get(r.nodeId)?.type === 'IfcGrid' ? gridAxisShapes(nodes.get(r.nodeId)?.props.axes) : [])), [snapItems, nodes]);
  const snapCurves = useMemo(() => elementCurves(snapItems, nodes), [snapItems, nodes]);
  // Marks with exact shapes or line segments are already parsed by snapShapes() with their endpoint/midpoint/centre kinds;
  // turning their flat snaps list into zero-length lines would label midpoints as endpoints (forum #111).
  const markSnaps = useMemo(() => (drawing?.marks ?? []).filter((m) => !(m as { shapes?: unknown[] }).shapes?.length && !m.segments?.length).flatMap((m) => pairs(m.snaps ?? [])), [drawing]);
  // What the tool on snaps to besides (the Pad footing tool: the columns of the storey above, decision 507).
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const toolSnaps = useMemo(() => tool?.snapPoints?.(storey?.id ?? null) ?? [], [tool, storey?.id, nodes, relations]);

  /** What the elements and the drawing's annotations span; null when the plan is empty. */
  const extent = useCallback((only?: Set<string>, clouds = true): Bounds | null => {
    const pts: Vec2[] = [];
    // A device's reach (a hydrant's 26 m) does not widen the frame.
    for (const r of items) if (!only || only.has(r.nodeId)) for (const it of r.plan) if (it.kind !== 'reach') for (let i = 0; i + 1 < it.points.length; i += 2) pts.push({ x: it.points[i], y: it.points[i + 1] });
    for (const m of drawing?.marks ?? []) if (!only || only.has(m.id)) for (let k = 0; k + 1 < m.segments.length; k += 2) pts.push({ x: m.segments[k], y: m.segments[k + 1] });
    // A point cloud's slice (decision 396) is what the plan shows of it.
    if (!only && clouds) for (const p of drawing?.pens ?? []) for (const img of p.images ?? []) if (img.id.startsWith('pointcloud:')) for (let k = 0; k + 1 < img.corners.length; k += 2) pts.push({ x: img.corners[k], y: img.corners[k + 1] });
    return boundsOf(pts);
  }, [items, drawing]);
  const fit = useCallback(() => {
    const b = extent();
    if (b) go(frameOn(corners(b), 0.8, 0.02, 400));
  }, [extent, frameOn, go]);

  /** Each element's, annotation's and point cloud slice's box, for the model's frame (decision 446). */
  const boxes = useCallback((clouds = true): Bounds[] => {
    const out: Bounds[] = [];
    const add = (pts: ArrayLike<number>) => { const b = boundsOf(pairs(pts)); if (b) out.push(b); };
    for (const r of items) { const pts: number[] = []; for (const it of r.plan) if (it.kind !== 'reach') for (let i = 0; i < it.points.length; i++) pts.push(it.points[i]); if (pts.length >= 2) add(pts); }
    for (const m of drawing?.marks ?? []) if (m.segments.length >= 2) add(m.segments);
    if (clouds) for (const p of drawing?.pens ?? []) for (const img of p.images ?? []) if (img.id.startsWith('pointcloud:')) add(img.corners);
    return out;
  }, [items, drawing]);
  const boxesRef = useRef(boxes);
  boxesRef.current = boxes;
  /** The model framed: its main body, what stands far from it left out and said so. */
  const fitModel = useCallback(() => {
    const c = coreBounds(boxes(false)) ?? coreBounds(boxes());
    if (!c) return;
    go(frameOn(corners(c.bounds), 0.8, 0.02, 400));
    if (c.left) notify(`Left out of the frame, far from the model: ${c.left}. ZE frames everything.`);
  }, [boxes, frameOn, go]);

  // A different storey: its remembered view, else frame it. Only then: an edit or a
  // resize keeps the view, so the effect reads the current fit through a ref, not as a dependency.
  const measured = size.w > 0 && size.h > 0;
  const fitRef = useRef(fit);
  fitRef.current = fit;
  /** The framing made by opening the plan, while the user has not moved it: framed again when the drawing comes. */
  const autoFit = useRef<{ key: string; view: View; drawn: boolean } | null>(null);
  const extentRef = useRef(extent);
  extentRef.current = extent;
  const frameNow = useCallback((): View => {
    // The model first; a point cloud's slice only on a plan with nothing else (an airborne scan is a whole district).
    const c = coreBounds(boxesRef.current(false)) ?? coreBounds(boxesRef.current());
    return c ? frameOn(corners(c.bounds), 0.8, 0.02, 400) : { cx: 4, cy: 2, scale: 60 };
  }, [drawing, frameOn]);
  const frameRef = useRef(frameNow);
  frameRef.current = frameNow;
  const drawingRef = useRef(drawing);
  drawingRef.current = drawing;
  useEffect(() => {
    if (!measured) return;
    const r = recall<View>(key);
    viewKey.current = key;
    if (r) { autoFit.current = null; setView(r); return; }
    const v = frameRef.current();
    autoFit.current = { key, view: v, drawn: !!drawingRef.current };
    setView(v);
  }, [key, measured]);
  // The plan's drawing come after it was framed (a point cloud's slice, the annotations): framed again, unless moved since.
  useEffect(() => {
    const a = autoFit.current;
    if (!a || a.key !== key || a.drawn || !drawing || !measured) return;
    if (view !== a.view) { autoFit.current = null; return; }
    const v = frameRef.current();
    autoFit.current = { key, view: v, drawn: true };
    setView(v);
  }, [drawing, key, measured, view]);

  // The zoom commands and keys, while this pane is in front.
  const host: ZoomHost = {
    fitAll: fit,
    fitModel,
    fitSelection: () => { const b = extent(new Set(selected)); if (!b) return false; go(frameOn(corners(b), 0.6, 0.02, 1000)); return true; },
    // The window's two corners as dragged on screen: on a turned plan its box is the screen's, not the plan's.
    zoomWindow: (a, b) => go(frameOn([a, b], 0.95, 0.02, 2000)),
    actual: () => go({ ...view, scale: clampScale(actualScale(drawing?.scale ?? 100)) }),
    zoomBy: (f) => go({ ...view, scale: clampScale(view.scale * f) }),
    previous: () => { const p = popHistory<View>(key); if (!p) return false; setView(p); return true; },
  };
  useZoomHost(active, host);
  const middleTwice = useRef(middleDouble());
  const wheelBurst = useRef(0);
  const wheelMode = useRef(wheelGesture());

  // ─── Picking ─────────────────────────────────────────────────────────────

  // In an edit scope only its elements are picked (the rest stays to snap to).
  const scope = useScope((s) => s.scope);
  // What the selection draws highlighted: a group's members with it.
  const expansion = useExpansion((s) => s.version);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const shown = useMemo(() => shownSelection(selected, nodes, relations), [selected, nodes, relations, expansion]);
  const shownList = useMemo(() => [...shown], [shown]);
  // What the selected or hovered tags tag, and what the TAG command hovers (report #130).
  const tagHover = useTagHover();
  const targets = useMemo(() => tagTargets(selected, nodes, tagHover), [selected, nodes, tagHover]);
  const targetList = useMemo(() => [...targets], [targets]);
  const anyTags = useMemo(() => viewHasTags(nodes, ownPlan ?? (storey ? `plan:${storey.id}` : null)), [nodes, ownPlan, storey]);
  // Drilled into a whole (platform/assemblies.ts): what is outside it dimmed, as an edit scope's is (more lightly).
  const drillPath = useDrill((st) => st.path);
  const drillOn = useDisplay((st) => st.drillSelect);
  const inside = useMemo(() => (drillOn && !scope ? drillInside(drillPath, selected, nodes, relations) : null), [drillOn, scope, drillPath, selected, nodes, relations]);
  const pickItems = useMemo(() => (scope ? snapItems.filter((r) => pickable(nodes.get(r.nodeId))) : snapItems), [scope, snapItems, nodes]);
  // What this plan hides, for Properties' note on an element selected from a list (decision 498).
  const paneKey = useRef(`plan:${Math.random().toString(36).slice(2)}`).current;
  useEffect(() => {
    if (!active) { clearShown(paneKey); return; }
    publishShown(paneKey, (id) => hiddenInDrawing(id, drawing, (x) => render.has(x), shownWholeOf(relations, (x) => wholeOf(x, nodes, relations))));
  }, [active, drawing, render, nodes, relations, paneKey]);
  useEffect(() => () => clearShown(paneKey), [paneKey]);
  // Tab over several elements lights the next one, and a click takes it (forum topic 105: pickCycle.ts).
  const cycleKey = useRef(`plan-cycle:${Math.random().toString(36).slice(2)}`).current;
  const pick = useCallback((p: Vec2, types?: string[]): NodeDto | null => {
    {
      const id = cycledId(usePickCycle.getState().cycle, cycleKey, p, 3 / view.scale);
      const hit = id ? markOwner(id, nodes) : null;
      if (hit && (!types || types.includes('*') || types.includes(hit.owner.type))) return hit.pick === hit.owner.id ? hit.owner : { ...hit.owner, id: hit.pick, name: `Axis ${hit.pick.slice(hit.pick.indexOf('#') + 1)}` };
    }
    return pickAt(p, 6 / view.scale, pickItems, nodes, scope ? undefined : drawing?.marks, types);
  }, [pickItems, nodes, view.scale, drawing, scope, cycleKey]);

  // ─── Gestures ────────────────────────────────────────────────────────────

  // The modelling lines as shapes to snap to (the drawing's own lines come from the canvas).
  const extra = useMemo(() => {
    const o: Shape[] = [];
    for (const r of snapItems) for (const it of r.plan) {
      if (it.kind === 'label' || it.kind === 'hit' || it.kind === 'grid' || it.kind === 'reach') continue;
      const ps = pairs(it.points);
      for (let i = 0; i + 1 < ps.length + (it.closed ? 1 : 0); i++) o.push({ type: 'line', a: ps[i], b: ps[(i + 1) % ps.length] });
    }
    for (const q of [...snapPoints, ...markSnaps, ...toolSnaps]) o.push({ type: 'line', a: q, b: q });
    if (underDrawing) o.push(...snapShapes(underDrawing).shapes);
    return o;
  }, [snapItems, snapPoints, markSnaps, toolSnaps, underDrawing]);
  const planViewOf = storey ? ownPlan ?? `plan:${storey.id}` : null;
  // The plan's scale: its view's (shown with modelling lines too, when no drawing is asked for), else its drawing's.
  const shownScale = Number((ownPlan ? nodes.get(ownPlan)?.props.scale : undefined) ?? drawing?.scale ?? 100);
  /** An element's modelling lines on this plan, as shapes (what MOVE and a dragged grip leave as the ghost and carry). */
  const outlines = useCallback((id: string): Shape[] => {
    const r = items.find((x) => x.nodeId === id);
    if (!r) return [];
    const o: Shape[] = [];
    for (const it of r.plan) {
      if (it.kind === 'label' || it.kind === 'hit' || it.kind === 'grid') continue;
      const ps = pairs(it.points);
      if (ps.length >= 2) o.push({ type: 'polyline', pts: ps, closed: it.closed });
    }
    return o;
  }, [items]);
  const { aim, acquire } = useCadCanvas({
    viewId: planViewOf, drawing, extra, scale: view.scale, pickAny: (p) => pick(p)?.id ?? null, outlines,
    inRegion: (takes) => regionIds(takes, pickItems, nodes, scope ? undefined : drawing?.marks), axes,
    wallAxes: snapWallAxes, gridAxes: snapGridAxes, curves: snapCurves,
  });
  const cadStep = useCad((s) => s.step);
  /** The selection dragged by its body (its trace shows over the plan). */
  const dragTrace = useTrace((s) => s.trace?.owner === 'drag');

  const snapAt = useCallback((clientX: number, clientY: number, from?: Vec2): Aimed => {
    const rect = svgRef.current!.getBoundingClientRect();
    const raw = toWorld(clientX - rect.left, clientY - rect.top);
    const a = aim(raw, from ?? null, { grid: gridSnapStep(view.scale), orthoHeld: ortho });
    useCad.setState({ cursor: a.point, aim: a });
    liveCursor(a.point);
    return a;
  }, [toWorld, view.scale, aim, ortho]);

  /** A tool's own rule for its next point (a pipe's material), over the snap. */
  const constrained = (a: Aimed): Aimed => {
    const g = tool?.gesture;
    if ((g?.kind !== 'polyline' && g?.kind !== 'threePoints') || !g.constrain || pts.length === 0) return a;
    const p = g.constrain(pts, a.point);
    return p.x === a.point.x && p.y === a.point.y ? a : { ...a, point: p, kind: 'free', guides: [], lock: undefined };
  };

  /** The start of the piece a chain tool drew last (its next piece's angle is measured from it); none when the chain starts again. */
  const chainPrev = useRef<Vec2 | null>(null);
  useEffect(() => { if (pts.length === 0) chainPrev.current = null; }, [pts.length]);
  useEffect(() => { chainPrev.current = null; }, [toolId]);

  /** The element a pick-then-point gesture took with its first click. */
  const picked = useRef<NodeDto | undefined>(undefined);
  useEffect(() => { picked.current = undefined; }, [toolId]);

  const complete = useCallback(async (points: Vec2[], entity?: NodeDto, face?: FaceHit, typed?: number, arcs?: number[], ellipses?: unknown[]) => {
    if (!tool) return;
    picked.current = undefined;
    // An annotation drawn over an inset view goes on its source, in its own coordinates (decision 576); the model's tools stay on the plan.
    const inset = drawsThroughInsets(tool.id) ? insetUnder(drawing?.marks, points) : null;
    if (inset) await completeTool(tool, { points: points.map(inset.map), entity, typed, ...(rot ? { rotation: rot } : {}) }, inset.source);
    else await completeTool(tool, { points, entity, face, typed, ...(rot ? { rotation: rot } : {}), ...(arcs?.some((r) => Math.abs(r) > 1e-6) ? { arcs } : {}), ...(ellipses?.some(Boolean) ? { ellipses } : {}) });
    // A chain goes on from the last point: the piece just drawn is what the next one's angle is measured from (decision 738).
    if (tool.gesture?.kind === 'twoPoints' && tool.gesture.repeat) { chainPrev.current = points.length >= 2 ? points[points.length - 2] : null; setPts([points[points.length - 1]]); }
    else setPts([]);
  }, [tool, drawing, rot]);

  /** The rise of the edge from the last point to q: an arc going on tangent from the edge before (straight after the first point). */
  const nextRise = (q: Vec2): number => {
    const dir = arrivingDir(pts, rises.current);
    return dir ? tangentRise(pts[pts.length - 1], dir, q) : 0;
  };
  /** The drawn polygon's rises, its closing edge's last. */
  const polygonArcs = (closing: number): number[] => [...pts.slice(0, -1).map((_, i) => rises.current[i] ?? 0), closing];

  const click = useCallback((p0: Vec2, raw: Vec2, add: boolean, typed0?: number, leaf = false, mods?: Mods) => {
    // A typed length, angle, width or height holds: the click gives the direction only, as the preview showed.
    const taken = liveTaken(p0);
    const p = taken?.point ?? p0;
    const typed = typed0 ?? taken?.typed;
    // A drafting command takes the click: a snapped point, or what lies under the cursor.
    const step = useCad.getState().step;
    if (step) { void feedPoint(step.want === 'pick' || step.want === 'select' ? raw : p, undefined, step.snap ? p : undefined); acquire(p); return; }
    if (tool?.gesture) acquire(p);
    if (!tool?.gesture) {
      // A member of a group picks the group (docs/groups.md); drilling, a part its wholes first (platform/assemblies.ts).
      const hitId = pick(raw)?.id;
      noteAxisClick(hitId, raw);
      if (mods) pickByMods(hitId, mods, currentNavStyle()); else pickClick(hitId, { add, leaf });
      return;
    }
    const g = tool.gesture;
    switch (g.kind) {
      case 'point': void complete([p]); break;
      case 'pick': {
        const e = g.reach ? nearestOf(raw, g.reach / view.scale, pickItems, nodes, g.types) : pick(raw, g.types);
        // A door or window a module refused where the cursor is goes to the place it offered, when that is near.
        const what = e && g.placing ? g.placing(e, raw) : null;
        const q = e && what ? requestFor(e, raw, what) : null;
        const to = q ? placedAt(q.arc, raw, verdictFor(q.req)).point : raw;
        if (e) void complete([to], e);
        else useSession.setState({ error: 'Pick an element this tool works on.' });
        break;
      }
      case 'face': {
        const e = pick(raw, g.types);
        if (e) void complete([raw], e, { point: [raw.x, raw.y] });
        else useSession.setState({ error: 'Pick an element this tool works on.' });
        break;
      }
      case 'pickThenPoint': {
        // The first click picks the element and is the first point; the second is where the cursor is (or the typed length).
        if (pts.length === 0) {
          const e = pick(raw, g.types);
          if (e) { picked.current = e; setPts([raw]); }
          else useSession.setState({ error: 'Pick an element this tool works on.' });
        } else void complete([pts[0], p], picked.current, undefined, typed);
        break;
      }
      case 'twoPoints':
      case 'rectangle':
        if (pts.length === 0) setPts([p]);
        // A measuring gesture hands on the true distance typed (Scale image, forum 143).
        else void complete([pts[0], p], undefined, undefined, g.kind === 'twoPoints' && g.measure ? typed : undefined);
        break;
      case 'threePoints':
        if (pts.length < 2) setPts([...pts, p]);
        else void complete([pts[0], pts[1], p]);
        break;
      case 'polygon': {
        // A shaped outline (forum topic 97): a rectangle, a circle, inside walls, or a polygon whose edges may be arcs.
        const shaped = g.shapes ? useOutlineShape.getState() : null;
        // Or taken whole with a click (audit S4): inside the walls, from a closed drafted shape, from a room.
        if (shaped && PICKED.includes(shaped.shape)) {
          void pickOutline(shaped.shape, p, storey?.id ?? null, useCad.getState().host?.viewId ?? ownPlan ?? null).then((o) => {
            if (typeof o === 'string') notify(o);
            else void complete(o.pts, undefined, undefined, undefined, o.arcs, o.ellipses);
          });
          break;
        }
        // An ellipse (audit S4): its centre, the end of one semi-axis, then the other semi-axis.
        if (shaped?.shape === 'ellipse') {
          if (pts.length < 2) { setPts([...pts, p]); break; }
          const e = ellipseOutline(pts[0], pts[1], p);
          if (e) void complete(e.pts, undefined, undefined, undefined, undefined, e.ellipses);
          break;
        }
        if (shaped?.shape === 'rectangle' || shaped?.shape === 'circle') {
          if (!pts.length) { setPts([p]); break; }
          if (shaped.shape === 'rectangle') void complete(rectangleOutline(pts[0], p));
          else { const c = circleOutline(pts[0], p); if (c) void complete(c.pts, undefined, undefined, undefined, c.arcs); }
          break;
        }
        const first = pts[0];
        const rise = shaped?.arcs ? nextRise(p) : 0;
        if (first && pts.length >= 3 && Math.hypot(first.x - p.x, first.y - p.y) < 8 / view.scale) void complete(pts, undefined, undefined, undefined, polygonArcs(shaped?.arcs ? nextRise(first) : 0));
        else { if (pts.length) rises.current = [...rises.current.slice(0, pts.length - 1), rise]; setPts([...pts, p]); }
        break;
      }
      case 'polyline': {
        const last = pts[pts.length - 1];
        if (last && pts.length >= 2 && Math.hypot(last.x - p.x, last.y - p.y) < 8 / view.scale) void complete(pts);
        else setPts([...pts, p]);
        break;
      }
    }
  }, [tool, pts, pick, complete, view.scale, acquire]);

  useEffect(() => {
    if (!active) return;
    const onKey = (e: KeyboardEvent) => {
      if ((e.target as HTMLElement)?.closest('input, select, textarea')) return;
      if (e.key === 'Shift') setOrtho(true);
      // Esc while the selection is dragged by its body: nothing moves.
      if (e.key === 'Escape' && carrying()) { drag.current = null; carryEnd(); return; }
      // What the key router took (a typed length committed or cleared, the command line) or a CAD step's: not the gesture's.
      if (e.defaultPrevented || useCad.getState().step) return;
      // One rule (gestureKeys.ts): Enter finishes, Esc cancels the step in progress. A typed length is the key router's (Enter takes it, Esc clears it).
      const act = gestureKey(e.key, tool?.gesture, pts.length, !!picked.current);
      if (act === 'completePick') {
        const l = useLive.getState();
        if (l.owner === 'tool' && (l.len || l.ang || l.rel)) return;
        void complete([pts[0]], picked.current!);
        return;
      }
      // A reference point being set or picked (forum #126): Esc drops it before the gesture's own rule.
      if (e.key === 'Escape' && pickRef) { setPickRef(false); return; }
      if (e.key === 'Escape' && refPt) { setRefPt(null); return; }
      if (act === 'clear') { setPts([]); picked.current = undefined; }
      else if (act === 'exit' || (e.key === 'Escape' && !tool?.gesture)) setTool(null);
      else if (act === 'complete' && tool?.gesture?.kind === 'polygon') void complete(pts, undefined, undefined, undefined, polygonArcs(tool.gesture.shapes && useOutlineShape.getState().arcs ? nextRise(pts[0]) : 0));
      else if (act === 'complete') void complete(pts);
    };
    const onUp = (e: KeyboardEvent) => { if (e.key === 'Shift') setOrtho(false); };
    window.addEventListener('keydown', onKey);
    window.addEventListener('keyup', onUp);
    return () => { window.removeEventListener('keydown', onKey); window.removeEventListener('keyup', onUp); };
  }, [active, pts, tool, complete, refPt, pickRef]);

  // A tool drawing from a point (or from a reference point before the first point, forum #126): its length/angle or X/Y offsets can be typed.
  const live = useLive();
  const clickRef = useRef(click);
  clickRef.current = click;
  const last = pts[pts.length - 1];
  useEffect(() => {
    const k = tool?.gesture?.kind;
    const base = last ?? refPt;
    if (base && (k === 'point' || k === 'twoPoints' || k === 'polyline' || k === 'polygon' || k === 'rectangle' || k === 'threePoints' || k === 'pickThenPoint')) {
      // The angle to the piece before (decision 738): a polyline's previous point, a chain tool's last piece's start.
      const prev = !last ? null : k === 'polyline' || k === 'polygon' ? pts[pts.length - 2] : k === 'twoPoints' ? chainPrev.current : null;
      const ref = previousRef(last ?? base, prev);
      liveBegin('tool', base, (p, typed) => { if (!last && refPt) setRefPt(null); clickRef.current(p, p, false, typed); },
        k === 'rectangle' && last ? { rect: tool?.id ?? 'rectangle' } : !last && refPt ? { rect: `ref:${tool?.id ?? 'point'}`, delta: true }
          : last && tool?.gesture?.kind === 'twoPoints' && tool.gesture.measure ? { measure: true }
          : last && (k === 'twoPoints' || k === 'polyline' || k === 'polygon') ? { angles: true, ...(ref ? { ref } : {}) } : {});
    } else liveEnd('tool');
  }, [last, refPt, tool]);
  useEffect(() => () => liveEnd('tool'), []);
  // A plan that goes while the selection is dragged takes the drag's trace with it.
  useEffect(() => () => { if (carrying()) carryEnd(); }, []);
  // Esc in autohold (issue 86), from any focus: the selection is not moved.
  useEffect(() => onAutoholdCancel(() => { if (carrying()) { drag.current = null; carryEnd(); } }), []);

  // Direct editing: the selection's axes, outlines and grips (none while a tool or a command is busy).
  const editor = useGripEditor({
    viewIds: [ownPlan, storey ? `plan:${storey.id}` : null], mode: 'plan', storeyId: storey?.id, toScreen, scale: view.scale,
    aim: (cx, cy, from) => { const s = snapAt(cx, cy, from); setHover(s); return s.point; },
    raw: (cx, cy) => { const r = svgRef.current!.getBoundingClientRect(); return toWorld(cx - r.left, cy - r.top); },
    disabled: !!tool?.gesture || !!cadStep, svg: svgRef, marks: drawing?.marks,
  });
  // What Tab cycles through here: every element under the pointer (also while a tool or a command picks elements; nothing while it waits for a point or a drag is on, so Tab keeps its meaning).
  cycleSource.current = {
    key: cycleKey,
    at: () => rawHover.current,
    ids: (at) => {
      if (!cycleOpen(tool?.gesture?.kind, cadStep?.want, !!(drag.current || editor.dragging))) return [];
      const types = tool?.gesture && 'types' in tool.gesture ? tool.gesture.types : undefined;
      const all = withFirst(pickAt(at, 6 / view.scale, pickItems, nodes, scope ? undefined : drawing?.marks, types)?.id, pickAllAt(at, 6 / view.scale, pickItems, nodes, scope ? undefined : drawing?.marks));
      return types && !types.includes('*') ? all.filter((id) => { const n = nodes.get(id.includes('#') ? id.slice(0, id.indexOf('#')) : id); return !!n && types.includes(n.type); }) : all;
    },
    tol: () => 3 / view.scale,
    // Tab held, then let go (tabHold.ts): what is lit is taken as the click does, at the pointer where it stands.
    take: () => {
      const c = lastClient.current, raw = rawHover.current;
      if (!c || !raw) return;
      const snapped = snapAt(c.x, c.y, pts[pts.length - 1] ?? refPt ?? useCad.getState().step?.base ?? undefined);
      click(constrained(snapped).point, raw, false, undefined, false, { shift: false, ctrl: false, alt: false });
    },
  };
  const cycle = usePickCycle((c) => c.cycle);
  useEffect(() => {
    // Tab lit another candidate: it takes the pre-highlight (the next move of the pointer gives it back to what is under it).
    if (!cycle || cycle.key !== cycleKey) return;
    const hit = pick(cycle.at);
    const id = hit ? pickTarget(hit.id) ?? hit.id : null;
    setHoverId((cur) => (cur === id ? cur : id));
  }, [cycle, cycleKey, pick]);
  useEffect(() => () => setCycleSource(null, cycleKey), [cycleKey]);

  /** A body drag held by autohold (issue 86) ends at a left click: where the pointer is, its snap, or the typed values. */
  const endHeldCarry = (e: React.PointerEvent) => {
    const d = drag.current;
    drag.current = null;
    if (!d?.carry) return;
    const a = d.base ?? snapAt(d.x, d.y).point;
    const by = carryBy(a, snapAt(e.clientX, e.clientY, a).point);
    carryEnd();
    if (by) void execute('element.transform', { ids: d.carry, op: d.copy || e.ctrlKey || e.metaKey || e.altKey ? 'copy' : 'move', by });
  };
  const onPointerDown = (e: React.PointerEvent) => {
    if (overridePress(e)) { drag.current = null; return; }
    if (e.button === 0 && clickEnds(autoholdOn()) && carrying() && drag.current?.carry) { endHeldCarry(e); return; }
    (e.target as Element).setPointerCapture?.(e.pointerId);
    if (middleTwice.current(e)) { fitModel(); return; }
    if (editor.onPointerDown(e)) return;
    // Pressed on what is selected, with no tool or command running: a drag carries the selection.
    let carry: string[] | undefined;
    if (e.button === 0 && !tool?.gesture && !useCad.getState().step) {
      const rect = svgRef.current!.getBoundingClientRect();
      const hit = pick(toWorld(e.clientX - rect.left, e.clientY - rect.top));
      const sel = useUi.getState().selected;
      if (hit && sel.includes(hit.id)) carry = sel;
    }
    const base = carry ? snapAt(e.clientX, e.clientY).point : undefined;
    drag.current = { x: e.clientX, y: e.clientY, view, moved: false, carry, copy: e.altKey || e.ctrlKey || e.metaKey, base, pan: pansIn2d(currentNavStyle(), e.button, modsOf(e)) };
  };
  const onPointerMove = (e: React.PointerEvent) => {
    if (peers.length) { const rect = svgRef.current!.getBoundingClientRect(); const w = toWorld(e.clientX - rect.left, e.clientY - rect.top); reportPresence({ cursor: [w.x, w.y] }); }
    if (editor.onPointerMove(e)) return;
    const d = drag.current;
    lastClient.current = { x: e.clientX, y: e.clientY };
    if (d) {
      const dx = e.clientX - d.x, dy = e.clientY - d.y;
      if (Math.hypot(dx, dy) > 4) d.moved = true;
      const step = useCad.getState().step;
      // Left drag with no tool: a selection box; middle or right: pan.
      if (d.moved && e.buttons === 1 && !tool?.gesture && !d.carry && (!step || step.want === 'select')) {
        const rect = svgRef.current!.getBoundingClientRect();
        const to = { x: e.clientX - rect.left, y: e.clientY - rect.top };
        setBox((cur) => dragTo(cur ?? { a: { x: d.x - rect.left, y: d.y - rect.top }, b: to }, to));
        return;
      }
      // Left drag on the selection: carried as a move grip carries it (the trace from the press, the readout, snaps from the base).
      if (d.moved && (e.buttons === 1 || autoholdOn()) && d.carry && d.base) {
        if (!carrying()) {
          const host = useCad.getState().host;
          const { carry: ids, base, copy } = d;
          carryBegin(base, ids.flatMap((id) => host?.outlinesOf?.(id) ?? []), (p) => {
            // A typed length or angle, then Enter: put there, as on release.
            drag.current = null;
            carryEnd();
            const by = [p.x - base.x, p.y - base.y];
            if (Math.hypot(by[0], by[1]) > 1e-9) void execute('element.transform', { ids, op: copy ? 'copy' : 'move', by });
          });
        }
        setHover(snapAt(e.clientX, e.clientY, d.base));
        return;
      }
      if (d.moved && e.buttons !== 1 && d.pan) { const m = screenDelta(dx, dy, d.view.scale); setView({ ...d.view, cx: d.view.cx - m.x, cy: d.view.cy - m.y }); }
    }
    setHover(constrained(snapAt(e.clientX, e.clientY, pts[pts.length - 1] ?? useCad.getState().step?.base ?? undefined)));
    { const r = svgRef.current!.getBoundingClientRect(); rawHover.current = toWorld(e.clientX - r.left, e.clientY - r.top); }
    setCycleSource(cycleSource.current);
    { const c = usePickCycle.getState().cycle; if (c && !cycledId(c, cycleKey, rawHover.current!, 3 / view.scale)) clearCycle(); }
    holdRefresh();
    if (tool?.gesture?.kind === 'pick' && tool.gesture.placing) {
      const g = tool.gesture, at = rawHover.current;
      const host = g.reach ? nearestOf(at, g.reach / view.scale, pickItems, nodes, g.types) : pick(at, g.types);
      const what = host ? g.placing!(host, at) : null;
      const q = host && what ? requestFor(host, at, what) : null;
      if (q) { askPlacement(q.req); setPlacing(q.req); } else resetPlacement();
    }
    {
      // Idle, or a step asking for a pick or a selection: what a click would take is lit (the same pick the click uses: decision 498).
      const step = useCad.getState().step;
      const idle = useHighlight.getState().preselect && !tool?.gesture && !d && !editor.dragging && (!step || step.want === 'pick' || step.want === 'select');
      const hit = idle ? pick(rawHover.current) : null;
      const id = hit ? pickTarget(hit.id) ?? hit.id : null;
      setHoverId((cur) => (cur === id ? cur : id));
    }
    if (tool?.gesture?.kind === 'face') {
      const g = tool.gesture, at = rawHover.current!;
      const hit = pick(at, g.types);
      const req = ++faceReq.current;
      if (!hit) { if (faceHover) setFaceHover(null); }
      else {
        void query<{ label?: string; finish?: string; bare?: boolean; outline?: number[][] } | null>(g.query ?? 'core.faceAt', { id: hit.id, point: [at.x, at.y] })
          .then((r) => {
            if (faceReq.current !== req) return;
            if (!r) { setFaceHover(null); return; }
            const what = r.bare ? 'bare' : r.finish;
            const label = what ? `${r.label ?? ''} · ${what}` : (r.label ?? '');
            const outline = (r.outline ?? []).filter((p) => p.length >= 2).map((p) => ({ x: p[0], y: p[1] }));
            setFaceHover({ at, label, outline });
          });
      }
    }
    trackTagHover(rawHover.current, { pickElement: (q) => pick(q)?.id ?? null, pickTag: (q) => pick(q, ['IfcAnnotation'])?.id ?? null, anyTags, busy: !!tool?.gesture || !!d });
  };
  const onPointerUp = (e: React.PointerEvent) => {
    if (editor.onPointerUp()) return;
    // Autohold (issue 86): the button is let go, the body drag is not.
    if (e.button === 0 && !releaseEnds(autoholdOn()) && carrying() && drag.current?.carry) return;
    spendOverride(e);
    const d = drag.current;
    drag.current = null;
    if (box) {
      const b = box;
      setBox(null);
      // A tiny box is the press taken as a click (where it began), not a window.
      if (selectBox(b, e) === 'done' || !d) return;
      const rect = svgRef.current!.getBoundingClientRect();
      click(constrained(snapAt(d.x, d.y, pts[pts.length - 1] ?? useCad.getState().step?.base ?? undefined)).point, toWorld(d.x - rect.left, d.y - rect.top), e.shiftKey, undefined, e.ctrlKey || e.metaKey, modsOf(e));
      return;
    }
    if (d?.moved && e.button === 0) {
      // A drag is two points: where it was pressed and where it was let go, both snapped.
      const step = useCad.getState().step;
      if (d.carry) {
        const a = d.base ?? snapAt(d.x, d.y).point;
        const by = carryBy(a, snapAt(e.clientX, e.clientY, a).point);
        carryEnd();
        if (by) void execute('element.transform', { ids: d.carry, op: d.copy || e.ctrlKey || e.metaKey || e.altKey ? 'copy' : 'move', by });
        return;
      }
      if (step?.want === 'point' && !tool?.gesture) {
        const a = snapAt(d.x, d.y, step.base ?? undefined).point;
        void (async () => {
          await feedPoint(a);
          acquire(a);
          const next = useCad.getState().step;
          if (next?.want !== 'point') return;
          const b0 = snapAt(e.clientX, e.clientY, next.base ?? a).point;
          const b = liveTaken(b0)?.point ?? b0;
          await feedPoint(b);
          acquire(b);
        })();
        return;
      }
    }
    // A right-click while a command asks for points is its Enter: it finishes (a dimension string, a polyline).
    if (d && !d.moved && e.button === 2 && useCad.getState().step?.enter) { void finishStep(); return; }
    if (d && !d.moved && e.button === 2 && canRef) {
      const snapped = snapAt(e.clientX, e.clientY).point;
      openContextMenu(e.clientX, e.clientY, [
        { label: 'From this point (offset…)', onSelect: () => { setRefPt(snapped); setPickRef(false); } },
        { label: 'Pick reference point…', onSelect: () => { setPickRef(true); } },
        ...(refPt ? [{ label: 'Clear reference point', onSelect: () => { setRefPt(null); setPickRef(false); } }] : []),
      ]);
      return;
    }
    if (d && !d.moved && e.button === 2 && !tool?.gesture && !useCad.getState().step) { if (!editor.onContextMenu(e)) roomMenu(e.clientX, e.clientY); return; }
    if (!d || d.moved || e.button !== 0) return;
    const rect = svgRef.current!.getBoundingClientRect();
    const snapped = snapAt(e.clientX, e.clientY, pts[pts.length - 1] ?? refPt ?? useCad.getState().step?.base ?? undefined);
    if (canRef && (pickRef || e.altKey)) { setRefPt(snapped.point); setPickRef(false); return; }
    const to = constrained(snapped).point;
    if (!pts.length && refPt) setRefPt(null);
    click(to, toWorld(e.clientX - rect.left, e.clientY - rect.top), e.shiftKey, undefined, e.ctrlKey || e.metaKey, modsOf(e));
  };
  /**
   * A right-click (not a drag): on drafting, its draw order (decision 339; what is under the cursor selected first when it
   * is not already); on a room, walk into it in 3D or select it; with a selection, group it (or a group: ungroup, edit) and
   * copy it to storeys or move it to another.
   */
  const roomMenu = (x: number, y: number) => {
    const rect = svgRef.current!.getBoundingClientRect();
    const at = toWorld(x - rect.left, y - rect.top);
    const under = pick(at);
    let selected = useUi.getState().selected;
    // Under the cursor, its group when it is in one (as a left click picks it).
    const target = under ? pickTarget(under.id) : null;
    if (isDrafted(under ?? undefined) && target && !selected.includes(target)) { select(target); selected = [target]; }
    const order = drawOrderItems(selected.flatMap((id) => [id, ...membersOf(id)]), nodes);
    const room = pick(at, ['IfcSpace']);
    const item = room ? render.get(room.id) : undefined;
    const rooms = room && item ? [
      { label: 'Walk in 3D', icon: <Footprints size={13} />, onSelect: () => walkIntoRoom(room, item, num(storey ?? undefined, 'elevation')) },
      { label: 'Select', icon: <MousePointer2 size={13} />, onSelect: () => select(room.id) },
    ] : [];
    openContextMenu(x, y, [...editor.cornerItems({ clientX: x, clientY: y }), ...order, ...symbolMenuItems(selected, nodes), ...rooms, ...dimensionItems(), ...selectionItems(), ...viewHideItems(storey ? ownPlan ?? `plan:${storey.id}` : null), ...viewSheetItems(storey ? ownPlan ?? `plan:${storey.id}` : null, nodes)]);
  };
  /** What a selection box or lasso takes: inside it wholly (a window, dragged to the right) or touching it (crossing, to the left). */
  const selectBox = (b: SelectDrag, e: React.PointerEvent): 'click' | 'done' => {
    const ids = pickRegion(regionIds(regionTestOf(b, toWorld), pickItems, nodes, scope ? undefined : drawing?.marks));
    const outcome = boxOutcome(b, ids.length);
    if (outcome === 'click') return 'click';
    if (outcome === 'keep') return 'done';
    // How it joins the selection is the navigation style's (Shift adds in Lapin's; Ctrl adds and Shift removes in Revit's); a command asking for a selection collects.
    const next = regionSelection(useUi.getState().selected, ids, useCad.getState().step ? 'add' : regionMode(currentNavStyle(), modsOf(e)));
    useUi.setState({ selected: next, selection: next[next.length - 1] ?? null });
    return 'done';
  };

  const onWheel = (e: React.WheelEvent) => {
    // A wheel, a pinch or two fingers up and down zoom; two fingers sideways or Shift pan; one or the other for a whole run.
    const mode = wheelMode.current(e);
    if (!mode) return;
    if (mode === 'pan') {
      { const m = screenDelta(e.deltaX, e.deltaY, view.scale); setView({ ...view, cx: view.cx + m.x, cy: view.cy + m.y }); }
      return;
    }
    const rect = svgRef.current!.getBoundingClientRect();
    const at = toWorld(e.clientX - rect.left, e.clientY - rect.top);
    const scale = clampScale(view.scale * wheelFactor(currentNavStyle(), wheelZoom(e, size.h)));
    // Zoom about the cursor; a burst of wheel steps is one zoom for ZP.
    const now = performance.now();
    if (now - wheelBurst.current > 400) pushHistory(key, view);
    wheelBurst.current = now;
    setView({ scale, cx: at.x - (at.x - view.cx) * (view.scale / scale), cy: at.y - (at.y - view.cy) * (view.scale / scale) });
  };

  // ─── Drawing ─────────────────────────────────────────────────────────────

  const path = useCallback((flat: ArrayLike<number>, closed: boolean) => {
    const p = pairs(flat).map(toScreen);
    if (p.length === 0) return '';
    return `M${p.map((q) => `${q.x.toFixed(1)},${q.y.toFixed(1)}`).join('L')}${closed ? 'Z' : ''}`;
  }, [toScreen]);

  // The background grid (F7) is for the eyes only: snapping to it stays. Adaptive (its step follows the zoom) or fixed
  // (File → Preferences: one step whatever the zoom, thinned to its heavier lines when they would crowd); in a UCS it
  // turns with its axes from its origin (decision 445).
  const gridShown = useDisplay((st) => st.grid);
  const gridPrefs = usePrefs(useShallow((p) => ({ gridMode: p.gridMode, gridStep: p.gridStep, gridMajor: p.gridMajor, gridSnap: p.gridSnap })));
  const ucs = useLocation((st) => ucsOf(st.loc));
  const ucsKey = ucs ? `${ucs.origin.x},${ucs.origin.y},${ucs.angle}` : '';
  const grid = useMemo(() => {
    const lines: { d: string; major: boolean }[] = [];
    if (!measured) return lines;
    const frame = ucs && !isIdentity(ucs) ? ucs : axes;
    let step = gridStepAt(view.scale, gridPrefs);
    const major = gridPrefs.gridMode === 'fixed' ? gridPrefs.gridMajor : 5;
    let every = 1;
    // A fixed step too fine for the zoom: only every major-th line, then every major²-th…
    while (step * every * view.scale < 6 && every < 1e9) every *= Math.max(2, major);
    step *= every;
    const L = (p: Vec2) => (frame ? toFrame(frame, p) : p), W = (q: Vec2) => (frame ? fromFrame(frame, q) : q);
    const corners = [toWorld(0, 0), toWorld(size.w, 0), toWorld(0, size.h), toWorld(size.w, size.h)].map(L);
    const min = { x: Math.min(...corners.map((c) => c.x)), y: Math.min(...corners.map((c) => c.y)) };
    const max = { x: Math.max(...corners.map((c) => c.x)), y: Math.max(...corners.map((c) => c.y)) };
    if ((max.x - min.x) / step > 2000 || (max.y - min.y) / step > 2000) return lines;
    const seg = (a: Vec2, b: Vec2) => { const p = toScreen(W(a)), q = toScreen(W(b)); return `M${p.x.toFixed(1)},${p.y.toFixed(1)}L${q.x.toFixed(1)},${q.y.toFixed(1)}`; };
    const isMajor = (v: number) => every > 1 || Math.round(v / step) % major === 0;
    for (let x = Math.floor(min.x / step) * step; x <= max.x; x += step) lines.push({ d: seg({ x, y: min.y }, { x, y: max.y }), major: isMajor(x) });
    for (let y = Math.floor(min.y / step) * step; y <= max.y; y += step) lines.push({ d: seg({ x: min.x, y }, { x: max.x, y }), major: isMajor(y) });
    return lines;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [view, size, measured, toScreen, toWorld, gridPrefs, ucsKey, axes]);

  const drawn = useMemo(() => {
    const out: { key: string; r: RenderItem; kind: string; d: string; closed: boolean; text: string | null; at: Vec2; pattern: string | null }[] = [];
    // Hit areas are drawn only round what is selected (a bare opening's gap: decision 312).
    for (const r of items) r.plan.forEach((it, i) => out.push({
      key: `${r.nodeId}:${i}`, r, kind: it.kind, closed: it.closed, text: it.text ?? null, pattern: it.pattern ?? null,
      d: it.kind === 'label' ? '' : path(it.points, it.closed), at: toScreen({ x: it.points[0] ?? 0, y: it.points[1] ?? 0 }),
    }));
    return out.sort((a, b) => (KIND_ORDER[a.kind] ?? 5) - (KIND_ORDER[b.kind] ?? 5));
  }, [items, toScreen, path]);

  const preview = (() => {
    if (!tool?.gesture || !hover) return null;
    const cur = (live.owner === 'tool' && (live.len || live.ang || live.rel) ? livePoint({ ...live, cursor: hover.point }) : null) ?? hover.point;
    if (pts.length === 0 && refPt) {
      const a = toScreen(refPt), b = toScreen(cur);
      return (
        <g className="plan-preview" data-testid="ref-point-preview">
          <line x1={a.x} y1={a.y} x2={b.x} y2={b.y} strokeDasharray="4 4" />
          <circle cx={a.x} cy={a.y} r={4} fill="none" />
          <path d={`M${a.x - 6},${a.y}H${a.x + 6}M${a.x},${a.y - 6}V${a.y + 6}`} />
        </g>
      );
    }
    const g = tool.gesture.kind;
    if (g === 'face' && faceHover) {
      const col = tool.id === 'architecture.paintClear' ? '#dc2626' : '#ea580c';
      const s = toScreen(faceHover.at);
      const pts2 = faceHover.outline.map(toScreen);
      return (
        <g className="plan-preview" data-testid="face-hover-preview">
          {pts2.length >= 2 && <polygon fill={col} fillOpacity={0.14} stroke={col} strokeWidth={2.5} points={pts2.map((p) => `${p.x},${p.y}`).join(' ')} />}
          {faceHover.label && <text x={s.x + 10} y={s.y - 8} fill={col}>{faceHover.label}</text>}
        </g>
      );
    }
    // What a pick would put down (a door, on the side of the wall the cursor is on).
    if (tool.gesture.kind === 'pick' && tool.gesture.ghost && rawHover.current) {
      const at = rawHover.current;
      const host = tool.gesture.reach ? nearestOf(at, tool.gesture.reach / view.scale, pickItems, nodes, tool.gesture.types) : pick(at, tool.gesture.types);
      if (!host) return null;
      // A module's verdict on whether it fits here: refused, the ghost is an error with the reason; or it stands at the place offered.
      const what = tool.gesture.placing?.(host, at) ?? null;
      const q = what ? requestFor(host, at, what) : null;
      const verdict = q ? verdictFor(q.req) : null;
      const put = q ? placedAt(q.arc, at, verdict) : { point: at, snapped: false };
      const bad = verdict?.ok === false && !put.snapped;
      const label = reasonLines(verdict, lang);
      const tip = toScreen(at);
      return (
        <g className={`plan-preview${bad ? ' bad' : ''}`} data-testid="pick-ghost" data-fits={verdict ? String(verdict.ok || put.snapped) : undefined}>
          {tool.gesture.ghost(host, put.point).map((l, i) => <polyline key={i} fill="none" points={l.map(toScreen).map((p) => `${p.x},${p.y}`).join(' ')} />)}
          {label.length > 0 && <text x={tip.x + 14} y={tip.y + 18} data-testid="pick-reason">{label.map((t, i) => <tspan key={i} x={tip.x + 14} dy={i ? 14 : 0}>{t}</tspan>)}</text>}
        </g>
      );
    }
    // A point tool's ghost: the outline of what the click would put down, at the cursor.
    if (tool.gesture.kind === 'point' && tool.gesture.ghost) {
      const lines = tool.gesture.ghost(cur);
      if (lines.length) return <g className="plan-preview" data-testid="point-ghost">{lines.map((l, i) => <polyline key={i} fill="none" points={l.map(toScreen).map((p) => `${p.x},${p.y}`).join(' ')} />)}</g>;
    }
    // The tool's own preview (a detour's bypass), when it has one for these points.
    const own = tool.gesture.kind === 'threePoints' && tool.gesture.preview && pts.length > 0 ? tool.gesture.preview(pts, cur) : null;
    if (own && own.length > 1) {
      const ps = own.map(toScreen);
      return <g className="plan-preview"><polyline points={ps.map((p) => `${p.x},${p.y}`).join(' ')} /></g>;
    }
    // What the path would build (a wall's body, on the side of the line it goes: decision 692).
    const body = (path: { a: Vec2; b: Vec2; rise: number }) => {
      const gb = tool.gesture?.kind === 'twoPoints' || tool.gesture?.kind === 'threePoints' ? tool.gesture.body : undefined;
      const lines = gb && Math.hypot(path.b.x - path.a.x, path.b.y - path.a.y) > 1e-6 ? gb(path) : [];
      return lines.length ? <g className="plan-body" data-testid="draw-body">{lines.map((l, i) => <polyline key={i} fill="none" points={l.map(toScreen).map((p) => `${p.x},${p.y}`).join(' ')} />)}</g> : null;
    };
    if ((g === 'twoPoints' || g === 'pickThenPoint') && pts.length === 1) {
      const a = toScreen(pts[0]), b = toScreen(cur);
      return (
        <g className="plan-preview">
          {g === 'twoPoints' && body({ a: pts[0], b: cur, rise: 0 })}
          <line x1={a.x} y1={a.y} x2={b.x} y2={b.y} />
          <text x={(a.x + b.x) / 2 + 8} y={(a.y + b.y) / 2 - 8}>{lengthText(Math.hypot(cur.x - pts[0].x, cur.y - pts[0].y))}</text>
        </g>
      );
    }
    if (tool.gesture.kind === 'threePoints' && tool.gesture.arc && pts.length >= 1) {
      // An arc: its ends, then through the cursor.
      const b = pts[1] ?? cur;
      const line = pts.length === 2 ? axisLine({ a: pts[0], b, rise: riseThrough(pts[0], b, cur) }) : [pts[0], b];
      const ps = line.map(toScreen);
      const m = toScreen(line[Math.floor(line.length / 2)]);
      return (
        <g className="plan-preview">
          {body(pts.length === 2 ? { a: pts[0], b, rise: riseThrough(pts[0], b, cur) } : { a: pts[0], b, rise: 0 })}
          <polyline points={ps.map((p) => `${p.x},${p.y}`).join(' ')} />
          <text x={m.x + 8} y={m.y - 8}>{pts.length === 2 ? `R ${lengthText(Math.abs(radiusOf(pts[0], b, cur)))}` : lengthText(Math.hypot(b.x - pts[0].x, b.y - pts[0].y))}</text>
        </g>
      );
    }
    if (g === 'threePoints' && pts.length >= 1) {
      const a = toScreen(pts[0]), b = toScreen(pts[1] ?? cur), c = toScreen(cur);
      return (
        <g className="plan-preview">
          <line x1={a.x} y1={a.y} x2={b.x} y2={b.y} />
          {pts.length === 2 && <line x1={c.x} y1={c.y} x2={c.x + b.x - a.x} y2={c.y + b.y - a.y} />}
          <text x={(a.x + b.x) / 2 + 8} y={(a.y + b.y) / 2 - 8}>{lengthText(Math.hypot((pts[1] ?? cur).x - pts[0].x, (pts[1] ?? cur).y - pts[0].y))}</text>
        </g>
      );
    }
    if (g === 'rectangle' && pts.length === 1) {
      const a = toScreen(pts[0]), b = toScreen(cur);
      return (
        <g className="plan-preview">
          <rect x={Math.min(a.x, b.x)} y={Math.min(a.y, b.y)} width={Math.abs(b.x - a.x)} height={Math.abs(b.y - a.y)} />
          <text x={Math.max(a.x, b.x) + 8} y={Math.min(a.y, b.y) - 6}>{formatLength(Math.abs(cur.x - pts[0].x))} × {lengthText(Math.abs(cur.y - pts[0].y))}</text>
        </g>
      );
    }
    // A shaped outline (forum topic 97): the rectangle or circle as it would be, or the polygon with its arcs and the next edge.
    if (g === 'polygon' && tool.gesture.shapes && pts.length > 0) {
      const { shape, arcs } = useOutlineShape.getState();
      const outline = shape === 'rectangle' ? { pts: rectangleOutline(pts[0], cur), arcs: undefined as number[] | undefined }
        : shape === 'circle' ? circleOutline(pts[0], cur) : null;
      const line = (ps: Vec2[], rs: (number | undefined)[], closed: boolean) => ps.flatMap((a, i) => {
        if (!closed && i === ps.length - 1) return [a];
        const seg = axisLine({ a, b: ps[(i + 1) % ps.length], rise: rs[i] ?? 0 });
        return seg.slice(0, -1);
      });
      if (shape === 'ellipse') {
        const e = pts.length >= 2 ? ellipseOutline(pts[0], pts[1], cur) : null;
        const ps = (e ? loopOf(e) : [pts[0], cur]).map(toScreen);
        if (e) ps.push(ps[0]);
        return <g className="plan-preview" data-testid="outline-preview"><polyline points={ps.map((p) => `${p.x},${p.y}`).join(' ')} /></g>;
      }
      if (outline) {
        const ps = line(outline.pts, outline.arcs ?? [], true).map(toScreen);
        ps.push(ps[0]);
        const label = shape === 'circle' ? `R ${lengthText(Math.hypot(cur.x - pts[0].x, cur.y - pts[0].y))}` : `${formatLength(Math.abs(cur.x - pts[0].x))} × ${lengthText(Math.abs(cur.y - pts[0].y))}`;
        return <g className="plan-preview" data-testid="outline-preview"><polyline points={ps.map((p) => `${p.x},${p.y}`).join(' ')} /><text x={toScreen(cur).x + 10} y={toScreen(cur).y - 8}>{label}</text></g>;
      }
      const all = [...pts, cur];
      const rs = [...pts.slice(0, -1).map((_, i) => rises.current[i] ?? 0), arcs ? nextRise(cur) : 0];
      const ps = line(all, rs, false).map(toScreen);
      return (
        <g className="plan-preview" data-testid="outline-preview">
          <polyline points={ps.map((p) => `${p.x},${p.y}`).join(' ')} />
          {pts.length >= 2 && <line className="closing" x1={ps[ps.length - 1].x} y1={ps[ps.length - 1].y} x2={ps[0].x} y2={ps[0].y} />}
        </g>
      );
    }
    if ((g === 'polyline' || g === 'polygon') && pts.length > 0) {
      const all = [...pts, cur].map(toScreen);
      return (
        <g className="plan-preview">
          <polyline points={all.map((p) => `${p.x},${p.y}`).join(' ')} />
          {g === 'polygon' && pts.length >= 2 && <line className="closing" x1={all[all.length - 1].x} y1={all[all.length - 1].y} x2={all[0].x} y2={all[0].y} />}
        </g>
      );
    }
    return null;
  })();
  // The straight piece being drawn, for the plan overlays (a wall's temporary dimensions before the click: decision 693).
  // A point tool's (a column's: decision 733) is the point the cursor would place, `a` and `b` both.
  const pointTool = tool?.gesture?.kind === 'point';
  const drawnTo = (tool?.gesture?.kind === 'twoPoints' && pts.length === 1) || (pointTool && pts.length === 0) ? hover
    ? (live.owner === 'tool' && (live.len || live.ang || live.rel) ? livePoint({ ...live, cursor: hover.point }) : null) ?? hover.point : null : null;
  useEffect(() => {
    setDrawnPath(drawnTo && tool ? { tool: tool.id, storeyId: storey?.id ?? null, a: pointTool ? drawnTo : pts[0], b: drawnTo } : null);
  }, [tool, pts, drawnTo?.x, drawnTo?.y, storey?.id]); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => () => setDrawnPath(null), []);

  const marker = hover && (tool?.gesture || editor.dragging || cadStep?.want === 'point' || cadStep?.snap || dragTrace) ? toScreen(hover.point) : null;

  return (
    <div className="view-pane">
      <div className="view-toolbar">
        <span className="view-title">{siteView ? <>Site · <span data-no-i18n>{siteView.name}</span></> : <>Plan · {storey?.name ?? '—'}</>}</span>
{(drawing?.lineworkOrphans?.length ?? 0) > 0 && ownPlan && (
          <button type="button" className="chip warn" title="The edges they were on are gone: remove them" onClick={() => void execute('view.lineworkReset', { id: ownPlan, orphans: true })}>
            {drawing!.lineworkOrphans!.length} line edit(s) lost their edge · clean
          </button>
        )}
                {(drawing?.problems?.length ?? 0) > 0 && (
          <button type="button" className="chip warn" title="The view range is out of order" onClick={() => useUi.setState({ dialog: 'viewRange' })}>view range: {drawing!.problems!.length} problem(s)</button>
        )}
        {storey && !siteView && <select className={`chip underlay-pick${under ? ' on' : ''}`} title="Underlay: another level's plan beneath this one, faded, to snap to" aria-label="Underlay"
          value={under ?? ''} onChange={(e) => { if (ownPlan) void execute('view.setUnderlay', { id: ownPlan, storeyId: e.target.value || null }); else setUnderlay(storey.id, e.target.value || null); }}>
          <option value="">No underlay</option>
          {[...nodes.values()].filter((n) => n.type === 'IfcBuildingStorey' && n.id !== storey.id)
            .sort((a, b) => Number(a.props.elevation ?? 0) - Number(b.props.elevation ?? 0))
            .map((n) => <option key={n.id} value={n.id}>{`Underlay: ${n.name}`}</option>)}
        </select>}
        {!siteView && <button type="button" className="icon" title="View range" onClick={() => useUi.setState({ dialog: 'viewRange' })}><Layers size={14} /></button>}
        <button type="button" className={`icon${planDrawing ? ' on' : ''}`} title={planDrawing ? 'Drawing: hatches and pens (click for modelling lines)' : 'Modelling lines (click for the drawing)'}
          onClick={() => useUi.setState({ planDrawing: !planDrawing })}><PenLine size={14} /></button>
        <ScalePicker viewId={planViewOf} scale={shownScale} />
        <StructurePicker viewId={planViewOf} kind="plan" />
        <ScaleChip pxPerMetre={view.scale} scale={shownScale} onActual={host.actual} />
        {planViewOf && (
          <button type="button" className={`icon${oriented ? ' on' : ''}`} data-testid="plan-orientation"
            title={oriented ? 'Rotated view: change its orientation or set north up again' : 'Rotate view: turn the plan on screen and on its sheets'}
            onClick={(e) => {
              const r = e.currentTarget.getBoundingClientRect();
              const id = planViewOf;
              const following = orientationValue === FOLLOW_UCS;
              openContextMenu(r.left, r.bottom + 4, [
                { label: 'North up (not rotated)', checked: !oriented && !following, onSelect: () => void setOrientation(id, null) },
                { label: 'Rotate view…', checked: oriented && !following, onSelect: async () => {
                  const v = await ask('Rotate view', [numberField('angle', 'Direction shown horizontal (° from X)', Math.round(orientation * 1e6) / 1e6)]);
                  if (v && Number.isFinite(Number(v.angle))) void setOrientation(id, Number(v.angle));
                } },
                { label: 'Rotate view to align with an edge', onSelect: () => void startCommand('ALIGNVIEW') },
                { label: 'Follow the UCS', checked: following, disabled: ucsAngle == null, onSelect: () => void setOrientation(id, FOLLOW_UCS) },
                ...(Math.abs(northAngle) > 1e-9 ? [{ label: 'True north up', checked: oriented && !following && Math.abs(normalizeDeg(orientation - northAngle)) < 1e-6, onSelect: () => void setOrientation(id, northAngle) }] : []),
              ]);
            }}><RotateCw size={14} /></button>
        )}
        {Math.abs(northAngle) > 1e-9 && !oriented && (
          <button type="button" className={`icon${trueNorth ? ' on' : ''}`} data-testid="plan-true-north"
            title={trueNorth ? 'True north up: click for project north' : 'Project north up: click to turn the plan to true north'}
            onClick={() => setTrueNorth(key, !trueNorth)}><Compass size={14} /></button>
        )}
        <button type="button" className="icon" title="Fit the model (ZE: everything)" onClick={fitModel}><Maximize size={14} /></button>
        {storey && <button type="button" className="icon" data-testid="plan-export-dxf" title="Export the plan as DXF: in the plan's, the base point's or the map's coordinates"
          onClick={(e) => {
            const r = e.currentTarget.getBoundingClientRect();
            const id = ownPlan ?? `plan:${storey.id}`;
            const name = `${siteView?.name ?? storey.name}.dxf`;
            const run = (coords: 'internal' | 'project' | 'map') => void exportFile('export.dxf', { id, coords }, name, 'dxf');
            const loc = useLocation.getState().loc;
            openContextMenu(r.left, r.bottom + 4, [
              { label: oriented ? 'In the plan’s coordinates, turned as it shows (mm)' : 'In the plan’s coordinates (mm)', onSelect: () => run('internal') },
              { label: 'From the project base point (mm)', onSelect: () => run('project') },
              { label: 'In map coordinates (E, N in metres)', disabled: !surveyPoint(loc), onSelect: () => run('map') },
            ]);
          }}><FileDown size={14} /><span className="small">DXF</span></button>}
      </div>
      <KeyPlan mode="plan" frame={measured ? screenCorners(toWorld, size.w, size.h) : null} onLookAt={(p) => go({ ...view, cx: p.x, cy: p.y })} />
      <svg ref={svgRef} className={`plan-svg${tool?.gesture || cadStep?.want === 'point' ? ' tool' : tool || cadStep ? ' pick' : hoverId ? ' pick' : ''}`} data-testid="plan"
        onPointerDown={onPointerDown} onPointerMove={onPointerMove} onPointerUp={onPointerUp} onPointerCancel={() => { drag.current = null; setBox(null); if (carrying()) carryEnd(); }}
        onPointerLeave={() => { setCycleSource(null, cycleKey); setHover(null); setHoverId(null); trackTagHover(null, { pickElement: () => null, pickTag: () => null, anyTags, busy: true }); editor.onPointerLeave(); }} onWheel={onWheel} onContextMenu={(e) => e.preventDefault()}
        onDoubleClick={(e) => {
          if (tool?.gesture || useCad.getState().step) return;
          const rect = svgRef.current!.getBoundingClientRect();
          const w = toWorld(e.clientX - rect.left, e.clientY - rect.top);
          const n = pick(w);
          // A group's member opens the group for editing; a view's mark its view; anything else, its dimension.
          if (n && !openGroupOf(n.id) && !openPickedView(n.id)) void editDimension(n.id, w);
        }}>
        {gridShown && <g className="plan-grid">{grid.map((l, i) => <path key={i} d={l.d} className={l.major ? 'major' : ''} />)}</g>}
        {ucs && !isIdentity(ucs) && <UcsIcon frame={ucs} toScreen={toScreen} />}
        {/* The drawing lays the orthophotos itself (CivilModule's marks, so sheets print them); the modelling lines lie over them here. */}
        {!(drawing && (planDrawing || siteView)) && orthophotos(nodes).filter((o) => !o.storeyId || o.storeyId === storey?.id).map((o) => {
          // The image's unit square onto its corners on screen: (1, 0) to the top right, (0, 1) to the bottom left.
          const [tl, tr, , bl] = o.corners.map(([x, y]) => toScreen({ x, y }));
          return <image key={`ortho:${o.id}`} href={o.image.url} x={0} y={0} width={1} height={1} preserveAspectRatio="none" opacity={o.opacity} className="plan-orthophoto"
            transform={`matrix(${tr.x - tl.x} ${tr.y - tl.y} ${bl.x - tl.x} ${bl.y - tl.y} ${tl.x} ${tl.y})`} />;
        })}
        {underDrawing && <g className="plan-underlay" aria-label="Underlay"><DrawingLayer drawing={underDrawing} toScreen={toScreen} pxPerMetre={view.scale} onDark={onDark} screen={size} turnDeg={(rot * 180) / Math.PI} /></g>}
        {drawing && (planDrawing || siteView) && <g className={scope ? 'scoped-out' : inside ? 'drill-out' : undefined}><DrawingLayer drawing={drawing} toScreen={toScreen} pxPerMetre={view.scale} onDark={onDark} screen={size} turnDeg={(rot * 180) / Math.PI} /></g>}
        <g className={`plan-items${drawing && (planDrawing || siteView) ? ' ghost' : ''}${scope ? ' scoped' : ''}${inside ? ' drilled' : ''}`}>
          {drawn.map(({ key, r, kind, d, closed, text, at, pattern }) => kind === 'hit' && !shown.has(r.nodeId) ? null : kind === 'label' ? (
            <text key={key} x={at.x} y={at.y} className="plan-label">{text}</text>
          ) : (
            <path key={key} d={d} strokeDasharray={pattern && DASHES[pattern] ? DASHES[pattern].map((mm) => mm * (drawing?.scale ?? 100) / 1000 * view.scale).join(' ') : undefined}
              className={`plan-item ${kind}${closed ? ' closed' : ''}${r.editable ? '' : ' foreign'}${shown.has(r.nodeId) ? ' selected' : targets.has(r.nodeId) ? ' tag-target' : ''}${scope && pickable(nodes.get(r.nodeId)) ? ' in-scope' : ''}${inside?.has(r.nodeId) ? ' in-drill' : ''}`} />
          ))}
        </g>
        {hoverId && !shown.has(hoverId) && (
          <g className="plan-hover" aria-hidden="true">
            {drawn.filter((x) => x.kind !== 'label' && x.kind !== 'hit' && (x.r.nodeId === hoverId || membersOf(hoverId).includes(x.r.nodeId))).map((x) => <path key={x.key} d={x.d} />)}
          </g>
        )}
        {peers.map((p) => {
          const theirs = new Set(p.presence.selection ?? []);
          return theirs.size === 0 ? null : (
            <g key={`peer:${p.id}`} className="plan-peer" style={{ stroke: p.color }} aria-label={`${p.name}'s selection`}>
              {drawn.filter((x) => x.kind !== 'label' && x.kind !== 'hit' && theirs.has(x.r.nodeId)).map((x) => <path key={x.key} d={x.d} />)}
            </g>
          );
        })}
        {peers.filter((p) => p.presence.view === 'plan' && p.presence.storeyId === storey?.id && p.presence.cursor).map((p) => {
          const c = toScreen({ x: p.presence.cursor![0], y: p.presence.cursor![1] });
          return (
            <g key={`cursor:${p.id}`} className="plan-peer-cursor" transform={`translate(${c.x} ${c.y})`} style={{ color: p.color }}>
              <path d="M0 0L0 14L4 10L7 17L9 16L6 9L11 9Z" />
              <text x={13} y={22}>{p.name}</text>
            </g>
          );
        })}
        {(drawing?.marks ?? []).filter((m) => targets.size > 0 && markSelected(m.id, targetList) && !markSelected(m.id, shownList)).map((m) => (
          <path key={`tag:${m.id}`} className="plan-mark tag-target" d={Array.from({ length: m.segments.length / 4 }, (_, k) => {
            const a = toScreen({ x: m.segments[4 * k], y: m.segments[4 * k + 1] }), b = toScreen({ x: m.segments[4 * k + 2], y: m.segments[4 * k + 3] });
            return `M${a.x} ${a.y}L${b.x} ${b.y}`;
          }).join('')} />
        ))}
        {hoverId && !markSelected(hoverId, shownList) && (drawing?.marks ?? []).filter((m) => m.id === hoverId || markSelected(m.id, [hoverId])).map((m) => (
          <path key={`hover:${m.id}`} className="plan-mark plan-hover-mark" aria-hidden="true" d={Array.from({ length: m.segments.length / 4 }, (_, k) => {
            const a = toScreen({ x: m.segments[4 * k], y: m.segments[4 * k + 1] }), b = toScreen({ x: m.segments[4 * k + 2], y: m.segments[4 * k + 3] });
            return `M${a.x} ${a.y}L${b.x} ${b.y}`;
          }).join('')} />
        ))}
        {(drawing?.marks ?? []).filter((m) => markSelected(m.id, shownList)).map((m) => (
          <path key={`sel:${m.id}`} className="plan-mark selected" d={Array.from({ length: m.segments.length / 4 }, (_, k) => {
            const a = toScreen({ x: m.segments[4 * k], y: m.segments[4 * k + 1] }), b = toScreen({ x: m.segments[4 * k + 2], y: m.segments[4 * k + 3] });
            return `M${a.x} ${a.y}L${b.x} ${b.y}`;
          }).join('')} />
        ))}
        {pts.map((p, i) => { const s = toScreen(p); return <circle key={i} cx={s.x} cy={s.y} r={3} className="plan-vertex" />; })}
        {editor.render}
        <PlanOverlays toScreen={toScreen} toWorld={toWorld} scale={view.scale} rotation={rot} view={{ id: ownPlan ?? null, storeyId: storey?.id ?? null }} />
        {preview}
        {marker && hover && !hover.mode && (hover.kind === 'point' && !hover.guides.length
          ? <rect x={marker.x - 5} y={marker.y - 5} width={10} height={10} className="plan-snap point" />
          : <path d={`M${marker.x - 6},${marker.y}H${marker.x + 6}M${marker.x},${marker.y - 6}V${marker.y + 6}`} className="plan-snap" />)}
        {marker && <CadOverlay aimed={hover} toScreen={toScreen} />}
        {box && <SelectRegion d={box} />}
      </svg>
      {active && <LiveBox toScreen={toScreen} />}
      {active && <LiveBar />}
      <CommandLine />
    </div>
  );
}
