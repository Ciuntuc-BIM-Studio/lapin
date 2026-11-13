/**
 * "Reverse layers" in the type dialogs (forum #45): the rows run the other way, so the layer that was on the R face is on
 * the L face. Walls of the type swap faces: what was exterior becomes interior (for a wall drawn anticlockwise). Each
 * layer keeps what it is (its pieces up its height, its heights, its flags); the core stays the core.
 */
export function reverseLayers<T>(rows: readonly T[]): T[] {
  return [...rows].reverse();
}
