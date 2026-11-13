import { describe, expect, it } from 'vitest';
import { readBarShapes, readFamily, readFurnitureLibrary, readLineTypes, readMapping, readParameters, readPatterns, readPriceList, readProfileLibrary, readRulePack, readStandards, readSymbolPack, readTitleBlock } from './packs';

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

describe('the libraries\' own files (symbols, line types, standards, rules, bar shapes, parameters, mappings, families)', () => {
  it('a symbol library', () => {
    const lib = { format: 'lapin-symbols', version: 1, name: 'Office', symbols: [{ name: 'North', def: { items: [{ type: 'circle', c: [0, 0], r: 3 }] } }] };
    expect(readSymbolPack(b(lib))).toMatchObject({ name: 'Office', names: ['North'] });
    expect(() => readSymbolPack(b({ ...lib, symbols: [{ name: 'Empty', def: { items: [] } }] }))).toThrow('A symbol of the library has no name or draws nothing.');
    expect(() => readSymbolPack(b({ ...lib, version: 2 }))).toThrow(/newer Lapin/);
    expect(() => readSymbolPack(b({ format: 'x' }))).toThrow('The file is not a Lapin symbol library.');
  });

  it('a .lin file: names with a pattern line each', () => {
    expect(readLineTypes(b('*DASHDOT, dash dot\nA,12,-6,0,-6\n*HIDDEN2\nA,3,-1.5\n')).names).toEqual(['DASHDOT', 'HIDDEN2']);
    expect(() => readLineTypes(b('*A\n*B\nA,1,-1\n'))).toThrow('The line type A has no pattern.');
    expect(() => readLineTypes(b('hello\n'))).toThrow(/does not start with a \*NAME line/);
    expect(() => readLineTypes(b('*A\n1,2\n'))).toThrow(/does not start with A,/);
  });

  it('standards, rules, bar shapes, parameters, families', () => {
    expect(readStandards(b({ format: 'lapin-standards', version: 1, name: 'DIN', drafting: {} })).name).toBe('DIN');
    expect(() => readStandards(b({ format: 'lapin-standards', version: 1 }))).toThrow('The standards file has no drafting standards.');
    expect(readRulePack(b({ format: 'lapin-rules', version: 1, name: 'RO', indicative: true, stairs: {} })).name).toBe('RO');
    expect(() => readRulePack(b({ format: 'lapin-rules', version: 1, name: 'RO', indicative: false, stairs: {} }))).toThrow(/indicative/);
    expect(() => readRulePack(b({ format: 'lapin-rules', version: 1, name: 'RO' }))).toThrow('The rule pack has no rules.');
    expect(readBarShapes(b({ format: 'lapin-bar-shapes', version: 1, shapes: [{ code: '21', label: 'L' }] })).codes).toEqual(['21 L']);
    expect(() => readBarShapes(b({ format: 'lapin-bar-shapes', version: 1, shapes: [{ label: 'L' }] }))).toThrow('A shape of the file has no code or label.');
    expect(readParameters(b({ format: 'lapin-parameters', version: 1, parameters: [{ name: 'Fire', kind: 'text' }] })).names).toEqual(['Fire']);
    expect(() => readParameters(b({ format: 'lapin-parameters', version: 1, parameters: [] }))).toThrow('The parameters file has no parameter.');
    expect(readFamily(b({ format: 'lapin-family', version: 1, name: 'Bracket', def: { params: [] } })).name).toBe('Bracket');
    expect(() => readFamily(b({ format: 'lapin-family', version: 1, name: 'x' }))).toThrow('The family has no definition.');
  });

  it('a mapping: a table\'s or a layer map', () => {
    expect(readMapping(b({ app: 'lapin', kind: 'table', version: 1, target: 'core.classification', mapping: [{ header: 'Code', key: 'code' }] })).kind).toBe('table');
    expect(readMapping(b({ app: 'lapin', kind: 'layers', version: 1, layers: [{ layer: 'A-WALL', converter: null, options: {} }] })).names).toEqual(['A-WALL']);
    expect(() => readMapping(b({ app: 'lapin', kind: 'table', target: 't', mapping: [] }))).toThrow('The table mapping has no target or no column.');
    expect(() => readMapping(b({ kind: 'x' }))).toThrow('The file is not a Lapin mapping file.');
  });
});
