/**
 * Document templates on this computer (docs/plan-documents.md, stage 4): kept whole in the storage, an id kept once unless
 * replaced on purpose, refused when the storage is full (the library left as it was); the list the Documents dialog offers,
 * the engine's templates first and the library's the project has not; a free id; the style set and taken off a page.
 */

import { beforeEach, describe, expect, it, vi } from 'vitest';

const stored = new Map<string, string>();
let quota = Infinity;
vi.stubGlobal('localStorage', {
  getItem: (k: string) => stored.get(k) ?? null,
  setItem: (k: string, v: string) => { if (v.length > quota) throw new Error('QuotaExceededError'); stored.set(k, v); },
  removeItem: (k: string) => void stored.delete(k),
});

const {
  changeLibraryTemplate, forgetTemplate, freeId, isTemplateJson, keepTemplate, libraryTemplate, libraryTemplates, mergeTemplates, withStyle, MAX_LIBRARY_CHARS,
} = await import('./templateLibrary');
import type { TemplateJson } from './templateLibrary';
import type { DocTemplate } from './docModel';

const tpl = (id: string, title = id, extra: Partial<TemplateJson> = {}): TemplateJson => ({ id, title, lang: 'ro', isoType: 'RP', blocks: [{ kind: 'heading', level: 1, text: title }], ...extra });
const engine = (id: string, origin: 'builtin' | 'project'): DocTemplate => ({ id, title: id, description: '', lang: 'ro', isoType: 'RP', inputs: [], origin });

beforeEach(() => { stored.clear(); quota = Infinity; });

describe('the library of document templates', () => {
  it('keeps a template whole with its day, and an id once unless replaced', () => {
    expect(keepTemplate(tpl('memoriu', 'Memoriu firmă'))).toBe('kept');
    expect(libraryTemplate('memoriu')?.title).toBe('Memoriu firmă');
    expect(libraryTemplate('memoriu')?.added).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    expect(keepTemplate(tpl('memoriu', 'Other'))).toBe('exists');
    expect(libraryTemplate('memoriu')?.title).toBe('Memoriu firmă');
    expect(keepTemplate(tpl('memoriu', 'Other'), true)).toBe('kept');
    expect(libraryTemplates().map((t) => t.title)).toEqual(['Other']);
    forgetTemplate('memoriu');
    expect(libraryTemplates()).toEqual([]);
  });

  it('refuses what the storage cannot take and leaves the library as it was', () => {
    keepTemplate(tpl('a'));
    const big = tpl('logo', 'With a big logo', { page: { style: { logo: { data: 'x'.repeat(MAX_LIBRARY_CHARS), w: 30 } } } });
    expect(keepTemplate(big)).toBe('full');
    expect(libraryTemplates().map((t) => t.id)).toEqual(['a']);
    // The browser's own quota, below the library's.
    quota = 600;
    expect(keepTemplate(tpl('b', 'B', { description: 'y'.repeat(800) }))).toBe('full');
    expect(libraryTemplates().map((t) => t.id)).toEqual(['a']);
    expect(changeLibraryTemplate('a', (t) => ({ ...t, description: 'z'.repeat(800) }))).toBe(false);
    expect(libraryTemplate('a')?.description).toBeUndefined();
  });

  it('renames and sets a style in place, and ignores what is not a template', () => {
    keepTemplate(tpl('a', 'A', { page: { size: 'A4' } }));
    expect(changeLibraryTemplate('a', (t) => ({ ...t, title: 'Renamed', page: withStyle(t.page, { font: 'Georgia', headingColor: '#1f4e8c' }) }))).toBe(true);
    expect(libraryTemplate('a')?.title).toBe('Renamed');
    expect(libraryTemplate('a')?.page).toEqual({ size: 'A4', style: { font: 'Georgia', headingColor: '#1f4e8c' } });
    expect(withStyle(libraryTemplate('a')?.page, null)).toEqual({ size: 'A4' });
    expect(withStyle(undefined, {})).toEqual({});
    expect(changeLibraryTemplate('missing', (t) => t)).toBe(false);
    stored.set('lapin.documentTemplates', JSON.stringify([{ id: 'ok', blocks: [] }, { title: 'no id' }, 3]));
    expect(libraryTemplates().map((t) => t.id)).toEqual(['ok']);
    stored.set('lapin.documentTemplates', '{broken');
    expect(libraryTemplates()).toEqual([]);
  });

  it('tells a template file from other JSON', () => {
    expect(isTemplateJson(tpl('a'))).toBe(true);
    expect(isTemplateJson({ id: ' ', blocks: [] })).toBe(false);
    expect(isTemplateJson({ id: 'a' })).toBe(false);
    expect(isTemplateJson(null)).toBe(false);
  });
});

describe('the templates a document starts from', () => {
  it('are the engine\'s, then the library\'s the project has not, each with its origin', () => {
    const merged = mergeTemplates(
      [engine('memoriu-arhitectura', 'builtin'), { ...engine('firm', 'project'), origin: undefined }, engine('shared', 'project')],
      [tpl('shared', 'Library copy'), tpl('own', 'Own', { description: 'Mine', inputs: [{ key: 'x', label: 'X', multiline: true }] })],
    );
    expect(merged.map((t) => [t.id, t.origin])).toEqual([['memoriu-arhitectura', 'builtin'], ['firm', 'builtin'], ['shared', 'project'], ['own', 'library']]);
    expect(merged[3]).toMatchObject({ title: 'Own', description: 'Mine', lang: 'ro', isoType: 'RP', inputs: [{ key: 'x' }] });
  });

  it('take a free id from a title, its diacritics dropped', () => {
    expect(freeId('Memoriu tehnic – firmă', [])).toBe('memoriu-tehnic-firma');
    expect(freeId('Memoriu', ['memoriu', 'memoriu-2'])).toBe('memoriu-3');
    expect(freeId('***', [])).toBe('template');
  });
});
