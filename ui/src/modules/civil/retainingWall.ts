/**
 * A retaining wall's segments as the inspector lists them (docs/plan-wing-walls.md, S1): each one's length and turn from the one
 * before (the first's direction from east), in degrees, + to the left, and its own overrides as stored (none: the wall's).
 */

export interface SegmentRow {
  index: number;
  length: number;
  /** Degrees: the turn from the segment before (+ left); the first segment's direction from east. */
  turn: number;
  own: Record<string, number | boolean>;
}

/** The keys a segment may hold of its own. */
export const SEGMENT_KEYS = ['frontBatter', 'backBatter', 'frontBatterEnd', 'backBatterEnd', 'thickness', 'baseThickness', 'topStart', 'topEnd', 'footingLevel', 'warp'] as const;

const norm = (deg: number) => { let d = deg; while (d > 180) d -= 360; while (d <= -180) d += 360; return d; };

/** The path's points without repeats (as the engine cleans them, within a micrometre). */
export function cleanPath(path: unknown): [number, number][] {
  const pts = Array.isArray(path) ? path.filter((p): p is [number, number] => Array.isArray(p) && typeof p[0] === 'number' && typeof p[1] === 'number') : [];
  const o: [number, number][] = [];
  for (const p of pts) if (!o.length || Math.hypot(p[0] - o[o.length - 1][0], p[1] - o[o.length - 1][1]) > 1e-6) o.push([p[0], p[1]]);
  return o;
}

export function segmentRows(path: unknown, segments: unknown): SegmentRow[] {
  const pts = cleanPath(path);
  const stored = Array.isArray(segments) ? segments : [];
  const rows: SegmentRow[] = [];
  let before = 0;
  for (let i = 0; i + 1 < pts.length; i++) {
    const [dx, dy] = [pts[i + 1][0] - pts[i][0], pts[i + 1][1] - pts[i][1]];
    const dir = (Math.atan2(dy, dx) * 180) / Math.PI;
    const own: Record<string, number | boolean> = {};
    const s = stored[i];
    if (s && typeof s === 'object') for (const k of SEGMENT_KEYS) { const v = (s as Record<string, unknown>)[k]; if (typeof v === 'number' || typeof v === 'boolean') own[k] = v; }
    rows.push({ index: i, length: Math.hypot(dx, dy), turn: i === 0 ? norm(dir) : norm(dir - before), own });
    before = dir;
  }
  return rows;
}

/** A batter n:1 as it reads: "10:1", or upright for none. */
export const batterText = (n: unknown) => (typeof n === 'number' && n > 0 ? `${Math.round(n * 100) / 100}:1` : 'upright');
