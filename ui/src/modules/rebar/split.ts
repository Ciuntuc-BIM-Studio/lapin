/**
 * Splitting a longitudinal bar with a lap (decision 227), the interface's arithmetic. The lap zone is the engineer's: two
 * stations along the bar from its start, `from` (where the second piece starts) and `to` (where the first piece ends),
 * the lap being `to − from`. Here: the shortcut from a station and a lap, the lap that makes the first piece exactly the
 * stock length, whether a lap is allowed, and the pieces' nominal lengths shown before the engine confirms them. The
 * engine (`rebar.split`, `rebar.splitPreview`) is the authority; this only drives the fields.
 */

/** The shortest lap accepted (m), as the engine has it. */
export const MIN_LAP = 0.01;

/** The lap for a bar: the project's lap length, or 50Ø. */
export const lapFor = (diameter: number, lapLength: number | null | undefined): number => (lapLength != null && lapLength > 0 ? lapLength : 50 * diameter);

/** The lap zone from a station and a lap: ending at the station by default, starting there with `after` (the engine's shortcut). */
export function lapFromTo(station: number, lap: number, side: 'before' | 'after' = 'before'): { from: number; to: number } {
  return side === 'after' ? { from: station, to: station + lap } : { from: station - lap, to: station };
}

/** The lap zone that makes the first piece exactly the stock length: the lap ends at the stock length. */
export const stockLap = (stock: number, lap: number): { from: number; to: number } => lapFromTo(stock, lap);

/** Why a lap from `from` to `to` cannot be made on a bar this long (the engine's words), or null. */
export function splitRefusal(length: number, from: number, to: number): string | null {
  if (!(to - from >= MIN_LAP - 1e-9)) return 'The lap needs a length.';
  if (from < 0 || to > length) return 'The cut is outside the bar.';
  if (from < 1e-6 || to > length - 1e-6) return 'The cut is too close to the end.';
  return null;
}

/** The two pieces' nominal lengths (hooks aside): the first to `to`, the second from `from`. */
export function pieceLengths(length: number, from: number, to: number): [number, number] {
  return [to, length - from];
}

/** Whether a bar is longer than the stock it is delivered in (a hair of tolerance for rounding). */
export const overStock = (length: number, stock: number): boolean => length > stock + 1e-6;
