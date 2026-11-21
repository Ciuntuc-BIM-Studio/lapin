/**
 * Aligning views on a sheet by what they draw (forum topic 210): the user picks a corner or a straight line in one view,
 * then the corner or line in another that must line up with it, and the second view moves. Everything is in paper mm,
 * read from the pens the engine composed for the sheet, so what lines up is what prints.
 */

import type { PenDto } from './drawing';

/** A corner (both axes), or a horizontal line (its y) or a vertical line (its x), with its extent for drawing the highlight. */
export type Feature =
  | { kind: 'point'; x: number; y: number }
  | { kind: 'h'; y: number; x0: number; x1: number }
  | { kind: 'v'; x: number; y0: number; y1: number };

export interface Hit { viewport: string; feature: Feature }

/** A segment shorter than this is a hatch tick or a dot, never a line to line up with. */
const MIN_LINE = 1;
/** A segment this close to the axis counts as horizontal or vertical (paper mm). */
const AXIS = 0.02;

/** Pens that draw the views' lines: a hatch or a plain fill is not something to line up with. */
const lines = (p: PenDto) => p.viewport && p.stroke !== null && !/hatch|fill/i.test(p.layer);

/** The corner or the straight line of a view nearest to a paper point, within `tol` mm: a corner wins over a line. */
export function featureAt(pens: PenDto[], at: { x: number; y: number }, tol: number): Hit | null {
  let corner: { d: number; hit: Hit } | null = null;
  let line: { d: number; hit: Hit } | null = null;
  const vertex = (viewport: string, x: number, y: number) => {
    const d = Math.hypot(x - at.x, y - at.y);
    if (d <= tol && (!corner || d < corner.d)) corner = { d, hit: { viewport, feature: { kind: 'point', x, y } } };
  };
  const segment = (viewport: string, x1: number, y1: number, x2: number, y2: number) => {
    vertex(viewport, x1, y1);
    vertex(viewport, x2, y2);
    if (Math.hypot(x2 - x1, y2 - y1) < MIN_LINE) return;
    if (Math.abs(y2 - y1) <= AXIS) {
      const x0 = Math.min(x1, x2), x1b = Math.max(x1, x2), y = (y1 + y2) / 2;
      const d = Math.abs(at.y - y);
      if (d <= tol && at.x >= x0 - tol && at.x <= x1b + tol && (!line || d < line.d)) line = { d, hit: { viewport, feature: { kind: 'h', y, x0, x1: x1b } } };
    } else if (Math.abs(x2 - x1) <= AXIS) {
      const y0 = Math.min(y1, y2), y1b = Math.max(y1, y2), x = (x1 + x2) / 2;
      const d = Math.abs(at.x - x);
      if (d <= tol && at.y >= y0 - tol && at.y <= y1b + tol && (!line || d < line.d)) line = { d, hit: { viewport, feature: { kind: 'v', x, y0, y1: y1b } } };
    }
  };
  for (const p of pens) {
    if (!lines(p)) continue;
    const vp = p.viewport as string;
    const s = p.segments;
    if (s) for (let i = 0; i + 3 < s.length; i += 4) segment(vp, s[i], s[i + 1], s[i + 2], s[i + 3]);
    if (p.loops) for (const l of p.loops) {
      const n = l.length / 2;
      for (let i = 0; i < n; i++) { const j = (i + 1) % n; segment(vp, l[2 * i], l[2 * i + 1], l[2 * j], l[2 * j + 1]); }
    }
  }
  const c = corner as { d: number; hit: Hit } | null, l = line as { d: number; hit: Hit } | null;
  return c?.hit ?? l?.hit ?? null;
}

/**
 * How far the moving view slides so its feature lines up with the reference one (paper mm), or null when they cannot:
 * a horizontal line with a vertical one. A corner against a line lines up that line's axis only; two corners, both.
 */
export function alignDelta(ref: Feature, mov: Feature): { dx: number; dy: number } | null {
  if (ref.kind === 'point' && mov.kind === 'point') return { dx: ref.x - mov.x, dy: ref.y - mov.y };
  const yOf = (f: Feature) => (f.kind === 'h' ? f.y : f.kind === 'point' ? f.y : null);
  const xOf = (f: Feature) => (f.kind === 'v' ? f.x : f.kind === 'point' ? f.x : null);
  if (ref.kind === 'v' || mov.kind === 'v') {
    const r = xOf(ref), m = xOf(mov);
    return r === null || m === null ? null : { dx: r - m, dy: 0 };
  }
  const r = yOf(ref), m = yOf(mov);
  return r === null || m === null ? null : { dx: 0, dy: r - m };
}
