import { describe, expect, it } from 'vitest';
import { applies, fieldText, fullSpec, groupsOf, isFormula, readField, specOut, withPreset, type PortalCatalog } from './portal';

const catalog: PortalCatalog = {
  params: [
    { key: 'kind', label: 'Structure', kind: 'choice', group: 'Layout', default: 'portal', formula: false, choices: ['portal', 'abutment'] },
    { key: 'crown', label: 'Crown elevation', kind: 'length', group: 'Layout', default: 7.5, formula: true },
    { key: 'wingAngle', label: 'Wing angle', kind: 'angle', group: 'Wings', default: 30, formula: true },
    { key: 'endCrown', label: 'Crown at the free end', kind: 'length', group: 'Wings', default: '{= embankment + 0.5}', formula: true },
    { key: 'mirror', label: 'Right wing mirrors the left', kind: 'bool', group: 'Wings', default: true, formula: false },
    { key: 'wingLengthRight', label: 'Right wing length', kind: 'length', group: 'Wings', default: '{= wingLength}', formula: true, showIf: { mirror: ['false'] } },
    { key: 'warp', label: 'Warped wings', kind: 'bool', group: 'Warp', default: false, formula: false },
    { key: 'twist', label: 'Twist along', kind: 'choice', group: 'Warp', default: 'slope', formula: false, showIf: { warp: ['true'] } },
  ],
  presets: [
    { key: 'flared', set: { kind: 'portal', wingAngle: 30, warp: false } },
    { key: 'u', set: { kind: 'portal', wingAngle: 0, warp: false } },
    { key: 'warped', set: { kind: 'portal', wingAngle: 30, warp: true } },
    { key: 'abutment', set: { kind: 'abutment', wingAngle: 0, warp: false, crown: 6 } },
  ],
};

describe('the portal configurator', () => {
  it('fills a spec from the defaults, under its preset, under what it gives', () => {
    const full = fullSpec(catalog, { preset: 'abutment', wingAngle: 15 });
    expect(full.kind).toBe('abutment');
    expect(full.crown).toBe(6);
    expect(full.wingAngle).toBe(15);
    expect(full.endCrown).toBe('{= embankment + 0.5}');
    expect(fullSpec(catalog, { preset: 'nonsense' }).preset).toBe('flared');
  });

  it('takes another preset over, its own numbers kept', () => {
    const spec = withPreset(catalog, { preset: 'flared', wingAngle: 12, endCrown: 2 }, 'warped');
    expect(spec).toEqual({ preset: 'warped', endCrown: 2 });
    expect(fullSpec(catalog, spec).warp).toBe(true);
  });

  it('shows a parameter only where it applies', () => {
    const p = catalog.params.find((x) => x.key === 'wingLengthRight')!;
    expect(applies(p, fullSpec(catalog, {}))).toBe(false);
    expect(applies(p, fullSpec(catalog, { mirror: false }))).toBe(true);
    const groups = groupsOf(catalog, fullSpec(catalog, { preset: 'warped' }));
    expect(groups.map((g) => g.group)).toEqual(['Layout', 'Wings', 'Warp']);
    expect(groups[2].params.map((x) => x.key)).toEqual(['warp', 'twist']);
  });

  it('reads a field as a number, a formula or the default', () => {
    expect(readField('7,25')).toBe(7.25);
    expect(readField(' = embankment + 0.5 ')).toBe('{= embankment + 0.5}');
    expect(readField('{= (crown - embankment) * slope}')).toBe('{= (crown - embankment) * slope}');
    expect(readField('')).toBeNull();
    expect(readField('seven')).toBeUndefined();
    expect(isFormula('=1+2')).toBe(true);
    expect(isFormula('{= a}')).toBe(true);
    expect(isFormula('12')).toBe(false);
    expect(fieldText(9.7500001)).toBe('9.75');
    expect(fieldText('{= a}')).toBe('{= a}');
  });

  it('sends only what was set', () => {
    expect(specOut({ preset: 'u', crown: null, endCrown: '', faceLength: 12 })).toEqual({ preset: 'u', faceLength: 12 });
  });
});
