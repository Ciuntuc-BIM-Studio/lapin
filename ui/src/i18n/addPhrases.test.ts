import { describe, expect, it } from 'vitest';
import { addPhrases, fillTemplate, loadLanguage, translate } from './index';

/** An extension's phrase tables (docs/plan-extension-modules.md §3.3), merged into the translator. */
describe('addPhrases', () => {
  it('merges into a loaded language; the host keeps its own translation', async () => {
    await loadLanguage('ro');
    expect(addPhrases('ro', { phrases: { 'Hello from an extension': 'Salut de la o extensie', Cancel: 'Anulează (extensie)' } })).toBe(true);
    expect(translate('Hello from an extension', 'ro')).toBe('Salut de la o extensie');
    expect(translate('Cancel', 'ro')).toBe('Renunță');
  });

  it('keeps a table until its language loads', async () => {
    expect(addPhrases('de', { phrases: { 'Only from an extension': 'Nur aus einer Erweiterung' } })).toBe(true);
    expect(translate('Only from an extension', 'de')).toBeNull();
    await loadLanguage('de');
    expect(translate('Only from an extension', 'de')).toBe('Nur aus einer Erweiterung');
  });

  it('fills a pattern\'s template with its groups', async () => {
    await loadLanguage('ro');
    addPhrases('ro', { patterns: [['^(\\d+) hellos from (\\w+)$', '{1} saluturi de la {2}'], ['(', 'broken'], ['^x$', 3 as unknown as string]] });
    expect(translate('12 hellos from Ana', 'ro')).toBe('12 saluturi de la Ana');
    expect(fillTemplate('{1}-{3}', /^(a)(b)?$/.exec('a')!)).toBe('a-');
  });

  it('ignores English, unknown languages and entries that are not text', async () => {
    expect(addPhrases('en', { phrases: { A: 'B' } })).toBe(false);
    expect(addPhrases('xx', { phrases: { A: 'B' } })).toBe(false);
    await loadLanguage('ro');
    addPhrases('ro', { phrases: { 'Not text here': 5 as unknown as string } });
    expect(translate('Not text here', 'ro')).toBeNull();
  });
});
