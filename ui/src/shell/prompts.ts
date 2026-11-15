/**
 * The questions the interface asks in the browser's own boxes (audit U3, I3): every text goes through the language chosen
 * first. A text may hold `{name}` places filled from `vars` after it is translated, so a phrase with a name or a number in it
 * is one phrase in the language files (`Delete the template „{name}”…`), not a sentence built in English.
 */

import { t } from '../i18n';
import { formatLength, parseLength, unitName } from '../engine/unitsStore';

/** The phrase in the language chosen, its `{name}` places filled. */
export function phrase(text: string, vars?: Record<string, string | number>): string {
  const out = t(text);
  return vars ? out.replace(/\{(\w+)\}/g, (m, k: string) => (k in vars ? String(vars[k]) : m)) : out;
}

/** OK / Cancel. */
export function confirmAction(text: string, vars?: Record<string, string | number>): boolean {
  return window.confirm(phrase(text, vars));
}

/** A line of text; null when cancelled. */
export function promptText(text: string, initial?: string, vars?: Record<string, string | number>): string | null {
  return window.prompt(phrase(text, vars), initial);
}

/** A length asked in the project's unit (`initial` in metres), metres back; null when cancelled or not a length. */
export function promptLength(text: string, initial?: number, vars?: Record<string, string | number>): number | null {
  const answer = window.prompt(`${phrase(text, vars)} [${unitName()}]`, initial == null ? '' : formatLength(initial));
  return answer == null ? null : parseLength(answer);
}
