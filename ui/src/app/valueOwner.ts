/**
 * Who owns the typed value (decision 378): a 3D tool waiting for a number
 * (Studio's direct sketch tools, the push/pull arrow, a shape drawn on a face)
 * claims the keyboard's value keys while it waits. keys.ts asks here before
 * anything else: digits, the decimal marks, '-', '<', '@', 'r', a unit's letters,
 * Backspace and Enter go to the owner's value, never to a quick tool (1–9) or a
 * shortcut, and the character typed is not lost. The overlays no longer race
 * the global handler by moving the focus.
 */

export interface ValueOwner {
  /** The value typed so far. */
  text(): string;
  set(text: string): void;
  /** Enter: the value applied (or the tool's own Enter with nothing typed). */
  enter(): void;
}

const owners: ValueOwner[] = [];

/** Claims the value keys while waiting for a value; the function returned lets them go. The last claim wins. */
export function claimValue(o: ValueOwner): () => void {
  owners.push(o);
  return () => { const i = owners.lastIndexOf(o); if (i >= 0) owners.splice(i, 1); };
}

/** The value's owner now, if a tool waits for one. */
export const valueOwner = (): ValueOwner | null => owners[owners.length - 1] ?? null;
