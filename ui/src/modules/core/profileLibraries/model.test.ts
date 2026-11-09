import { beforeEach, describe, expect, it, vi } from 'vitest';
import { asSweepShape, withProfile, definition, families, fieldOf, formatValue, matches, newerInLibrary, rememberChoice, rememberedChoice, search, setColumn, type LibraryEntry, type ProfileJson, type ProjectCopy } from './model';

const store = new Map<string, string>();
vi.stubGlobal('localStorage', {
  getItem: (k: string) => store.get(k) ?? null, setItem: (k: string, v: string) => { store.set(k, v); }, removeItem: (k: string) => { store.delete(k); },
});

const ipe = (n: number, tw = 7.1): ProfileJson => ({ name: `IPE ${n}`, kind: 'I', family: 'IPE', standard: 'EN 10365:2017', material: 'steel', h: n / 1000, b: 0.15, tw: tw / 1000, tf: 0.0107 });
const lib = (key: string, name: string, profiles: ProfileJson[], origin: LibraryEntry['origin'] = 'computer'): LibraryEntry => ({ key, origin, name, library: { name, profiles } });

describe('profile libraries', () => {
  beforeEach(() => store.clear());

  it('find a profile by any word of its name, family, standard or kind, × typed as x', () => {
    const l = { name: 'L 100×100×10', kind: 'L', family: 'L equal', standard: 'EN 10056-1:2017' };
    expect(matches(l, 'l 100x100')).toBe(true);
    expect(matches(l, '10056')).toBe(true);
    expect(matches(l, 'angle')).toBe(true);
    expect(matches(l, 'ipe')).toBe(false);
    const all = [lib('a', 'EN', [ipe(300), ipe(200)], 'builtin'), lib('b', 'Firm', [{ name: 'W14X90', kind: 'I', family: 'W' }])];
    expect(search(all, 'w14').map((f) => f.profile.name)).toEqual(['W14X90']);
    expect(search(all, '', { family: 'IPE' })).toHaveLength(2);
    expect(search(all, '', { kinds: ['L'] })).toHaveLength(0);
  });

  it('list families in their natural order', () => {
    expect(families([ipe(300), { name: 'HEA 100', kind: 'I', family: 'HEA' }, ipe(80)])).toEqual([{ family: 'HEA', count: 1 }, { family: 'IPE', count: 2 }]);
  });

  it('mark a project copy its library has another version of, never one from another library', () => {
    const copy: ProjectCopy = { id: 'p1', name: 'IPE 300', library: 'Firm', profile: ipe(300), types: ['t1'] };
    const same = lib('c', 'Firm', [{ ...ipe(300), summary: { mass: 42 }, source: 'x.csv' }]);
    expect(newerInLibrary(copy, [same])).toBeNull();
    const changed = lib('c', 'Firm', [ipe(300, 7.5)]);
    expect(newerInLibrary(copy, [changed])?.profile.tw).toBe(0.0075);
    expect(newerInLibrary(copy, [lib('d', 'Other', [ipe(300, 7.5)])])).toBeNull();
    expect(definition({ ...ipe(300), summary: { A: 1 } })).toBe(definition(ipe(300)));
  });

  it('remember a table\'s column choice by its layout', () => {
    expect(rememberedChoice('name|h|b')).toBeNull();
    rememberChoice('name|h|b', { map: { name: { index: 0, unit: '' }, h: { index: 1, unit: 'in' } }, kind: 'I', material: 'steel' });
    expect(rememberedChoice('name|h|b')?.map.h).toEqual({ index: 1, unit: 'in' });
  });

  it('move a field to the column it is chosen for', () => {
    const m = setColumn({ name: { index: 0, unit: '' }, h: { index: 1, unit: 'mm' } }, 1, 'b', 'mm');
    expect(m.h).toBeNull();
    expect(m.b).toEqual({ index: 1, unit: 'mm' });
    expect(fieldOf(m, 1)).toBe('b');
  });

  it('show values in their units to four figures', () => {
    expect(formatValue(53.81e-4, 1e-4)).toBe('53.81');
    expect(formatValue(-1e-12, 1e-3)).toBe('0');
    expect(formatValue(8356e-8, 1e-8)).toBe('8356');
    expect(formatValue(0.1259e-6, 1e-9)).toBe('125.9');
    expect(formatValue(undefined, 1)).toBe('');
  });

  it('make a sweep\'s 2D profile of the outside, leaving the holes', () => {
    const shape = asSweepShape([[[-0.05, -0.03], [0.05, -0.03], [0.05, 0.03], [-0.05, 0.03]], [[-0.04, -0.02], [-0.04, 0.02], [0.04, 0.02], [0.04, -0.02]]], 'footing', 'Tube');
    expect(shape?.points).toEqual([[0, 0], [0.1, 0], [0.1, 0.06], [0, 0.06]]);
    expect(shape?.insertion[0]).toBeCloseTo(0.05);
    expect(asSweepShape([], 'sweep', 'x')).toBeNull();
  });

  it('add a profile to a library, one of its name replaced, in order', () => {
    const l = withProfile({ name: 'F', profiles: [ipe(300), ipe(80)] }, ipe(300, 9));
    expect(l.profiles.map((p) => p.name)).toEqual(['IPE 80', 'IPE 300']);
    expect(l.profiles[1].tw).toBe(0.009);
  });
});
