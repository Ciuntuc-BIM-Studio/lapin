/**
 * How an outline tool draws (forum topic 97, stage 2; one gesture for every outline tool, audit S4): a floor, a floor opening, a
 * ceiling, a drawn room or balcony, a platform, a parcel, a hole. Its bar offers Polygon (corners clicked; with Arcs on, or Space,
 * the next edges are arcs going on tangent from the edge before), Rectangle (two corners), Circle (its centre, then a point on it
 * or its radius typed), and three sources a click takes the whole outline from: Inside walls, From drawn shape, From room. The plan's polygon gesture with `shapes` reads it; the outline comes to the tool as its corners and, when an edge is
 * curved, their rises (`arcs`, as the engine keeps them).
 */

import { create } from 'zustand';
import type { Vec2 } from '../platform/module';
import { tangentAt, arcLength, type Arc } from './arc';
import { useSession, query } from '../engine/session';
import { arcsOf, fromPairs, outlinePoints } from './contour';

/**
 * The outline's source (audit S4, one gesture for every outline tool): drawn (a polygon whose edges may be arcs, a rectangle, a
 * circle) or taken with a click: inside the walls that close round it, from a closed drafted shape, from a room.
 */
export type OutlineShape = 'polygon' | 'rectangle' | 'circle' | 'ellipse' | 'inside' | 'drawn' | 'room';
/** The sources a click takes the whole outline from. */
export const PICKED: readonly OutlineShape[] = ['inside', 'drawn', 'room'];

export const useOutlineShape = create<{ shape: OutlineShape; arcs: boolean }>(() => ({ shape: 'polygon', arcs: false }));

export const setOutlineShape = (shape: OutlineShape) => useOutlineShape.setState({ shape });
/** Space while drawing a polygon: the next edges arcs, or lines again. */
export const toggleOutlineArcs = () => useOutlineShape.setState((s) => ({ arcs: !s.arcs, shape: 'polygon' }));

/** The rectangle with corners a and b, anticlockwise. */
export function rectangleOutline(a: Vec2, b: Vec2): Vec2[] {
  const pts = [a, { x: b.x, y: a.y }, b, { x: a.x, y: b.y }];
  const area = (b.x - a.x) * (b.y - a.y);
  return area >= 0 ? pts : [pts[0], pts[3], pts[2], pts[1]];
}

/** A circle as an outline: four corners a quarter turn apart from the point clicked on it, each edge a quarter arc bulging out. */
export function circleOutline(c: Vec2, on: Vec2): { pts: Vec2[]; arcs: number[] } | null {
  const r = Math.hypot(on.x - c.x, on.y - c.y);
  if (!(r > 1e-4)) return null;
  const a0 = Math.atan2(on.y - c.y, on.x - c.x);
  const pts = [0, 1, 2, 3].map((k) => ({ x: c.x + r * Math.cos(a0 + (k * Math.PI) / 2), y: c.y + r * Math.sin(a0 + (k * Math.PI) / 2) }));
  const rise = -r * (1 - Math.cos(Math.PI / 4));
  return { pts, arcs: [rise, rise, rise, rise] };
}

/**
 * The rise of the arc from p to q leaving p along `dir` (tangent to the edge before): to the left of p → q positive, as
 * the engine keeps it; 0 when q lies (nearly) on the tangent or behind it.
 */
export function tangentRise(p: Vec2, dir: Vec2, q: Vec2): number {
  const v = { x: q.x - p.x, y: q.y - p.y };
  const len = Math.hypot(v.x, v.y);
  if (len < 1e-9 || Math.hypot(dir.x, dir.y) < 1e-12) return 0;
  const phi = Math.atan2(dir.x * v.y - dir.y * v.x, dir.x * v.x + dir.y * v.y);
  if (Math.abs(phi) < 1e-6 || Math.abs(phi) > Math.PI * 0.95) return 0;
  return -(len / 2) * Math.tan(phi / 2);
}

/** The direction an outline being drawn arrives at its last point: along its last edge, or its arc's tangent there. */
export function arrivingDir(pts: Vec2[], rises: number[]): Vec2 | null {
  const n = pts.length;
  if (n < 2) return null;
  const arc: Arc = { a: pts[n - 2], b: pts[n - 1], rise: rises[n - 2] ?? 0 };
  return tangentAt(arc, arcLength(arc));
}

/** An elliptical edge as the engine takes it (`ellipses`): centre, major semi-axis, ratio, way round. */
export interface EllipseEdgeJson { c: [number, number]; m: [number, number]; ratio: number; ccw: boolean }

/**
 * A whole ellipse as an outline (audit S4): its centre, the end of one semi-axis, and a point giving the other's length (the longer
 * kept as the major axis); four corners at its axes' ends, each edge a quarter of it, anticlockwise.
 */
