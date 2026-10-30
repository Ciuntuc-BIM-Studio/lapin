// The user guide's languages, in one place: run.mjs takes the pictures in
// each, render.mjs writes each one's pages, scenes.mjs types each one's words.
// English is docs/user (→ build/user-docs, /docs/); every other language is
// docs/user/<id> (→ build/user-docs/<id>, /docs/<id>/), its pictures taken
// with the interface in that language (ui/src/i18n, the same ids). A page a
// language does not have yet is rendered from English.
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

/** Each language named in itself, as the interface's Help menu names it. */
export const LANGUAGES = [
  { id: 'en', name: 'English' },
  { id: 'ro', name: 'Română' },
  { id: 'es', name: 'Español' },
  { id: 'de', name: 'Deutsch' },
  { id: 'fr', name: 'Français' },
  { id: 'da', name: 'Dansk' },
];

export const LANGS = LANGUAGES.map((l) => l.id);

/** A phrase a scene types, in the run's language (English when the table has none for it). */
export const say = (phrases, lang) => phrases[lang] ?? phrases.en;

/** The interface must offer every language the guide is written in: its LANGUAGES list (ui/src/i18n/index.ts) is read to be sure. */
export function checkInterfaceLanguages(root = join(dirname(fileURLToPath(import.meta.url)), '../..')) {
  const src = readFileSync(join(root, 'ui/src/i18n/index.ts'), 'utf8');
  const ui = [...src.matchAll(/\{ id: '([a-z]{2})', name: '[^']+' \}/g)].map((m) => m[1]);
  const lacking = LANGS.filter((l) => !ui.includes(l));
  if (lacking.length) throw new Error(`the interface has no language ${lacking.join(', ')} (ui/src/i18n/index.ts): the guide's pictures cannot be taken in it`);
}
