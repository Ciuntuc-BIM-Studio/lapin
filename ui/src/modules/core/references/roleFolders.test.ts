import { describe, expect, it } from 'vitest';
import { folderNames, groupByFolder, roleOfItem, roleOfName } from './roleFolders';

describe('the role of an ISO 19650 name', () => {
  it('is its sixth field', () => {
    expect(roleOfName('PRJ-ORG-ZZ-XX-DR-A-0001')).toBe('A');
    expect(roleOfName('PRJ-ORG-ZZ-XX-DR-s-0001_P01.pdf')).toBe('S');
    expect(roleOfName('PRJ_ORG_ZZ_XX_DR_E_0001.pdf', '_')).toBe('E');
  });
  it('is none for a name with fewer fields or a field that is no role', () => {
    expect(roleOfName('Plan parter.pdf')).toBeNull();
    expect(roleOfName('PRJ-ORG-ZZ-XX-DR-ARCH-0001')).toBeNull();
    expect(roleOfItem(['x.pdf', 'PRJ-ORG-ZZ-XX-M3-M-0002.ifc'])).toBe('M');
  });
});

describe('group by folder', () => {
  const tree = [{ id: 'issued', name: { en: 'Issued', ro: 'Emise' }, children: [{ id: 'issued-sheets', name: { en: 'Drawings', ro: 'Planșe' } }] }];
  const names = folderNames(tree, 'ro');
  const rows = [{ n: 'P-O-Z-X-DR-A-1' }, { n: 'P-O-Z-X-DR-S-2' }, { n: 'P-O-Z-X-DR-A-3' }, { n: 'unnamed' }, { n: 'P-O-Z-X-DR-Q-4' }];
  const groups = groupByFolder(rows, (r) => [r.n], { A: 'issued-sheets', S: 'issued', Q: 'gone' }, names);

  it('names the folders in the language', () => { expect(names.get('issued-sheets')).toBe('Planșe'); });
  it('puts each role in its folder, in order of appearance, and the rest under Other last', () => {
    expect(groups.map((g) => [g.name, g.rows.length])).toEqual([['Planșe', 2], ['Emise', 1], [null, 2]]);
    expect(groups[2].rows.map((r) => r.n)).toEqual(['unnamed', 'P-O-Z-X-DR-Q-4']);
  });
});
