/**
 * Where in height a run is being drawn (the Run tool, Redraw path): the plane
 * (floor, ceiling, wall: the system's elevations for them) or an elevation
 * typed, for the leg being drawn and for each leg drawn before it. F, C and W
 * change the plane once a point is placed, E takes a typed elevation; a change
 * with legs already drawn makes the run rise or drop at the last point placed.
 * The run goes to the engine as one command (one undo step): the first leg's
 * plane or elevation as the run's own, and, when the legs are not all at one
 * height, a level per point, [in, out] where it rises (Runs.PointLevel).
 */

import { create } from 'zustand';
import type { Vec2 } from '../../platform/module';
import type { Defaults } from './network';
import type { Plane } from './prefs';

/** A leg's height: a plane (null: the system's own plane) or an elevation above the storey (m), which wins. */
export interface Level { plane: Plane | null; elevation: number | null }

export const SAME: Level = { plane: null, elevation: null };

/** The letters that set the plane, and E for an elevation typed. */
export const PLANE_KEYS: Record<string, Plane> = { F: 'floor', C: 'ceiling', W: 'wall' };
export const ELEVATION_KEY = 'E';

interface DrawLevelState {
  /** The height of the leg being drawn. */
  current: Level;
  /** The heights of the legs drawn so far: leg i goes from point i to point i + 1. */
  legs: Level[];
  /** The path's first point, by which a new path is told from the one going on. */
  first: Vec2 | null;
  /** E pressed: the elevation being typed (the project's unit), until Enter. */
  typing: string | null;
  /** The last point placed as the legs last saw it (a point placed since, before the cursor moved, is told by the live box's base). */
  last: Vec2 | null;
  /** A height was chosen before the path it is for was seen (a key pressed right after the first click): a new path keeps it. */
  chosen: boolean;
}

export const useDrawLevel = create<DrawLevelState>(() => ({ current: SAME, legs: [], first: null, last: null, typing: null, chosen: false }));

const near = (a: Vec2, b: Vec2) => Math.hypot(a.x - b.x, a.y - b.y) < 1e-9;

/**
 * The legs as the points placed now say: a new path (another first point) starts at `start`'s height with no legs, unless
 * one was just chosen for it; a point taken back drops its leg; each leg the cursor left behind keeps the height it was drawn at.
 */
export function syncLegs(s: Pick<DrawLevelState, 'current' | 'legs' | 'first'> & { chosen?: boolean }, placed: Vec2[], start: (first: Vec2) => Level): Pick<DrawLevelState, 'current' | 'legs' | 'first'> {
  if (!placed.length) return { current: s.current, legs: [], first: null };
  const fresh = !s.first || !near(s.first, placed[0]);
  const current = fresh && !s.chosen ? start(placed[0]) : s.current;
  const legs = (fresh ? [] : s.legs).slice(0, placed.length - 1);
  while (legs.length < placed.length - 1) legs.push(current);
  return { current, legs, first: placed[0] };
}

/** The level a plane key or a typed elevation makes. */
export const planeLevel = (plane: Plane): Level => ({ plane, elevation: null });
export const elevationLevel = (elevation: number): Level => ({ plane: null, elevation });

/** A level's plane and elevation above the storey (m), as the system sets them. */
export function resolve(l: Level, d: Defaults | null): { plane: Plane | null; z: number | null } {
  if (l.elevation != null) return { plane: null, z: l.elevation };
  const plane = l.plane ?? d?.plane ?? null;
  if (!plane || !d) return { plane, z: null };
  return { plane, z: plane === 'ceiling' ? d.ceilingElevation : plane === 'wall' ? d.wallElevation : d.floorElevation };
}

export type StoredLevel = number | null | [number | null, number | null];

/**
 * What the run's command takes for its height: the first leg's plane or elevation as the run's own, and, when its legs are
 * not all at one height, one level per point (null where the run's own; [in, out] where it rises or drops). `n`: its points.
 * `runZ`: a run drawn again (Redraw path) keeps its own elevation, the levels said from it, and has none when all are at it.
 */
export function runHeights(n: number, legs: Level[], d: Defaults | null, runZ?: number | null): { plane?: Plane; elevation?: number; levels?: StoredLevel[] } {
  const first = legs[0] ?? SAME;
  const own: { plane?: Plane; elevation?: number } = runZ !== undefined ? {} : first.elevation != null ? { elevation: first.elevation } : first.plane ? { plane: first.plane } : {};
  if (n < 2 || legs.length < n - 1) return own;
  const z = legs.slice(0, n - 1).map((l) => resolve(l, d).z);
  const base = runZ !== undefined ? runZ : z[0];
  if (base == null || z.some((v) => v == null)) return own;
  if (z.every((v) => Math.abs(v! - base) < 1e-6)) return own;
  const stored = (v: number): number | null => (Math.abs(v - base) < 1e-6 ? null : Math.round(v * 1e4) / 1e4);
  const levels: StoredLevel[] = [];
  for (let i = 0; i < n; i++) {
    const zin = z[Math.max(0, i - 1)]!, zout = z[Math.min(i, n - 2)]!;
    levels.push(Math.abs(zin - zout) < 1e-6 ? stored(zin) : [stored(zin), stored(zout)]);
  }
  return { ...own, levels };
}

/** What changes at the last point placed: the rise (+) or drop (−) to the leg being drawn, m; null when none. */
export function change(s: Pick<DrawLevelState, 'current' | 'legs'>, d: Defaults | null): number | null {
  const before = s.legs[s.legs.length - 1];
  if (!before) return null;
  const a = resolve(before, d).z, b = resolve(s.current, d).z;
  return a == null || b == null || Math.abs(b - a) < 1e-6 ? null : b - a;
}

/** A leg kept square to the plan's axes (a run's default with no laying rules to follow): the aimed point onto the nearer axis. */
export function squared(placed: Vec2[], aimed: Vec2): Vec2 {
  const from = placed[placed.length - 1];
  if (!from) return aimed;
  return Math.abs(aimed.x - from.x) >= Math.abs(aimed.y - from.y) ? { x: aimed.x, y: from.y } : { x: from.x, y: aimed.y };
}
