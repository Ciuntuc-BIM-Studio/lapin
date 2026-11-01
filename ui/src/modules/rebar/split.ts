/**
 * Splitting a longitudinal bar with a lap (decision 227), the interface's arithmetic: where the cut goes so the first
 * piece is exactly the stock length, whether a station is allowed, and the pieces' nominal lengths shown before the
 * engine confirms them. The engine (`rebar.split`, `rebar.splitPreview`) is the authority; this only drives the fields.
 */

/** The lap for a bar: the project's lap length, or 50Ø. */
export const lapFor = (diameter: number, lapLength: number | null | undefined): number => (lapLength != null && lapLength > 0 ? lapLength : 50 * diameter);

/** The station (m from the start) that makes the first piece exactly the stock length: the lap is centred on it. */
export const stockStation = (stock: number, lap: number): number => stock - lap / 2;

/** Why a cut at this station cannot be made (the engine's words), or null. */
export function splitRefusal(length: number, station: number, lap: number): string | null {
  if (!(lap > 0)) return 'The lap is a length.';
  if (station < 0 || station > length) return 'The cut is outside the bar.';
  if (station - lap / 2 < 1e-6 || station + lap / 2 > length - 1e-6) return 'The cut is too close to the end.';
  return null;
}

/** The two pieces' nominal lengths (hooks aside): the first to station + lap/2, the second from station − lap/2. */
export function pieceLengths(length: number, station: number, lap: number): [number, number] {
  return [station + lap / 2, length - (station - lap / 2)];
}

/** Whether a bar is longer than the stock it is delivered in (a hair of tolerance for rounding). */
export const overStock = (length: number, stock: number): boolean => length > stock + 1e-6;
