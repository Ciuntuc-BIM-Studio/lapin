/** The material graphics sets' strings are in every language (stage 4). */

import { describe, expect, it } from 'vitest';
import { LANGUAGES, loadAllLanguages, translate, type Lang } from '../../i18n';

await loadAllLanguages();

const PHRASES = ['Material graphics sets', 'Matches', 'Add an entry', 'Edited', 'Used by no style', 'Material graphics', 'Edit sets…', 'Fill colour', 'The project draws by', 'Material graphics set',
  'none: the materials\' own', 'A set needs a name.'];

describe('the material graphics sets\' strings', () => {
  for (const lang of LANGUAGES.map((l) => l.id).filter((l) => l !== ('en' as Lang))) {
    it(`are translated in ${lang}`, () => {
      for (const p of PHRASES) expect(translate(p, lang), p).not.toBeNull();
      const entries = translate('3 entries', lang);
      expect(entries).toContain('3');
      expect(translate('Entry 2 names no material or word: pick one or choose another match.', lang)).toContain('2');
      expect(translate('Delete the set „{name}”?', lang)).toContain('{name}');
      expect(translate('Delete the set „{name}”, used by {styles}? Those styles will draw materials as the materials say.', lang)).toMatch(/\{styles\}/);
    });
  }
});
