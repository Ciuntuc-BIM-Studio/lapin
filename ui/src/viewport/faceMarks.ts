/**
 * A wall's faces as the interface names them (tester's report #79): R, the face on the right of the way it was drawn,
 * where a type's first layer goes, and L, the left one. The selected wall shows them in plan with an arrow the way it was
 * drawn, and the Wall types dialog's legend uses the same marks. Drawings are not translated, so the letters follow the
 * interface's language here.
 */

import type { Lang } from '../i18n';

const LETTERS: Partial<Record<Lang, [string, string]>> = {
  ro: ['D', 'S'], es: ['D', 'I'], fr: ['D', 'G'], it: ['D', 'S'], pt: ['D', 'E'], pl: ['P', 'L'], da: ['H', 'V'],
};

/** The right and the left face's letters in a language (English's R and L where it has none of its own). */
export function faceLetters(lang: Lang): { right: string; left: string } {
  const [right, left] = LETTERS[lang] ?? ['R', 'L'];
  return { right, left };
}
