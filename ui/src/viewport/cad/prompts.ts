/**
 * The CAD command line's prompts in the interface's language. A prompt is a phrase with `{0}`, `{1}` for what is filled in
 * (a tool's name, a part wanted), looked up in the language tables like any other phrase; keywords (Driven, X, Y, D, R) and
 * command names are never translated. The constraint tools use it first; other commands may adopt it as they like.
 */

import { t } from '../../i18n';

/** A phrase in the language chosen, with its `{n}` filled in. */
export function tp(phrase: string, ...args: string[]): string {
  return t(phrase).replace(/\{(\d+)\}/g, (m, i) => args[Number(i)] ?? m);
}

/** Words joined by "or" in the language chosen. */
export const orList = (words: readonly string[]): string => words.map((w) => t(w)).join(` ${t('or')} `);
