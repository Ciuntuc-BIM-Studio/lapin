/**
 * The plan of one storey: the engine's plan items for it, drawn in SVG, and
 * the gestures the active tool needs (adapted from BubbleBIM Pro's plan
 * view). North is up; metres inside, mm shown. Every gesture ends in one
 * engine command. Hold Shift to square a line; Esc drops the gesture.
 */

import { orthophotos } from './textures';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Footprints, Layers, Maximize, MousePointer2, PenLine } from 'lucide-react';
import { openContextMenu } from '../shell/ContextMenu';
import { num } from '../engine/model';
import { useWalk, walkInto } from './walk';
import { select, setTool, useUi } from '../app/store';
import { usePaneActive, usePaneField } from '../app/pane';
import { activeStorey, execute, useSession } from '../engine/session';
import type { NodeDto, RenderItem } from '../engine/protocol';
import type { FaceHit, Vec2 } from '../platform/module';
import { findTool } from '../platform/modules';
import { useGripEditor } from './GripLayer';
import { LiveBar, LiveBox } from './LiveBox';
import { liveBegin, liveCursor, liveEnd, livePoint, useLive } from './liveInput';
import { boxIds, pickAt } from './planPick';
import { pickable, useScope } from '../platform/scope';
import { pairs, toMm } from './snap';
import { completeTool } from './tools';
import { DrawingLayer } from './DrawingLayer';
import { useDrawing } from './drawing';
import { setUnderlay, useUnderlay } from './underlay';
import { editDimension } from './dimensionEdit';
import { CadOverlay } from './cad/canvas';
import { snapShapes, useCadCanvas, type Aimed } from './cad/aim';
import { CommandLine } from './cad/CommandLine';
import { feedPoint, useCad } from './cad/session';
import type { Shape } from './cad/shapes';
import { actualScale, boundsOf, framing, middleDouble, popHistory, pushHistory, recall, remember, useZoomHost, wheelPans, wheelZoom, type Bounds, type View2, type ZoomHost } from './zoom';
import { ScaleChip } from './ScaleChip';
import { axisLine, riseThrough } from './arc';
import { others, reportPresence, useShare } from '../engine/share';

/** The radius of the arc from a to b through p (Infinity: in line). */
function radiusOf(a: Vec2, b: Vec2, p: Vec2): number {
  const rise = riseThrough(a, b, p), c = Math.hypot(b.x - a.x, b.y - a.y);
  return rise === 0 ? Infinity : (c * c / 4 + rise * rise) / (2 * Math.abs(rise));
}
import { KeyPlan } from './KeyPlan';

