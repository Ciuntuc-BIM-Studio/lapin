/**
 * Grips on sketch lines in 3D (stage B, decision 375): the points of a shape
 * drafted on a plane (a line's ends, a polyline's corners, a circle's centre
 * and a point on it, an arc's ends and middle), and the shape with one moved,
 * in the plane's (x, y). SketchGrips3D.tsx draws them on the plane and drags.
 */

import type { Vec2 } from '../../platform/module';
import { arcThrough, type Shape } from '../cad/shapes';

const sweepOf = (start: number, end: number) => { const s = (((end - start) % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI); return s < 1e-12 ? 2 * Math.PI : s; };
const at = (c: Vec2, r: number, a: number): Vec2 => ({ x: c.x + r * Math.cos(a), y: c.y + r * Math.sin(a) });

/** A shape's grips. */
export function gripPoints(s: Shape): Vec2[] {
  switch (s.type) {
    case 'line': return [s.a, s.b];
    case 'polyline': return [...s.pts];
    case 'circle': return [s.c, { x: s.c.x + s.r, y: s.c.y }];
    case 'arc': return [at(s.c, s.r, s.start), at(s.c, s.r, s.start + sweepOf(s.start, s.end) / 2), at(s.c, s.r, s.end)];
  }
}

/** The shape with its grip `i` moved to `p` (a circle's centre moves it, its other grip sets the radius; an arc goes through its three grips); null when it would fold. */
export function moveGrip(s: Shape, i: number, p: Vec2): Shape | null {
  switch (s.type) {
    case 'line': return i === 0 ? { ...s, a: p } : { ...s, b: p };
    case 'polyline': return { ...s, pts: s.pts.map((q, k) => (k === i ? p : q)) };
    case 'circle': {
      if (i === 0) return { ...s, c: p };
      const r = Math.hypot(p.x - s.c.x, p.y - s.c.y);
      return r > 1e-6 ? { ...s, r } : null;
    }
    case 'arc': {
      const g = gripPoints(s);
      g[i] = p;
      return arcThrough(g[0], g[1], g[2]);
    }
  }
}
