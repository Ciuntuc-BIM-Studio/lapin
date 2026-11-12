import { describe, expect, it } from 'vitest';
import { readFurnitureLibrary, readPatterns, readPriceList, readProfileLibrary, readTitleBlock } from './packs';

const b = (v: unknown) => new TextEncoder().encode(typeof v === 'string' ? v : JSON.stringify(v));

describe('a content pack\'s file', () => {
  it('a furniture library: its items with their types', () => {
    const lib = { format: 'lapin-furniture-library', version: 1, items: [{ id: 'a', name: 'Oak bookcase', category: 'storage', tags: [], type: { class: 'IfcFurnitureType', name: 'Oak bookcase', spec: { kind: 'casework' }, materials: [] } }] };
    expect(readFurnitureLibrary(b(lib)).names).toEqual(['Oak bookcase']);
    expect(() => readFurnitureLibrary(b({ ...lib, version: 2 }))).toThrow(/newer Lapin/);
    expect(() => readFurnitureLibrary(b({ ...lib, items: [{ name: 'x' }] }))).toThrow('An item of the furniture library has no name or no type.');
    expect(() => readFurnitureLibrary(b('not json'))).toThrow('The file is not a Lapin furniture library: it is not JSON.');
    expect(() => readFurnitureLibrary(new Uint8Array([0xff, 0xfe, 0x00]))).toThrow('The file is not UTF-8 text.');
  });

  it('a profile library', () => {
    const lib = { format: 'lapin-profiles', version: 1, name: 'Țevi RO', material: 'steel', profiles: [{ name: 'CHS 48.3x3.2', kind: 'CHS', b: 48.3, tw: 3.2 }] };
    expect(readProfileLibrary(b(lib)).name).toBe('Țevi RO');
    expect(() => readProfileLibrary(b({ ...lib, profiles: [] }))).toThrow('The profile library has no profile.');
  });

  it('a .pat file: its patterns, each with its lines', () => {
    const pat = '; my patterns\n*CARAMIDA, brick courses\n0, 0,0, 0,75\n90, 0,0, 75,75, 75,-75\n*SOLID-RO, plin\n45, 0,0, 0,1\n';
    expect(readPatterns(b(pat)).names).toEqual(['CARAMIDA', 'SOLID-RO']);
    expect(() => readPatterns(b('*EMPTY, nothing\n*NEXT\n0,0,0,0,1\n'))).toThrow('The pattern EMPTY has no line.');
    expect(() => readPatterns(b('hello world\n'))).toThrow(/does not start with a \*NAME line/);
    expect(() => readPatterns(b('*A, a\n0, zero\n'))).toThrow(/is not numbers/);
  });

  it('a title block', () => {
    expect(readTitleBlock(b({ format: 'lapin.titleBlock', version: 1, name: 'Cartuș RO', template: { w: 180, h: 50, items: [] } })).name).toBe('Cartuș RO');
    expect(() => readTitleBlock(b({ name: 'x' }))).toThrow('The file is not a Lapin title block.');
  });

  it('a price list, marked indicative', () => {
    const list = { format: 'lapin-price-list', version: 1, name: 'Prețuri orientative', currency: 'RON', indicative: true, rows: [{ c: 'CA01', n: 'Zidărie BCA', u: 'm3', m: 410, l: 120 }] };
    expect(readPriceList(b(list)).rows).toHaveLength(1);
    expect(() => readPriceList(b({ ...list, indicative: false }))).toThrow('A price list from an extension says its prices are indicative.');
    expect(() => readPriceList(b({ ...list, rows: [{ c: 'X', n: 'x', u: 'm', m: -1 }] }))).toThrow('The row X has a price that is not a number.');
  });
});
