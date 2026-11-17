/**
 * Suggestions while typing a text with `{fields}` and `{= formulas}` (a drafting table's cell, its formula bar), the pure side:
 * whether the caret is inside braces (only then is the engine asked), what an accepted suggestion makes of the text, and what
 * a key does while the list is open. The engine says what fits (`drafting.tableSuggest`), so the list is what the evaluator reads.
 */

/** One suggestion: what goes in, how the list shows it, a value or how it is called, and what it is. */
export interface SuggestItem { insert: string; label: string; detail?: string | null; kind: string }

/** What the engine suggests for a text and its caret: the items, and the part of the text one replaces (`from`..`to`). */
export interface SuggestResult { from: number; to: number; items: SuggestItem[]; note?: string | null; word?: string }

/** Whether the caret is inside braces still open (`{…`, `{= …`): where names and functions are typed. */
export function inBraces(text: string, caret: number): boolean {
  let depth = 0;
  for (let i = 0; i < Math.min(caret, text.length); i++) {
    if (text[i] === '{') depth++;
    else if (text[i] === '}' && depth > 0) depth--;
  }
  return depth > 0;
}

/** The text with an item put in place of what was typed (`from`..`to`), and the caret after it. */
export function accept(text: string, r: Pick<SuggestResult, 'from' | 'to'>, item: SuggestItem): { text: string; caret: number } {
  const a = Math.max(0, Math.min(r.from, text.length)), b = Math.max(a, Math.min(r.to, text.length));
  return { text: text.slice(0, a) + item.insert + text.slice(b), caret: a + item.insert.length };
}

/** Whether an accepted item leads on to more (`el.`, `schedule.Rooms.`): the list is asked again at once. */
export const leadsOn = (item: SuggestItem): boolean => item.insert.endsWith('.');

export type SuggestKey = 'up' | 'down' | 'accept' | 'close' | null;

/** What a key does while the list is open (null: the input takes it). */
export function keyOf(key: string, shift: boolean): SuggestKey {
  if (key === 'ArrowDown') return 'down';
  if (key === 'ArrowUp') return 'up';
  if ((key === 'Enter' && !shift) || key === 'Tab') return 'accept';
  if (key === 'Escape') return 'close';
  return null;
}

/** The highlighted item moved by one, round the list. */
export const stepIndex = (i: number, n: number, dir: 1 | -1): number => (n <= 0 ? 0 : (i + dir + n) % n);
