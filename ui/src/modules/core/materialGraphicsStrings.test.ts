/** The configurator's strings are in every language (decision 678, stage 0b). */

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

describe('words that mean something else elsewhere', () => {
  it('the facade view and the material band have their own phrases', () => {
    expect(translate('facade|Elevation', 'ro')).toBe('Fațadă');
    expect(translate('material|Band', 'ro')).toBe('Bandă');
    expect(translate('Elevation', 'ro')).toBe('Cotă');
    for (const lang of LANGUAGES.map((l) => l.id).filter((l) => l !== ('en' as Lang))) {
      expect(translate('Elevation', lang, 'facade'), lang).not.toBe(translate('Elevation', lang));
      expect(translate('Band', lang, 'material'), lang).not.toBeNull();
    }
  });
});
