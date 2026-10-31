/**
 * A section or an elevation: the view's generated drawing, panned and zoomed
 * (wheel, drag). The drawing's x runs along the view, its y up. Drafting
 * works here as in plans: the command line, snaps, ortho and guide lines; a
 * click selects an annotation or a drafted shape, a left drag selects by a
 * box (window to the right, crossing to the left), a middle or right drag pans.
 */

import { useCallback, useEffect, useRef, useState } from 'react';
import { Maximize } from 'lucide-react';
import { select, useUi } from '../app/store';
import { usePaneActive, usePaneField } from '../app/pane';
import { execute, useSession } from '../engine/session';
import type { Vec2 } from '../platform/module';
import { DrawingLayer } from './DrawingLayer';
import { useDrawing } from './drawing';
import { editDimension } from './dimensionEdit';
import { openPickedView } from './openView';
import { distToSegment } from './snap';
import { CadOverlay } from './cad/canvas';
import { useCadCanvas, type Aimed } from './cad/aim';
import { CommandLine } from './cad/CommandLine';
import { feedPoint, useCad } from './cad/session';
import { boxTest, segsOf } from './selectBox';
import { useGripEditor } from './GripLayer';
import { LiveBar, LiveBox } from './LiveBox';
import { liveCursor } from './liveInput';
import { actualScale, boundsOf, framing, middleDouble, popHistory, pushHistory, recall, remember, useZoomHost, wheelPans, wheelZoom, type View2, type ZoomHost } from './zoom';
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
  /** A selection box being dragged (screen px). */
  const [box, setBox] = useState<{ a: Vec2; b: Vec2 } | null>(null);
  const cadStep = useCad((s) => s.step);

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
  const toScreen = useCallback((p: Vec2) => ({ x: (p.x - v.cx) * v.scale + size.w / 2, y: size.h / 2 - (p.y - v.cy) * v.scale }), [v.cx, v.cy, v.scale, size]);
  const toWorld = useCallback((x: number, y: number): Vec2 => ({ x: (x - size.w / 2) / v.scale + v.cx, y: (size.h / 2 - y) / v.scale + v.cy }), [v.cx, v.cy, v.scale, size]);

  /** The annotation or drafted shape under a point. */
  const pickAny = useCallback((p: Vec2): string | null => {
    const tol = 6 / v.scale;
    for (const m of drawing?.marks ?? []) {
      const s = m.segments;
      for (let k = 0; k + 3 < s.length; k += 4)
        if (distToSegment(p, { x: s[k], y: s[k + 1] }, { x: s[k + 2], y: s[k + 3] }) < tol && nodes.get(m.id.includes('#') ? m.id.slice(0, m.id.indexOf('#')) : m.id)) return m.id;
    }
    return null;
  }, [drawing, nodes, v.scale]);

  /** The annotations and drafted shapes a box takes, added to the selection with shift (or while a command asks for a selection). */
  const selectBox = (b: { a: Vec2; b: Vec2 }, add: boolean) => {
    const takes = boxTest(toWorld(b.a.x, b.a.y), toWorld(b.b.x, b.b.y), b.b.x >= b.a.x);
    const ids: string[] = [];
    for (const m of drawing?.marks ?? []) {
      const owner = m.id.includes('#') ? m.id.slice(0, m.id.indexOf('#')) : m.id;
      if (nodes.get(owner) && takes(segsOf(m.segments)) && !ids.includes(m.id)) ids.push(m.id);
    }
    const before = add || useCad.getState().step ? useUi.getState().selected : [];
    const next = [...new Set([...before, ...ids])];
    useUi.setState({ selected: next, selection: next[next.length - 1] ?? null });
  };

  const { aim, acquire } = useCadCanvas({ viewId: id, drawing, scale: v.scale, pickAny });

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
      <svg ref={ref} className={`plan-svg drawing-svg${cadStep ? ' tool' : ''}`} data-testid="drawing"
        onWheel={(e) => {
          // A wheel, a pinch or two fingers up and down zoom; two fingers sideways or Shift pan.
          if (wheelPans(e)) { setView({ ...v, cx: v.cx + e.deltaX / v.scale, cy: v.cy - e.deltaY / v.scale }); return; }
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
              setBox({ a: { x: d.x - r.left, y: d.y - r.top }, b: { x: e.clientX - r.left, y: e.clientY - r.top } });
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
        {(drawing?.marks ?? []).filter((m) => selected.includes(m.id)).map((m) => (
          <path key={`sel:${m.id}`} className="plan-mark selected" d={Array.from({ length: m.segments.length / 4 }, (_, k) => {
            const a = toScreen({ x: m.segments[4 * k], y: m.segments[4 * k + 1] }), b = toScreen({ x: m.segments[4 * k + 2], y: m.segments[4 * k + 3] });
            return `M${a.x} ${a.y}L${b.x} ${b.y}`;
          }).join('')} />
        ))}
        {box && <rect className={`select-box${box.b.x >= box.a.x ? ' window' : ' crossing'}`} x={Math.min(box.a.x, box.b.x)} y={Math.min(box.a.y, box.b.y)} width={Math.abs(box.b.x - box.a.x)} height={Math.abs(box.b.y - box.a.y)} />}
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
