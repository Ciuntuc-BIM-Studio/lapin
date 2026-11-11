/** Project templates (issues #9 and #20, decision 470): the browser's library and the choice File → New starts from. */

import { describe, expect, it, vi } from 'vitest';

vi.mock('./session', () => ({ engine: { call: vi.fn() }, engineInBrowser: true }));

import { BrowserTemplates, defaultTemplateOf, sortTemplates, templateId, type TemplateRow, type TemplateRows } from './templates';
import { readPrefs } from '../app/prefs';

function memoryRows(): TemplateRows & { map: Map<string, TemplateRow> } {
  const map = new Map<string, TemplateRow>();
  return { map, all: async () => [...map.values()], put: async (r) => { map.set(r.id, r); }, remove: async (id) => { map.delete(id); } };
}

describe('the browser keeps project templates', () => {
  it('under names the systems accept, as the desktop names their files', () => {
    expect(templateId('  Office: houses/2026 ')).toBe('Office_ houses_2026');
    expect(templateId('..hidden')).toBe('hidden');
    expect(templateId('   ')).toBe('');
    expect(templateId('x'.repeat(100))).toHaveLength(80);
  });

  it('keeps, lists by name, refuses a name taken unless replacing, renames and deletes', async () => {
    const rows = memoryRows();
    const lib = new BrowserTemplates(rows);
    expect(await lib.keep('office', 'AAAA', false)).toBe('office');
    expect(await lib.keep('Apartments', 'BBBBBBBB', false)).toBe('Apartments');
    expect((await lib.list()).map((t) => t.name)).toEqual(['Apartments', 'office']);
    expect((await lib.list())[0].size).toBe(6);
    // One of that name stays, unless replacing it is asked.
    expect(await lib.keep('office', 'CCCC', false)).toBe('exists');
    expect(await lib.data('office')).toBe('AAAA');
    expect(await lib.keep('office', 'CCCC', true)).toBe('office');
    expect(await lib.data('office')).toBe('CCCC');
    await expect(lib.keep('  ', 'DDDD', false)).rejects.toThrow('name');
    expect(await lib.rename('office', 'Office 2026')).toBe('Office 2026');
    await expect(lib.rename('Office 2026', 'Apartments')).rejects.toThrow('already');
    expect(await lib.data('Office 2026')).toBe('CCCC');
    await lib.remove('Apartments');
    expect((await lib.list()).map((t) => t.id)).toEqual(['Office 2026']);
    await expect(lib.data('Apartments')).rejects.toThrow('no template');
  });
});

describe('the template File → New starts from', () => {
  const list = sortTemplates([{ id: 'b', name: 'b', at: '', size: 1 }, { id: 'A', name: 'A', at: '', size: 1 }]);

  it('is the one Preferences chose while it is there, else Lapin’s default', () => {
    expect(list.map((t) => t.id)).toEqual(['A', 'b']);
    expect(defaultTemplateOf('b', list)?.id).toBe('b');
    expect(defaultTemplateOf('gone', list)).toBeNull();
    expect(defaultTemplateOf(null, list)).toBeNull();
  });

  it('is kept with the preferences, Lapin’s default when nothing readable was kept', () => {
    expect(readPrefs({ defaultTemplate: 'Office' }).defaultTemplate).toBe('Office');
    expect(readPrefs({ defaultTemplate: '  ' }).defaultTemplate).toBeNull();
    expect(readPrefs({ defaultTemplate: 3 }).defaultTemplate).toBeNull();
    expect(readPrefs(null).defaultTemplate).toBeNull();
  });
});
