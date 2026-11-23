/**
 * The hardscape's pieces the interface works out itself (site grading T4, docs/plan-site-grading.md): a grade typed, the kerb's
 * edges and drops as the engine keeps them, the hardscape under a point, and its top's height grips in 3D. The engine owns the
 * element (`civil.addHardscape`, `civil.updateHardscape`, `civil.setHardscapeKerb`); these only read and shape its arguments.
 */

import type { NodeDto } from '../../engine/protocol';
import type { Vec2 } from '../../platform/module';

export const HARDSCAPE = 'LapinHardscape';
export const HARDSCAPE_TYPE = 'LapinHardscapeType';
export const KERB = 'LapinKerb';

export type TopMode = 'level' | 'slope' | 'corners';
export const TOP_MODES: [TopMode, string][] = [['level', 'Flat and level'], ['slope', 'Sloped plane'], ['corners', 'By its corners']];
export const KERB_SHAPES: [string, string][] = [['upright', 'Upright'], ['bevelled', 'Bevelled'], ['rounded', 'Rounded']];

/** A grade as typed: "2 %", "2%", "1:50" or a fraction (0.02), the decimal comma read too; null when it does not read. */
export function parseGrade(text: string): number | null {
  const s = text.trim().replace(',', '.');
  if (!s) return null;
  const n = (t: string) => { const v = Number(t.trim()); return t.trim() !== '' && Number.isFinite(v) ? v : null; };
  if (s.endsWith('%')) { const v = n(s.slice(0, -1)); return v == null ? null : v / 100; }
  const k = s.indexOf(':');
  if (k > 0) { const a = n(s.slice(0, k)), b = n(s.slice(k + 1)); return a != null && b != null && b !== 0 ? a / b : null; }
  return n(s);
}

/** A grade (a fraction) as the field shows it: "2.0 %". */
export const gradeText = (g: number): string => `${(g * 100).toFixed(Math.abs(g * 100) < 10 ? 1 : 0)} %`;

/** The corners of an outline as stored ([[x, y]…]). */
export function cornersOf(raw: unknown): Vec2[] {
  return Array.isArray(raw) ? raw.filter((p): p is number[] => Array.isArray(p) && typeof p[0] === 'number' && typeof p[1] === 'number').map(([x, y]) => ({ x, y })) : [];
}

/** Which of a hardscape's `n` edges the kerb runs along (no list kept: all of them). */
export function kerbEdges(n: number, raw: unknown): boolean[] {
  return Array.from({ length: n }, (_, i) => !Array.isArray(raw) || raw[i] === true);
}

/** Each edge's reveal where the kerb is dropped there, or null. */
export function kerbDrops(n: number, raw: unknown): (number | null)[] {
  return Array.from({ length: n }, (_, i) => (Array.isArray(raw) && typeof raw[i] === 'number' ? (raw[i] as number) : null));
}

/** The kerb's edges with one switched. */
export const withEdge = (edges: boolean[], i: number, on: boolean): boolean[] => edges.map((e, k) => (k === i ? on : e));

/** Whether a point is inside a polygon (its corners; an arc's bulge aside). */
export function inside(poly: Vec2[], p: Vec2): boolean {
  let c = false;
  for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
    const a = poly[i], b = poly[j];
    if ((a.y > p.y) !== (b.y > p.y) && p.x < ((b.x - a.x) * (p.y - a.y)) / (b.y - a.y) + a.x) c = !c;
  }
  return c;
}

/** The hardscape a point lies in: the selected one first, else the last drawn. */
export function hardscapeAt(nodes: Iterable<NodeDto>, p: Vec2, selected: string[] = []): NodeDto | undefined {
  const all = [...nodes].filter((n) => n.type === HARDSCAPE && inside(cornersOf(n.props.contour), p));
  return all.find((n) => selected.includes(n.id)) ?? all[all.length - 1];
}

/** Where a hardscape's top can be raised from in 3D: by its corners, one grip each; else one in its middle for its elevation. */
export interface TopGrip { key: string; at: { x: number; y: number; z: number }; corner: number | null }

export function topGrips(n: NodeDto): TopGrip[] {
  const c = cornersOf(n.props.contour);
  const z = Array.isArray(n.props.topZ) ? (n.props.topZ as number[]) : [];
  if (c.length < 3 || z.length !== c.length) return [];
  if (n.props.topMode === 'corners') return c.map((p, i) => ({ key: `hs${i}`, at: { x: p.x, y: p.y, z: z[i] }, corner: i }));
  const mid = { x: c.reduce((s, p) => s + p.x, 0) / c.length, y: c.reduce((s, p) => s + p.y, 0) / c.length };
  const e = typeof n.props.elevation === 'number' ? n.props.elevation : z[0];
  // A plane holds its elevation at its origin: the grip stands there.
  const o = Array.isArray(n.props.slopeOrigin) && n.props.topMode === 'slope' ? { x: n.props.slopeOrigin[0] as number, y: n.props.slopeOrigin[1] as number } : mid;
  return [{ key: 'hsTop', at: { ...o, z: e }, corner: null }];
}

/** The corners' heights after one is raised by `by` (the others kept as the top puts them). */
export function raisedCorners(n: NodeDto, i: number, by: number): number[] {
  const z = Array.isArray(n.props.topZ) ? (n.props.topZ as number[]) : [];
  return z.map((v, k) => Math.round((k === i ? v + by : v) * 1e6) / 1e6);
}