export function ellipseOutline(c: Vec2, a: Vec2, q: Vec2): { pts: Vec2[]; ellipses: EllipseEdgeJson[] } | null {
  const la = Math.hypot(a.x - c.x, a.y - c.y);
  if (!(la > 1e-4)) return null;
  const n = { x: -(a.y - c.y) / la, y: (a.x - c.x) / la };
  const b = Math.abs((q.x - c.x) * n.x + (q.y - c.y) * n.y);
  if (!(b > 1e-4)) return null;
  const m = b <= la ? { x: a.x - c.x, y: a.y - c.y } : { x: n.x * b, y: n.y * b };
  const ratio = b <= la ? b / la : la / b;
  return wholeEllipse(c, m, ratio);
}

/** A whole ellipse (centre, major semi-axis, ratio) as an outline of four quarter edges. */
export function wholeEllipse(c: Vec2, m: Vec2, ratio: number): { pts: Vec2[]; ellipses: EllipseEdgeJson[] } {
  const nn = { x: -m.y * ratio, y: m.x * ratio };
  const pts = [0, 1, 2, 3].map((k) => { const t = (k * Math.PI) / 2; return { x: c.x + m.x * Math.cos(t) + nn.x * Math.sin(t), y: c.y + m.y * Math.cos(t) + nn.y * Math.sin(t) }; });
  const e: EllipseEdgeJson = { c: [c.x, c.y], m: [m.x, m.y], ratio, ccw: true };
  return { pts, ellipses: [e, e, e, e] };
}

/** The tool's bar: the shape it draws, and in a polygon whether the next edges are arcs (Space); or where a click takes it from. */
export function OutlineShapeBar({ ellipse = false }: { ellipse?: boolean }) {
  const { shape, arcs } = useOutlineShape();
  const choices: [OutlineShape, string, string][] = [
    ['polygon', 'Polygon', 'Click the corners, then the first one again (or Enter)'],
    ['rectangle', 'Rectangle', 'Two opposite corners'],
    ['circle', 'Circle', 'Its centre, then a point on it (or its radius typed)'],
    ...(ellipse ? [['ellipse', 'Ellipse', 'Its centre, the end of one semi-axis, then the other semi-axis'] as [OutlineShape, string, string]] : []),
    ['inside', 'Inside walls', 'Click inside the walls that close round it'],
    ['drawn', 'From drawn shape', 'Click inside a closed drafted shape: a polyline, a rectangle, a circle'],
    ['room', 'From room', 'Click inside a room: its outline'],
  ];
  return (
    <span className="outline-shape" data-testid="outline-shape">
      {choices.map(([id, label, title]) => (
        <button key={id} type="button" className={`cad-toggle${shape === id ? ' on' : ''}`} data-shape={id} aria-pressed={shape === id} title={title} onClick={() => setOutlineShape(id)}>{label}</button>
      ))}
      {shape === 'polygon' && <button type="button" className={`cad-toggle${arcs ? ' on' : ''}`} data-shape="arcs" aria-pressed={arcs} title="The next edges are arcs, each going on tangent from the edge before" onClick={toggleOutlineArcs}>{arcs ? 'Arcs (Space)' : 'Lines (Space)'}</button>}
    </span>
  );
}
export const PlainShapeBar = () => <OutlineShapeBar />;
/** The bar of the tools whose outline may be an ellipse (a floor, a floor opening, a ceiling, a room by outline). */
export const EllipseShapeBar = () => <OutlineShapeBar ellipse />;
export const FloorShapeBar = EllipseShapeBar;

/** Whether a point lies inside a closed loop (even-odd). */
export function inLoop(loop: Vec2[], p: Vec2): boolean {
  let inside = false;
  for (let i = 0, j = loop.length - 1; i < loop.length; j = i++) {
    const a = loop[i], b = loop[j];
    if ((a.y > p.y) !== (b.y > p.y) && p.x < ((b.x - a.x) * (p.y - a.y)) / (b.y - a.y) + a.x) inside = !inside;
  }
  return inside;
}
const area = (loop: Vec2[]) => Math.abs(loop.reduce((s, a, i) => { const b = loop[(i + 1) % loop.length]; return s + a.x * b.y - b.x * a.y; }, 0) / 2);

