/**
 * Editing a closed outline vertex by vertex (slabs, openings, roofs,
 * platforms), keeping data that belongs to its edges in step: a roof's
 * edges[i] belongs to the edge from point i to point i + 1, so inserting a
 * vertex splits that edge (both halves keep its data) and removing one merges
 * two edges (the first one's data stays).
 */

import type { Vec2 } from '../platform/module';

export interface Outline<E = unknown> { pts: Vec2[]; edges?: E[] }

const sub = (a: Vec2, b: Vec2) => ({ x: a.x - b.x, y: a.y - b.y });
const cross = (a: Vec2, b: Vec2) => a.x * b.y - a.y * b.x;

export function moveVertex<E>(o: Outline<E>, i: number, to: Vec2): Outline<E> {
  return { ...o, pts: o.pts.map((p, k) => (k === i ? to : p)) };
}

/** A new vertex on edge i (from point i to i + 1), at `at` (its middle when not given). */
export function insertVertex<E>(o: Outline<E>, edge: number, at?: Vec2): Outline<E> {
  const n = o.pts.length;
  const a = o.pts[edge], b = o.pts[(edge + 1) % n];
  const p = at ?? { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
  const pts = [...o.pts.slice(0, edge + 1), p, ...o.pts.slice(edge + 1)];
  const edges = o.edges ? [...o.edges.slice(0, edge + 1), structuredClone(o.edges[edge]), ...o.edges.slice(edge + 1)] : undefined;
  return { pts, edges };
}

/** Vertex i taken out (null when the outline would have fewer than three). */
export function removeVertex<E>(o: Outline<E>, i: number): Outline<E> | null {
  const n = o.pts.length;
  if (n <= 3) return null;
  const pts = o.pts.filter((_, k) => k !== i);
  // The edge that ended at i (i − 1) now runs to i + 1: it keeps its data; edge i goes.
  const edges = o.edges ? o.edges.filter((_, k) => k !== i) : undefined;
  return { pts, edges };
}

/** The intersection of two lines (point + direction), null when parallel. */
function meet(p: Vec2, d: Vec2, q: Vec2, e: Vec2): Vec2 | null {
  const den = cross(d, e);
  if (Math.abs(den) < 1e-12) return null;
  const t = cross(sub(q, p), e) / den;
  return { x: p.x + d.x * t, y: p.y + d.y * t };
}

/**
 * Edge i moved parallel to itself so it passes through `to` (projected on its
 * normal); its two ends slide along the neighbouring edges, which keep their
 * directions. Null when a neighbour runs parallel to it.
 */
export function offsetEdge<E>(o: Outline<E>, edge: number, to: Vec2): Outline<E> | null {
  const n = o.pts.length;
  const i0 = edge, i1 = (edge + 1) % n;
  const a = o.pts[i0], b = o.pts[i1];
  const d = sub(b, a);
  const len = Math.hypot(d.x, d.y);
  if (len < 1e-12) return null;
  const nrm = { x: -d.y / len, y: d.x / len };
  const shift = (to.x - a.x) * nrm.x + (to.y - a.y) * nrm.y;
  const a2 = { x: a.x + nrm.x * shift, y: a.y + nrm.y * shift };
  const prev = o.pts[(i0 - 1 + n) % n], next = o.pts[(i1 + 1) % n];
  const pa = meet(a2, d, prev, sub(a, prev));
  const pb = meet(a2, d, next, sub(b, next));
  if (!pa || !pb) return null;
  return { ...o, pts: o.pts.map((p, k) => (k === i0 ? pa : k === i1 ? pb : p)) };
}

/** Each edge's middle. */
export const edgeMiddles = (pts: Vec2[]): Vec2[] => pts.map((a, i) => { const b = pts[(i + 1) % pts.length]; return { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 }; });

/** Points as the engine takes them. */
export const toPairs = (pts: Vec2[]): number[][] => pts.map((p) => [p.x, p.y]);
export const fromPairs = (raw: unknown): Vec2[] => (Array.isArray(raw) ? raw.filter((p) => Array.isArray(p) && p.length >= 2).map((p) => ({ x: Number(p[0]), y: Number(p[1]) })) : []);
