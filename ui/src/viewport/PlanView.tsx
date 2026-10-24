/**
 * The plan of one storey: the engine's plan items for it, drawn in SVG, and
 * the gestures the active tool needs (adapted from BubbleBIM Pro's plan
 * view). North is up; metres inside, mm shown. Every gesture ends in one
 * engine command. Hold Shift to square a line; Esc drops the gesture.
 */

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Layers, Maximize, PenLine } from 'lucide-react';
import { select, setTool, useUi } from '../app/store';
import { usePaneActive, usePaneField } from '../app/pane';
import { activeStorey, execute, useSession } from '../engine/session';
import type { NodeDto, RenderItem } from '../engine/protocol';
import type { Vec2 } from '../platform/module';
import { findTool } from '../platform/modules';
import { useGripEditor } from './GripLayer';
import { boxIds, pickAt } from './planPick';
import { pairs, toMm } from './snap';
import { completeTool } from './tools';
import { DrawingLayer } from './DrawingLayer';
import { useDrawing } from './drawing';
import { editDimension } from './dimensionEdit';
import { CadOverlay } from './cad/canvas';
import { useCadCanvas, type Aimed } from './cad/aim';
import { CommandLine } from './cad/CommandLine';
import { feedPoint, useCad } from './cad/session';
import type { Shape } from './cad/shapes';
import { actualScale, boundsOf, framing, middleDouble, popHistory, pushHistory, recall, remember, useZoomHost, wheelPans, wheelPixels, type Bounds, type View2, type ZoomHost } from './zoom';
import { ScaleChip } from './ScaleChip';
import { KeyPlan } from './KeyPlan';

type View = View2; // scale = px per metre
const clampScale = (s: number) => Math.max(2, Math.min(s, 2000));

const KIND_ORDER: Record<string, number> = { grid: 0, overhead: 1, ridge: 2, hip: 2, valley: 2, break: 2, outline: 3, cut: 4, symbol: 5, heavy: 5, label: 6 };

