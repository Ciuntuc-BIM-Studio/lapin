import { describe, expect, it, vi } from 'vitest';

vi.mock('virtual:lapin-modules', () => ({ default: [], product: { id: 't', name: 'T', brand: { name: 'T', short: 'Lapin' }, modules: [] } }));

import { gatherFirmFiles, writableFolders } from './firmFolders';

const file = (name: string, kind: string, modified: string) => ({ file: `/x/${name}.${kind}`, name, kind, modified, text: '' });

describe('the firm folders', () => {
  it('gathers the files of every folder, each knowing its folder and whether it is writable, by name', () => {
    const folders = [{ path: '/a', writable: true }, { path: '/b', writable: false }];
    const r = gatherFirmFiles(folders, [
      { folder: '/a', exists: true, files: [file('Walls', 'lapintypes', '2026-11-01')], problems: [] },
      { folder: '/b', exists: false, files: [file('Hatches', 'pat', '2026-10-01')], problems: ['The folder /b cannot be reached.'] },
    ]);
    expect(r.files.map((f) => [f.name, f.folder, f.writable])).toEqual([['Hatches', '/b', false], ['Walls', '/a', true]]);
    expect(r.problems).toEqual(['The folder /b cannot be reached.']);
    expect(writableFolders(folders).map((f) => f.path)).toEqual(['/a']);
  });
});
