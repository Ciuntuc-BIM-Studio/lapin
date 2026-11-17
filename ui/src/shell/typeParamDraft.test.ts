import { describe, expect, it } from 'vitest';
import { addDraft, draftJson, patchDraft, startValue } from './typeParamDraft';

describe("a new type's parameters (issue #32)", () => {
  it('start unticked, empty, at the first choice or at zero', () => {
    expect(startValue('bool')).toBe(false);
    expect(startValue('text')).toBe('');
    expect(startValue('choice', ['new', 'existing'])).toBe('new');
    expect(startValue('length')).toBe(0);
  });

  it('are added once per set and name, a standard one in its set', () => {
    let list = addDraft([], ' IsExternal ', 'bool', 'Pset_WallCommon');
    list = addDraft(list, 'isexternal', 'bool', 'Pset_WallCommon');
    list = addDraft(list, 'IsExternal', 'text');
    list = addDraft(list, '  ', 'text');
    expect(list).toEqual([
      { name: 'IsExternal', kind: 'bool', value: false, pset: 'Pset_WallCommon' },
      { name: 'IsExternal', kind: 'text', value: '' },
    ]);
  });

  it('go to the command as it takes them, a ticked yes/no and an instance default kept', () => {
    let list = addDraft([], 'IsExternal', 'bool', 'Pset_WallCommon');
    list = addDraft(list, 'Renovation', 'choice', undefined, ['new', 'existing']);
    list = patchDraft(list, 'pset_wallcommon/isexternal', { value: true });
    list = patchDraft(list, '/renovation', { instance: true });
    expect(draftJson(list)).toEqual([
      { name: 'IsExternal', kind: 'bool', value: true, pset: 'Pset_WallCommon' },
      { name: 'Renovation', kind: 'choice', value: 'new', options: ['new', 'existing'], instance: true },
    ]);
  });
});
