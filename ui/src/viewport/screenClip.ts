/**
 * A drawing's lines kept to what the screen shows (forum topic 162). A 2D view draws its pens in screen pixels: zoomed far
 * in, a plan's lines run hundreds of thousands of pixels past the screen, and the browser dashes every one of them whole
 * (a dash stays a few pixels long however far one zooms), so each frame grew with the zoom. Each segment is cut to the
 * screen (with a margin), its start moved by whole dash periods so the dashes stay where they were; what lies wholly
 * outside is left out. The browser restarts the dash pattern at every subpath, so leaving a segment out changes nothing
 * on the others.
 */

/** The screen's rectangle in pixels, grown by a margin: x0 y0 x1 y1. */
export type ScreenRect = readonly [number, number, number, number];

export interface Pt { x: number; y: number }

/** The screen (w × h px) grown by `margin` px each side. */
export function screenRect(w: number, h: number, margin = 64): ScreenRect {
  return [-margin, -margin, w + margin, h + margin];
}

/**
 * The part of segment a→b inside the rectangle (Liang–Barsky), or null when none is. With a dash `period` (px, the sum
 * of the dash array), the start stays on a whole number of periods from `a` and the end is rounded out the same way, so
 * the dashes fall where the whole segment put them.
 */
export function clipSegment(a: Pt, b: Pt, r: ScreenRect, period = 0): [Pt, Pt] | null {
  const dx = b.x - a.x, dy = b.y - a.y;
  let t0 = 0, t1 = 1;
  const edges: [number, number][] = [[-dx, a.x - r[0]], [dx, r[2] - a.x], [-dy, a.y - r[1]], [dy, r[3] - a.y]];
  for (const [p, q] of edges) {
    if (p === 0) { if (q < 0) return null; continue; }
    const t = q / p;
    if (p < 0) { if (t > t1) return null; if (t > t0) t0 = t; }
    else { if (t < t0) return null; if (t < t1) t1 = t; }
  }
  if (t0 === 0 && t1 === 1) return [a, b];
  const len = Math.hypot(dx, dy);
  if (period > 0 && len > 0) {
    const step = period / len;
    t0 = Math.floor(t0 / step) * step;
    t1 = Math.min(1, Math.ceil(t1 / step) * step);
  }
  return [{ x: a.x + dx * t0, y: a.y + dy * t0 }, { x: a.x + dx * t1, y: a.y + dy * t1 }];
}

/** Whether points given as x y x y … (already on screen) lie wholly outside the rectangle, on one side of it. */
export function outside(xy: ArrayLike<number>, r: ScreenRect): boolean {
  let x0 = Infinity, y0 = Infinity, x1 = -Infinity, y1 = -Infinity;
  for (let k = 0; k + 1 < xy.length; k += 2) {
    const x = xy[k], y = xy[k + 1];
    if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
  }
  return x1 < r[0] || x0 > r[2] || y1 < r[1] || y0 > r[3];
}
