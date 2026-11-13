/**
 * "Reverse layers" in the type dialogs (forum #45): the rows run the other way, so the layer that was on the R face is on
 * the L face. Walls of the type swap faces: what was exterior becomes interior (for a wall drawn anticlockwise). Each
 * layer keeps what it is (its pieces up its height, its heights, its flags); the core stays the core.
 */
export function reverseLayers<T>(rows: readonly T[]): T[] {
  return [...rows].reverse();
}

/** Move the layer at `i` one place (`d` is -1 up, 1 down) in a type dialog's rows; the rows as they were when it cannot move. Nothing is mutated. */
export function moveLayer<T>(rows: readonly T[], i: number, d: number): T[] {
  const j = i + d;
  if (i < 0 || i >= rows.length || j < 0 || j >= rows.length) return [...rows];
  const next = [...rows];
  [next[i], next[j]] = [next[j], next[i]];
  return next;
}
