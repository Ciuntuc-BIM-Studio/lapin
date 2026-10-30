/**
 * What a 2D view (plan, section, elevation) shares for drafting: the aim of
 * the cursor (object snaps, ortho, guide lines, the grid) and the command
 * line's host (its view, what lies under a point). The overlay that shows
 * them is in canvas.tsx.
 */

import { useCallback, useEffect, useMemo, useRef } from 'react';
import type { Vec2 } from '../../platform/module';
import type { DrawingDto } from '../drawing';
import { ALL_MODES, SnapIndex, findSnap, type SnapMode } from './snaps';
import { fromJson, nearestOnShape, type Shape } from './shapes';
import { applyOrtho } from './input';
import { track, trackOnOrtho, type Guide } from './tracking';
import { useCad, type CadHost } from './session';
import type { RegionTest } from '../selectBox';

/** What holds an aimed point that no object snap took: the ortho line from the base (on, or Shift held), a polar angle from the base, or a tracking guide through an acquired point. */
export type AimLock = 'ortho' | 'polar' | 'tracking';

export interface Aimed { point: Vec2; kind: 'point' | 'grid' | 'free'; mode?: SnapMode; guides: Guide[]; lock?: AimLock }

/**
 * The aim's rule, with the snap already found: an object snap (other than
 * nearest) wins; then, with ortho on (or Shift held) and a base, the ortho
 * line, held where a guide crosses it; else a tracking guide or a polar
 * angle; then the nearest snap; then the grid or the raw point (squared to
 * the base with ortho). `lock` says which constraint actually holds the point.
 */
export function resolveAim(raw: Vec2, c: { base: Vec2 | null; ortho: boolean; polar: boolean; snap: { point: Vec2; mode: SnapMode } | null; acquired: readonly Vec2[]; tol: number; grid?: number }): Aimed {
  const { base, snap: s, tol } = c;
  const ortho = c.ortho && !!base;
  if (s && s.mode !== 'nearest') return { point: s.point, kind: 'point', mode: s.mode, guides: [] };
  // Guide lines: the orthogonal lines through acquired points (and the base), the polar angles.
  const pool = base ? [...c.acquired, base] : c.acquired;
  if (ortho && base) {
    const t = trackOnOrtho(applyOrtho(base, raw), base, pool, tol);
    if (t) return { point: t.point, kind: 'point', guides: t.guides, lock: 'ortho' };
  } else {
    const t = track(raw, pool, tol, { base, polarStep: c.polar ? 45 : null });
    if (t) return { point: t.point, kind: 'point', guides: t.guides, lock: t.guides.some((g) => g.kind === 'polar') ? 'polar' : 'tracking' };
  }
  if (s) return { point: s.point, kind: 'point', mode: s.mode, guides: [] };
  let q = ortho && base ? applyOrtho(base, raw) : raw;
  const lock: AimLock | undefined = ortho ? 'ortho' : undefined;
  if (c.grid && c.grid > 0) {
    const g = c.grid;
    const r = (v: number) => Math.round(v / g) * g;
    q = ortho && base ? (q.y === base.y ? { x: r(q.x), y: base.y } : { x: base.x, y: r(q.y) }) : { x: r(q.x), y: r(q.y) };
    return { point: q, kind: 'grid', guides: [], lock };
  }
  return { point: q, kind: 'free', guides: [], lock };
}

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
  /** An element's outline in this view (a plan's modelling lines), for previews and the move's ghost. */
  outlines?: (id: string) => Shape[];
  /** Everything selectable a region takes (the command line's WP and CP). */
  inRegion?: (takes: RegionTest) => string[];
}) {
  const { shapes, drafts } = useMemo(() => snapShapes(o.drawing, o.extra), [o.drawing, o.extra]);
  const index = useMemo(() => new SnapIndex(shapes), [shapes]);
  const acquired = useRef<Vec2[]>([]);
  const hold = useRef<{ p: Vec2; timer: ReturnType<typeof setTimeout> } | null>(null);
  const scaleRef = useRef(o.scale);
  scaleRef.current = o.scale;
  const pickRef = useRef(o.pickAny);
  pickRef.current = o.pickAny;
  const outlinesRef = useRef(o.outlines);
  outlinesRef.current = o.outlines;
  const regionRef = useRef(o.inRegion);
  regionRef.current = o.inRegion;
  const marks = o.drawing?.marks;

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
      outlinesOf: (id) => {
        const exact = drafts.get(id);
        if (exact) return exact;
        const own = outlinesRef.current?.(id);
        if (own?.length) return own;
        // An annotation: its drawn segments.
        const s = marks?.find((m) => m.id === id)?.segments ?? [];
        const out: Shape[] = [];
        for (let k = 0; k + 3 < s.length; k += 4) out.push({ type: 'line', a: { x: s[k], y: s[k + 1] }, b: { x: s[k + 2], y: s[k + 3] } });
        return out;
      },
      inRegion: (takes) => regionRef.current?.(takes) ?? [],
    };
    useCad.setState({ host });
    return () => { if (useCad.getState().host === host) useCad.setState({ host: null }); };
  }, [o.viewId, drafts, marks]);

  // A new command starts with nothing acquired.
  const command = useCad((s) => s.command);
  useEffect(() => { acquired.current = []; }, [command]);

  /** Where a raw cursor point lands: a snap, a guide, the ortho line, the grid. */
  const aim = useCallback((raw: Vec2, from?: Vec2 | null, o2: { grid?: number; orthoHeld?: boolean } = {}): Aimed => {
    const cad = useCad.getState();
    const tol = 10 / scaleRef.current;
    const base = from ?? cad.step?.base ?? null;
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
    } else if (hold.current) { clearTimeout(hold.current.timer); hold.current = null; }
    return resolveAim(raw, { base, ortho: cad.ortho || !!o2.orthoHeld, polar: cad.polar, snap: s, acquired: acquired.current, tol, grid: o2.grid });
  }, [shapes, index]);

  /** A point the gesture placed: acquired for the guides. */
  const acquire = useCallback((p: Vec2) => {
    if (!acquired.current.some((q) => Math.hypot(q.x - p.x, q.y - p.y) < 1e-9)) acquired.current = [...acquired.current, p].slice(-7);
  }, []);

  return { aim, acquire };
}
