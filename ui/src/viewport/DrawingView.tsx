/**
 * A section or an elevation: the view's generated drawing, panned and zoomed
 * (wheel, drag). The drawing's x runs along the view, its y up.
 */

import { useCallback, useEffect, useRef, useState } from 'react';
import { Maximize } from 'lucide-react';
import { useUi } from '../app/store';
import { useSession } from '../engine/session';
import { DrawingLayer } from './DrawingLayer';
import { useDrawing } from './drawing';

interface View { cx: number; cy: number; scale: number }

export function DrawingView() {
  const id = useUi((s) => s.drawingViewId);
  const node = useSession((s) => (id ? s.nodes.get(id) : undefined));
  const drawing = useDrawing(id);
  const ref = useRef<SVGSVGElement>(null);
  const [size, setSize] = useState({ w: 0, h: 0 });
  const [view, setView] = useState<View | null>(null);
  const drag = useRef<{ x: number; y: number; view: View } | null>(null);

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
  const toScreen = useCallback((p: { x: number; y: number }) => ({ x: (p.x - v.cx) * v.scale + size.w / 2, y: size.h / 2 - (p.y - v.cy) * v.scale }), [v.cx, v.cy, v.scale, size]);

  return (
    <div className="view-pane">
      <div className="view-toolbar">
        <span className="view-title">{node?.name ?? 'View'} · 1:{drawing?.scale ?? 100}</span>
        <button type="button" className="icon" title="Fit" onClick={fit}><Maximize size={14} /></button>
      </div>
      <svg ref={ref} className="plan-svg drawing-svg" data-testid="drawing"
        onWheel={(e) => { const k = Math.exp(-e.deltaY * 0.0015); setView({ ...v, scale: Math.min(4000, Math.max(2, v.scale * k)) }); }}
        onPointerDown={(e) => { drag.current = { x: e.clientX, y: e.clientY, view: v }; (e.target as Element).setPointerCapture?.(e.pointerId); }}
        onPointerMove={(e) => { const d = drag.current; if (d) setView({ ...d.view, cx: d.view.cx - (e.clientX - d.x) / d.view.scale, cy: d.view.cy + (e.clientY - d.y) / d.view.scale }); }}
        onPointerUp={() => { drag.current = null; }}>
        <rect width="100%" height="100%" className="paper" />
        {drawing && <DrawingLayer drawing={drawing} toScreen={toScreen} pxPerMetre={v.scale} />}
      </svg>
      {drawing?.problem && <div className="error-text view-problem">{drawing.problem}</div>}
    </div>
  );
}
