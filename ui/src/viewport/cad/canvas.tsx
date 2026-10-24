/**
 * What a 2D view (plan, section, elevation) shares for drafting: the aim of
 * the cursor (object snaps, ortho, guide lines, the grid), the command
 * line's host (its view, what lies under a point), and the overlay that
 * shows the snap, the guides and the running command's preview.
 */

import { useCallback, useEffect, useMemo, useRef } from 'react';
import type { Vec2 } from '../../platform/module';
import type { DrawingDto } from '../drawing';
import { ALL_MODES, SnapIndex, findSnap, type SnapMode } from './snaps';
import { fromJson, nearestOnShape, polylines, type Shape } from './shapes';
import { applyOrtho } from './input';
import { track, trackOnOrtho, type Guide } from './tracking';
import { useCad, type CadHost } from './session';

export interface Aimed { point: Vec2; kind: 'point' | 'grid' | 'free'; mode?: SnapMode; guides: Guide[] }

/** The drawing's lines a cursor can snap to (no hatches, fills or texts). */
const SNAP_LAYERS = new Set(['CUT', 'SEEN', 'HIDDEN', 'OVERHEAD', 'BEYOND', 'MEMBRANE']);

export function snapShapes(drawing: DrawingDto | null, extra: Shape[] = []): { shapes: Shape[]; drafts: Map<string, Shape[]> } {
  const shapes: Shape[] = [...extra];
  const drafts = new Map<string, Shape[]>();
  for (const p of drawing?.pens ?? []) {
    if (!SNAP_LAYERS.has(p.layer) || !p.segments) continue;
    const s = p.segments;
    for (let k = 0; k + 3 < s.length; k += 4) shapes.push({ type: 'line', a: { x: s[k], y: s[k + 1] }, b: { x: s[k + 2], y: s[k + 3] } });
  }
  for (const m of drawing?.marks ?? []) {
    const exact = (m as { shapes?: Record<string, unknown>[] }).shapes;
    if (exact?.length) {
      const list = exact.filter((j) => !j.hatch).map(fromJson).filter((x): x is Shape => !!x);
      if (list.length) drafts.set(m.id, list);
      shapes.push(...list);
      continue;
    }
    const s = m.segments;
    for (let k = 0; k + 3 < s.length; k += 4) shapes.push({ type: 'line', a: { x: s[k], y: s[k + 1] }, b: { x: s[k + 2], y: s[k + 3] } });
  }
  return { shapes, drafts };
}

export function useCadCanvas(o: {
  viewId: string | null;
  drawing: DrawingDto | null;
  extra?: Shape[];
  /** Pixels per metre. */
  scale: number;
  /** Anything selectable under a point (the view's own picking). */
  pickAny: (p: Vec2) => string | null;
}) {
  const { shapes, drafts } = useMemo(() => snapShapes(o.drawing, o.extra), [o.drawing, o.extra]);
  const index = useMemo(() => new SnapIndex(shapes), [shapes]);
  const acquired = useRef<Vec2[]>([]);
  const hold = useRef<{ p: Vec2; timer: ReturnType<typeof setTimeout> } | null>(null);
  const scaleRef = useRef(o.scale);
  scaleRef.current = o.scale;
  const pickRef = useRef(o.pickAny);
  pickRef.current = o.pickAny;

  // The command line works on this view while it is shown.
  useEffect(() => {
    if (!o.viewId) return;
    const host: CadHost = {
      viewId: o.viewId,
      pickDraft: (p) => {
        const tol = 8 / scaleRef.current;
        let best: { id: string; shape: Shape; d: number } | null = null;
        for (const [id, list] of drafts) for (const s of list) {
          const d = nearestOnShape(s, p).dist;
          if (d <= tol && (!best || d < best.d)) best = { id, shape: s, d };
        }
        return best ? { id: best.id, shape: best.shape } : null;
      },
      pickAny: (p) => pickRef.current(p),
      shapeOf: (id) => drafts.get(id)?.[0] ?? null,
    };
    useCad.setState({ host });
    return () => { if (useCad.getState().host === host) useCad.setState({ host: null }); };
  }, [o.viewId, drafts]);

  // A new command starts with nothing acquired.
  const command = useCad((s) => s.command);
  useEffect(() => { acquired.current = []; }, [command]);

  /** Where a raw cursor point lands: a snap, a guide, the ortho line, the grid. */
  const aim = useCallback((raw: Vec2, from?: Vec2 | null, o2: { grid?: number; orthoHeld?: boolean } = {}): Aimed => {
    const cad = useCad.getState();
    const tol = 10 / scaleRef.current;
    const base = from ?? cad.step?.base ?? null;
    const ortho = (cad.ortho || !!o2.orthoHeld) && !!base;
    const s = cad.osnap ? findSnap(raw, shapes, index, { tolerance: tol, modes: ALL_MODES, from: base }) : null;
    if (s && s.mode !== 'nearest') {
      // Resting on a snap acquires it for the guide lines.
      if (!hold.current || Math.hypot(hold.current.p.x - s.point.x, hold.current.p.y - s.point.y) > 1e-9) {
        if (hold.current) clearTimeout(hold.current.timer);
        const p = s.point;
        hold.current = { p, timer: setTimeout(() => {
          if (!acquired.current.some((q) => Math.hypot(q.x - p.x, q.y - p.y) < 1e-9)) acquired.current = [...acquired.current, p].slice(-7);
        }, 350) };
      }
      return { point: s.point, kind: 'point', mode: s.mode, guides: [] };
    }
    if (hold.current) { clearTimeout(hold.current.timer); hold.current = null; }
    // Guide lines: the orthogonal lines through acquired points (and the base), the polar angles.
    const pool = base ? [...acquired.current, base] : acquired.current;
    if (ortho && base) {
      const t = trackOnOrtho(applyOrtho(base, raw), base, pool, tol);
      if (t) return { point: t.point, kind: 'point', guides: t.guides };
    } else {
      const t = track(raw, pool, tol, { base, polarStep: cad.polar ? 45 : null });
      if (t) return { point: t.point, kind: 'point', guides: t.guides };
    }
    if (s) return { point: s.point, kind: 'point', mode: s.mode, guides: [] };
    let q = ortho && base ? applyOrtho(base, raw) : raw;
    if (o2.grid && o2.grid > 0) {
      const g = o2.grid;
      const r = (v: number) => Math.round(v / g) * g;
      q = ortho && base ? (q.y === base.y ? { x: r(q.x), y: base.y } : { x: base.x, y: r(q.y) }) : { x: r(q.x), y: r(q.y) };
      return { point: q, kind: 'grid', guides: [] };
    }
    return { point: q, kind: 'free', guides: [] };
  }, [shapes, index]);

  /** A point the gesture placed: acquired for the guides. */
  const acquire = useCallback((p: Vec2) => {
    if (!acquired.current.some((q) => Math.hypot(q.x - p.x, q.y - p.y) < 1e-9)) acquired.current = [...acquired.current, p].slice(-7);
  }, []);

  return { aim, acquire };
}

