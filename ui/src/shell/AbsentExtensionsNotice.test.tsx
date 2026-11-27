import { afterEach, describe, expect, it } from 'vitest';
import { renderToStaticMarkup } from 'react-dom/server';
import { AbsentExtensionsBar, AbsentExtensionsNotice, openAbsentExtensions } from './AbsentExtensionsNotice';
import { absentList, absentNotice, dismissAbsentExtensions, takeExtensionsTab, tellAbsentExtensions, useAbsentExtensions } from '../platform/extensions/absent';
import { loadAllLanguages, translate } from '../i18n';
import { useUi } from '../app/store';

const OPEN = { absentExtensions: [{ module: 'planning-plus', id: 'ro.ciuntucbimstudio.planning-plus', version: '1.0.0' }] };

describe('the notice of extensions a project uses that are not installed', () => {
  afterEach(() => { dismissAbsentExtensions(); useUi.setState({ dialog: null }); takeExtensionsTab(); });

  it('reads the list project.open returns, and ignores anything else', () => {
    expect(absentList(OPEN)).toEqual(OPEN.absentExtensions);
    expect(absentList({})).toEqual([]);
    expect(absentList(null)).toEqual([]);
    expect(absentList({ absentExtensions: [null, 3, { module: 'x' }] })).toEqual([]);
  });

  it('says nothing for a project that needs none, and clears an earlier notice', () => {
    expect(renderToStaticMarkup(<AbsentExtensionsNotice />)).toBe('');
    tellAbsentExtensions(OPEN);
    expect(useAbsentExtensions.getState().list).toHaveLength(1);
    tellAbsentExtensions({ absentExtensions: [] });
    expect(useAbsentExtensions.getState().list).toEqual([]);
  });

  it('names the extension, says its data is kept, and offers Extensions and a close', () => {
    const html = renderToStaticMarkup(<AbsentExtensionsBar names={['Planning plus']} onOpen={() => undefined} onClose={() => undefined} />);
    expect(html).toContain('This project uses Planning plus (extension), which is not installed. Its data is kept and untouched.');
    expect(html).toContain('data-action="absent-extensions-open"');
    expect(html).toContain('data-action="absent-extensions-close"');
    expect(html).toContain('role="alert"');
  });

  it('says several extensions in the plural', () => {
    expect(absentNotice(['A', 'B'])).toBe('This project uses A, B (extensions), which are not installed. Their data is kept and untouched.');
  });

  it('opens the Extensions dialog (Browse, or Installed when it is installed but off) and goes', () => {
    tellAbsentExtensions(OPEN);
    openAbsentExtensions(useAbsentExtensions.getState().list, []);
    expect(useUi.getState().dialog).toBe('extensions');
    expect(takeExtensionsTab()).toBe('browse');
    expect(useAbsentExtensions.getState().list).toEqual([]);
    tellAbsentExtensions(OPEN);
    openAbsentExtensions(useAbsentExtensions.getState().list, ['ro.ciuntucbimstudio.planning-plus']);
    expect(takeExtensionsTab()).toBe('installed');
    expect(takeExtensionsTab()).toBe('browse');
  });

  it('is said in every language, with the name put in after the translation', async () => {
    await loadAllLanguages();
    const one = 'This project uses {name} (extension), which is not installed. Its data is kept and untouched.';
    for (const lang of ['ro', 'es', 'de', 'fr', 'da', 'it', 'pl', 'pt', 'lo', 'zh', 'el', 'sv', 'th', 'ru', 'uk'] as const) {
      for (const k of [one, 'This project uses {names} (extensions), which are not installed. Their data is kept and untouched.', 'Open Extensions', 'Restart now']) {
        const out = translate(k, lang);
        expect(out, `${lang}: ${k}`).not.toBeNull();
        expect(out).not.toBe(k);
      }
      expect(translate(one, lang)).toContain('{name}');
    }
    expect(translate(one, 'ro')).toBe('Acest proiect folosește {name} (extensie), care nu este instalată. Datele ei sunt păstrate neatinse.');
  });
});
