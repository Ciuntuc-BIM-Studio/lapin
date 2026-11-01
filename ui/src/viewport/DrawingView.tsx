/**
 * A section or an elevation: the view's generated drawing, panned and zoomed
 * (wheel, drag). The drawing's x runs along the view, its y up. Drafting
 * works here as in plans: the command line, snaps, ortho and guide lines; a
 * click selects an annotation or a drafted shape, a left drag selects by a
 * box (window to the right, crossing to the left), a middle or right drag pans.
 * A tool with a gesture (a bar, a stirrup distribution, a section across an element) draws here as in a plan.
 */

import { useCallback, useEffect, useRef, useState } from 'react';
import { Maximize } from 'lucide-react';
import { select, setTool, useUi } from '../app/store';
import { usePaneActive, usePaneField } from '../app/pane';
import { execute, useSession } from '../engine/session';
import type { Vec2 } from '../platform/module';
import type { NodeDto } from '../engine/protocol';
import { findTool } from '../platform/modules';
import { completeTool } from './tools';
import { DrawingLayer } from './DrawingLayer';
import { useDrawing } from './drawing';
import { editDimension } from './dimensionEdit';
import { openPickedView } from './openView';
import { pickDrawing } from './drawingPick';
import { CadOverlay } from './cad/canvas';
import { useCadCanvas, type Aimed } from './cad/aim';
import { CommandLine } from './cad/CommandLine';
import { feedPoint, useCad } from './cad/session';
import { markIds, markSelected } from './planPick';
import { dragTo, regionTestOf, SelectRegion, useLassoKey, type SelectDrag } from './selectDrag';
import { useGripEditor } from './GripLayer';
import { LiveBar, LiveBox } from './LiveBox';
import { liveBegin, liveCursor, liveEnd, livePoint, useLive } from './liveInput';
import { actualScale, boundsOf, framing, middleDouble, popHistory, pushHistory, recall, remember, useZoomHost, wheelGesture, wheelZoom, type View2, type ZoomHost } from './zoom';
import { ScaleChip } from './ScaleChip';
import { KeyPlan } from './KeyPlan';

type View = View2;
const clampScale = (s: number) => Math.min(4000, Math.max(2, s));

