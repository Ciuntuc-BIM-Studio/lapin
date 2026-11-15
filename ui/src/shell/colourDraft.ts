/**
 * The colour picker fires an input event on every move of the pointer while it is open. A field that sends a colour to the
 * engine must send it once, when the pick is settled (the picker closed, the field left, a swatch chosen): this is that rule,
 * apart from React so it can be tested.
 */
export interface ColourDraft {
  /** The picker moved: remember the colour, send nothing. */
  move(colour: string): void;
  /** The pick is settled: sends the last colour once, if it was moved and differs from the one the field started with. */
  settle(): void;
  /** The colour to show: the one being picked, else the field's own. */
  shown(own: string): string;
  /** A pick is open (moved, not settled). */
  pending(): boolean;
}

export function colourDraft(current: () => string, send: (colour: string) => void): ColourDraft {
  let draft: string | null = null;
  return {
    move: (c) => { draft = c; },
    settle: () => {
      const c = draft;
      draft = null;
      if (c !== null && c.toLowerCase() !== current().toLowerCase()) send(c);
    },
    shown: (own) => draft ?? own,
    pending: () => draft !== null,
  };
}
