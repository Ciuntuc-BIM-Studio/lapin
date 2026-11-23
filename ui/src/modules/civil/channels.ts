/**
 * Channels and standalone kerbs (site grading T6, docs/plan-site-grading.md; forum 230 §5): what the tools and the inspector work
 * out before asking the engine — the line a click follows (an edge of a hardscape, a path's side), a grade typed, the outlets of a
 * channel and the drops of a kerb as lists to add to and take from.
 */

import type { NodeDto } from '../../engine/protocol';
import type { Vec2 } from '../../platform/module';
import { fromPairs } from '../../viewport/contour';
import { parseGrade } from './hardscape';
import { sideOf } from './grading';

export const CHANNEL = 'LapinChannel';

/** What a channel or a kerb can run along: a feature line, a hardscape, a kerb, a channel, a platform, a path or road, a retaining wall. */
export const FOLLOW_TYPES = ['LapinFeatureLine', 'LapinHardscape', 'LapinKerb', 'LapinChannel', 'IfcEarthworksCut', 'IfcPavement', 'LapinRetainingWall'];

/** The sections a channel is made in (a profile of the project's is chosen beside them). */
export const CHANNEL_KINDS: [string, string][] = [
  ['u', 'U channel'], ['v', 'V channel'], ['slot', 'Slot drain'], ['grated', 'Grated drain'], ['swale', 'Cobble swale'],
];

export type InvertMode = 'grade' | 'depth';

/** What a channel tool or the inspector holds for its invert: at a grade from a start level, or at a depth under its top. */
export interface InvertOptions { invert: InvertMode; grade: string; startZ: string }

/** A grade typed (0.5 %, 1:200, 0.005): the fraction, or null when it does not read or is steeper than the engine's 50 %. */
export function readGrade(text: string): number | null {
  const g = parseGrade(text);
  return g != null && Number.isFinite(g) && Math.abs(g) <= 0.5 ? g : null;
}

/** A grade as the field shows it: „0.5 %”. */
export const gradeShown = (g: number): string => `${Math.round(g * 1e5) / 1e3} %`;

/** The nearest edge of an outline to a point: its index (the edge from corner i to the next), within `reach` metres; null when none is near. */
export function nearestEdge(pts: Vec2[], at: Vec2, reach: number): number | null {
  let best = reach, found: number | null = null;
  for (let i = 0; i < pts.length; i++) {
    const a = pts[i], b = pts[(i + 1) % pts.length];
    const dx = b.x - a.x, dy = b.y - a.y;
    const len2 = dx * dx + dy * dy;
    const t = len2 < 1e-18 ? 0 : Math.max(0, Math.min(1, ((at.x - a.x) * dx + (at.y - a.y) * dy) / len2));
    const d = Math.hypot(at.x - (a.x + dx * t), at.y - (a.y + dy * t));
    if (d < best) { best = d; found = i; }
  }
  return found;
}

/**
 * The `along` of what was clicked: a path's carriageway edge on the side clicked, one edge of a hardscape when the click is near
 * it (else its whole ring), anything else as itself.
 */
export function followArg(n: NodeDto, at: Vec2, reach = 1.5): { id: string; edge?: number; side?: 'left' | 'right' } {
  if (n.type === 'IfcPavement') {
    const pts = fromPairs(n.props.path);
    return pts.length >= 2 ? { id: n.id, side: sideOf(pts, at) } : { id: n.id };
  }
  if (n.type === 'LapinHardscape') {
    const edge = nearestEdge(fromPairs(n.props.contour), at, reach);
    return edge == null ? { id: n.id } : { id: n.id, edge };
  }
  return { id: n.id };
}

/** The invert arguments of a channel from what is typed: null when the grade or the start level does not read (`bad` says which). */
export function invertArgs(o: InvertOptions, level: (t: string) => number | null): { args: Record<string, unknown> } | { bad: 'grade' | 'start' } {
  if (o.invert === 'depth') return { args: { invert: 'depth' } };
  const grade = readGrade(o.grade);
  if (grade == null) return { bad: 'grade' };
  const args: Record<string, unknown> = { invert: 'grade', grade };
  if (o.startZ.trim() !== '') {
    const z = level(o.startZ);
    if (z == null || !Number.isFinite(z)) return { bad: 'start' };
    args.startZ = z;
  } else args.startZ = null;
  return { args };
}

