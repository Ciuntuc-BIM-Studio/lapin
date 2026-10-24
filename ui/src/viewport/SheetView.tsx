/**
 * A sheet: its paper as it prints (the engine composes it: frame, title
 * block, views at their scale), its views moved by dragging, a view placed
 * from the list, PDF and DXF out.
 */

import { useCallback, useEffect, useRef, useState } from 'react';
import { FileDown, Maximize } from 'lucide-react';
import { notify, select, useUi } from '../app/store';
import { execute, query, useSession } from '../engine/session';
import { exportFile } from '../engine/files';
import { DrawingLayer } from './DrawingLayer';
import type { PenDto } from './drawing';

interface SheetDrawing { w: number; h: number; pens: PenDto[]; viewports: { id: string; viewId: string; x0: number; y0: number; x1: number; y1: number; scale: number }[] }

export function SheetView() {
  const id = useUi((s) => s.sheetId);
  const nodes = useSession((s) => s.nodes);
  const sheet = id ? nodes.get(id) : undefined;
  const [d, setD] = useState<SheetDrawing | null>(null);
  const ref = useRef<SVGSVGElement>(null);
  const [size, setSize] = useState({ w: 0, h: 0 });
  const [z, setZ] = useState<{ k: number; ox: number; oy: number } | null>(null);
  const drag = useRef<{ kind: 'pan' | 'vp'; x: number; y: number; vp?: SheetDrawing['viewports'][number]; start: { k: number; ox: number; oy: number } } | null>(null);
  const [moved, setMoved] = useState<{ id: string; dx: number; dy: number } | null>(null);

  useEffect(() => {
    if (!id) return;
    let live = true;
    const t = setTimeout(() => { void query<SheetDrawing>('sheet.drawing', { id }).then((r) => { if (live && r) setD(r); }); }, 100);
    return () => { live = false; clearTimeout(t); };
  }, [id, nodes]);
  useEffect(() => { setZ(null); setD(null); }, [id]);
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const ro = new ResizeObserver(([e]) => setSize({ w: e.contentRect.width, h: e.contentRect.height }));
    ro.observe(el);
    return () => ro.disconnect();
  }, []);
  const fit = useCallback(() => {
    if (!d || !size.w) return;
    const k = Math.min(size.w / (d.w + 20), size.h / (d.h + 20));
    setZ({ k, ox: (size.w - d.w * k) / 2, oy: (size.h - d.h * k) / 2 });
  }, [d, size]);
  useEffect(() => { if (!z && d) fit(); }, [d, z, fit]);

  const v = z ?? { k: 1, ox: 0, oy: 0 };
  const toScreen = useCallback((p: { x: number; y: number }) => ({ x: p.x * v.k + v.ox, y: p.y * v.k + v.oy }), [v.k, v.ox, v.oy]);
  const placed = new Set(d?.viewports.map((x) => x.viewId) ?? []);
  const views = [...nodes.values()].filter((n) => n.type === 'LapinView');
  const storeyPlans = [...nodes.values()].filter((n) => n.type === 'IfcBuildingStorey').map((s) => ({ id: `plan:${s.id}`, name: `${s.name} (plan)` }));
  const schedules = [...nodes.values()].filter((n) => n.type === 'LapinSchedule').map((x) => ({ id: x.id, name: x.name }));
  const placeable = [...storeyPlans, ...views.map((x) => ({ id: x.id, name: x.name })), ...schedules].filter((x) => !placed.has(x.id));

  const exportAs = async (kind: 'pdf' | 'dxf') => {
    if (!sheet) return;
    const base = `${sheet.props.number ?? ''} ${sheet.name}`.trim();
    await exportFile(kind === 'pdf' ? 'export.pdf' : 'export.dxf', kind === 'pdf' ? { sheets: [sheet.id] } : { id: sheet.id }, `${base}.${kind}`, kind);
  };

  if (!sheet) return <div className="view-pane"><p className="muted">No sheet.</p></div>;
  return (
    <div className="view-pane">
      <div className="view-toolbar">
        <span className="view-title">{String(sheet.props.number ?? '')} · {sheet.name}</span>
        <select value="" onChange={(e) => e.target.value && void execute('sheet.addViewport', { id: sheet.id, viewId: e.target.value })} title="Place a view on the sheet">
          <option value="">Place view…</option>
          {placeable.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
        </select>
        <button type="button" className="icon" title="Export PDF" onClick={() => void exportAs('pdf')}><FileDown size={14} /><span className="small">PDF</span></button>
        <button type="button" className="icon" title="Export DXF" onClick={() => void exportAs('dxf')}><FileDown size={14} /><span className="small">DXF</span></button>
        <button type="button" className="icon" title="Fit" onClick={fit}><Maximize size={14} /></button>
      </div>
      <svg ref={ref} className="plan-svg sheet-svg" data-testid="sheet"
        onWheel={(e) => {
          const r = ref.current!.getBoundingClientRect();
          const mx = e.clientX - r.left, my = e.clientY - r.top, f = Math.exp(-e.deltaY * 0.0015);
          setZ({ k: v.k * f, ox: mx - (mx - v.ox) * f, oy: my - (my - v.oy) * f });
        }}
        onPointerDown={(e) => {
          const r = ref.current!.getBoundingClientRect();
          const px = (e.clientX - r.left - v.ox) / v.k, py = (e.clientY - r.top - v.oy) / v.k;
          const vp = d?.viewports.find((b) => px >= b.x0 && px <= b.x1 && py >= b.y0 && py <= b.y1);
          drag.current = { kind: vp && e.button === 0 ? 'vp' : 'pan', x: e.clientX, y: e.clientY, vp, start: v };
          if (vp) select(sheet.id);
          (e.target as Element).setPointerCapture?.(e.pointerId);
        }}
        onPointerMove={(e) => {
          const g = drag.current;
          if (!g) return;
          if (g.kind === 'pan') setZ({ ...g.start, ox: g.start.ox + e.clientX - g.x, oy: g.start.oy + e.clientY - g.y });
          else if (g.vp) setMoved({ id: g.vp.id, dx: (e.clientX - g.x) / v.k, dy: (e.clientY - g.y) / v.k });
        }}
        onPointerUp={() => {
          const g = drag.current;
          drag.current = null;
          if (g?.kind === 'vp' && g.vp && moved && Math.hypot(moved.dx, moved.dy) > 0.5) {
            const vp = g.vp;
            void execute('sheet.updateViewport', { id: sheet.id, viewportId: vp.id, x: (vp.x0 + vp.x1) / 2 + moved.dx, y: (vp.y0 + vp.y1) / 2 + moved.dy }).then(() => notify(null));
          }
          setMoved(null);
        }}>
        {d && (
          <>
            <rect x={v.ox} y={v.oy} width={d.w * v.k} height={d.h * v.k} className="paper-sheet" />
            <g transform={moved ? `translate(${moved.dx * v.k} ${moved.dy * v.k})` : undefined}>
              <DrawingLayer drawing={{ pens: d.pens, scale: 1000 }} toScreen={toScreen} pxPerMetre={v.k} />
            </g>
            {d.viewports.map((b) => {
              const a = toScreen({ x: b.x0, y: b.y0 });
              const dx = moved?.id === b.id ? moved.dx * v.k : 0, dy = moved?.id === b.id ? moved.dy * v.k : 0;
              return (
                <g key={b.id} className="sheet-viewport">
                  <rect x={a.x + dx} y={a.y + dy} width={(b.x1 - b.x0) * v.k} height={(b.y1 - b.y0) * v.k} />
                  <foreignObject x={a.x + dx + (b.x1 - b.x0) * v.k - 22} y={a.y + dy + 2} width={20} height={20}>
                    <button type="button" className="icon mini" title="Take the view off the sheet" onPointerDown={(e) => e.stopPropagation()}
                      onClick={() => void execute('sheet.removeViewport', { id: sheet.id, viewportId: b.id })}>×</button>
                  </foreignObject>
                </g>
              );
            })}
          </>
        )}
      </svg>
    </div>
  );
}
