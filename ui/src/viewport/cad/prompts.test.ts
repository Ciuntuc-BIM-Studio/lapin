import { afterAll, describe, expect, it } from 'vitest';
import { loadLanguage, useLang } from '../../i18n';
import { orList, tp } from './prompts';
import { wantedWords } from './constraints';

await loadLanguage('ro');

describe('prompts in the interface language', () => {
  afterAll(() => useLang.setState({ lang: 'en' }));
  it('stay English in English', () => {
    useLang.setState({ lang: 'en' });
    expect(tp('{0}: pick {1} (Esc ends)', 'Horizontal', orList(wantedWords('horizontal', [])))).toBe('Horizontal: pick a line or a point (Esc ends)');
  });
  it('fill the translated phrase with the translated parts', () => {
    useLang.setState({ lang: 'ro' });
    const text = tp('{0}: pick {1} (Esc ends)', 'Horizontal', orList(wantedWords('horizontal', [])));
    expect(text).toContain('alege');
    expect(text).toContain('Esc încheie');
    expect(text).not.toContain('{');
  });
  it('keep the keywords', () => {
    useLang.setState({ lang: 'ro' });
    expect(tp('{0} {1}: type a value, =formula, or Enter to keep it {2}', 'Rază', 'R 1 m', '[Driven/X]')).toContain('[Driven/X]');
  });
});