export function DrawingView() {
  const id = usePaneField('drawingViewId');
  const active = usePaneActive();
  const node = useSession((s) => (id ? s.nodes.get(id) : undefined));
  const nodes = useSession((s) => s.nodes);
  const selected = useUi((s) => s.selected);
  const drawing = useDrawing(id);
  const ref = useRef<SVGSVGElement>(null);
  const [size, setSize] = useState({ w: 0, h: 0 });
  // The view is remembered per section or elevation for the session; a new one fits itself once its drawing arrives.
  const key = id ?? 'drawing:none';
  const [view, setView] = useState<View | null>(() => recall<View>(key) ?? null);
  useEffect(() => { if (view) remember(key, view); }, [key, view]);
  const go = useCallback((v: View) => { setView((old) => { if (old) pushHistory(key, old); return v; }); }, [key]);
  const [hover, setHover] = useState<Aimed | null>(null);
  const drag = useRef<{ x: number; y: number; view: View; moved: boolean } | null>(null);
  /** A selection box or lasso being dragged (screen px); Space turns the box into a lasso. */
  const [box, setBox] = useState<SelectDrag | null>(null);
  useLassoKey(!!box, setBox);
  const cadStep = useCad((s) => s.step);
  const toolId = useUi((s) => s.tool);
  const tool = findTool(toolId);
  /** The points of the gesture being drawn. */
  const [pts, setPts] = useState<Vec2[]>([]);
  useEffect(() => { setPts([]); }, [toolId, id]);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const ro = new ResizeObserver(([e]) => setSize({ w: e.contentRect.width, h: e.contentRect.height }));
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  const fit = useCallback(() => {
    const b = drawing?.bounds;
    if (!b || !size.w) return;
    go(framing(b, size, 0.85, 2, 4000));
  }, [drawing?.bounds, size, go]);
  useEffect(() => { setView(recall<View>(id ?? 'drawing:none') ?? null); }, [id]);
  useEffect(() => { if (!view && drawing?.bounds) fit(); }, [drawing, view, fit]);

  const v = view ?? { cx: 0, cy: 0, scale: 40 };
  const host: ZoomHost = {
    fitAll: fit,
    fitSelection: () => {
      const pts: Vec2[] = [];
      for (const m of drawing?.marks ?? []) if (selected.includes(m.id)) for (let k = 0; k + 1 < m.segments.length; k += 2) pts.push({ x: m.segments[k], y: m.segments[k + 1] });
      const b = boundsOf(pts);
      if (!b || !size.w) return false;
      go(framing(b, size, 0.6, 2, 4000));
      return true;
    },
    zoomWindow: (a, b) => go(framing([Math.min(a.x, b.x), Math.min(a.y, b.y), Math.max(a.x, b.x), Math.max(a.y, b.y)], size, 0.95, 2, 4000)),
    actual: () => go({ ...v, scale: clampScale(actualScale(drawing?.scale ?? 100)) }),
    zoomBy: (f) => go({ ...v, scale: clampScale(v.scale * f) }),
    previous: () => { const p = popHistory<View>(key); if (!p) return false; setView(p); return true; },
  };
  useZoomHost(active, host);
  const middleTwice = useRef(middleDouble());
  const wheelBurst = useRef(0);
  const wheelMode = useRef(wheelGesture());
  const toScreen = useCallback((p: Vec2) => ({ x: (p.x - v.cx) * v.scale + size.w / 2, y: size.h / 2 - (p.y - v.cy) * v.scale }), [v.cx, v.cy, v.scale, size]);
  const toWorld = useCallback((x: number, y: number): Vec2 => ({ x: (x - size.w / 2) / v.scale + v.cx, y: (size.h / 2 - y) / v.scale + v.cy }), [v.cx, v.cy, v.scale, size]);

  /** The annotation, drafted shape or element under a point: by a line, else inside its cut (decision 223). */
  const pickAny = useCallback((p: Vec2): string | null => pickDrawing(p, 6 / v.scale, drawing?.marks, nodes), [drawing, nodes, v.scale]);

  /** The annotations and drafted shapes a box takes, added to the selection with shift (or while a command asks for a selection). */
  const selectBox = (b: SelectDrag, add: boolean) => {
    const ids = markIds(regionTestOf(b, toWorld), nodes, drawing?.marks);
    const before = add || useCad.getState().step ? useUi.getState().selected : [];
    const next = [...new Set([...before, ...ids])];
    useUi.setState({ selected: next, selection: next[next.length - 1] ?? null });
  };

  const { aim, acquire } = useCadCanvas({ viewId: id, drawing, scale: v.scale, pickAny, inRegion: (takes) => markIds(takes, nodes, drawing?.marks) });

  /** The inset view the points all fall in, with its mapping into its source (host → source: centre + (p − at) / factor); null when none. */
  const insetUnder = useCallback((points: Vec2[]) => {
    for (const m of drawing?.marks ?? []) {
      const d = m.data as { source?: string; at?: number[]; centre?: number[]; factor?: number; bounds?: number[] } | undefined;
      if (!d?.source || !d.bounds || !d.at || !d.centre || !d.factor) continue;
      const [x0, y0, x1, y1] = d.bounds;
      if (points.every((p) => p.x >= x0 && p.x <= x1 && p.y >= y0 && p.y <= y1)) {
        const { at, centre, factor, source } = d;
        return { source, map: (p: Vec2): Vec2 => ({ x: centre[0] + (p.x - at[0]) / factor, y: centre[1] + (p.y - at[1]) / factor }) };
      }
    }
    return null;
  }, [drawing]);

  /** The element a pick-then-point gesture took with its first click. */
  const picked = useRef<NodeDto | undefined>(undefined);
  useEffect(() => { picked.current = undefined; }, [toolId, id]);

  const complete = useCallback(async (points: Vec2[], entity?: NodeDto, typed?: number) => {
    if (!tool) return;
    picked.current = undefined;
    // Drawn on an inset view: it goes on the source view, in its own coordinates, and shows here through the inset.
    const inset = insetUnder(points);
    if (inset && tool.id !== 'rebar.inset') await completeTool(tool, { points: points.map(inset.map), entity, typed }, inset.source);
    else await completeTool(tool, { points, entity, typed });
    if (tool.gesture?.kind === 'twoPoints' && tool.gesture.repeat) setPts([points[points.length - 1]]);
    else setPts([]);
  }, [tool, insetUnder]);

  /** The element under the cursor that a tool may work on (picked by what it drew), or null. */
  const pickFor = useCallback((raw: Vec2, types: string[]): NodeDto | null => {
    const hit = pickAny(raw);
    const owner = hit?.includes('#') ? hit.slice(0, hit.indexOf('#')) : hit;
    const n = owner ? nodes.get(owner) : undefined;
    return n && (types.includes('*') || types.includes(n.type)) ? n : null;
  }, [pickAny, nodes]);

  /** A click for the tool's gesture: the points it takes, or the element under the cursor; `typed`: the length typed in the live input, when the point came from it. */
  const gestureClick = useCallback((p: Vec2, raw: Vec2, typed?: number) => {
    const g = tool?.gesture;
    if (!g) return false;
    acquire(p);
    switch (g.kind) {
      case 'point': void complete([p]); break;
      case 'pick':
      case 'face': {
        const n = pickFor(raw, g.types);
        if (n) void complete([raw], n);
        else useSession.setState({ error: 'Pick an element this tool works on.' });
        break;
      }
      case 'pickThenPoint': {
        // The first click picks the element and is the first point; the second is where the cursor is (or the typed length).
        if (pts.length === 0) {
          const n = pickFor(raw, g.types);
          if (n) { picked.current = n; setPts([raw]); }
          else useSession.setState({ error: 'Pick an element this tool works on.' });
        } else void complete([pts[0], p], picked.current, typed);
        break;
      }
      case 'twoPoints':
      case 'rectangle':
        if (pts.length === 0) setPts([p]); else void complete([pts[0], p]);
        break;
      case 'threePoints':
        if (pts.length < 2) setPts([...pts, p]); else void complete([pts[0], pts[1], p]);
        break;
      case 'polygon': {
        const first = pts[0];
        if (first && pts.length >= 3 && Math.hypot(first.x - p.x, first.y - p.y) < 8 / v.scale) void complete(pts); else setPts([...pts, p]);
        break;
      }
      case 'polyline': {
        const last = pts[pts.length - 1];
        if (last && pts.length >= 2 && Math.hypot(last.x - p.x, last.y - p.y) < 8 / v.scale) void complete(pts); else setPts([...pts, p]);
        break;
      }
    }
    return true;
  }, [tool, pts, complete, acquire, pickFor, v.scale]);

  useEffect(() => {
    if (!active) return;
    const onKey = (e: KeyboardEvent) => {
      if ((e.target as HTMLElement)?.closest('input, select, textarea') || useCad.getState().step) return;
      // A pick-then-point gesture ended after its pick (Enter or Esc) completes with the pick alone; a typed length is the key router's (Enter takes it, Esc clears it).
      if ((e.key === 'Escape' || e.key === 'Enter') && tool?.gesture?.kind === 'pickThenPoint' && pts.length === 1 && picked.current) {
        const l = useLive.getState();
        if (l.owner === 'tool' && (l.len || l.ang)) return;
        void complete([pts[0]], picked.current);
        return;
      }
      if (e.key === 'Escape' && tool?.gesture) { if (pts.length) setPts([]); else setTool(null); }
      if (e.key === 'Enter' && tool?.gesture?.kind === 'polyline' && pts.length >= 2) void complete(pts);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [active, tool, pts, complete]);

  // A pick-then-point tool after its pick: the second point's distance can be typed (Enter takes it; the tool measures it along its own path).
  const live = useLive();
  const clickRef = useRef(gestureClick);
  clickRef.current = gestureClick;
  const first = tool?.gesture?.kind === 'pickThenPoint' && pts.length === 1 && active ? pts[0] : undefined;
  useEffect(() => {
    if (first) liveBegin('tool', first, (p, typed) => clickRef.current(p, p, typed));
    else liveEnd('tool');
  }, [first]);
  useEffect(() => () => liveEnd('tool'), []);

  const at = (e: React.PointerEvent): { raw: Vec2; aimed: Aimed } => {
    const r = ref.current!.getBoundingClientRect();
    const raw = toWorld(e.clientX - r.left, e.clientY - r.top);
    const aimed = aim(raw, useCad.getState().step?.base ?? null, { orthoHeld: e.shiftKey });
    useCad.setState({ cursor: aimed.point });
    liveCursor(aimed.point);
    return { raw, aimed };
  };

  // Direct editing of the shapes drafted on this view (and of a solid sketched here): their grips and lines.
  const editor = useGripEditor({
    viewIds: [id], mode: 'drawing', toScreen, scale: v.scale, disabled: !!cadStep, svg: ref, marks: drawing?.marks,
    aim: (cx, cy, from) => {
      const r = ref.current!.getBoundingClientRect();
      const a = aim(toWorld(cx - r.left, cy - r.top), from, { orthoHeld: false });
      setHover(a);
      liveCursor(a.point);
      return a.point;
    },
  });

  return (
    <div className="view-pane">
      <div className="view-toolbar">
        <span className="view-title">{node?.name ?? 'View'} · 1:{drawing?.scale ?? 100}</span>
{(drawing?.lineworkOrphans?.length ?? 0) > 0 && id && (
          <button type="button" className="chip warn" title="The edges they were on are gone: remove them" onClick={() => void execute('view.lineworkReset', { id: id, orphans: true })}>
            {drawing!.lineworkOrphans!.length} line edit(s) lost their edge · clean
          </button>
        )}
        <ScaleChip pxPerMetre={v.scale} scale={drawing?.scale ?? 100} onActual={host.actual} />
        <button type="button" className="icon" title="Fit (ZE)" onClick={fit}><Maximize size={14} /></button>
      </div>
      <KeyPlan mode="drawing" viewId={id} />
      <svg ref={ref} className={`plan-svg drawing-svg${cadStep || tool?.gesture ? ' tool' : ''}`} data-testid="drawing"
        onWheel={(e) => {
          // A wheel, a pinch or two fingers up and down zoom; two fingers sideways or Shift pan; one or the other for a whole run.
          const mode = wheelMode.current(e);
          if (!mode) return;
          if (mode === 'pan') { setView({ ...v, cx: v.cx + e.deltaX / v.scale, cy: v.cy - e.deltaY / v.scale }); return; }
          const r = ref.current!.getBoundingClientRect();
          const p = toWorld(e.clientX - r.left, e.clientY - r.top);
          const scale = clampScale(v.scale * wheelZoom(e, size.h));
          const now = performance.now();
          if (now - wheelBurst.current > 400) pushHistory(key, v);
          wheelBurst.current = now;
          setView({ scale, cx: p.x - (p.x - v.cx) * (v.scale / scale), cy: p.y - (p.y - v.cy) * (v.scale / scale) });
        }}
        onPointerDown={(e) => { if (middleTwice.current(e)) { fit(); return; } if (editor.onPointerDown(e)) return; drag.current = { x: e.clientX, y: e.clientY, view: v, moved: false }; (e.target as Element).setPointerCapture?.(e.pointerId); }}
        onPointerMove={(e) => {
          if (editor.onPointerMove(e)) return;
          const d = drag.current;
          if (d) {
            if (Math.hypot(e.clientX - d.x, e.clientY - d.y) > 4) d.moved = true;
            const step = useCad.getState().step;
            if (d.moved && e.buttons === 1 && (!step || step.want === 'select')) {
              const r = ref.current!.getBoundingClientRect();
              const to = { x: e.clientX - r.left, y: e.clientY - r.top };
              setBox((cur) => dragTo(cur ?? { a: { x: d.x - r.left, y: d.y - r.top }, b: to }, to));
              return;
            }
            if (d.moved) { setView({ ...d.view, cx: d.view.cx - (e.clientX - d.x) / d.view.scale, cy: d.view.cy + (e.clientY - d.y) / d.view.scale }); return; }
          }
          setHover(at(e).aimed);
        }}
        onPointerUp={(e) => {
          if (editor.onPointerUp()) return;
          const d = drag.current;
          drag.current = null;
          if (box) { selectBox(box, e.shiftKey); setBox(null); return; }
          if (!d || d.moved || e.button !== 0) return;
          const { raw, aimed } = at(e);
          const step = useCad.getState().step;
          if (step) { void feedPoint(step.want === 'pick' || step.want === 'select' ? raw : aimed.point); acquire(aimed.point); return; }
          if (gestureClick(aimed.point, raw)) return;
          select(pickAny(raw), e.shiftKey);
        }}
        onPointerLeave={() => setHover(null)}
        onDoubleClick={(e) => {
          if (useCad.getState().step) return;
          const r = ref.current!.getBoundingClientRect();
          const w = toWorld(e.clientX - r.left, e.clientY - r.top);
          const id = pickAny(w);
          if (id && !openPickedView(id)) void editDimension(id, w);
        }}>
        <rect width="100%" height="100%" className="paper" />
        {drawing && <DrawingLayer drawing={drawing} toScreen={toScreen} pxPerMetre={v.scale} />}
        {(drawing?.marks ?? []).filter((m) => markSelected(m.id, selected)).map((m) => (
          <path key={`sel:${m.id}`} className="plan-mark selected" d={Array.from({ length: m.segments.length / 4 }, (_, k) => {
            const a = toScreen({ x: m.segments[4 * k], y: m.segments[4 * k + 1] }), b = toScreen({ x: m.segments[4 * k + 2], y: m.segments[4 * k + 3] });
            return `M${a.x} ${a.y}L${b.x} ${b.y}`;
          }).join('')} />
        ))}
        {pts.length > 0 && (() => {
          // The next point: the cursor's, or where a typed length puts it.
          const cur = hover ? (live.owner === 'tool' && (live.len || live.ang) ? livePoint({ ...live, cursor: hover.point }) : null) ?? hover.point : null;
          const chain = [...pts, ...(cur ? [cur] : [])].map(toScreen);
          const closed = tool?.gesture?.kind === 'polygon' || tool?.gesture?.kind === 'rectangle';
          const d = tool?.gesture?.kind === 'rectangle' && chain.length === 2
            ? `M${chain[0].x} ${chain[0].y}H${chain[1].x}V${chain[1].y}H${chain[0].x}Z`
            : chain.map((q, i) => `${i ? 'L' : 'M'}${q.x} ${q.y}`).join('') + (closed && chain.length > 2 ? 'Z' : '');
          return <path className="plan-preview" d={d} fill="none" stroke="currentColor" strokeDasharray="5 4" opacity={0.7} />;
        })()}
        {box && <SelectRegion d={box} />}
        {editor.render}
        {(cadStep || editor.dragging) && <CadOverlay aimed={hover} toScreen={toScreen} />}
      </svg>
      {drawing?.problem && <div className="error-text view-problem">{drawing.problem}</div>}
      {active && <LiveBox toScreen={toScreen} />}
      {active && <LiveBar />}
      <CommandLine />
    </div>
  );
}
