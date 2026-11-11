import { describe, expect, it } from 'vitest';
import { AISC, EN, groupFound, groupOf, projectStandardsOf, search, standardGroup, standardsOf, toggled, type LibraryEntry, type ProfileJson } from './model';
import { readPrefs } from '../../../app/prefs';

const p = (name: string, standard?: string): ProfileJson => ({ name, kind: 'I', standard: standard ?? null });
const lib = (key: string, name: string, profiles: ProfileJson[], origin: LibraryEntry['origin'] = 'computer', standard?: string): LibraryEntry =>
  ({ key, origin, name, library: { name, standard: standard ?? null, profiles } });

describe('profile standards (decision 457)', () => {
  it('name the group of a standard as the engine does', () => {
    // The same cases as ProfileStandardTests.A_standard_s_name_is_the_group_it_is_shown_under.
    const cases: [string | null, string | null][] = [
      ['EN 10365:2017', EN], ['EN 10210-2:2019', EN], ['DIN EN 10025-2', EN], ['SR EN 10056-1', EN], ['PN-EN 10219', EN],
      ['ASTM A6/A6M', AISC], ['AISC Shapes Database v16.0', AISC], ['BS 4-1:2005', 'BS'], ['DIN 1025-5', 'DIN'], ['JIS G 3192', 'JIS'],
      ['AS/NZS 3679.1', 'AS/NZS'], ['GB/T 11263', 'GB'], ['ISO 657', null], ['Firm catalogue 2024', null], ['', null], [null, null],
    ];
    for (const [s, g] of cases) expect(standardGroup(s)).toBe(g);
  });

  it('put a profile under its library\'s standard, else its own, else the library\'s name', () => {
    const builtin = lib('builtin', 'EN steel (Lapin)', [p('IPE 300', 'EN 10365:2017'), p('C 150×50×2')], 'builtin', EN);
    const firm = lib('folder:a', 'Firm X', [p('W14X90', 'ASTM A6'), p('Alu 50')], 'folder');
    expect(groupOf(builtin, builtin.library.profiles[1])).toBe(EN);
    expect(groupOf(firm, firm.library.profiles[0])).toBe(AISC);
    expect(groupOf(firm, firm.library.profiles[1])).toBe('Firm X');
    expect(standardsOf([firm, builtin])).toEqual([EN, AISC, 'Firm X']);
  });

  it('list the project\'s copies first, then each standard under its heading, those not offered or hidden left out', () => {
    const builtin = lib('builtin', 'EN steel (Lapin)', [p('IPE 300', 'EN 10365:2017'), p('HEA 200', 'EN 10365:2017')], 'builtin', EN);
    const aisc = lib('computer:aisc', 'AISC v16', [p('W14X90', 'ASTM A6')]);
    const own = lib('computer:mine', 'My profiles', [p('Alu 50')]);
    const project = lib('project', 'In the project', [p('HEB 240', 'EN 10365:2017')], 'project');
    const found = search([project, aisc, own, builtin], '');
    const all = groupFound(found, null, []);
    expect(all.map((g) => (g.project ? '(project)' : g.group))).toEqual(['(project)', EN, AISC, 'My profiles']);
    expect(all[1].items.map((f) => f.profile.name)).toEqual(['IPE 300', 'HEA 200']);
    // The project offers EN only; the user hid EN: the project's copies stay.
    expect(groupFound(found, [EN], []).map((g) => g.group)).toEqual(['', EN]);
    expect(groupFound(found, [EN], [EN]).map((g) => g.project)).toEqual([true]);
    // At most `limit` profiles, the hidden ones not counted.
    expect(groupFound(found, null, [EN], 2).flatMap((g) => g.items).map((f) => f.profile.name)).toEqual(['HEB 240', 'W14X90']);
  });

  it('turn a standard on or off from all or a list', () => {
    expect(toggled(null, [EN, AISC, 'Firm'], AISC, false)).toEqual([EN, 'Firm']);
    expect(toggled([EN], [EN, AISC], AISC, true)).toEqual([EN, AISC]);
    expect(toggled([AISC], [EN, AISC], EN, true)).toEqual([EN, AISC]);
  });

  it('read the project\'s standards and the user\'s choices', () => {
    const nodes = new Map([['p', { type: 'IfcProject', props: { profileStandards: [EN, 3] } as Record<string, unknown> }]]);
    expect(projectStandardsOf(nodes)).toEqual([EN]);
    expect(projectStandardsOf(new Map([['p', { type: 'IfcProject', props: {} }]]))).toBeNull();
    const prefs = readPrefs({ profileStandardsHidden: [AISC, ' ', AISC], newProjectProfileStandards: [EN] });
    expect(prefs.profileStandardsHidden).toEqual([AISC]);
    expect(prefs.newProjectProfileStandards).toEqual([EN]);
    expect(readPrefs({ newProjectProfileStandards: 'EN' }).newProjectProfileStandards).toBeNull();
  });
});
