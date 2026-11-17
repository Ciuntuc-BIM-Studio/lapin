/** The configurator's strings are in every language (decision 670, stage 0b). */

import { describe, expect, it } from 'vitest';
import { LANGUAGES, loadAllLanguages, translate, type Lang } from '../../i18n';

await loadAllLanguages();

const PHRASES = ['Second pattern', 'Cut outline', 'Separator lines', 'Fill opacity', 'Weight, mm', 'Copy graphics from…', 'Clear selection', 'Automatic', 'Lines', 'Background'];
const SENTENCES = ['Changes go to all 3 selected materials.', '3 materials selected', 'Graphics copied from Brick.'];

describe('the material graphics configurator\'s strings', () => {
  for (const lang of LANGUAGES.map((l) => l.id).filter((l) => l !== ('en' as Lang))) {
    it(`are translated in ${lang}`, () => {
      for (const p of PHRASES) expect(translate(p, lang), p).not.toBeNull();
      for (const s of SENTENCES) {
        const out = translate(s, lang);
        expect(out, s).not.toBeNull();
        if (/\d/.test(s)) expect(out).toContain('3');
      }
      expect(translate('Graphics copied from Brick.', lang)).toContain('Brick');
    });
  }
});
