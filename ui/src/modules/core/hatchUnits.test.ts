import { describe, expect, it } from 'vitest';
import { LANGUAGES, loadAllLanguages, translate, type Lang } from '../../i18n';
import { ALIGN_LABEL, HATCH_ALIGNS, effectiveUnits, groupByUnits, scaleLabel, unitsOf } from './hatchUnits';
import { parseMinSpacing } from './viewStyles';

await loadAllLanguages();

describe('hatch alignment (decision 725)', () => {
  it('offers the element first, then the view and each layer, as the engine takes them', () => {
    expect(HATCH_ALIGNS).toEqual(['element', 'view', 'layer']);
    expect(HATCH_ALIGNS.map((a) => ALIGN_LABEL[a])).toEqual(['With the element', 'Fixed to the view', 'Each layer']);
  });
  it('words them, and the Align field\'s help, in every language', () => {
    const help = "How the hatch turns: with the element (along a wall's axis in a plan, turning with it), fixed to the view, or from each band it fills (along the band's longest edge)";
    for (const lang of LANGUAGES.map((l) => l.id).filter((l) => l !== ('en' as Lang)))
      for (const p of [...Object.values(ALIGN_LABEL), help]) expect(translate(p, lang), `${lang}: ${p}`).not.toBeNull();
  });
});

describe('hatch units', () => {
  it('groups drafting before real size, leaving empty groups out', () => {
    const g = groupByUnits([{ name: 'A', units: 'model' as const }, { name: 'B', units: 'paper' as const }, { name: 'C' }]);
    expect(g.map((x) => [x.units, x.items.map((i) => i.name)])).toEqual([['paper', ['B', 'C']], ['model', ['A']]]);
    expect(groupByUnits([{ name: 'A', units: 'model' as const }]).map((x) => x.units)).toEqual(['model']);
  });
  it('takes the material\'s choice over the pattern\'s', () => {
    expect(effectiveUnits(undefined, { name: 'X', units: 'model' })).toBe('model');
    expect(effectiveUnits('paper', { name: 'X', units: 'model' })).toBe('paper');
    expect(unitsOf(undefined)).toBe('paper');
  });
  it('words the scale by units', () => {
    expect(scaleLabel('model')).toContain('real size');
    expect(scaleLabel('paper')).toContain('×');
  });
  it('parses the minimum spacing', () => {
    expect(parseMinSpacing('0,7')).toBe(0.7);
    expect(parseMinSpacing('0')).toBe(0);
    expect(parseMinSpacing('-1')).toBeNull();
    expect(parseMinSpacing('')).toBeNull();
  });
});
