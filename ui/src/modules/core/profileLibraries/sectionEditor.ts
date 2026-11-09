/**
 * The section editor's arithmetic (report #128): rings of points in millimetres — the first the outside, the others holes — with the
 * shapes it starts from (a rectangle, a hollow box, a T, an L, a channel) and the edits a click or a typed value makes. The engine
 * checks what is made (`profiles.fromRings`: no ring crosses itself or another) and centres it.
 */

export type Pt = [number, number];
export type MmRing = Pt[];

export type Starter = 'rectangle' | 'box' | 'T' | 'L' | 'U';
export const STARTERS: [Starter, string][] = [['rectangle', 'Rectangle'], ['box', 'Hollow box'], ['T', 'T'], ['L', 'Angle'], ['U', 'Channel']];

/** A starter's sizes: width, depth and the thickness of its walls (mm). */
export interface Sizes { width: number; depth: number; wall: number }

const round = (v: number) => Math.round(v * 1000) / 1000;

/** The rings of a starter, outside anticlockwise, holes clockwise, the bottom left corner at the origin. */
export function starter(kind: Starter, { width: w, depth: h, wall: t }: Sizes): MmRing[] {
  const tt = Math.max(0.5, Math.min(t, Math.min(w, h) / 2 - 0.5));
  switch (kind) {
    case 'rectangle': return [[[0, 0], [w, 0], [w, h], [0, h]]];
    case 'box': return [[[0, 0], [w, 0], [w, h], [0, h]], [[tt, tt], [tt, h - tt], [w - tt, h - tt], [w - tt, tt]]];
    case 'T': return [[[(w - tt) / 2, 0], [(w + tt) / 2, 0], [(w + tt) / 2, h - tt], [w, h - tt], [w, h], [0, h], [0, h - tt], [(w - tt) / 2, h - tt]]];
    case 'L': return [[[0, 0], [w, 0], [w, tt], [tt, tt], [tt, h], [0, h]]];
    case 'U': return [[[0, 0], [w, 0], [w, h], [w - tt, h], [w - tt, tt], [tt, tt], [tt, h], [0, h]]];
  }
}

/** Twice a ring's signed area (mm²): positive anticlockwise. */
export const signedArea = (r: MmRing): number => r.reduce((s, [x, y], i) => s + (x * r[(i + 1) % r.length][1] - r[(i + 1) % r.length][0] * y), 0) / 2;

/** The rings' box and the section's area (outside less the holes), mm and mm². */
export function measure(rings: MmRing[]): { width: number; depth: number; area: number } {
  const pts = rings.flat();
  if (!pts.length) return { width: 0, depth: 0, area: 0 };
  const xs = pts.map((p) => p[0]), ys = pts.map((p) => p[1]);
  const area = rings.reduce((s, r, i) => s + (i === 0 ? Math.abs(signedArea(r)) : -Math.abs(signedArea(r))), 0);
  return { width: round(Math.max(...xs) - Math.min(...xs)), depth: round(Math.max(...ys) - Math.min(...ys)), area: round(area) };
}

/** A point of ring `ring` moved to `to` (to the millimetre, or to `step` mm when given). */
export function movePoint(rings: MmRing[], ring: number, i: number, to: Pt, step = 1): MmRing[] {
  const snap = (v: number) => Math.round(v / step) * step;
  return rings.map((r, a) => (a === ring ? r.map((p, b): Pt => (b === i ? [round(snap(to[0])), round(snap(to[1]))] : p)) : r));
}

/** A point put on the edge from point `i` to the next of ring `ring`, where `at` is (its foot on the edge). */
export function insertPoint(rings: MmRing[], ring: number, i: number, at: Pt): MmRing[] {
  return rings.map((r, a) => (a === ring ? [...r.slice(0, i + 1), [round(at[0]), round(at[1])] as Pt, ...r.slice(i + 1)] : r));
}

/** A point taken out (a ring keeps three at least). */
export function removePoint(rings: MmRing[], ring: number, i: number): MmRing[] {
  return rings.map((r, a) => (a === ring && r.length > 3 ? r.filter((_, b) => b !== i) : r));
}

/** The nearest edge of a ring to `p` and the foot of `p` on it: where a click on the outline puts a point. */
export function nearestEdge(r: MmRing, p: Pt): { i: number; foot: Pt; dist: number } {
  let best = { i: 0, foot: r[0], dist: Infinity };
  for (let i = 0; i < r.length; i++) {
    const a = r[i], b = r[(i + 1) % r.length];
    const dx = b[0] - a[0], dy = b[1] - a[1];
    const t = Math.max(0, Math.min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / Math.max(dx * dx + dy * dy, 1e-12)));
    const foot: Pt = [a[0] + dx * t, a[1] + dy * t];
    const dist = Math.hypot(p[0] - foot[0], p[1] - foot[1]);
    if (dist < best.dist) best = { i, foot, dist };
  }
  return best;
}

/** A rectangular hole in the middle of the outside, `w` × `h` (clockwise); null when the outside has no room for it. */
export function holeIn(rings: MmRing[], w: number, h: number): MmRing | null {
  const outer = rings[0];
  if (!outer) return null;
  const xs = outer.map((p) => p[0]), ys = outer.map((p) => p[1]);
  const [cx, cy] = [(Math.min(...xs) + Math.max(...xs)) / 2, (Math.min(...ys) + Math.max(...ys)) / 2];
  if (w <= 0 || h <= 0 || w >= Math.max(...xs) - Math.min(...xs) || h >= Math.max(...ys) - Math.min(...ys)) return null;
  return [[cx - w / 2, cy - h / 2], [cx - w / 2, cy + h / 2], [cx + w / 2, cy + h / 2], [cx + w / 2, cy - h / 2]].map(([x, y]): Pt => [round(x), round(y)]);
}

/** A profile's rings (metres, the engine's) in millimetres, to edit. */
export const toMm = (rings: [number, number][][]): MmRing[] => rings.map((r) => r.map(([x, y]): Pt => [round(x * 1000), round(y * 1000)]));
