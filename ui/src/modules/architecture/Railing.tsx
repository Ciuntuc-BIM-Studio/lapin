/**
 * A railing of its own (decision 238): drawn along a balcony's edge, a terrace or a gallery (Architecture → Railing, a
 * polyline, Enter finishes), in the stair railings' designs; its design, height and sizes in its properties, its line by
 * its vertices' grips (Ctrl/⌘-click an edge's middle to add one, a vertex to take it out). Its segments may be arcs (a
 * user's request): a curved one has a round grip at its arc's middle, dragged to bend it through the point; a right-click
 * offers "Make arc" and "Make straight" on a segment, "Fillet corner…" and "Chamfer corner…" on a corner (one undo step each).
 */

import type { NodeDto } from '../../engine/protocol';
import type { Grip, GripStep, RefLine, ToolDef, Vec2 } from '../../platform/module';
import { RailingIcon } from '../../icons';
import { alongOfChain, keepLine, nearestOnSegment, pt } from '../../viewport/gripKit';
import { arcLength, axisLine, isArc, middle, pointAt, project, riseThrough, type Arc } from '../../viewport/arc';

export const railing: ToolDef = {
  id: 'architecture.railing', label: 'Railing', icon: RailingIcon, shortcut: 'RL',
  gesture: { kind: 'polyline' },
  complete: async ({ points }, ctx) => { await ctx.execute('architecture.addRailing', { storeyId: ctx.storeyId, points: points.map((p) => [p.x, p.y]) }); },
};

/** Each segment's rise off its chord (segment i from point i to i + 1, left of it positive), straight ones 0. */
export function railingArcs(v: unknown, segments: number): number[] {
  const a = Array.isArray(v) ? v : [];
  return Array.from({ length: Math.max(0, segments) }, (_, i) => (typeof a[i] === 'number' && Number.isFinite(a[i]) ? (a[i] as number) : 0));
}

/** A railing's line as points, its arcs drawn in short steps. */
export function railingLine(pts: Vec2[], arcs: number[]): Vec2[] {
  const out: Vec2[] = [];
  for (let i = 0; i + 1 < pts.length; i++) {
    const seg = axisLine({ a: pts[i], b: pts[i + 1], rise: arcs[i] ?? 0 });
    out.push(...(i === 0 ? seg : seg.slice(1)));
  }
  return pts.length === 1 ? [...pts] : out;
}

/** A point put on segment i (at `at`, on the segment): an arc split into two arcs of the same circle. */
export function splitSegment(pts: Vec2[], arcs: number[], i: number, at?: Vec2): { pts: Vec2[]; arcs: number[] } {
  const c: Arc = { a: pts[i], b: pts[i + 1], rise: arcs[i] ?? 0 };
  let p: Vec2, r1 = 0, r2 = 0;
  if (isArc(c)) {
    const l = arcLength(c);
    const s = at ? Math.min(l - 1e-6, Math.max(1e-6, project(c, at))) : l / 2;
    p = pointAt(c, s);
    r1 = riseThrough(c.a, p, pointAt(c, s / 2));
    r2 = riseThrough(p, c.b, pointAt(c, (s + l) / 2));
  } else p = at ? nearestOnSegment(c.a, c.b, at).at : { x: (c.a.x + c.b.x) / 2, y: (c.a.y + c.b.y) / 2 };
  return { pts: [...pts.slice(0, i + 1), p, ...pts.slice(i + 1)], arcs: [...arcs.slice(0, i), r1, r2, ...arcs.slice(i + 1)] };
}

/** Point i taken out: the segments either side become one, straight. */
export function removePoint(pts: Vec2[], arcs: number[], i: number): { pts: Vec2[]; arcs: number[] } {
  const next = pts.filter((_, k) => k !== i);
  if (i === 0) return { pts: next, arcs: arcs.slice(1) };
  if (i === pts.length - 1) return { pts: next, arcs: arcs.slice(0, -1) };
  return { pts: next, arcs: [...arcs.slice(0, i - 1), 0, ...arcs.slice(i + 1)] };
}

export function railingGrips(node: NodeDto): Grip[] {
  const pts = ((node.props.points as unknown[] | undefined) ?? []).map(pt).filter((p): p is Vec2 => p !== null);
  if (pts.length < 2) return [];
  const arcs = railingArcs(node.props.arcs, pts.length - 1);
  const send = (next: Vec2[], nextArcs: number[] = arcs): GripStep => ({ command: 'architecture.updateRailing', args: { id: node.id, points: next.map((p) => [p.x, p.y]), arcs: nextArcs } });
  const straight = (i: number) => Math.abs(arcs[i] ?? 0) <= 1e-6;
  const grips: Grip[] = pts.map((p, i) => ({
    key: `v${i}`, kind: 'vertex', at: p, title: 'Drag: the railing\'s point',
    step: (to) => send(pts.map((q, k) => (k === i ? to : q))),
    ...keepLine(alongOfChain(pts, i, arcs)),
    remove: pts.length > 2 ? () => { const r = removePoint(pts, arcs, i); return send(r.pts, r.arcs); } : undefined,
    ...(i > 0 && i < pts.length - 1 ? {
      corner: {
        straight: straight(i - 1) && straight(i),
        fillet: (radius: number) => ({ command: 'architecture.roundRailingCorner', args: { id: node.id, index: i, radius } }),
        chamfer: (distance: number) => ({ command: 'architecture.chamferRailingCorner', args: { id: node.id, index: i, distance } }),
      },
    } : {}),
  }));
  for (let i = 0; i + 1 < pts.length; i++) {
    const a = pts[i], b = pts[i + 1];
    const c: Arc = { a, b, rise: arcs[i] };
    const bent = isArc(c);
    const mid = { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
    const setRise = (rise: number) => send(pts, arcs.map((r, k) => (k === i ? rise : r)));
    grips.push({
      key: `m${i}`, kind: 'edgeMid', at: mid, title: 'Drag: move the segment',
      step: (to) => { const d = { x: to.x - mid.x, y: to.y - mid.y }; return send(pts.map((q, k) => (k === i || k === i + 1 ? { x: q.x + d.x, y: q.y + d.y } : q))); },
      insert: () => { const r = splitSegment(pts, arcs, i); return send(r.pts, r.arcs); },
      edge: bent ? axisLine(c) : [a, b],
      addCornerAt: (to: Vec2) => { const r = splitSegment(pts, arcs, i, to); return send(r.pts, r.arcs); },
      // Bent to its right by an eighth of its length; dragged by its round grip after.
      ...(bent ? { straighten: () => setRise(0) } : { bend: () => setRise(-Math.hypot(b.x - a.x, b.y - a.y) / 8) }),
    });
    if (bent) grips.push({ key: `a${i}`, kind: 'radius', at: middle(c), title: 'Drag to bend the segment through the point', step: (to) => setRise(riseThrough(a, b, to)) });
  }
  return grips;
}

/** A railing's line, to snap and align to (its arcs as they are). */
export function railingRefs(node: NodeDto): RefLine[] {
  const pts = ((node.props.points as unknown[] | undefined) ?? []).map(pt).filter((p): p is Vec2 => p !== null);
  return pts.length >= 2 ? [{ points: railingLine(pts, railingArcs(node.props.arcs, pts.length - 1)), closed: false, kind: 'axis' }] : [];
}