export function PlanView() {
  const svgRef = useRef<SVGSVGElement>(null);
  const [size, setSize] = useState({ w: 0, h: 0 });
  const [pts, setPts] = useState<Vec2[]>([]);
  const [hover, setHover] = useState<Aimed | null>(null);
  const [ortho, setOrtho] = useState(false);
  const drag = useRef<{ x: number; y: number; view: View; moved: boolean } | null>(null);
  /** A selection box being dragged (screen px): left to right a window, right to left crossing. */
  const [box, setBox] = useState<{ a: Vec2; b: Vec2 } | null>(null);

  const render = useSession((s) => s.render);
  const nodes = useSession((s) => s.nodes);
  useUi((s) => s.storeyId);
  const storey = activeStorey();
  const selected = useUi((s) => s.selected);
  const toolId = useUi((s) => s.tool);
  const tool = findTool(toolId);
  // The generated drawing of this storey's plan (its own view, else the storey's), the modelling lines over it only where selected.
  const planViewId = usePaneField('planViewId');
  const active = usePaneActive();
  const planDrawing = useUi((s) => s.planDrawing);
  const ownPlan = planViewId && nodes.get(planViewId)?.props.storeyId === storey?.id ? planViewId : null;
  const drawing = useDrawing(storey ? ownPlan ?? `plan:${storey.id}` : null, planDrawing);
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

  const pick = useCallback((p: Vec2, types?: string[]): NodeDto | null =>
    pickAt(p, 6 / view.scale, items, nodes, drawing?.marks, types), [items, nodes, view.scale, drawing]);

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
    return o;
  }, [items, snapPoints, markSnaps]);
  const planViewOf = storey ? ownPlan ?? `plan:${storey.id}` : null;
  const { aim, acquire } = useCadCanvas({ viewId: planViewOf, drawing, extra, scale: view.scale, pickAny: (p) => pick(p)?.id ?? null });
  const cadStep = useCad((s) => s.step);

  const snapAt = useCallback((clientX: number, clientY: number, from?: Vec2): Aimed => {
    const rect = svgRef.current!.getBoundingClientRect();
    const raw = toWorld(clientX - rect.left, clientY - rect.top);
    const a = aim(raw, from ?? null, { grid: view.scale > 30 ? 0.05 : 0.5, orthoHeld: ortho });
    useCad.setState({ cursor: a.point });
    return a;
  }, [toWorld, view.scale, aim, ortho]);

  const complete = useCallback(async (points: Vec2[], entity?: NodeDto) => {
    if (!tool) return;
    await completeTool(tool, { points, entity });
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

  // Direct editing: the selection's axes, outlines and grips (none while a tool or a command is busy).
  const editor = useGripEditor({
    viewIds: [ownPlan, storey ? `plan:${storey.id}` : null], mode: 'plan', storeyId: storey?.id, toScreen, scale: view.scale,
    aim: (cx, cy, from) => { const s = snapAt(cx, cy, from); setHover(s); return s.point; },
    disabled: !!tool?.gesture || !!cadStep, svg: svgRef,
  });

  const onPointerDown = (e: React.PointerEvent) => {
    (e.target as Element).setPointerCapture?.(e.pointerId);
    if (middleTwice.current(e)) { fit(); return; }
    if (editor.onPointerDown(e)) return;
    drag.current = { x: e.clientX, y: e.clientY, view, moved: false };
  };
  const onPointerMove = (e: React.PointerEvent) => {
    if (editor.onPointerMove(e)) return;
    const d = drag.current;
    if (d) {
      const dx = e.clientX - d.x, dy = e.clientY - d.y;
      if (Math.hypot(dx, dy) > 4) d.moved = true;
      const step = useCad.getState().step;
      // Left drag with no tool: a selection box; middle or right: pan.
      if (d.moved && e.buttons === 1 && !tool?.gesture && (!step || step.want === 'select')) {
        const rect = svgRef.current!.getBoundingClientRect();
        setBox({ a: { x: d.x - rect.left, y: d.y - rect.top }, b: { x: e.clientX - rect.left, y: e.clientY - rect.top } });
        return;
      }
      if (d.moved && e.buttons !== 1) setView({ ...d.view, cx: d.view.cx - dx / d.view.scale, cy: d.view.cy + dy / d.view.scale });
    }
    setHover(snapAt(e.clientX, e.clientY, pts[pts.length - 1] ?? useCad.getState().step?.base ?? undefined));
  };
  const onPointerUp = (e: React.PointerEvent) => {
    if (editor.onPointerUp()) return;
    const d = drag.current;
    drag.current = null;
    if (box) { selectBox(box, e.shiftKey); setBox(null); return; }
    if (!d || d.moved || e.button !== 0) return;
    const rect = svgRef.current!.getBoundingClientRect();
    click(snapAt(e.clientX, e.clientY, pts[pts.length - 1] ?? useCad.getState().step?.base ?? undefined).point, toWorld(e.clientX - rect.left, e.clientY - rect.top), e.shiftKey);
  };
  /** What a selection box takes: inside it wholly (a window, dragged to the right) or touching it (crossing, to the left). */
  const selectBox = (b: { a: Vec2; b: Vec2 }, add: boolean) => {
    const p = toWorld(b.a.x, b.a.y), q = toWorld(b.b.x, b.b.y);
    const ids = boxIds(p, q, b.b.x >= b.a.x, items, nodes, drawing?.marks);
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
    const scale = clampScale(view.scale * Math.exp(-wheelPixels(e, size.h) * 0.0015));
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
    const cur = hover.point;
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
          const n = pick(toWorld(e.clientX - rect.left, e.clientY - rect.top));
          if (n) void editDimension(n.id);
        }}>
        <g className="plan-grid">{grid.map((l, i) => <path key={i} d={l.d} className={l.major ? 'major' : ''} />)}</g>
        {drawing && planDrawing && <DrawingLayer drawing={drawing} toScreen={toScreen} pxPerMetre={view.scale} />}
        <g className={`plan-items${drawing && planDrawing ? ' ghost' : ''}`}>
          {drawn.map(({ key, r, kind, d, closed, text, at }) => kind === 'label' ? (
            <text key={key} x={at.x} y={at.y} className="plan-label">{text}</text>
          ) : (
            <path key={key} d={d} className={`plan-item ${kind}${closed ? ' closed' : ''}${r.editable ? '' : ' foreign'}${selected.includes(r.nodeId) ? ' selected' : ''}`} />
          ))}
        </g>
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
      <CommandLine />
    </div>
  );
}
