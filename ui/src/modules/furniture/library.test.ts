import { describe, expect, it } from 'vitest';
import type { Rows } from '../../engine/templates';
import type { PreviewDto } from './furniture';
import { BrowserFurniture, allTags, canonical, filterEntries, guessCategory, mergeImported, parseTags, thumbnailSvg, type LibraryEntry } from './library';

const type = (kind: string, extra: Record<string, unknown> = {}) => ({ class: 'IfcFurnitureType', name: kind, spec: { kind, ...extra }, materials: [] });
const entry = (id: string, name: string, more: Partial<LibraryEntry> = {}): LibraryEntry =>
  ({ id, name, category: 'other', tags: [], at: '2026-11-11T10:00:00.000Z', type: type('casework', { preset: 'desk' }), ...more });

function memoryRows(): Rows<LibraryEntry> & { map: Map<string, LibraryEntry> } {
  const map = new Map<string, LibraryEntry>();
  return { map, all: async () => [...map.values()], put: async (r) => { map.set(r.id, r); }, remove: async (id) => { map.delete(id); } };
}

describe('my furniture library', () => {
  it('reads tags as typed: split at commas, trimmed, once each', () => {
    expect(parseTags(' oak, Modern ,, oak ,modern, walnut ')).toEqual(['oak', 'Modern', 'walnut']);
    expect(parseTags('')).toEqual([]);
  });

  it('finds entries by words in their name, tags, article and kind, by category and tag, by name', () => {
    const all = [
      entry('1', 'Walnut desk', { category: 'office', tags: ['walnut'], article: 'WD-140' }),
      entry('2', 'Oak bookcase', { category: 'living', tags: ['Oak', 'tall'], type: type('casework', { preset: 'bookcase' }) }),
      entry('3', 'Corner sofa', { category: 'living', type: type('cornerSofa') }),
    ];
    expect(filterEntries(all, {}).map((e) => e.name)).toEqual(['Corner sofa', 'Oak bookcase', 'Walnut desk']);
    expect(filterEntries(all, { search: 'wd-140' }).map((e) => e.id)).toEqual(['1']);
    expect(filterEntries(all, { search: 'bookcase tall' }).map((e) => e.id)).toEqual(['2']);
    expect(filterEntries(all, { search: 'cornersofa' }).map((e) => e.id)).toEqual(['3']);
    expect(filterEntries(all, { category: 'living' }).map((e) => e.id)).toEqual(['3', '2']);
    expect(filterEntries(all, { tag: 'oak' }).map((e) => e.id)).toEqual(['2']);
    expect(allTags(all)).toEqual(['Oak', 'tall', 'walnut']);
  });

  it('takes a file in as the engine does: the same skipped, the same id with other content under a new one', () => {
    const kept = [entry('a', 'Desk'), entry('b', 'Shelf')];
    const incoming = [
      { ...entry('a', 'Desk'), at: '2027-01-01T00:00:00.000Z' },
      entry('b', 'Shelf, taller'),
      entry('c', 'Bed'),
    ];
    let n = 0;
    const r = mergeImported(kept, incoming, () => `new${++n}`);
    expect(r.skipped).toBe(1);
    expect(r.add.map((e) => [e.id, e.name])).toEqual([['new1', 'Shelf, taller'], ['c', 'Bed']]);
    expect(canonical({ b: 1, a: [2, { d: null, c: 'x' }] })).toBe(canonical({ a: [2, { c: 'x', d: null }], b: 1 }));
  });

  it('draws the engine’s preview as a square SVG thumbnail of the front, in the materials’ colours', () => {
    const p: PreviewDto = {
      plan: [], width: 1.2, depth: 0.45, height: 0.8,
      front: [{ role: 'body', color: '#5b3a29', x0: -0.6, x1: 0.6, z0: 0, z1: 0.8 }, { role: 'fronts', color: '#9cae8f', x0: -0.58, x1: 0, z0: 0.1, z1: 0.78 }],
    };
    const url = thumbnailSvg(p);
    expect(url.startsWith('data:image/svg+xml;base64,')).toBe(true);
    const svg = atob(url.slice('data:image/svg+xml;base64,'.length));
    expect(svg.match(/<rect /g)?.length).toBe(2);
    expect(svg).toContain('fill="#5b3a29"');
    const [, , w, h] = svg.match(/viewBox="([^"]+)"/)![1].split(' ').map(Number);
    expect(w).toBe(h);
    expect(w).toBeGreaterThan(1.2);
  });

  it('starts Save to my library… with a category from the kind and the preset', () => {
    expect(guessCategory('kitchen')).toBe('kitchen');
    expect(guessCategory('casework', 'vanity')).toBe('bathroom');
    expect(guessCategory('casework', 'desk')).toBe('office');
    expect(guessCategory('casework', 'wardrobeSliding')).toBe('bedroom');
    expect(guessCategory('sofa')).toBe('living');
    expect(guessCategory('casework', 'hallUnit')).toBe('storage');
    expect(guessCategory('somethingNew')).toBe('other');
  });

  it('keeps the browser’s entries in its rows, each checked by the engine', async () => {
    const rows = memoryRows();
    const checked: unknown[] = [];
    const lib = new BrowserFurniture(rows, async (e) => { checked.push(e); return e as LibraryEntry; });
    const id = await lib.add({ name: 'Walnut desk', category: 'office', tags: ['walnut'], type: type('casework') });
    expect(rows.map.get(id)?.name).toBe('Walnut desk');
    const copy = await lib.duplicate(id);
    expect((await lib.get(copy)).name).toBe('Walnut desk copy');
    await lib.update(id, { tags: ['walnut', 'office'], price: null });
    expect((await lib.get(id)).tags).toEqual(['walnut', 'office']);
    expect((await lib.list()).map((e) => e.name)).toEqual(['Walnut desk', 'Walnut desk copy']);
    const again = await lib.import([await lib.get(id)]);
    expect(again).toEqual({ added: [], skipped: 1 });
    await lib.remove(copy);
    expect(rows.map.size).toBe(1);
    expect(checked.length).toBe(3);
    await expect(lib.get('nope')).rejects.toThrow('no such furniture');
  });
});
