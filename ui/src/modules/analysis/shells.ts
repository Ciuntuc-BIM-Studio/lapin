/**
 * Structural analysis, stage S2b (docs/plan-fem.md §10.10): the pure parts of the shells' contours. The engine gives each
 * slab's and wall's mesh with a quantity at its nodes in mechanics' sign (a moment positive when the face its normal points
 * to is in tension); a slab's moments are shown in the engineer's (§10.12): its bottom face in tension positive.
 */

export const SHELL_QUANTITIES = ['w', 'mx', 'my', 'mxy', 'nx', 'ny', 'nxy', 'vx', 'vy'] as const;
export type ShellQuantity = typeof SHELL_QUANTITIES[number];

export const SHELL_TITLE: Record<ShellQuantity, string> = {
  w: 'Displacement across the slab or wall',
  mx: 'Bending moment mx (its x axis: global X on a slab, along a wall)',
  my: 'Bending moment my (its y axis: global Y on a slab, up a wall)',
  mxy: 'Twisting moment mxy',
  nx: 'Membrane force nx (tension positive)',
  ny: 'Membrane force ny (tension positive)',
  nxy: 'Membrane shear force nxy',
  vx: 'Transverse shear force vx',
  vy: 'Transverse shear force vy',
};

export interface SurfaceDto {
  id: string; sourceId: string; name: string; kind: 'slab' | 'wall' | string; normal: number[]; thickness?: number;
  /** Its nodes, flat [x, y, z, …] (world metres). */
  positions: number[];
  /** Four node indices a face, a triangle's last repeated. */
  faces: number[];
  values?: number[]; min?: number[]; max?: number[];
}
export interface ContoursDto { kind: string; key: string; quantity: ShellQuantity; unit: string; min: number; max: number; surfaces: SurfaceDto[] }

export interface ShellsRunDto {
  surfaces: number; elements: number; quads: number; triangles: number; nodes: number; meshSize: number;
  list: { id: string; sourceId: string; name: string; kind: string; elements: number }[];
}
export interface ShellExtremeDto { surface: string; sourceId: string; value: number; at: number[] }
export type ShellMaximaDto = Partial<Record<ShellQuantity, { max: ShellExtremeDto; min: ShellExtremeDto }>>;
/** A surface in analysis.results: its extremes of each quantity (mechanics' sign), where. */
export interface ShellResultDto {
  id: string; sourceId: string; name: string; kind: string; normal?: number[]; thickness?: number;
  extremes?: Partial<Record<ShellQuantity, { max: number; atMax: number[]; min: number; atMin: number[] }>>;
}

/** The largest and least of a quantity over the surfaces, in the engineer's sign (a slab's moments turned over), with the surface's name and where. */
export function shellExtremes(surfaces: ShellResultDto[], q: ShellQuantity): { max: { value: number; name: string; at: number[] }; min: { value: number; name: string; at: number[] } } | null {
  type At = { value: number; name: string; at: number[] };
  let max: At | null = null;
  let min: At | null = null;
  for (const s of surfaces) {
    const e = s.extremes?.[q];
    if (!e) continue;
    const flip = flipped(s.kind, q);
    const hi = flip ? { value: -e.min, at: e.atMin } : { value: e.max, at: e.atMax };
    const lo = flip ? { value: -e.max, at: e.atMax } : { value: e.min, at: e.atMin };
    if (!max || hi.value > max.value) max = { ...hi, name: s.name };
    if (!min || lo.value < min.value) min = { ...lo, name: s.name };
  }
  return max && min ? { max, min } : null;
}

export interface AnalysisOptionsDto { shells: 'both' | 'slabs' | 'walls' | 'none'; meshSize: number; perSide: number }
export const SHELL_CHOICES = ['both', 'slabs', 'walls', 'none'] as const;
export const SHELL_CHOICE_LABEL: Record<typeof SHELL_CHOICES[number], string> = {
  both: 'Slabs and walls', slabs: 'Slabs only', walls: 'Walls only', none: 'None (slabs by the 45° rule, walls as supports)',
};

/** Whether a quantity is shown turned over on this surface: a slab's moments (§10.12). */
export const flipped = (kind: string, q: ShellQuantity): boolean => kind === 'slab' && (q === 'mx' || q === 'my' || q === 'mxy');

/**
 * The values shown at a surface's nodes, in the engineer's sign: a case's or a combination's as they are (a slab's moments
 * turned over); an envelope's, at each node its least or its largest, whichever is larger in size.
 */
export function shownValues(s: SurfaceDto, q: ShellQuantity): number[] {
  const k = flipped(s.kind, q) ? -1 : 1;
  if (s.values) return s.values.map((v) => v * k);
  const lo = s.min ?? [], hi = s.max ?? [];
  return lo.map((a, i) => {
    const b = hi[i] ?? a;
    return (Math.abs(b) >= Math.abs(a) ? b : a) * k;
  });
}