type View = View2; // scale = px per metre
const clampScale = (s: number) => Math.max(2, Math.min(s, 2000));

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
  const svgRef = useRef<SVGSVGElement>(null);
  const [size, setSize] = useState({ w: 0, h: 0 });
  const [pts, setPts] = useState<Vec2[]>([]);
  const [hover, setHover] = useState<Aimed | null>(null);
  const [ortho, setOrtho] = useState(false);
  /** A press: where, the view then, whether it moved; `carry`: the selection it was pressed on (a drag moves it, or copies it with Ctrl). */
  const drag = useRef<{ x: number; y: number; view: View; moved: boolean; carry?: string[]; copy?: boolean } | null>(null);
  /** A selection box being dragged (screen px): left to right a window, right to left crossing. */
  const [box, setBox] = useState<{ a: Vec2; b: Vec2 } | null>(null);

  const render = useSession((s) => s.render);
  const nodes = useSession((s) => s.nodes);
  useUi((s) => s.storeyId);
  const storey = activeStorey();
  const selected = useUi((s) => s.selected);
  // The others of a shared session (docs/plan-multiplayer.md): their selections and cursors on this storey.
  const participants = useShare((s) => s.participants);
  const welcome = useShare((s) => s.welcome);
  const peers = participants.length > 1 ? others(participants, welcome) : [];
  const toolId = useUi((s) => s.tool);
  const tool = findTool(toolId);
  // The generated drawing of this storey's plan (its own view, else the storey's), the modelling lines over it only where selected.
  const planViewId = usePaneField('planViewId');
  const active = usePaneActive();
  const planDrawing = useUi((s) => s.planDrawing);
  const ownPlan = planViewId && nodes.get(planViewId)?.props.storeyId === storey?.id ? planViewId : null;
  const drawing = useDrawing(storey ? ownPlan ?? `plan:${storey.id}` : null, planDrawing);
  // Another storey beneath this one, faded: seen and snapped to, never picked.
  // The reference level: the plan's own view's (kept with the project), else the one remembered in this browser for the level.
  const remembered = useUnderlay((u) => (storey ? u.of[storey.id] : undefined));
  const ownUnder = ownPlan ? nodes.get(ownPlan)?.props.underlay : undefined;
  const underId = typeof ownUnder === 'string' ? ownUnder : ownPlan ? undefined : remembered;
  const under = underId && underId !== storey?.id && nodes.get(underId) ? underId : null;
  const underDrawing = useDrawing(under ? `plan:${under}` : null);
  // The view (centre, zoom) is remembered per plan for the session, so the tab comes back as it was left.
  const key = ownPlan ?? (storey ? `plan:${storey.id}` : 'plan:none');
  const [view, setView] = useState<View>(() => recall<View>(key) ?? { cx: 4, cy: 2, scale: 60 });
  useEffect(() => { remember(key, view); }, [key, view]);
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
  useEffect(() => { setPts([]); }, [toolId]);

  const toScreen = useCallback((p: Vec2) => ({
    x: (p.x - view.cx) * view.scale + size.w / 2,
    y: size.h / 2 - (p.y - view.cy) * view.scale,
  }), [view, size]);
  const toWorld = useCallback((x: number, y: number): Vec2 => ({
    x: (x - size.w / 2) / view.scale + view.cx,
    y: (size.h / 2 - y) / view.scale + view.cy,
  }), [view, size]);

  // The shown storey only.
  const items = useMemo(() => [...render.values()].filter((r) => r.storeyId === storey?.id), [render, storey?.id]);
  const snapPoints = useMemo(() => items.flatMap((r) => pairs(r.snaps ?? [])), [items]);
  const markSnaps = useMemo(() => (drawing?.marks ?? []).flatMap((m) => pairs(m.snaps ?? [])), [drawing]);

  /** What the elements and the drawing's annotations span; null when the plan is empty. */
  const extent = useCallback((only?: Set<string>): Bounds | null => {
    const pts: Vec2[] = [];
    for (const r of items) if (!only || only.has(r.nodeId)) for (const it of r.plan) for (let i = 0; i + 1 < it.points.length; i += 2) pts.push({ x: it.points[i], y: it.points[i + 1] });
    for (const m of drawing?.marks ?? []) if (!only || only.has(m.id)) for (let k = 0; k + 1 < m.segments.length; k += 2) pts.push({ x: m.segments[k], y: m.segments[k + 1] });
    return boundsOf(pts);
  }, [items, drawing]);
  const fit = useCallback(() => {
    const b = extent();
    if (b) go(framing(b, size, 0.8, 5, 400));
  }, [extent, size, go]);

  // A different storey: its remembered view, else frame it. Only then: an edit or a
  // resize keeps the view, so the effect reads the current fit through a ref, not as a dependency.
  const measured = size.w > 0 && size.h > 0;
  const fitRef = useRef(fit);
  fitRef.current = fit;
  useEffect(() => {
    if (!measured) return;
    const r = recall<View>(key);
    if (r) setView(r); else fitRef.current();
  }, [key, measured]);

  // The zoom commands and keys, while this pane is in front.
  const host: ZoomHost = {
    fitAll: fit,
    fitSelection: () => { const b = extent(new Set(selected)); if (!b) return false; go(framing(b, size, 0.6, 5, 1000)); return true; },
    zoomWindow: (a, b) => go(framing([Math.min(a.x, b.x), Math.min(a.y, b.y), Math.max(a.x, b.x), Math.max(a.y, b.y)], size, 0.95, 2, 2000)),
    actual: () => go({ ...view, scale: clampScale(actualScale(drawing?.scale ?? 100)) }),
    zoomBy: (f) => go({ ...view, scale: clampScale(view.scale * f) }),
    previous: () => { const p = popHistory<View>(key); if (!p) return false; setView(p); return true; },
  };
  useZoomHost(active, host);
  const middleTwice = useRef(middleDouble());
  const wheelBurst = useRef(0);

  // ─── Picking ─────────────────────────────────────────────────────────────

  // In an edit scope only its elements are picked (the rest stays to snap to).
  const scope = useScope((s) => s.scope);
  const pickItems = useMemo(() => (scope ? items.filter((r) => pickable(nodes.get(r.nodeId))) : items), [scope, items, nodes]);
  const pick = useCallback((p: Vec2, types?: string[]): NodeDto | null =>
    pickAt(p, 6 / view.scale, pickItems, nodes, scope ? undefined : drawing?.marks, types), [pickItems, nodes, view.scale, drawing, scope]);

  // ─── Gestures ────────────────────────────────────────────────────────────

  // The modelling lines as shapes to snap to (the drawing's own lines come from the canvas).
  const extra = useMemo(() => {
    const o: Shape[] = [];
    for (const r of items) for (const it of r.plan) {
      if (it.kind === 'label' || it.kind === 'hit' || it.kind === 'grid') continue;
      const ps = pairs(it.points);
      for (let i = 0; i + 1 < ps.length + (it.closed ? 1 : 0); i++) o.push({ type: 'line', a: ps[i], b: ps[(i + 1) % ps.length] });
    }
    for (const q of [...snapPoints, ...markSnaps]) o.push({ type: 'line', a: q, b: q });
    if (underDrawing) o.push(...snapShapes(underDrawing).shapes);
    return o;
  }, [items, snapPoints, markSnaps, underDrawing]);
  const planViewOf = storey ? ownPlan ?? `plan:${storey.id}` : null;
  const { aim, acquire } = useCadCanvas({ viewId: planViewOf, drawing, extra, scale: view.scale, pickAny: (p) => pick(p)?.id ?? null });
  const cadStep = useCad((s) => s.step);

  const snapAt = useCallback((clientX: number, clientY: number, from?: Vec2): Aimed => {
    const rect = svgRef.current!.getBoundingClientRect();
    const raw = toWorld(clientX - rect.left, clientY - rect.top);
    const a = aim(raw, from ?? null, { grid: view.scale > 30 ? 0.05 : 0.5, orthoHeld: ortho });
    useCad.setState({ cursor: a.point });
    liveCursor(a.point);
    return a;
  }, [toWorld, view.scale, aim, ortho]);

  /** A tool's own rule for its next point (a pipe's material), over the snap. */
  const constrained = (a: Aimed): Aimed => {
    const g = tool?.gesture;
    if (g?.kind !== 'polyline' || !g.constrain || pts.length === 0) return a;
    const p = g.constrain(pts, a.point);
    return p.x === a.point.x && p.y === a.point.y ? a : { ...a, point: p, kind: 'free', guides: [] };
  };

  const complete = useCallback(async (points: Vec2[], entity?: NodeDto, face?: FaceHit) => {
    if (!tool) return;
    await completeTool(tool, { points, entity, face });
    if (tool.gesture?.kind === 'twoPoints' && tool.gesture.repeat) setPts([points[points.length - 1]]);
    else setPts([]);
  }, [tool]);

  const click = useCallback((p: Vec2, raw: Vec2, add: boolean) => {
    // A drafting command takes the click: a snapped point, or what lies under the cursor.
    const step = useCad.getState().step;
    if (step) { void feedPoint(step.want === 'pick' || step.want === 'select' ? raw : p); acquire(p); return; }
    if (tool?.gesture) acquire(p);
    if (!tool?.gesture) {
      select(pick(raw)?.id ?? null, add);
      return;
    }
    const g = tool.gesture;
    switch (g.kind) {
      case 'point': void complete([p]); break;
      case 'pick': {
        const e = pick(raw, g.types);
        if (e) void complete([raw], e);
        else useSession.setState({ error: 'Pick an element this tool works on.' });
        break;
      }
      case 'face': {
        const e = pick(raw, g.types);
        if (e) void complete([raw], e, { point: [raw.x, raw.y] });
        else useSession.setState({ error: 'Pick an element this tool works on.' });
        break;
      }
      case 'twoPoints':
      case 'rectangle':
        if (pts.length === 0) setPts([p]);
        else void complete([pts[0], p]);
        break;
      case 'threePoints':
        if (pts.length < 2) setPts([...pts, p]);
        else void complete([pts[0], pts[1], p]);
        break;
      case 'polygon': {
        const first = pts[0];
        if (first && pts.length >= 3 && Math.hypot(first.x - p.x, first.y - p.y) < 8 / view.scale) void complete(pts);
        else setPts([...pts, p]);
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
    const onKey = (e: KeyboardEvent) => {
      if ((e.target as HTMLElement)?.closest('input, select, textarea')) return;
      if (e.key === 'Escape') { if (pts.length) setPts([]); else setTool(null); }
      if (e.key === 'Enter' && tool?.gesture?.kind === 'polygon' && pts.length >= 3) void complete(pts);
      if (e.key === 'Enter' && tool?.gesture?.kind === 'polyline' && pts.length >= 2) void complete(pts);
      if (e.key === 'Shift') setOrtho(true);
    };
    const onUp = (e: KeyboardEvent) => { if (e.key === 'Shift') setOrtho(false); };
    window.addEventListener('keydown', onKey);
    window.addEventListener('keyup', onUp);
    return () => { window.removeEventListener('keydown', onKey); window.removeEventListener('keyup', onUp); };
  }, [pts, tool, complete]);

  // A tool drawing from a point: its next segment's length and angle can be typed (Enter takes it).
  const live = useLive();
  const clickRef = useRef(click);
  clickRef.current = click;
  const last = pts[pts.length - 1];
  useEffect(() => {
    const k = tool?.gesture?.kind;
    if (last && (k === 'twoPoints' || k === 'polyline' || k === 'polygon' || k === 'rectangle' || k === 'threePoints')) liveBegin('tool', last, (p) => clickRef.current(p, p, false));
    else liveEnd('tool');
  }, [last, tool]);
  useEffect(() => () => liveEnd('tool'), []);

  // Direct editing: the selection's axes, outlines and grips (none while a tool or a command is busy).
  const editor = useGripEditor({
    viewIds: [ownPlan, storey ? `plan:${storey.id}` : null], mode: 'plan', storeyId: storey?.id, toScreen, scale: view.scale,
    aim: (cx, cy, from) => { const s = snapAt(cx, cy, from); setHover(s); return s.point; },
    disabled: !!tool?.gesture || !!cadStep, svg: svgRef, marks: drawing?.marks,
  });

  const onPointerDown = (e: React.PointerEvent) => {
    (e.target as Element).setPointerCapture?.(e.pointerId);
    if (middleTwice.current(e)) { fit(); return; }
    if (editor.onPointerDown(e)) return;
    // Pressed on what is selected, with no tool or command running: a drag carries the selection.
    let carry: string[] | undefined;
    if (e.button === 0 && !tool?.gesture && !useCad.getState().step) {
      const rect = svgRef.current!.getBoundingClientRect();
      const hit = pick(toWorld(e.clientX - rect.left, e.clientY - rect.top));
      const sel = useUi.getState().selected;
      if (hit && sel.includes(hit.id)) carry = sel;
    }
    drag.current = { x: e.clientX, y: e.clientY, view, moved: false, carry, copy: e.altKey || e.ctrlKey || e.metaKey };
  };
  const onPointerMove = (e: React.PointerEvent) => {
    if (peers.length) { const rect = svgRef.current!.getBoundingClientRect(); const w = toWorld(e.clientX - rect.left, e.clientY - rect.top); reportPresence({ cursor: [w.x, w.y] }); }
    if (editor.onPointerMove(e)) return;
    const d = drag.current;
    if (d) {
      const dx = e.clientX - d.x, dy = e.clientY - d.y;
      if (Math.hypot(dx, dy) > 4) d.moved = true;
      const step = useCad.getState().step;
      // Left drag with no tool: a selection box; middle or right: pan.
      if (d.moved && e.buttons === 1 && !tool?.gesture && !d.carry && (!step || step.want === 'select')) {
        const rect = svgRef.current!.getBoundingClientRect();
        setBox({ a: { x: d.x - rect.left, y: d.y - rect.top }, b: { x: e.clientX - rect.left, y: e.clientY - rect.top } });
        return;
      }
      if (d.moved && e.buttons !== 1) setView({ ...d.view, cx: d.view.cx - dx / d.view.scale, cy: d.view.cy + dy / d.view.scale });
    }
    setHover(constrained(snapAt(e.clientX, e.clientY, pts[pts.length - 1] ?? useCad.getState().step?.base ?? undefined)));
  };
  const onPointerUp = (e: React.PointerEvent) => {
    if (editor.onPointerUp()) return;
    const d = drag.current;
    drag.current = null;
    if (box) { selectBox(box, e.shiftKey); setBox(null); return; }
    if (d?.moved && e.button === 0) {
      // A drag is two points: where it was pressed and where it was let go, both snapped.
      const step = useCad.getState().step;
      if (d.carry) {
        const a = snapAt(d.x, d.y).point;
        const b = snapAt(e.clientX, e.clientY, a).point;
        if (Math.hypot(b.x - a.x, b.y - a.y) > 1e-9) void execute('element.transform', { ids: d.carry, op: d.copy || e.ctrlKey || e.metaKey || e.altKey ? 'copy' : 'move', by: [b.x - a.x, b.y - a.y] });
        return;
      }
      if (step?.want === 'point' && !tool?.gesture) {
        const a = snapAt(d.x, d.y, step.base ?? undefined).point;
        void (async () => {
          await feedPoint(a);
          acquire(a);
          const next = useCad.getState().step;
          if (next?.want !== 'point') return;
          const b = snapAt(e.clientX, e.clientY, next.base ?? a).point;
          await feedPoint(b);
          acquire(b);
        })();
        return;
      }
    }
    if (d && !d.moved && e.button === 2 && !tool?.gesture && !useCad.getState().step) { roomMenu(e.clientX, e.clientY); return; }
    if (!d || d.moved || e.button !== 0) return;
    const rect = svgRef.current!.getBoundingClientRect();
    click(constrained(snapAt(e.clientX, e.clientY, pts[pts.length - 1] ?? useCad.getState().step?.base ?? undefined)).point, toWorld(e.clientX - rect.left, e.clientY - rect.top), e.shiftKey);
  };
  /** A right-click (not a drag) on a room: walk into it in 3D, or select it. */
  const roomMenu = (x: number, y: number) => {
    const rect = svgRef.current!.getBoundingClientRect();
    const room = pick(toWorld(x - rect.left, y - rect.top), ['IfcSpace']);
    const item = room ? render.get(room.id) : undefined;
    if (!room || !item) return;
    openContextMenu(x, y, [
      { label: 'Walk in 3D', icon: <Footprints size={13} />, onSelect: () => walkIntoRoom(room, item, num(storey ?? undefined, 'elevation')) },
      { label: 'Select', icon: <MousePointer2 size={13} />, onSelect: () => select(room.id) },
    ]);
  };
  /** What a selection box takes: inside it wholly (a window, dragged to the right) or touching it (crossing, to the left). */
  const selectBox = (b: { a: Vec2; b: Vec2 }, add: boolean) => {
    const p = toWorld(b.a.x, b.a.y), q = toWorld(b.b.x, b.b.y);
    const ids = boxIds(p, q, b.b.x >= b.a.x, pickItems, nodes, scope ? undefined : drawing?.marks);
    const before = add || useCad.getState().step ? useUi.getState().selected : [];
    const next = [...new Set([...before, ...ids])];
    useUi.setState({ selected: next, selection: next[next.length - 1] ?? null });
  };

  const onWheel = (e: React.WheelEvent) => {
    // A wheel, a pinch or two fingers up and down zoom; two fingers sideways or Shift pan.
    if (wheelPans(e)) {
      setView({ ...view, cx: view.cx + e.deltaX / view.scale, cy: view.cy - e.deltaY / view.scale });
      return;
    }
    const rect = svgRef.current!.getBoundingClientRect();
    const at = toWorld(e.clientX - rect.left, e.clientY - rect.top);
    const scale = clampScale(view.scale * wheelZoom(e, size.h));
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

  const grid = useMemo(() => {
    const lines: { d: string; major: boolean }[] = [];
    if (!measured) return lines;
    const step = view.scale > 30 ? 1 : view.scale > 6 ? 5 : 25;
    const a = toWorld(0, size.h), b = toWorld(size.w, 0);
    for (let x = Math.floor(a.x / step) * step; x <= b.x; x += step) {
      const s = toScreen({ x, y: 0 }).x;
      lines.push({ d: `M${s},0V${size.h}`, major: Math.round(x / step) % 5 === 0 });
    }
    for (let y = Math.floor(a.y / step) * step; y <= b.y; y += step) {
      const s = toScreen({ x: 0, y }).y;
      lines.push({ d: `M0,${s}H${size.w}`, major: Math.round(y / step) % 5 === 0 });
    }
    return lines;
  }, [view, size, measured, toScreen, toWorld]);

  const drawn = useMemo(() => {
    const out: { key: string; r: RenderItem; kind: string; d: string; closed: boolean; text: string | null; at: Vec2 }[] = [];
    for (const r of items) r.plan.forEach((it, i) => it.kind !== 'hit' && out.push({
      key: `${r.nodeId}:${i}`, r, kind: it.kind, closed: it.closed, text: it.text ?? null,
      d: it.kind === 'label' ? '' : path(it.points, it.closed), at: toScreen({ x: it.points[0] ?? 0, y: it.points[1] ?? 0 }),
    }));
    return out.sort((a, b) => (KIND_ORDER[a.kind] ?? 5) - (KIND_ORDER[b.kind] ?? 5));
  }, [items, toScreen, path]);

  const preview = (() => {
    if (!tool?.gesture || !hover) return null;
    const cur = (live.owner === 'tool' && (live.len || live.ang) ? livePoint({ ...live, cursor: hover.point }) : null) ?? hover.point;
    const g = tool.gesture.kind;
    if (g === 'twoPoints' && pts.length === 1) {
      const a = toScreen(pts[0]), b = toScreen(cur);
      return (
        <g className="plan-preview">
          <line x1={a.x} y1={a.y} x2={b.x} y2={b.y} />
          <text x={(a.x + b.x) / 2 + 8} y={(a.y + b.y) / 2 - 8}>{toMm(Math.hypot(cur.x - pts[0].x, cur.y - pts[0].y))}</text>
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
          <polyline points={ps.map((p) => `${p.x},${p.y}`).join(' ')} />
          <text x={m.x + 8} y={m.y - 8}>{pts.length === 2 ? `R ${toMm(Math.abs(radiusOf(pts[0], b, cur)))}` : toMm(Math.hypot(b.x - pts[0].x, b.y - pts[0].y))}</text>
        </g>
      );
    }
    if (g === 'threePoints' && pts.length >= 1) {
      const a = toScreen(pts[0]), b = toScreen(pts[1] ?? cur), c = toScreen(cur);
      return (
        <g className="plan-preview">
          <line x1={a.x} y1={a.y} x2={b.x} y2={b.y} />
          {pts.length === 2 && <line x1={c.x} y1={c.y} x2={c.x + b.x - a.x} y2={c.y + b.y - a.y} />}
          <text x={(a.x + b.x) / 2 + 8} y={(a.y + b.y) / 2 - 8}>{toMm(Math.hypot((pts[1] ?? cur).x - pts[0].x, (pts[1] ?? cur).y - pts[0].y))}</text>
        </g>
      );
    }
    if (g === 'rectangle' && pts.length === 1) {
      const a = toScreen(pts[0]), b = toScreen(cur);
      return (
        <g className="plan-preview">
          <rect x={Math.min(a.x, b.x)} y={Math.min(a.y, b.y)} width={Math.abs(b.x - a.x)} height={Math.abs(b.y - a.y)} />
          <text x={Math.max(a.x, b.x) + 8} y={Math.min(a.y, b.y) - 6}>{toMm(Math.abs(cur.x - pts[0].x))} × {toMm(Math.abs(cur.y - pts[0].y))}</text>
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

  const marker = hover && (tool?.gesture || editor.dragging || cadStep) ? toScreen(hover.point) : null;

  return (
    <div className="view-pane">
      <div className="view-toolbar">
        <span className="view-title">Plan · {storey?.name ?? '—'}</span>
{(drawing?.lineworkOrphans?.length ?? 0) > 0 && ownPlan && (
          <button type="button" className="chip warn" title="The edges they were on are gone: remove them" onClick={() => void execute('view.lineworkReset', { id: ownPlan, orphans: true })}>
            {drawing!.lineworkOrphans!.length} line edit(s) lost their edge · clean
          </button>
        )}
                {(drawing?.problems?.length ?? 0) > 0 && (
          <button type="button" className="chip warn" title="The view range is out of order" onClick={() => useUi.setState({ dialog: 'viewRange' })}>view range: {drawing!.problems!.length} problem(s)</button>
        )}
        {storey && <select className={`chip underlay-pick${under ? ' on' : ''}`} title="Underlay: another level's plan beneath this one, faded, to snap to" aria-label="Underlay"
          value={under ?? ''} onChange={(e) => { if (ownPlan) void execute('view.setUnderlay', { id: ownPlan, storeyId: e.target.value || null }); else setUnderlay(storey.id, e.target.value || null); }}>
          <option value="">No underlay</option>
          {[...nodes.values()].filter((n) => n.type === 'IfcBuildingStorey' && n.id !== storey.id)
            .sort((a, b) => Number(a.props.elevation ?? 0) - Number(b.props.elevation ?? 0))
            .map((n) => <option key={n.id} value={n.id}>Underlay: {n.name}</option>)}
        </select>}
        <button type="button" className="icon" title="View range" onClick={() => useUi.setState({ dialog: 'viewRange' })}><Layers size={14} /></button>
        <button type="button" className={`icon${planDrawing ? ' on' : ''}`} title={planDrawing ? 'Drawing: hatches and pens (click for modelling lines)' : 'Modelling lines (click for the drawing)'}
          onClick={() => useUi.setState({ planDrawing: !planDrawing })}><PenLine size={14} /></button>
        <ScaleChip pxPerMetre={view.scale} scale={drawing?.scale ?? 100} onActual={host.actual} />
        <button type="button" className="icon" title="Fit everything (ZE)" onClick={fit}><Maximize size={14} /></button>
      </div>
      <KeyPlan mode="plan" frame={measured ? [toWorld(0, size.h), toWorld(size.w, 0)] : null} onLookAt={(p) => go({ ...view, cx: p.x, cy: p.y })} />
      <svg ref={svgRef} className={`plan-svg${tool?.gesture || cadStep ? ' tool' : ''}`} data-testid="plan"
        onPointerDown={onPointerDown} onPointerMove={onPointerMove} onPointerUp={onPointerUp}
        onPointerLeave={() => setHover(null)} onWheel={onWheel} onContextMenu={(e) => e.preventDefault()}
        onDoubleClick={(e) => {
          if (tool?.gesture || useCad.getState().step) return;
          const rect = svgRef.current!.getBoundingClientRect();
          const w = toWorld(e.clientX - rect.left, e.clientY - rect.top);
          const n = pick(w);
          if (n) void editDimension(n.id, w);
        }}>
        <g className="plan-grid">{grid.map((l, i) => <path key={i} d={l.d} className={l.major ? 'major' : ''} />)}</g>
        {orthophotos(nodes).filter((o) => !o.storeyId || o.storeyId === storey?.id).map((o) => {
          // The image's unit square onto its corners on screen: (1, 0) to the top right, (0, 1) to the bottom left.
          const [tl, tr, , bl] = o.corners.map(([x, y]) => toScreen({ x, y }));
          return <image key={`ortho:${o.id}`} href={o.image.url} x={0} y={0} width={1} height={1} preserveAspectRatio="none" opacity={o.opacity} className="plan-orthophoto"
            transform={`matrix(${tr.x - tl.x} ${tr.y - tl.y} ${bl.x - tl.x} ${bl.y - tl.y} ${tl.x} ${tl.y})`} />;
        })}
        {underDrawing && <g className="plan-underlay" aria-label="Underlay"><DrawingLayer drawing={underDrawing} toScreen={toScreen} pxPerMetre={view.scale} /></g>}
        {drawing && planDrawing && <g className={scope ? 'scoped-out' : undefined}><DrawingLayer drawing={drawing} toScreen={toScreen} pxPerMetre={view.scale} /></g>}
        <g className={`plan-items${drawing && planDrawing ? ' ghost' : ''}${scope ? ' scoped' : ''}`}>
          {drawn.map(({ key, r, kind, d, closed, text, at }) => kind === 'label' ? (
            <text key={key} x={at.x} y={at.y} className="plan-label">{text}</text>
          ) : (
            <path key={key} d={d} className={`plan-item ${kind}${closed ? ' closed' : ''}${r.editable ? '' : ' foreign'}${selected.includes(r.nodeId) ? ' selected' : ''}${scope && pickable(nodes.get(r.nodeId)) ? ' in-scope' : ''}`} />
          ))}
        </g>
        {peers.map((p) => {
          const theirs = new Set(p.presence.selection ?? []);
          return theirs.size === 0 ? null : (
            <g key={`peer:${p.id}`} className="plan-peer" style={{ stroke: p.color }} aria-label={`${p.name}'s selection`}>
              {drawn.filter((x) => x.kind !== 'label' && theirs.has(x.r.nodeId)).map((x) => <path key={x.key} d={x.d} />)}
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
        {(drawing?.marks ?? []).filter((m) => selected.includes(m.id)).map((m) => (
          <path key={`sel:${m.id}`} className="plan-mark selected" d={Array.from({ length: m.segments.length / 4 }, (_, k) => {
            const a = toScreen({ x: m.segments[4 * k], y: m.segments[4 * k + 1] }), b = toScreen({ x: m.segments[4 * k + 2], y: m.segments[4 * k + 3] });
            return `M${a.x} ${a.y}L${b.x} ${b.y}`;
          }).join('')} />
        ))}
        {pts.map((p, i) => { const s = toScreen(p); return <circle key={i} cx={s.x} cy={s.y} r={3} className="plan-vertex" />; })}
        {editor.render}
        {preview}
        {marker && hover && !hover.mode && (hover.kind === 'point' && !hover.guides.length
          ? <rect x={marker.x - 5} y={marker.y - 5} width={10} height={10} className="plan-snap point" />
          : <path d={`M${marker.x - 6},${marker.y}H${marker.x + 6}M${marker.x},${marker.y - 6}V${marker.y + 6}`} className="plan-snap" />)}
        {marker && <CadOverlay aimed={hover} toScreen={toScreen} />}
        {box && <rect className={`select-box${box.b.x >= box.a.x ? ' window' : ' crossing'}`} x={Math.min(box.a.x, box.b.x)} y={Math.min(box.a.y, box.b.y)} width={Math.abs(box.b.x - box.a.x)} height={Math.abs(box.b.y - box.a.y)} />}
      </svg>
      {active && <LiveBox toScreen={toScreen} />}
      {active && <LiveBar />}
      <CommandLine />
    </div>
  );
}