const rounded = (v: number) => Math.round(v * 1e6) / 1e6;

/** A channel's outlets (stations along it, nearest first) with one more; the same station twice is one. */
export function withOutlet(stations: number[], at: number): number[] {
  return [...new Set([...stations, rounded(at)])].sort((a, b) => a - b);
}

/** A channel's outlets with the k-th taken away: possibly none (no gully at all). */
export const withoutOutlet = (stations: number[], k: number): number[] => stations.filter((_, i) => i !== k);

/** One outlet moved to another station. */
export const movedOutlet = (stations: number[], k: number, to: number): number[] => [...new Set(stations.map((s, i) => rounded(i === k ? to : s)))].sort((a, b) => a - b);

export interface DropRow { from: number; to: number; reveal: number }

/** A standalone kerb's drops as stored ({from, to, reveal} in stations along it), ordered. */
export function dropRows(raw: unknown): DropRow[] {
  if (!Array.isArray(raw)) return [];
  return raw
    .filter((d): d is DropRow => !!d && typeof d === 'object' && ['from', 'to', 'reveal'].every((k) => typeof (d as Record<string, unknown>)[k] === 'number'))
    .map((d) => ({ from: d.from, to: d.to, reveal: d.reveal }))
    .sort((a, b) => a.from - b.from);
}

/**
 * A drop a click on "Add" puts in: a stretch of 1 m (or what is left of the kerb) after the last drop, else from its start, its
 * reveal 0 (a crossing). null when the kerb is shorter than a centimetre.
 */
export function nextDrop(rows: DropRow[], length: number): DropRow | null {
  const from = rows.length ? Math.min(rows[rows.length - 1].to, Math.max(0, length - 0.01)) : 0;
  const to = Math.min(length, from + 1);
  return to - from < 0.01 ? null : { from: rounded(from), to: rounded(to), reveal: 0 };
}

/** The stations of a drop typed: the two ends in order, inside the kerb, at least a millimetre apart; null when they do not make a stretch. */
export function dropEnds(from: number, to: number, length: number): { from: number; to: number } | null {
  if (!Number.isFinite(from) || !Number.isFinite(to)) return null;
  const [a, b] = [Math.max(0, Math.min(from, to)), Math.min(length, Math.max(from, to))];
  return b - a >= 0.001 ? { from: rounded(a), to: rounded(b) } : null;
}

/** The arguments that make a drop at the stretch given: `reveal` null takes away the drops it overlaps. */
export const dropArg = (from: number, to: number, reveal: number | null) => ({ drop: { from, to, reveal } });

/** A channel's or a kerb's source of heights, as the inspector says it. */
export const followsName = (n: NodeDto, nodes: Map<string, NodeDto>): string | null => {
  const along = n.props.along as { id?: string } | undefined;
  return along && typeof along.id === 'string' ? nodes.get(along.id)?.name ?? null : null;
};

export type SizeKey = 'width' | 'depth' | 'wall' | 'base' | 'grating' | 'slot' | 'neck';

/** The sizes a section has (a drawn profile has its own shape: only its depth). */
export function sizeFields(kind: string): SizeKey[] {
  switch (kind) {
    case 'v': return ['width', 'depth', 'wall'];
    case 'slot': return ['width', 'depth', 'wall', 'base', 'slot', 'neck'];
    case 'grated': return ['width', 'depth', 'wall', 'base', 'grating'];
    case 'swale': return ['width', 'depth', 'wall'];
    case 'profile': return ['depth'];
    default: return ['width', 'depth', 'wall', 'base'];
  }
}

/** A section's default size from the engine's catalogue (`civil.channelKinds`), else a plain fallback. */
export function kindDefault(catalog: unknown, kind: string, key: SizeKey): number {
  const row = Array.isArray(catalog) ? (catalog as Record<string, unknown>[]).find((r) => r.key === kind) : undefined;
  const v = row?.[key];
  return typeof v === 'number' ? v : key === 'depth' ? 0.3 : 0;
}
