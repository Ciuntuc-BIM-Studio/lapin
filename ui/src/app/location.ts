/**
 * The project's location as the interface reads it (Manage → Location; engine Location.cs): the internal origin
 * (the plan's 0,0, which never moves), the project base point, the survey point with its map coordinates, the
 * angle to true north, whether views show them, and the user coordinate system (UCS) the plans draft in. Read
 * again from the engine whenever the model changes; the UCS is kept in the project, each change one undo step.
 */

import { create } from 'zustand';
import type { Vec2 } from '../platform/module';
import { execute, query, useSession } from '../engine/session';
import type { Frame } from './frame';
export * from './frame';

type P = Record<string, number>;
export interface LocationDto { base?: P; survey?: P; show?: boolean; crs?: string; ucs?: { x: number; y: number; angle: number } }

export const useLocation = create<{ loc: LocationDto }>(() => ({ loc: {} }));

let pending: ReturnType<typeof setTimeout> | null = null;
let lastNodes: unknown = null;
function refresh(): void {
  if (pending) clearTimeout(pending);
  pending = setTimeout(() => {
    pending = null;
    if (!useSession.getState().connected) return;
    void query<LocationDto>('project.location').then((r) => useLocation.setState({ loc: r ?? {} })).catch(() => { /* no project yet */ });
  }, 30);
}
let watching = false;
/** Read the location again whenever the model changes (called once by the app; tests that mock the session never start it). */
export function watchLocation(): void {
  if (watching) return;
  watching = true;
  useSession.subscribe((s) => {
    if (s.nodes === lastNodes) return;
    lastNodes = s.nodes;
    refresh();
  });
  refresh();
}

/** The UCS, or null when the plans draft in their own axes. */
export const ucsOf = (loc: LocationDto): Frame | null => {
  const u = loc.ucs;
  if (!u) return null;
  return { origin: { x: u.x ?? 0, y: u.y ?? 0 }, angle: u.angle ?? 0 };
};

/** The base point in the plan, with its angle to true north (degrees). */
export const basePoint = (loc: LocationDto) => ({ x: loc.base?.x ?? 0, y: loc.base?.y ?? 0, elevation: loc.base?.elevation ?? 0, northAngle: loc.base?.northAngle ?? 0 });

/** The survey point, or null when the project is not tied to the map. */
export const surveyPoint = (loc: LocationDto) => (loc.survey ? { x: loc.survey.x ?? 0, y: loc.survey.y ?? 0, easting: loc.survey.easting ?? 0, northing: loc.survey.northing ?? 0, elevation: loc.survey.elevation ?? 0 } : null);

/** A plan point in map coordinates, as the engine's GeoRef.ToMap: through the survey point, turned by the angle to true north. */
export function toMap(loc: LocationDto, p: Vec2): { e: number; n: number } {
  const s = surveyPoint(loc) ?? { x: 0, y: 0, easting: 0, northing: 0, elevation: 0 };
  const th = (basePoint(loc).northAngle * Math.PI) / 180;
  const c = Math.cos(th), sn = -Math.sin(th);
  const dx = p.x - s.x, dy = p.y - s.y;
  return { e: s.easting + c * dx - sn * dy, n: s.northing + sn * dx + c * dy };
}

/** The frame whose axes are the map's east and north, at the survey point (or the plan's origin). */
export function mapFrame(loc: LocationDto): Frame {
  const s = surveyPoint(loc);
  return { origin: { x: s?.x ?? 0, y: s?.y ?? 0 }, angle: basePoint(loc).northAngle };
}

/** Set the UCS (null: the plan's own axes): one undo step. */
export const setUcs = (f: Frame | null) => execute('project.location', { ucs: f ? { x: f.origin.x, y: f.origin.y, angle: f.angle } : null });

/** Show or hide the internal origin, the base point and the survey point in plans and in 3D: one undo step. */
export const setLocationShown = (show: boolean) => execute('project.location', { show });

// ─── A plan turned to true north (decision 447) ───
// Each plan's choice (project north up, as drawn; or true north up, the plan turned by the angle to true north),
// kept on this computer by the plan's key; on screen only: sheets and the PDF print project north up.
const NORTH_KEY = 'lapin.trueNorth';
function readNorth(): Record<string, boolean> {
  try { const v = JSON.parse(localStorage.getItem(NORTH_KEY) ?? '{}'); return v && typeof v === 'object' ? v as Record<string, boolean> : {}; } catch { return {}; }
}
export const useTrueNorth = create<{ on: Record<string, boolean> }>(() => ({ on: readNorth() }));
export function setTrueNorth(key: string, on: boolean): void {
  const next = { ...useTrueNorth.getState().on };
  if (on) next[key] = true; else delete next[key];
  useTrueNorth.setState({ on: next });
  try { localStorage.setItem(NORTH_KEY, JSON.stringify(next)); } catch { /* kept for this session */ }
}

/** The screen turn (radians, anticlockwise) that puts true north up: the plan's north (−sin θ, cos θ) turned by −θ. */
export const trueNorthTurn = (northAngle: number) => (-northAngle * Math.PI) / 180;

/** Points turned by an angle about the origin. */
export const turn = (p: Vec2, a: number): Vec2 => (a ? { x: p.x * Math.cos(a) - p.y * Math.sin(a), y: p.x * Math.sin(a) + p.y * Math.cos(a) } : p);
