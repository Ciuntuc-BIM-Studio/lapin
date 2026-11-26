import { describe, expect, it } from 'vitest';
import { addDef, addDraft, defsFor, draftJson, newTypeArgs, patchDraft, startValue } from './typeParamDraft';

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

describe("every type dialog's new type (issues #146, #147)", () => {
  const defs = [
    { name: 'Fire rating', kind: 'text' },
    { name: 'Span class', kind: 'choice', options: ['A', 'B'], classes: ['IfcBeam'] },
    { name: 'U-value', kind: 'number', pset: 'Office_Thermal', classes: ['IfcWallType'] },
  ];

  it('offers the shared definitions for every class and those naming the type or its elements, before any element uses them', () => {
    expect(defsFor(defs, 'IfcBeamType').map((d) => d.name)).toEqual(['Fire rating', 'Span class']);
    expect(defsFor(defs, 'IfcWallType').map((d) => d.name)).toEqual(['Fire rating', 'U-value']);
    expect(defsFor(defs, 'LapinCeilingType').map((d) => d.name)).toEqual(['Fire rating']);
  });

  it("adds a shared definition's parameter as its kind, set and choices, once", () => {
    let list = addDef([], defs[1]);
    list = addDef(list, defs[1]);
    list = addDef(list, defs[2]);
    expect(list).toEqual([
      { name: 'Span class', kind: 'choice', value: 'A', options: ['A', 'B'] },
      { name: 'U-value', kind: 'number', value: 0, pset: 'Office_Thermal' },
    ]);
  });

  it('sends the mark typed and the parameters chosen, and nothing when there are none', () => {
    expect(newTypeArgs('  ', [])).toEqual({});
    expect(newTypeArgs(' B1 ', addDraft([], 'Fire rating', 'text'))).toEqual({ mark: 'B1', params: [{ name: 'Fire rating', kind: 'text', value: '' }] });
  });
});
