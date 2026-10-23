/**
 * The plan of one storey: the engine's plan items for it, drawn in SVG, and
 * the gestures the active tool needs (adapted from BubbleBIM Pro's plan
 * view). North is up; metres inside, mm shown. Every gesture ends in one
 * engine command. Hold Shift to square a line; Esc drops the gesture.
 */

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Maximize } from 'lucide-react';
import { select, setTool, useUi } from '../app/store';
import { activeStorey, useSession } from '../engine/session';
import type { NodeDto, RenderItem } from '../engine/protocol';
import type { Vec2 } from '../platform/module';
import { findTool, gripsOf } from '../platform/modules';
import type { Grip } from '../platform/module';
import { distToSegment, pairs, pointInPolygon, snap, toMm, type SnapResult } from './snap';
import { completeTool } from './tools';

interface View { cx: number; cy: number; scale: number } // scale = px per metre

const KIND_ORDER: Record<string, number> = { grid: 0, outline: 3, cut: 4, symbol: 5, label: 6 };
const PICK_PRIORITY: Record<string, number> = { hit: 0, symbol: 0, cut: 1, outline: 2, overhead: 8 };

export function PlanView() {
  const svgRef = useRef<SVGSVGElement>(null);
  const [size, setSize] = useState({ w: 0, h: 0 });
  const [view, setView] = useState<View>({ cx: 4, cy: 2, scale: 60 });
  const [pts, setPts] = useState<Vec2[]>([]);
  const [hover, setHover] = useState<SnapResult | null>(null);
  const [ortho, setOrtho] = useState(false);
  const drag = useRef<{ x: number; y: number; view: View; moved: boolean } | null>(null);

  const render = useSession((s) => s.render);
  const nodes = useSession((s) => s.nodes);
  useUi((s) => s.storeyId);
  const storey = activeStorey();
  const selected = useUi((s) => s.selected);
  /** A grip being dragged: which, and where it is now. */
  const [gripDrag, setGripDrag] = useState<{ grip: Grip; at: Vec2; moved: boolean } | null>(null);
  const toolId = useUi((s) => s.tool);
  const tool = findTool(toolId);

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

  const fit = useCallback(() => {
    let minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity;
    for (const r of items) for (const it of r.plan) for (let i = 0; i + 1 < it.points.length; i += 2) {
      const x = it.points[i], y = it.points[i + 1];
      minX = Math.min(minX, x); maxX = Math.max(maxX, x); minY = Math.min(minY, y); maxY = Math.max(maxY, y);
    }
    if (!Number.isFinite(minX)) return;
    const scale = Math.min(size.w / Math.max(maxX - minX, 1), size.h / Math.max(maxY - minY, 1)) * 0.8;
    setView({ cx: (minX + maxX) / 2, cy: (minY + maxY) / 2, scale: Math.max(5, Math.min(scale, 400)) });
  }, [items, size]);

  // A different storey: frame it.
  const measured = size.w > 0 && size.h > 0;
  useEffect(() => { if (measured) fit(); }, [storey?.id, measured]);

  // ─── Picking ─────────────────────────────────────────────────────────────

  const pick = useCallback((p: Vec2, types?: string[]): NodeDto | null => {
    const tol = 6 / view.scale;
    let best: { e: NodeDto; prio: number } | null = null;
    for (const r of items) {
      const e = nodes.get(r.nodeId);
      if (!e || (types && !types.includes(e.type))) continue;
      for (const it of r.plan) {
        if (it.kind === 'label' || it.kind === 'grid') continue;
        const poly = pairs(it.points);
        let hit = it.closed && pointInPolygon(p, poly);
        const n = it.closed ? poly.length : poly.length - 1;
        for (let i = 0; i < n && !hit; i++) if (distToSegment(p, poly[i], poly[(i + 1) % poly.length]) < tol) hit = true;
        if (!hit) continue;
        const prio = PICK_PRIORITY[it.kind] ?? 9;
        if (!best || prio <= best.prio) best = { e, prio };
      }
    }
    return best?.e ?? null;
  }, [items, nodes, view.scale]);

  // ─── Gestures ────────────────────────────────────────────────────────────

  const snapAt = useCallback((clientX: number, clientY: number, from?: Vec2): SnapResult => {
    const rect = svgRef.current!.getBoundingClientRect();
    const raw = toWorld(clientX - rect.left, clientY - rect.top);
    return snap(raw, { grid: view.scale > 30 ? 0.05 : 0.5, points: snapPoints, pickRadius: 10 / view.scale, from, ortho });
  }, [toWorld, view.scale, snapPoints, ortho]);

  const complete = useCallback(async (points: Vec2[], entity?: NodeDto) => {
    if (!tool) return;
    await completeTool(tool, { points, entity });
    if (tool.gesture?.kind === 'twoPoints' && tool.gesture.repeat) setPts([points[points.length - 1]]);
    else setPts([]);
  }, [tool]);

  const click = useCallback((p: Vec2, raw: Vec2, add: boolean) => {
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
  }, [tool, pts, pick, complete, view.scale]);

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

  // The grips of what is selected, when no tool is busy.
  const grips = useMemo(() => (tool?.gesture ? [] : selected.flatMap((id) => {
    const n = nodes.get(id);
    return n?.editable ? gripsOf(n) : [];
  })), [tool, selected, nodes]);

  const onPointerDown = (e: React.PointerEvent) => {
    (e.target as Element).setPointerCapture?.(e.pointerId);
    if (e.button === 0 && grips.length) {
      const rect = svgRef.current!.getBoundingClientRect();
      const hit = grips.find((g) => { const q = toScreen(g.at); return Math.hypot(q.x - (e.clientX - rect.left), q.y - (e.clientY - rect.top)) < 7; });
      if (hit) { setGripDrag({ grip: hit, at: hit.at, moved: false }); return; }
    }
    drag.current = { x: e.clientX, y: e.clientY, view, moved: false };
  };
  const onPointerMove = (e: React.PointerEvent) => {
    if (gripDrag) {
      const s = snapAt(e.clientX, e.clientY, gripDrag.grip.at);
      setHover(s);
      setGripDrag({ ...gripDrag, at: s.point, moved: gripDrag.moved || Math.hypot(s.point.x - gripDrag.grip.at.x, s.point.y - gripDrag.grip.at.y) > 1e-6 });
      return;
    }
    const d = drag.current;
    if (d) {
      const dx = e.clientX - d.x, dy = e.clientY - d.y;
      if (Math.hypot(dx, dy) > 4) d.moved = true;
      // Pan: any button with no tool; middle or right always.
      if (d.moved && (!tool?.gesture || e.buttons !== 1)) setView({ ...d.view, cx: d.view.cx - dx / d.view.scale, cy: d.view.cy + dy / d.view.scale });
    }
    setHover(snapAt(e.clientX, e.clientY, pts[pts.length - 1]));
  };
  const onPointerUp = (e: React.PointerEvent) => {
    if (gripDrag) {
      if (gripDrag.moved) void gripDrag.grip.move(gripDrag.at);
      setGripDrag(null);
      return;
    }
    const d = drag.current;
    drag.current = null;
    if (!d || d.moved || e.button !== 0) return;
    const rect = svgRef.current!.getBoundingClientRect();
    click(snapAt(e.clientX, e.clientY, pts[pts.length - 1]).point, toWorld(e.clientX - rect.left, e.clientY - rect.top), e.shiftKey);
  };
  const onWheel = (e: React.WheelEvent) => {
    const rect = svgRef.current!.getBoundingClientRect();
    const at = toWorld(e.clientX - rect.left, e.clientY - rect.top);
    const scale = Math.max(2, Math.min(view.scale * Math.exp(-e.deltaY * 0.0015), 2000));
    // Zoom about the cursor.
    setView({ scale, cx: at.x - (at.x - view.cx) * (view.scale / scale), cy: at.y - (at.y - view.cy) * (view.scale / scale) });
  };

  // ─── Drawing ─────────────────────────────────────────────────────────────

  const path = (flat: ArrayLike<number>, closed: boolean) => {
    const p = pairs(flat).map(toScreen);
    if (p.length === 0) return '';
    return `M${p.map((q) => `${q.x.toFixed(1)},${q.y.toFixed(1)}`).join('L')}${closed ? 'Z' : ''}`;
  };

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
  }, [items, toScreen]);

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

  const marker = hover && (tool?.gesture || gripDrag) ? toScreen(hover.point) : null;

  return (
    <div className="view-pane">
      <div className="view-toolbar">
        <span className="view-title">Plan · {storey?.name ?? '—'}</span>
        <button type="button" className="icon" title="Fit everything" onClick={fit}><Maximize size={14} /></button>
      </div>
      <svg ref={svgRef} className={`plan-svg${tool?.gesture ? ' tool' : ''}`} data-testid="plan"
        onPointerDown={onPointerDown} onPointerMove={onPointerMove} onPointerUp={onPointerUp}
        onPointerLeave={() => setHover(null)} onWheel={onWheel} onContextMenu={(e) => e.preventDefault()}>
        <g className="plan-grid">{grid.map((l, i) => <path key={i} d={l.d} className={l.major ? 'major' : ''} />)}</g>
        <g>
          {drawn.map(({ key, r, kind, d, closed, text, at }) => kind === 'label' ? (
            <text key={key} x={at.x} y={at.y} className="plan-label">{text}</text>
          ) : (
            <path key={key} d={d} className={`plan-item ${kind}${closed ? ' closed' : ''}${r.editable ? '' : ' foreign'}${selected.includes(r.nodeId) ? ' selected' : ''}`} />
          ))}
        </g>
        {pts.map((p, i) => { const s = toScreen(p); return <circle key={i} cx={s.x} cy={s.y} r={3} className="plan-vertex" />; })}
        {grips.map((g) => {
          const q = toScreen(gripDrag?.grip === g ? gripDrag.at : g.at);
          return <rect key={g.key} x={q.x - 4} y={q.y - 4} width={8} height={8} className="plan-grip"><title>Drag</title></rect>;
        })}
        {gripDrag?.moved && (() => { const a = toScreen(gripDrag.grip.at), b = toScreen(gripDrag.at); return <line x1={a.x} y1={a.y} x2={b.x} y2={b.y} className="plan-grip-drag" />; })()}
        {preview}
        {marker && hover && (hover.kind === 'point'
          ? <rect x={marker.x - 5} y={marker.y - 5} width={10} height={10} className="plan-snap point" />
          : <path d={`M${marker.x - 6},${marker.y}H${marker.x + 6}M${marker.x},${marker.y - 6}V${marker.y + 6}`} className="plan-snap" />)}
      </svg>
    </div>
  );
}
