/** The SVG path of screen points (the 3D overlays draw their outlines and previews with it). */

export interface ScreenPoint { x: number; y: number }

/** `points` as an open path, or closed (`close`); null when there are fewer than two or one is off screen (not projected). */
export function screenPath(points: readonly (ScreenPoint | null | undefined)[], close: boolean): string | null {
  if (points.length < 2 || points.some((p) => !p)) return null;
  return points.map((p, i) => `${i ? 'L' : 'M'}${p!.x.toFixed(1)} ${p!.y.toFixed(1)}`).join('') + (close ? 'Z' : '');
}