const GLYPH: Record<SnapMode, (x: number, y: number) => JSX.Element> = {
  endpoint: (x, y) => <rect x={x - 5} y={y - 5} width={10} height={10} />,
  midpoint: (x, y) => <path d={`M${x},${y - 6}L${x + 6},${y + 5}L${x - 6},${y + 5}Z`} />,
  intersection: (x, y) => <path d={`M${x - 5},${y - 5}L${x + 5},${y + 5}M${x + 5},${y - 5}L${x - 5},${y + 5}`} />,
  center: (x, y) => <circle cx={x} cy={y} r={5} />,
  quadrant: (x, y) => <path d={`M${x},${y - 6}L${x + 6},${y}L${x},${y + 6}L${x - 6},${y}Z`} />,
  perpendicular: (x, y) => <path d={`M${x - 5},${y + 5}H${x + 5}M${x - 3},${y + 5}V${y - 5}`} />,
  nearest: (x, y) => <path d={`M${x - 5},${y - 5}H${x + 5}L${x - 5},${y + 5}H${x + 5}Z`} />,
};

/** The snap marker, the guides and the running command's preview, in screen coordinates. */
export function CadOverlay({ aimed, toScreen }: { aimed: Aimed | null; toScreen: (p: Vec2) => { x: number; y: number } }) {
  const step = useCad((s) => s.step);
  if (!aimed) return null;
  const c = toScreen(aimed.point);
  const preview = step?.preview ? step.preview(aimed.point) : [];
  return (
    <g className="cad-overlay">
      {aimed.guides.map((g, i) => {
        const a = toScreen(g.from), b = toScreen(g.to);
        const len = Math.hypot(b.x - a.x, b.y - a.y) || 1;
        const ex = { x: b.x + ((b.x - a.x) / len) * 40, y: b.y + ((b.y - a.y) / len) * 40 };
        return <g key={i}><line className="cad-guide" x1={a.x} y1={a.y} x2={ex.x} y2={ex.y} /><path className="cad-acquired" d={`M${a.x - 3},${a.y}H${a.x + 3}M${a.x},${a.y - 3}V${a.y + 3}`} /></g>;
      })}
      {preview.flatMap((s, i) => polylines(s).map((pl, j) => (
        <polyline key={`${i}:${j}`} className="cad-preview" points={pl.map((p) => { const q = toScreen(p); return `${q.x},${q.y}`; }).join(' ')} />
      )))}
      {aimed.mode && <g className="cad-snap">{GLYPH[aimed.mode](c.x, c.y)}<text x={c.x + 9} y={c.y - 9}>{aimed.mode}</text></g>}
    </g>
  );
}
