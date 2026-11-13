import { describe, expect, it, vi } from 'vitest';

vi.mock('virtual:lapin-modules', () => ({ default: [], product: { id: 't', name: 'T', brand: { name: 'T', short: 'Lapin' }, modules: [] } }));

import { registerLibraryKind } from '../platform/libraries';
import { countByKind, fileEntry, filterEntries, namesText, symbolEntries, typeEntries, type CatalogEntry } from './catalogEntries';

registerLibraryKind({ ext: 'lapinrules', label: 'Rule packs', names: (t) => { const o = JSON.parse(t); if (o.format !== 'lapin-rules') throw new Error('not a rule pack'); return [o.name]; }, bring: async () => 'ok' });

const e = (over: Partial<CatalogEntry>): CatalogEntry => ({ key: over.name ?? 'k', kind: 'lin', name: 'x', source: 'computer', ...over });

describe('the unified catalog', () => {
  it('shortens a long list of names', () => {
    expect(namesText(['a', 'b'])).toBe('a, b');
    expect(namesText(['a', 'b', 'c', 'd', 'e', 'f'])).toBe('a, b, c, d +2');
  });

  it('makes one entry of a library file, read by its kind, and says why it left a bad one out', () => {
    const good = fileEntry({ kind: 'lapinrules', name: 'Romania', text: JSON.stringify({ format: 'lapin-rules', name: 'Romania' }), source: 'firm', from: 'Office' });
    expect(good).toMatchObject({ kind: 'lapinrules', name: 'Romania', detail: 'Romania', source: 'firm', from: 'Office' });
    expect('add' in good && typeof good.add).toBe('function');
    expect(fileEntry({ kind: 'lapinrules', name: 'bad', text: '{"format":"x"}', source: 'firm' })).toEqual({ problem: 'bad: not a rule pack' });
    expect(fileEntry({ kind: 'nothing', name: 'z', text: '', source: 'firm' })).toHaveProperty('problem');
  });

  it('lists a symbol library by symbol and a type pack by type', () => {
    const pack = JSON.stringify({ format: 'lapin-symbols', version: 1, symbols: [{ name: 'North', def: { scale: 'annotation', category: 'north', items: [{ type: 'circle', c: [0, 0], r: 3 }], attributes: [] } }] });
    const s = symbolEntries(pack, { source: 'extension', from: 'Office symbols', license: 'MIT' }, async () => undefined);
    expect(s.map((x) => [x.name, x.detail, x.license])).toEqual([['North', 'north', 'MIT']]);
    const types = JSON.stringify({ format: 'lapin-type-pack', version: 1, types: [{ ref: 't1', class: 'IfcWallType', name: 'WT1', attrs: {} }] });
    const t = typeEntries(types, { source: 'computer', pack: 'My walls' }, async () => undefined);
    expect(t.map((x) => [x.name, x.detail])).toEqual([['WT1', 'Wall · My walls']]);
    expect(typeEntries('not json', { source: 'computer', pack: 'x' }, async () => undefined)).toEqual([]);
  });

  it('filters by search, kind and source, and counts by kind', () => {
    const list = [e({ name: 'Zeta', detail: 'dash dot' }), e({ name: 'Alpha', kind: 'lapinrules', source: 'firm', from: 'Office' }), e({ name: 'Beta', source: 'firm' })];
    expect(filterEntries(list, {}).map((x) => x.name)).toEqual(['Alpha', 'Beta', 'Zeta']);
    expect(filterEntries(list, { search: 'dot' }).map((x) => x.name)).toEqual(['Zeta']);
    expect(filterEntries(list, { search: 'office' }).map((x) => x.name)).toEqual(['Alpha']);
    expect(filterEntries(list, { kind: 'lin', source: 'firm' }).map((x) => x.name)).toEqual(['Beta']);
    expect(countByKind(list)).toEqual({ lin: 2, lapinrules: 1 });
  });
});
