/**
 * Drawing on a face in the 3D view, as SketchUp does (decision 272): a
 * rectangle, a circle or a polygon drawn right on the face picked, its points
 * kept on the face's plane, snapped to the face's corners, the middles of its
 * edges and its middle, else to the centimetre; then pushed or pulled at once
 * (axisDrag.ts). The overlay (FaceSketch3D.tsx) takes the mouse and the keys;
 * this holds the state and the geometry.
 */

import { create } from 'zustand';
import type { Vec3 } from './camera';
import { add } from './axisDrag';

export type FaceShape = 'rect' | 'circle' | 'polygon';

/** The face's plane: a point on it, its normal (out of the face), and its x and y. */
export interface FaceFrame { origin: Vec3; normal: Vec3; right: Vec3; up: Vec3 }

export interface FaceSketchSpec {
  shape: FaceShape;
  /** The pick it began with (the element, the point, the normal), for the engine. */
  pick: { id: string; point: number[]; normal: number[] };
  frame: FaceFrame;
  /** The face's outline (lit while drawing, its corners and middles snapped to). */
  outline: Vec3[];
  /** Done: the shape's outline on the face (a circle as its centre and radius). */
  done(shape: { outline: Vec3[] } | { center: Vec3; radius: number; outline: Vec3[] }): void;
}

interface FaceSketchState {
  spec: FaceSketchSpec | null;
  /** The points placed (a rectangle's first corner, a circle's centre, a polygon's corners). */
  pts: Vec3[];
  /** Where the next point goes (under the cursor, snapped). */
  cursor: Vec3 | null;
  /** What the cursor snapped to, shown beside it. */
  snap: string | null;
  /** A typed size: a rectangle's "width, height", a circle's radius, a polygon side's length. */
  text: string;
}

export const useFaceSketch = create<FaceSketchState>(() => ({ spec: null, pts: [], cursor: null, snap: null, text: '' }));

export function beginFaceSketch(spec: FaceSketchSpec, first: Vec3): void {
  useFaceSketch.setState({ spec, pts: [first], cursor: first, snap: null, text: '' });
}

export function endFaceSketch(): void {
  useFaceSketch.setState({ spec: null, pts: [], cursor: null, snap: null, text: '' });
}

const dot = (a: Vec3, b: Vec3) => a.x * b.x + a.y * b.y + a.z * b.z;
const sub = (a: Vec3, b: Vec3): Vec3 => ({ x: a.x - b.x, y: a.y - b.y, z: a.z - b.z });
const cross = (a: Vec3, b: Vec3): Vec3 => ({ x: a.y * b.z - a.z * b.y, y: a.z * b.x - a.x * b.z, z: a.x * b.y - a.y * b.x });
const unit = (a: Vec3): Vec3 => { const l = Math.hypot(a.x, a.y, a.z) || 1; return { x: a.x / l, y: a.y / l, z: a.z / l }; };

/** The face's plane: x along its longest edge (so a rectangle lies square to the face), y across it, both on the face. */
export function frameOf(normal: Vec3, outline: Vec3[]): FaceFrame {
  const n = unit(normal);
  let right: Vec3 | null = null, best = 0;
  outline.forEach((p, i) => {
    const d = sub(outline[(i + 1) % outline.length], p);
    const l = Math.hypot(d.x, d.y, d.z);
    if (l > best + 1e-9) { best = l; right = d; }
  });
  if (!right || best < 1e-9) right = Math.abs(n.z) > 0.999 ? { x: 1, y: 0, z: 0 } : { x: -n.y, y: n.x, z: 0 };
  // Kept on the plane, and pointing the plane's usual way (+x on a level face, rising to the right on a standing one).
  let r = unit(sub(right, add({ x: 0, y: 0, z: 0 }, n, dot(right, n))));
  const usual = Math.abs(n.z) > 0.999 ? { x: 1, y: 0, z: 0 } : { x: -n.y, y: n.x, z: 0 };
  if (dot(r, usual) < -1e-9) r = { x: -r.x, y: -r.y, z: -r.z };
  return { origin: outline[0] ?? { x: 0, y: 0, z: 0 }, normal: n, right: r, up: unit(cross(n, r)) };
}

