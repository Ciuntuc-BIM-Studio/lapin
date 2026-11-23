/**
 * The crosshair across the whole drawing area (forum topic 226, as AutoCAD's cursor): a vertical and a horizontal line
 * through the point the cursor is aimed at, so its alignment with walls, grid lines and other elements shows across the
 * view, not only at the tip. Off by default; File → Preferences → Grid and snaps → Cursor.
 */

import { usePrefs } from '../app/prefs';

export function Crosshair({ at, width, height }: { at: { x: number; y: number } | null; width: number; height: number }) {
  const on = usePrefs((p) => p.crosshair);
  if (!on || !at || !(width > 0) || !(height > 0)) return null;
  return (
    <path className="crosshair" data-testid="crosshair" d={`M0,${at.y.toFixed(1)}H${width}M${at.x.toFixed(1)},0V${height}`}
      fill="none" stroke="currentColor" strokeOpacity={0.5} strokeWidth={1} pointerEvents="none" aria-hidden="true" />
  );
}
