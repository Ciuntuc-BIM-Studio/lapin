import { describe, expect, it } from 'vitest';
import { counts, librariesArg, setColumn, type PreviewRow } from './typeTables';

describe('structure types from a table (report #135)', () => {
  it('sends the computer and shared folder libraries, not the built-in one or the project', () => {
    const lib = (name: string) => ({ name, profiles: [{ name: 'P', kind: 'rectangle' }] });
    const entries = [
      { key: 'builtin', origin: 'builtin' as const, name: 'Lapin', library: lib('Lapin') },
      { key: 'computer:A', origin: 'computer' as const, name: 'A', library: lib('A') },
      { key: 'folder:B', origin: 'folder' as const, name: 'B', library: lib('B') },
    ];
    expect(librariesArg(entries as never).map((l) => l.name)).toEqual(['A', 'B']);
  });
  it('counts the rows by what the import does with them', () => {
    const r = (status: PreviewRow['status']): PreviewRow => ({ line: 1, name: 'x', kind: 'beam', lengths: [], status });
    expect(counts([r('new'), r('new'), r('same'), r('error')])).toEqual({ new: 2, same: 1, differs: 0, error: 1 });
  });
  it('gives a column to one field only, keeping the unit', () => {
    const m = setColumn({ name: { index: 0 }, h: { index: 1, unit: 'cm' }, b: null }, 'b', 1);
    expect(m).toEqual({ name: { index: 0 }, h: null, b: { index: 1 } });
    expect(setColumn({ h: { index: 1, unit: 'cm' } }, 'h', 2)).toEqual({ h: { index: 2, unit: 'cm' } });
    expect(setColumn({ h: { index: 1 } }, 'h', -1)).toEqual({ h: null });
  });
});
