/**
 * A door's detailed threshold (forum topic 204, LVK): its height, material and, as a window's sill has its projection, its width
 * across the wall (along the wall's thickness), centred on the frame and moved by an offset towards the exterior (− the interior).
 * No width (empty or 0): across the whole wall, as before. Read by the engine as JoineryThreshold.Across.
 */
export interface Threshold { on: boolean; height: number; material?: string | null; width?: number | null; offset?: number | null }

/** The threshold with its width set (metres) or cleared (null, 0 or less: across the whole wall again, its offset dropped too). */
export function withThresholdWidth(th: Threshold, width: number | null): Threshold {
  const { width: _w, offset: _o, ...rest } = th;
  return width != null && width > 0 ? { ...rest, width, ...(th.offset ? { offset: th.offset } : {}) } : rest;
}

/** The threshold moved across the wall (metres, + the exterior); 0 or empty: centred on the frame, nothing stored. */
export function withThresholdOffset(th: Threshold, offset: number | null): Threshold {
  const { offset: _o, ...rest } = th;
  return offset ? { ...rest, offset } : rest;
}
