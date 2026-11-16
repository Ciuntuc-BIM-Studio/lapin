import { readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import {
  ENVELOPE, HEADER_KEY, checkEnvelope, chooseAll, defaultChoices, freeName, headerOf, rowsByName, setLibraryApp, withEnvelope, withoutHeader,
  type LibraryRow,
} from './libraryFile';
import {
  readBarShapes, readFamily, readFurnitureLibrary, readLineTypes, readMapping, readParameters, readPriceList, readProfileLibrary, readRulePack,
  readStandards, readSymbolPack, readTitleBlock, readTypePack, writePriceList,
} from './extensions/packs';
import { parseMappingFile, tableMappingText } from '../modules/core/tables/tableModel';
import { mergeChosen, previewRows, type LibraryEntry } from '../modules/furniture/library';

/** The files the build before the envelope wrote (engine/tests/…/Fixtures/libraries/v0, saved by that build). */
const v0 = resolve(__dirname, '../../../engine/tests/Lapin.Engine.Tests/Fixtures/libraries/v0');
const old = (file: string) => readFileSync(join(v0, file));
const enc = (v: unknown) => new TextEncoder().encode(JSON.stringify(v));
/** The same file with this build's header, as it is written now. */
const enveloped = (file: string) => {
  const o = JSON.parse(old(file).toString()) as Record<string, unknown>;
  return enc(withEnvelope(o.format as string, o.version as number, o, 'Test'));
};

describe('the one envelope of the library files', () => {
  it('writes format and version first, then the header, then the payload', () => {
    setLibraryApp('0.1.2000');
    const f = withEnvelope('lapin-price-list', 1, { format: 'x', version: 9, name: 'Ofertă', rows: [] }, 'Ofertă', new Date('2026-11-16T10:00:00.123Z'));
    expect(Object.keys(f)).toEqual(['format', 'version', HEADER_KEY, 'name', 'rows']);
    expect(f[HEADER_KEY]).toEqual({ envelope: ENVELOPE, app: '0.1.2000', created: '2026-11-16T10:00:00Z', source: 'Ofertă' });
    expect(headerOf(f)).toEqual({ format: 'lapin-price-list', version: 1, envelope: 1, app: '0.1.2000', created: '2026-11-16T10:00:00Z', source: 'Ofertă' });
    expect(Object.keys(withoutHeader(f))).toEqual(['format', 'version', 'name', 'rows']);
  });

  it('reads a file without a header as envelope 0, and refuses a newer one', () => {
    expect(headerOf({ format: 'lapin-symbols', version: 1 }).envelope).toBe(0);
    expect(() => checkEnvelope({ format: 'lapin-symbols', version: 1, lapin: { envelope: 2, app: '9.0' } }, 'symbol library')).toThrow('This symbol library was written by a newer Lapin (9.0): update Lapin to read it.');
    expect(() => readSymbolPack(enc({ format: 'lapin-symbols', version: 1, lapin: { envelope: 5 }, symbols: [{ name: 'A', def: { items: [{}] } }] }))).toThrow(/newer Lapin/);
  });

  it('every reader of the interface takes the files the build before the envelope wrote, and the same with the header', () => {
    for (const read of [(f: string) => old(f), enveloped]) {
      expect(readSymbolPack(read('office.lapinsymbols')).names).toEqual(['Arrow']);
      expect(readTypePack(read('walls.lapintypes')).types.map((t) => t.name)).toEqual(['WT1 Exterior']);
      expect(readStandards(read('office.lapinstandards')).name).toBe('Office');
      expect(readRulePack(read('Office (DE).lapinrules')).name).toBe('Office (DE)');
      expect(readParameters(read('parameters.lapinparams')).names).toEqual(['FireRating']);
      expect(readTitleBlock(read('Office block.lapintitleblock')).name).toBe('Office block');
      expect(readBarShapes(read('shapes.lapinbarshapes')).codes).toEqual(['21 U bar']);
      expect(readFamily(read('Bracket.lapinpart')).name).toBe('Bracket');
      expect(readFamily(read('Shelf bracket.lapinfamily')).name).toBe('Shelf bracket');
      expect(readFurnitureLibrary(read('My furniture.lapinlib')).names).toEqual(['Chair']);
      expect(readProfileLibrary(read('Office steel.lapinprofiles')).profiles).toHaveLength(3);
    }
    expect(readLineTypes(old('lapin.lin')).names).toEqual(['Fence']);
  });

  it('the interface\'s own files: a price list and a mapping as the build before wrote them, and as written now', () => {
    const priceV0 = JSON.stringify({ format: 'lapin-price-list', version: 1, name: 'Oferta', currency: 'RON', indicative: true, rows: [{ c: 'A1', n: 'Zidărie', u: 'm3', m: 300 }] }, null, 1);
    const list = readPriceList(new TextEncoder().encode(priceV0));
    expect(list.name).toBe('Oferta');
    const now = writePriceList(list);
    expect(headerOf(JSON.parse(now)).envelope).toBe(1);
    expect(readPriceList(new TextEncoder().encode(now))).toEqual(list);

    const mappingV0 = JSON.stringify({ app: 'lapin', kind: 'table', version: 1, target: 'core.classification', mapping: [{ header: 'Code', key: 'code' }] }, null, 2);
    expect(readMapping(new TextEncoder().encode(mappingV0)).names).toEqual(['core.classification']);
    const written = tableMappingText('core.classification', ['Code'], [{ key: 'code' }] as never);
    expect(JSON.parse(written).format).toBe('lapin-mapping');
    expect(readMapping(new TextEncoder().encode(written)).kind).toBe('table');
    expect(parseMappingFile(written).kind).toBe('table');
  });
});

describe('the one import preview', () => {
  const row = (key: string, status: LibraryRow['status'], choices: LibraryRow['choices'] = ['both', 'update', 'skip']): LibraryRow =>
    ({ key, kind: 'symbol', name: key, status, differs: [], choices: status === 'name' || status === 'id' ? choices : [], choice: status === 'id' ? 'update' : status === 'name' ? 'both' : null });

  it('starts each conflict at its default and sets one choice for all that offer it', () => {
    const rows = [row('A', 'name'), row('B', 'id', ['update', 'skip']), row('C', 'new'), row('D', 'same')];
    expect(defaultChoices(rows)).toEqual({ A: 'both', B: 'update' });
    expect(chooseAll(rows, defaultChoices(rows), 'both')).toEqual({ A: 'both', B: 'update' });
    expect(chooseAll(rows, defaultChoices(rows), 'skip')).toEqual({ A: 'skip', B: 'skip' });
  });

  it('a library the interface keeps compares by name, and names a copy kept beside „Name (2)”', () => {
    const have = new Map([['Oferta', 1], ['Deviz', 2]]);
    const rows = rowsByName([{ name: 'Oferta', item: 1 }, { name: 'Deviz', item: 3 }, { name: 'Nou', item: 4 }], (n) => have.get(n), (a, b) => a === b, 'priceList');
    expect(rows.map((r) => r.status)).toEqual(['same', 'name', 'new']);
    expect(freeName((n) => n === 'Deviz (2)', 'Deviz')).toBe('Deviz (3)');
  });

  it('my furniture in the browser: the engine\'s rules, by id then by name', () => {
    const e = (id: string, name: string, price?: number) => ({ id, name, category: 'living', tags: [], at: '2026-01-01', type: { class: 'IfcFurnitureType', spec: { kind: 'chair' } }, ...(price != null ? { price } : {}) }) as unknown as LibraryEntry;
    const kept = [e('a', 'Chair'), e('b', 'Table')];
    const incoming = [e('a', 'Chair', 120), e('c', 'Table'), e('b', 'Table'), e('d', 'Sofa')];
    expect(previewRows(kept, incoming).map((r) => [r.status, r.choice])).toEqual([['id', 'update'], ['name', 'both'], ['same', null], ['new', null]]);
    let n = 0;
    const { put, tally } = mergeChosen(kept, incoming, {}, () => `new${++n}`);
    expect(tally).toMatchObject({ added: 2, updated: 1, kept: 1, skipped: 0, renamed: ['Table (2)'] });
    expect(put.map((x) => [x.id, x.name])).toEqual([['a', 'Chair'], ['new1', 'Table (2)'], ['d', 'Sofa']]);
    expect(mergeChosen(kept, incoming, { a: 'skip', c: 'skip' }, () => 'x').tally).toMatchObject({ added: 1, skipped: 2 });
  });
});