/** An outline as points for the screen: its arcs in steps, its elliptical edges along their ellipses. */
export function loopOf(o: PickedOutline): Vec2[] {
  if (!o.ellipses?.some(Boolean)) return outlinePoints(o);
  const n = o.pts.length;
  return o.pts.flatMap((a, i) => {
    const e = o.ellipses?.[i];
    if (!e) return [a];
    const b = o.pts[(i + 1) % n], m = { x: e.m[0], y: e.m[1] }, nn = { x: -m.y * e.ratio, y: m.x * e.ratio };
    const param = (q: Vec2) => { const d = { x: q.x - e.c[0], y: q.y - e.c[1] }; return Math.atan2((d.x * nn.x + d.y * nn.y) / (nn.x * nn.x + nn.y * nn.y), (d.x * m.x + d.y * m.y) / (m.x * m.x + m.y * m.y)); };
    const t0 = param(a);
    let sw = param(b) - t0;
    if (e.ccw) { while (sw <= 0) sw += 2 * Math.PI; } else { while (sw >= 0) sw -= 2 * Math.PI; }
    return Array.from({ length: 16 }, (_, k) => { const t = t0 + (sw * k) / 16; return { x: e.c[0] + m.x * Math.cos(t) + nn.x * Math.sin(t), y: e.c[1] + m.y * Math.cos(t) + nn.y * Math.sin(t) }; });
  });
}

export interface PickedOutline { pts: Vec2[]; arcs?: number[]; ellipses?: (EllipseEdgeJson | null)[] }

/** A closed drafted shape as an outline: a closed polyline's corners; a circle as four quarter arcs. */
export function outlineOfDraft(shape: unknown): PickedOutline | null {
  const s = shape as { type?: string; pts?: unknown; closed?: boolean; c?: [number, number]; r?: number } | null;
  if (!s) return null;
  if (s.type === 'polyline' && s.closed) { const pts = fromPairs(s.pts); return pts.length >= 3 ? { pts } : null; }
  if (s.type === 'circle' && Array.isArray(s.c) && typeof s.r === 'number') return circleOutline({ x: s.c[0], y: s.c[1] }, { x: s.c[0] + s.r, y: s.c[1] });
  // A whole ellipse (audit S4), its four quarter edges along it.
  const e = shape as { type?: string; c?: [number, number]; m?: [number, number]; ratio?: number; start?: number; end?: number };
  if (e.type === 'ellipse' && Array.isArray(e.c) && Array.isArray(e.m) && typeof e.ratio === 'number' && !(e.start || e.end)) return wholeEllipse({ x: e.c[0], y: e.c[1] }, { x: e.m[0], y: e.m[1] }, e.ratio);
  return null;
}

/**
 * The outline a click takes (audit S4): inside the walls round it (the engine's), the smallest closed drafted shape of the plan
 * round it (`viewId`: the plan's own drafting; any plan's when none), or the smallest room of the storey round it (a drawn room's
 * corners and arcs; else the outline it found). A text saying why when there is none.
 */
export async function pickOutline(source: OutlineShape, p: Vec2, storeyId: string | null, viewId?: string | null): Promise<PickedOutline | string> {
  if (source === 'inside') {
    const r = await query<{ points?: unknown }>('architecture.outlineInside', { ...(storeyId ? { storeyId } : {}), x: p.x, y: p.y });
    const pts = fromPairs(r?.points);
    return pts.length >= 3 ? { pts } : 'No walls close round that point.';
  }
  const { nodes, render } = useSession.getState();
  const found: PickedOutline[] = [];
  for (const n of nodes.values()) {
    if (source === 'drawn') {
      if (n.props.predefinedType !== 'DRAFT' || (viewId && n.props.viewId !== viewId)) continue;
      const o = outlineOfDraft(n.props.shape);
      if (o) found.push(o);
    } else if (source === 'room' && n.type === 'IfcSpace' && (!storeyId || n.storeyId === storeyId)) {
      const corners = fromPairs(n.props.contour);
      if (n.props.auto === false && corners.length >= 3) {
        const ellipses = Array.isArray(n.props.ellipses) && n.props.ellipses.length === corners.length ? (n.props.ellipses as (EllipseEdgeJson | null)[]) : undefined;
        found.push({ pts: corners, arcs: arcsOf(n.props.arcs, corners.length), ...(ellipses ? { ellipses } : {}) });
        continue;
      }
      const item = render.get(n.id)?.plan.find((x) => x.kind === 'room');
      const pts = item ? Array.from({ length: Math.floor(item.points.length / 2) }, (_, i) => ({ x: item.points[2 * i], y: item.points[2 * i + 1] })) : corners;
      if (pts.length >= 3) found.push({ pts });
    }
  }
  const hit = found.filter((o) => inLoop(loopOf(o), p)).sort((a, b) => area(loopOf(a)) - area(loopOf(b)))[0];
  if (hit) return hit;
  return source === 'drawn' ? 'Click inside a closed drafted shape (a closed polyline, a rectangle or a circle).' : 'Click inside a room.';
}
