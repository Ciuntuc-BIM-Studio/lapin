/**
 * A sheet: its paper as it prints (the engine composes it: frame, title
 * block, views at their scale), its views moved by dragging (the view's own
 * pens alone go with the pointer; snapping to the frame and to each other,
 * with guides), nudged with the arrows (1 mm, 10 mm
 * with Shift), a schedule's width set by its side handles, a double-click
 * opening the view or schedule; warnings for what runs past the frame or over
 * the title block, and a schedule's next page placed on a new sheet. PDF and
 * DXF out.
 */

import { useCallback, useEffect, useRef, useState } from 'react';
import { FileDown, Maximize, MessageSquare } from 'lucide-react';
import { notify, select, useUi } from '../app/store';
import { usePaneActive, usePaneField } from '../app/pane';
import { execute, query, useSession } from '../engine/session';
import { exportFile } from '../engine/files';
import { RemarkCard, RemarksOverlay } from '../modules/core/cde/pdfRemarks/RemarksOverlay';
import { countOpen, remarksOf, useRemarkUi } from '../modules/core/cde/pdfRemarks/remarks';
import { DrawingLayer } from './DrawingLayer';
import type { PenDto } from './drawing';
import { PX_PER_MM, middleDouble, popHistory, pushHistory, recall, remember, useZoomHost, wheelZoom, type ZoomHost } from './zoom';

interface Vp { id: string; viewId: string; x0: number; y0: number; x1: number; y1: number; scale: number; content: boolean; page: number; pages: number; width?: number; warnings: string[] }
interface SheetDrawing { w: number; h: number; pens: PenDto[]; frame: number[]; titleBlock: number[]; viewports: Vp[] }
type Drag =
  | { kind: 'pan'; x: number; y: number; start: { k: number; ox: number; oy: number } }
  | { kind: 'vp'; x: number; y: number; vp: Vp }
  | { kind: 'width'; x: number; y: number; vp: Vp; side: -1 | 1 };

const SNAP = 3; // paper mm

