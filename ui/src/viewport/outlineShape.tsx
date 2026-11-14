/**
 * How an outline tool draws (forum topic 97, stage 2): a floor, a floor opening, a ceiling. Its bar offers Polygon (corners
 * clicked; with Arcs on, or Space, the next edges are arcs going on tangent from the edge before), Rectangle (two corners),
 * Circle (its centre, then a point on it or its radius typed) and, for a floor, Inside walls (a click inside the walls that close
 * round it). The plan's polygon gesture with `shapes` reads it; the outline comes to the tool as its corners and, when an edge is
 * curved, their rises (`arcs`, as the engine keeps them).
 */

import { create } from 'zustand';
import type { Vec2 } from '../platform/module';
import { tangentAt, arcLength, type Arc } from './arc';

export type OutlineShape = 'polygon' | 'rectangle' | 'circle' | 'inside';

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

/** The tool's bar: the shape it draws, and in a polygon whether the next edges are arcs (Space). */
export function OutlineShapeBar({ inside = false }: { inside?: boolean }) {
  const { shape, arcs } = useOutlineShape();
  const choices: [OutlineShape, string, string][] = [
    ['polygon', 'Polygon', 'Click the corners, then the first one again (or Enter)'],
    ['rectangle', 'Rectangle', 'Two opposite corners'],
    ['circle', 'Circle', 'Its centre, then a point on it (or its radius typed)'],
    ...(inside ? [['inside', 'Inside walls', 'Click inside the walls that close round it'] as [OutlineShape, string, string]] : []),
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
export const FloorShapeBar = () => <OutlineShapeBar inside />;
export const PlainShapeBar = () => <OutlineShapeBar />;
