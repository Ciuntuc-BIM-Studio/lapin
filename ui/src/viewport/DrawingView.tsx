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
import { execute, useSession } from '../engine/session';
import type { Vec2 } from '../platform/module';
import { DrawingLayer } from './DrawingLayer';
import { useDrawing } from './drawing';
import { editDimension } from './dimensionEdit';
import { distToSegment } from './snap';
import { CadOverlay } from './cad/canvas';
import { useCadCanvas, type Aimed } from './cad/aim';
import { CommandLine } from './cad/CommandLine';
import { feedPoint, useCad } from './cad/session';
import { boxTest, segsOf } from './selectBox';

interface View { cx: number; cy: number; scale: number }

export function DrawingView() {
  const id = useUi((s) => s.drawingViewId);
  const node = useSession((s) => (id ? s.nodes.get(id) : undefined));
  const nodes = useSession((s) => s.nodes);
  const selected = useUi((s) => s.selected);
  const drawing = useDrawing(id);
  const ref = useRef<SVGSVGElement>(null);
  const [size, setSize] = useState({ w: 0, h: 0 });
  const [view, setView] = useState<View | null>(null);
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
    const w = Math.max(b[2] - b[0], 1), h = Math.max(b[3] - b[1], 1);
    setView({ cx: (b[0] + b[2]) / 2, cy: (b[1] + b[3]) / 2, scale: Math.min(size.w / w, size.h / h) * 0.85 });
  }, [drawing?.bounds, size]);
  // A new view fits itself once its drawing arrives.
  useEffect(() => { setView(null); }, [id]);
  useEffect(() => { if (!view && drawing?.bounds) fit(); }, [drawing, view, fit]);

  const v = view ?? { cx: 0, cy: 0, scale: 40 };
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
    return { raw, aimed };
  };

  return (
    <div className="view-pane">
      <div className="view-toolbar">
        <span className="view-title">{node?.name ?? 'View'} · 1:{drawing?.scale ?? 100}</span>
{(drawing?.lineworkOrphans?.length ?? 0) > 0 && id && (
          <button type="button" className="chip warn" title="The edges they were on are gone: remove them" onClick={() => void execute('view.lineworkReset', { id: id, orphans: true })}>
            {drawing!.lineworkOrphans!.length} line edit(s) lost their edge · clean
          </button>
        )}
                <button type="button" className="icon" title="Fit" onClick={fit}><Maximize size={14} /></button>
      </div>
      <svg ref={ref} className={`plan-svg drawing-svg${cadStep ? ' tool' : ''}`} data-testid="drawing"
        onWheel={(e) => {
          // A trackpad's two fingers pan; a pinch (ctrl) or a mouse wheel zooms.
          if (!e.ctrlKey && (e.deltaX !== 0 || !Number.isInteger(e.deltaY))) { setView({ ...v, cx: v.cx + e.deltaX / v.scale, cy: v.cy - e.deltaY / v.scale }); return; }
          const r = ref.current!.getBoundingClientRect();
          const p = toWorld(e.clientX - r.left, e.clientY - r.top);
          const scale = Math.min(4000, Math.max(2, v.scale * Math.exp(-e.deltaY * 0.0015)));
          setView({ scale, cx: p.x - (p.x - v.cx) * (v.scale / scale), cy: p.y - (p.y - v.cy) * (v.scale / scale) });
        }}
        onPointerDown={(e) => { drag.current = { x: e.clientX, y: e.clientY, view: v, moved: false }; (e.target as Element).setPointerCapture?.(e.pointerId); }}
        onPointerMove={(e) => {
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
          const id = pickAny(toWorld(e.clientX - r.left, e.clientY - r.top));
          if (id) void editDimension(id);
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
        {cadStep && <CadOverlay aimed={hover} toScreen={toScreen} />}
      </svg>
      {drawing?.problem && <div className="error-text view-problem">{drawing.problem}</div>}
      <CommandLine />
    </div>
  );
}