export function SheetView() {
  const id = usePaneField('sheetId');
  const paneActive = usePaneActive();
  const nodes = useSession((s) => s.nodes);
  const sheet = id ? nodes.get(id) : undefined;
  const [d, setD] = useState<SheetDrawing | null>(null);
  const ref = useRef<SVGSVGElement>(null);
  const [size, setSize] = useState({ w: 0, h: 0 });
  type Zoom = { k: number; ox: number; oy: number };
  const key = id ?? 'sheet:none';
  const [z, setZ] = useState<Zoom | null>(() => recall<Zoom>(key) ?? null);
  useEffect(() => { if (z) remember(key, z); }, [key, z]);
  const go = useCallback((v: Zoom) => { setZ((old) => { if (old) pushHistory(key, old); return v; }); }, [key]);
  const drag = useRef<Drag | null>(null);
  const [moved, setMoved] = useState<{ id: string; dx: number; dy: number; guides: number[][] } | null>(null);
  const [widened, setWidened] = useState<{ id: string; w: number } | null>(null);
  const [active, setActive] = useState<string | null>(null);
  // The remarks read from reviewed PDFs: shown over the paper (the screen alone; the sheet prints without them).
  const showRemarks = useRemarkUi((s) => s.show);
  const remarkCount = id ? countOpen(nodes, id) : 0;
  // The overlay draws the open and the resolved ones (a closed remark leaves it): the toggle is there while any is drawn, its number the open ones.
  const remarksDrawn = id ? remarksOf(nodes, id).some((r) => r.status !== 'closed') : false;

  useEffect(() => {
    if (!id) return;
    let live = true;
    const t = setTimeout(() => { void query<SheetDrawing>('sheet.drawing', { id }).then((r) => { if (live && r) setD(r); }); }, 100);
    return () => { live = false; clearTimeout(t); };
  }, [id, nodes]);
  useEffect(() => { setZ(recall<Zoom>(id ?? 'sheet:none') ?? null); setD(null); setActive(null); }, [id]);
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const ro = new ResizeObserver(([e]) => setSize({ w: e.contentRect.width, h: e.contentRect.height }));
    ro.observe(el);
    return () => ro.disconnect();
  }, []);
  /** A box of paper (mm) framed in the pane. */
  const frame = useCallback((x0: number, y0: number, x1: number, y1: number, pad = 20) => {
    if (!size.w) return;
    const k = Math.min(size.w / (x1 - x0 + pad), size.h / (y1 - y0 + pad));
    go({ k, ox: size.w / 2 - ((x0 + x1) / 2) * k, oy: size.h / 2 - ((y0 + y1) / 2) * k });
  }, [size, go]);
  const fit = useCallback(() => { if (d) frame(0, 0, d.w, d.h); }, [d, frame]);
  useEffect(() => { if (!z && d) fit(); }, [d, z, fit]);

  const moveTo = useCallback((vp: Vp, dx: number, dy: number) => {
    if (!sheet) return;
    void execute('sheet.updateViewport', { id: sheet.id, viewportId: vp.id, x: (vp.x0 + vp.x1) / 2 + dx, y: (vp.y0 + vp.y1) / 2 + dy }).then(() => notify(null));
  }, [sheet]);

  // The arrows nudge the active view: 1 mm, 10 mm with Shift.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.target as HTMLElement)?.closest('input, select, textarea') || !active || !d) return;
      const step = e.shiftKey ? 10 : 1;
      const delta = ({ ArrowLeft: [-step, 0], ArrowRight: [step, 0], ArrowUp: [0, -step], ArrowDown: [0, step] } as Record<string, number[]>)[e.key];
      const vp = d.viewports.find((b) => b.id === active);
      if (!delta || !vp) return;
      e.preventDefault();
      e.stopPropagation();
      moveTo(vp, delta[0], delta[1]);
    };
    window.addEventListener('keydown', onKey, true);
    return () => window.removeEventListener('keydown', onKey, true);
  }, [active, d, moveTo]);

  const v = z ?? { k: 1, ox: 0, oy: 0 };
  const zoomAbout = (k: number, mx = size.w / 2, my = size.h / 2) => ({ k, ox: mx - (mx - v.ox) * (k / v.k), oy: my - (my - v.oy) * (k / v.k) });
  const host: ZoomHost = {
    fitAll: fit,
    fitSelection: () => { const vp = d?.viewports.find((b) => b.id === active); if (!vp) return false; frame(vp.x0, vp.y0, vp.x1, vp.y1, 30); return true; },
    zoomWindow: (a, b) => frame(Math.min(a.x, b.x), Math.min(a.y, b.y), Math.max(a.x, b.x), Math.max(a.y, b.y), 0),
    actual: () => go(zoomAbout(PX_PER_MM)),
    zoomBy: (f) => go(zoomAbout(v.k * f)),
    previous: () => { const p = popHistory<Zoom>(key); if (!p) return false; setZ(p); return true; },
  };
  useZoomHost(paneActive, host);
  const middleTwice = useRef(middleDouble());
  const wheelBurst = useRef(0);
  const toScreen = useCallback((p: { x: number; y: number }) => ({ x: p.x * v.k + v.ox, y: p.y * v.k + v.oy }), [v.k, v.ox, v.oy]);
  const paperAt = (e: { clientX: number; clientY: number }) => {
    const r = ref.current!.getBoundingClientRect();
    return { x: (e.clientX - r.left - v.ox) / v.k, y: (e.clientY - r.top - v.oy) / v.k };
  };
  const placed = new Set(d?.viewports.map((x) => x.viewId) ?? []);
  const views = [...nodes.values()].filter((n) => n.type === 'LapinView');
  const storeyPlans = [...nodes.values()].filter((n) => n.type === 'IfcBuildingStorey').map((s) => ({ id: `plan:${s.id}`, name: `${s.name} (plan)` }));
  const schedules = [...nodes.values()].filter((n) => n.type === 'LapinSchedule').map((x) => ({ id: x.id, name: x.name }));
  const placeable = [...storeyPlans, ...views.map((x) => ({ id: x.id, name: x.name })), ...schedules].filter((x) => !placed.has(x.id));

  /** A dragged view snapped: its edges and middle to the frame's and to the other views', with the guides it holds on (paper mm). */
  const snapMove = (vp: Vp, dx: number, dy: number) => {
    if (!d) return { dx, dy, guides: [] as number[][] };
    const xs = [d.frame[0], d.frame[2], (d.frame[0] + d.frame[2]) / 2], ys = [d.frame[1], d.frame[3], (d.frame[1] + d.frame[3]) / 2];
    for (const o of d.viewports) if (o.id !== vp.id) { xs.push(o.x0, o.x1, (o.x0 + o.x1) / 2); ys.push(o.y0, o.y1, (o.y0 + o.y1) / 2); }
    const mine = (a: number, b: number) => [a, b, (a + b) / 2];
    let bx: { d: number; at: number } | null = null, by: { d: number; at: number } | null = null;
    for (const m of mine(vp.x0 + dx, vp.x1 + dx)) for (const t of xs) { const g = t - m; if (Math.abs(g) < SNAP && (!bx || Math.abs(g) < Math.abs(bx.d))) bx = { d: g, at: t }; }
    for (const m of mine(vp.y0 + dy, vp.y1 + dy)) for (const t of ys) { const g = t - m; if (Math.abs(g) < SNAP && (!by || Math.abs(g) < Math.abs(by.d))) by = { d: g, at: t }; }
    const guides: number[][] = [];
    if (bx) guides.push([bx.at, 0, bx.at, d.h]);
    if (by) guides.push([0, by.at, d.w, by.at]);
    return { dx: dx + (bx?.d ?? 0), dy: dy + (by?.d ?? 0), guides };
  };

  const exportAs = async (kind: 'pdf' | 'dxf') => {
    if (!sheet) return;
    const base = `${sheet.props.number ?? ''} ${sheet.name}`.trim();
    await exportFile(kind === 'pdf' ? 'export.pdf' : 'export.dxf', kind === 'pdf' ? { sheets: [sheet.id] } : { id: sheet.id }, `${base}.${kind}`, kind);
  };

  /** A schedule's next page on a new sheet. */
  const continueOn = async (vp: Vp) => {
    const r = await execute('sheet.create', { paper: sheet?.props.paper, orientation: sheet?.props.orientation, name: `${nodes.get(vp.viewId)?.name ?? 'Schedule'} (continued)` }) as { id?: string } | null;
    if (r?.id) { await execute('sheet.addViewport', { id: r.id, viewId: vp.viewId, page: vp.page + 1 }); useUi.setState({ sheetId: r.id }); }
  };

  /** Opening what a viewport shows. */
  const open = (vp: Vp) => {
    const n = nodes.get(vp.viewId);
    if (vp.viewId.startsWith('plan:')) useUi.setState({ view: 'plan', storeyId: vp.viewId.slice(5), planViewId: null });
    else if (n?.type === 'LapinSchedule') useUi.setState({ view: 'schedule', scheduleId: n.id });
    else if (n?.props.kind === 'plan') useUi.setState({ view: 'plan', planViewId: n.id, storeyId: String(n.props.storeyId ?? '') || null });
    else if (n) useUi.setState({ view: 'drawing', drawingViewId: n.id });
  };

  if (!sheet) return <div className="view-pane"><p className="muted">No sheet.</p></div>;
  const problems = (d?.viewports ?? []).flatMap((b) => [
    ...b.warnings.map((w) => ({ b, text: `${nodes.get(b.viewId)?.name ?? 'A view'} ${w === 'outside' ? 'runs past the frame' : 'lies over the title block'}` })),
    ...(b.content && b.page < b.pages && !(d?.viewports ?? []).some((o) => o.viewId === b.viewId && o.page === b.page + 1)
      ? [{ b, text: `${nodes.get(b.viewId)?.name ?? 'The schedule'} goes on: ${b.pages - b.page} more page(s)`, next: true }] : []),
  ]);
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
        {remarksDrawn && (
          <button type="button" className={`icon${showRemarks ? ' on' : ''}`} title="Remarks read from PDFs: show or hide (they never print); the number counts the open ones" data-testid="sheet-remarks-toggle"
            onClick={() => useRemarkUi.setState({ show: !showRemarks, highlight: null })}><MessageSquare size={14} />{remarkCount > 0 && <span className="small">{remarkCount}</span>}</button>
        )}
        <button type="button" className="chip scale" title="The sheet's size on screen against paper · click for paper size (ZA)" onClick={host.actual}>{Math.round((v.k / PX_PER_MM) * 100)} %</button>
        <button type="button" className="icon" title="Fit (ZE)" onClick={fit}><Maximize size={14} /></button>
      </div>
      {problems.length > 0 && (
        <div className="sheet-warnings">
          {problems.map((p, i) => (
            <span key={i} className="chip warn">{p.text}{'next' in p && p.next && <button type="button" className="link" onClick={() => void continueOn(p.b)}>place it on a new sheet</button>}</span>
          ))}
        </div>
      )}
      <svg ref={ref} className="plan-svg sheet-svg" data-testid="sheet" tabIndex={0}
        onWheel={(e) => {
          const r = ref.current!.getBoundingClientRect();
          const now = performance.now();
          if (now - wheelBurst.current > 400) pushHistory(key, v);
          wheelBurst.current = now;
          setZ(zoomAbout(v.k * wheelZoom(e, size.h), e.clientX - r.left, e.clientY - r.top));
        }}
        onPointerDown={(e) => {
          if (middleTwice.current(e)) { fit(); return; }
          const p = paperAt(e);
          const vp = d?.viewports.find((b) => p.x >= b.x0 && p.x <= b.x1 && p.y >= b.y0 && p.y <= b.y1);
          if (vp && e.button === 0) { drag.current = { kind: 'vp', x: e.clientX, y: e.clientY, vp }; setActive(vp.id); select(sheet.id); }
          else { drag.current = { kind: 'pan', x: e.clientX, y: e.clientY, start: v }; if (e.button === 0) setActive(null); }
          (e.target as Element).setPointerCapture?.(e.pointerId);
        }}
        onPointerMove={(e) => {
          const g = drag.current;
          if (!g) return;
          if (g.kind === 'pan') setZ({ ...g.start, ox: g.start.ox + e.clientX - g.x, oy: g.start.oy + e.clientY - g.y });
          else if (g.kind === 'vp') { const s = snapMove(g.vp, (e.clientX - g.x) / v.k, (e.clientY - g.y) / v.k); setMoved({ id: g.vp.id, ...s }); }
          else {
            const dx = (e.clientX - g.x) / v.k * g.side;
            setWidened({ id: g.vp.id, w: Math.max(60, (g.vp.x1 - g.vp.x0) + 2 * dx) });
          }
        }}
        onPointerUp={() => {
          const g = drag.current;
          drag.current = null;
          if (g?.kind === 'vp' && moved && Math.hypot(moved.dx, moved.dy) > 0.5) moveTo(g.vp, moved.dx, moved.dy);
          if (g?.kind === 'width' && widened) void execute('sheet.updateViewport', { id: sheet.id, viewportId: g.vp.id, width: Math.round(widened.w) });
          setMoved(null);
          setWidened(null);
        }}
        onDoubleClick={(e) => {
          const p = paperAt(e);
          const vp = d?.viewports.find((b) => p.x >= b.x0 && p.x <= b.x1 && p.y >= b.y0 && p.y <= b.y1);
          if (vp) open(vp);
        }}>
        {d && (
          <>
            <rect x={v.ox} y={v.oy} width={d.w * v.k} height={d.h * v.k} className="paper-sheet" />
            {/* While a view is dragged, its own pens go with the pointer; the frame, the title block and the other views stay. */}
            {moved ? (
              <>
                <DrawingLayer drawing={{ pens: d.pens.filter((p) => p.viewport !== moved.id), scale: 1000 }} toScreen={toScreen} pxPerMetre={v.k} />
                <g transform={`translate(${moved.dx * v.k} ${moved.dy * v.k})`}>
                  <DrawingLayer drawing={{ pens: d.pens.filter((p) => p.viewport === moved.id), scale: 1000 }} toScreen={toScreen} pxPerMetre={v.k} />
                </g>
              </>
            ) : <DrawingLayer drawing={{ pens: d.pens, scale: 1000 }} toScreen={toScreen} pxPerMetre={v.k} />}
            {d.viewports.map((b) => {
              const a = toScreen({ x: b.x0, y: b.y0 });
              const dx = moved?.id === b.id ? moved.dx * v.k : 0, dy = moved?.id === b.id ? moved.dy * v.k : 0;
              const w = (widened?.id === b.id ? widened.w : b.x1 - b.x0) * v.k;
              const x = widened?.id === b.id ? a.x + ((b.x1 - b.x0) * v.k - w) / 2 : a.x;
              const h = (b.y1 - b.y0) * v.k;
              return (
                <g key={b.id} className={`sheet-viewport${b.warnings.length ? ' warn' : ''}${active === b.id ? ' active' : ''}`}>
                  <rect x={x + dx} y={a.y + dy} width={w} height={h} />
                  {b.content && active === b.id && ([-1, 1] as const).map((side) => (
                    <rect key={side} className="sheet-handle" x={(side < 0 ? x : x + w) + dx - 4} y={a.y + dy + h / 2 - 10} width={8} height={20}
                      onPointerDown={(e) => { e.stopPropagation(); drag.current = { kind: 'width', x: e.clientX, y: e.clientY, vp: b, side }; (e.target as Element).setPointerCapture?.(e.pointerId); }}><title>Drag to set the width</title></rect>
                  ))}
                  <foreignObject x={x + dx + w - 22} y={a.y + dy + 2} width={20} height={20}>
                    <button type="button" className="icon mini" title="Take the view off the sheet" onPointerDown={(e) => e.stopPropagation()}
                      onClick={() => void execute('sheet.removeViewport', { id: sheet.id, viewportId: b.id })}>×</button>
                  </foreignObject>
                </g>
              );
            })}
            {moved?.guides.map((g, i) => { const p = toScreen({ x: g[0], y: g[1] }), q = toScreen({ x: g[2], y: g[3] }); return <line key={i} className="sheet-guide" x1={p.x} y1={p.y} x2={q.x} y2={q.y} />; })}
            {showRemarks && <RemarksOverlay sheetId={sheet.id} toScreen={toScreen} k={v.k} />}
          </>
        )}
      </svg>
      {showRemarks && <RemarkCard sheetId={sheet.id} />}
    </div>
  );
}