/** The range of what is shown over every surface. */
export function shownRange(c: ContoursDto): { min: number; max: number } {
  let min = Infinity, max = -Infinity;
  for (const s of c.surfaces) for (const v of shownValues(s, c.quantity)) { if (v < min) min = v; if (v > max) max = v; }
  return Number.isFinite(min) ? { min, max } : { min: 0, max: 0 };
}

/** A displacement's unit on the screen: millimetres; the others as the engine gives them. */
export const shownUnit = (unit: string): string => (unit === 'm' ? 'mm' : unit);
export const shownScale = (unit: string): number => (unit === 'm' ? 1000 : 1);

/** The bands' edges: `count` equal bands from the least to the largest (one band when they are equal). */
export function bandEdges(min: number, max: number, count = 10): number[] {
  if (!(max - min > 1e-12 * Math.max(1, Math.abs(max), Math.abs(min)))) return [min, min + 1e-9];
  return Array.from({ length: count + 1 }, (_, i) => min + ((max - min) * i) / count);
}

/** A band's colour, 0 the lowest, 1 the highest: blue, cyan, green, yellow, red (the colours FEM programs use). */
export function bandColour(t: number): string {
  const stops: [number, number, number][] = [[33, 102, 172], [67, 147, 195], [102, 194, 165], [171, 221, 164], [254, 224, 139], [253, 174, 97], [244, 109, 67], [215, 48, 39]];
  const x = Math.min(1, Math.max(0, t)) * (stops.length - 1);
  const i = Math.min(stops.length - 2, Math.floor(x)), f = x - i;
  const [a, b] = [stops[i]!, stops[i + 1]!];
  const c = a.map((v, k) => Math.round(v + (b[k]! - v) * f));
  return `rgb(${c[0]}, ${c[1]}, ${c[2]})`;
}
/** Each band's colour, at its middle. */
export const bandColours = (bands: number): string[] => Array.from({ length: bands }, (_, i) => bandColour(bands === 1 ? 0.5 : (i + 0.5) / bands));

/** A surface's faces as triangles (node indices): a quadrilateral split along its first diagonal, a triangle as it is. */
export function triangles(faces: number[]): [number, number, number][] {
  const out: [number, number, number][] = [];
  for (let k = 0; k + 3 < faces.length; k += 4) {
    const [a, b, c, d] = [faces[k]!, faces[k + 1]!, faces[k + 2]!, faces[k + 3]!];
    out.push([a, b, c]);
    if (d !== c) out.push([a, c, d]);
  }
  return out;
}

/**
 * A triangle cut into the bands its values cross (marching triangles): each band's part, a polygon of points with the
 * value interpolated linearly along the edges. `points` are any coordinates (a plan's, a screen's).
 */
export function bandPieces(points: [number, number][], values: [number, number, number], edges: number[]): { band: number; poly: [number, number][] }[] {
  const out: { band: number; poly: [number, number][] }[] = [];
  const lo = Math.min(...values), hi = Math.max(...values);
  const bands = edges.length - 1;
  for (let b = 0; b < bands; b++) {
    const e0 = b === 0 ? -Infinity : edges[b]!, e1 = b === bands - 1 ? Infinity : edges[b + 1]!;
    if (hi < e0 || lo > e1) continue;
    let poly = points.map((p, i) => ({ p, v: values[i]! }));
    poly = clip(poly, (v) => v - e0);
    poly = clip(poly, (v) => e1 - v);
    if (poly.length >= 3) out.push({ band: b, poly: poly.map((x) => x.p) });
  }
  return out;
}

/** Keeps the part of a polygon where side(v) ≥ 0 (Sutherland–Hodgman on the value). */
function clip(poly: { p: [number, number]; v: number }[], side: (v: number) => number): { p: [number, number]; v: number }[] {
  const out: { p: [number, number]; v: number }[] = [];
  for (let i = 0; i < poly.length; i++) {
    const a = poly[i]!, b = poly[(i + 1) % poly.length]!;
    const [da, db] = [side(a.v), side(b.v)];
    if (da >= 0) out.push(a);
    if ((da >= 0) !== (db >= 0) && Number.isFinite(da) && Number.isFinite(db)) {
      const t = da / (da - db);
      out.push({ p: [a.p[0] + (b.p[0] - a.p[0]) * t, a.p[1] + (b.p[1] - a.p[1]) * t], v: a.v + (b.v - a.v) * t });
    }
  }
  return out;
}

/** The legend's note on the sign of what is shown. */
export function signNote(q: ShellQuantity, kinds: string[]): string | null {
  if (q === 'mx' || q === 'my' || q === 'mxy') {
    const slab = kinds.includes('slab'), wall = kinds.includes('wall');
    if (slab && wall) return 'Positive: slabs with their bottom face in tension, walls with the face on the right of their direction in tension.';
    if (slab) return 'Positive: the bottom face in tension.';
    return 'Positive: the face on the right of the wall\'s direction in tension.';
  }
  if (q === 'nx' || q === 'ny') return 'Positive: tension.';
  if (q === 'w') return 'Along the normal: up on a slab, to the right of a wall\'s direction.';
  return null;
}