/** A point's place on the face's plane (x, y), and back. */
export const local = (f: FaceFrame, p: Vec3) => { const d = sub(p, f.origin); return { x: dot(d, f.right), y: dot(d, f.up) }; };
export const world = (f: FaceFrame, x: number, y: number): Vec3 => add(add(f.origin, f.right, x), f.up, y);

/** Where a ray (origin, direction) meets the face's plane; null when it runs along it or away. */
export function onPlane(f: FaceFrame, origin: Vec3, dir: Vec3): Vec3 | null {
  const den = dot(dir, f.normal);
  if (Math.abs(den) < 1e-9) return null;
  const t = dot(sub(f.origin, origin), f.normal) / den;
  return t < 0 ? null : add(origin, dir, t);
}

/** A point kept to the centimetre on the face's plane (measured from the face's first corner). */
export function roundOnFace(f: FaceFrame, p: Vec3, step = 0.01): Vec3 {
  const q = local(f, p);
  return world(f, Math.round(q.x / step) * step, Math.round(q.y / step) * step);
}

/** What a point on the face snaps to: its corners, the middles of its edges, its middle. */
export function snapPoints(outline: Vec3[]): { at: Vec3; what: string }[] {
  const o: { at: Vec3; what: string }[] = outline.map((p) => ({ at: p, what: 'Corner' }));
  outline.forEach((p, i) => {
    const q = outline[(i + 1) % outline.length];
    o.push({ at: { x: (p.x + q.x) / 2, y: (p.y + q.y) / 2, z: (p.z + q.z) / 2 }, what: 'Middle of edge' });
  });
  if (outline.length) {
    const n = outline.length;
    o.push({ at: outline.reduce((s, p) => ({ x: s.x + p.x / n, y: s.y + p.y / n, z: s.z + p.z / n }), { x: 0, y: 0, z: 0 }), what: 'Middle of face' });
  }
  return o;
}

/** A rectangle on the face from one corner to the opposite one, square to the face's x and y. */
export function rectOnFace(f: FaceFrame, a: Vec3, b: Vec3): Vec3[] {
  const p = local(f, a), q = local(f, b);
  return [world(f, p.x, p.y), world(f, q.x, p.y), world(f, q.x, q.y), world(f, p.x, q.y)];
}

/** A rectangle typed as its width and height from its first corner, the way the cursor lies from it. */
export function rectTyped(f: FaceFrame, a: Vec3, toward: Vec3 | null, w: number, h: number): Vec3[] {
  const p = local(f, a), q = toward ? local(f, toward) : { x: p.x + 1, y: p.y + 1 };
  const sx = q.x < p.x ? -1 : 1, sy = q.y < p.y ? -1 : 1;
  return rectOnFace(f, a, world(f, p.x + sx * Math.abs(w), p.y + sy * Math.abs(h)));
}

/** A circle on the face as the polygon the preview draws (the engine keeps it a circle). */
export function circleOnFace(f: FaceFrame, c: Vec3, r: number, n = 48): Vec3[] {
  const p = local(f, c);
  return Array.from({ length: n }, (_, i) => world(f, p.x + r * Math.cos((2 * Math.PI * i) / n), p.y + r * Math.sin((2 * Math.PI * i) / n)));
}

export const distance3 = (a: Vec3, b: Vec3) => Math.hypot(a.x - b.x, a.y - b.y, a.z - b.z);

/** A rectangle's typed size: "1200, 600", "1200 600" or "1200x600", each in the project's unit; null until both parse. */
export function parsePair(text: string, parse: (s: string) => number | null): [number, number] | null {
  const parts = text.split(/\s*[,;x×*]\s*|\s+/i).filter(Boolean);
  if (parts.length !== 2) return null;
  const [w, h] = parts.map(parse);
  return w != null && h != null && w !== 0 && h !== 0 ? [w, h] : null;
}
