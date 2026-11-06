import { describe, expect, it } from 'vitest';
import { loadAllLanguages, translate } from '../../i18n';
import { draftArgs, emptyDraft, featureFields, fit, isDirty, partLines, specValue, type UnderStairSpec } from './underStair';

// The languages are loaded on demand in the app; the tests compare them all.
await loadAllLanguages();

const spec: UnderStairSpec = {
  name: 'Storage cupboard', front: 'doors', doorWidth: 0.5, shelves: true, shelfSpacing: 0.35, bookcase: true, bookcaseWidth: 0.5, feature: 'none', featureWidth: 1.2,
  benchHeight: 0.45, cushion: true, deskHeight: 0.75, light: false, hookHeight: 1.65, hookSpacing: 0.25, openingWidth: 0.6, openingHeight: 1.1, window: false,
  thickness: 0.018, carcass: 'Oak plywood', fronts: 'Painted MDF', fabric: 'Upholstery fabric',
};

describe('the under-stair configurator', () => {
  it('asks for a style with the changes over it, else the whole spec as it stands with the changes', () => {
    expect(isDirty(emptyDraft())).toBe(false);
    expect(draftArgs({ style: 'office', spec: { deskHeight: 0.72 }, place: {} }, spec)).toEqual({ style: 'office', spec: { deskHeight: 0.72 } });
    expect(draftArgs({ style: 'office', spec: {}, place: { side: 'left' } }, spec)).toEqual({ style: 'office', side: 'left' });
    const whole = draftArgs({ spec: { doorWidth: 0.6 }, place: { start: null } }, spec);
    expect(whole.spec).toEqual({ ...spec, doorWidth: 0.6 });
    expect(whole.start).toBeNull();
    expect(isDirty({ spec: {}, place: { depth: 0.8 } })).toBe(true);
  });

  it('shows a field as changed by hand, else as the preview resolved it', () => {
    const d = { spec: { shelves: false }, place: {} };
    expect(specValue(d, { spec }, 'shelves')).toBe(false);
    expect(specValue(d, { spec }, 'doorWidth')).toBe(0.5);
    expect(specValue(d, null, 'doorWidth')).toBeUndefined();
  });

  it('offers each feature its own fields', () => {
    expect(featureFields('none')).toEqual([]);
    expect(featureFields('mudroom')).toContain('hookHeight');
    expect(featureFields('playhouse')).toContain('window');
    expect(featureFields('desk')).not.toContain('benchHeight');
  });

  it('fits the drawings, y up, and counts the doors as one line', () => {
    const f = fit([{ pts: [0, 0, 4, 0, 4, 2] }], 220, 120, 10)!;
    expect(f.X(0)).toBeCloseTo(10);
    expect(f.X(4)).toBeCloseTo(210);
    expect(f.Y(0)).toBeGreaterThan(f.Y(2));
    expect(fit([], 100, 100, 5)).toBeNull();
    expect(partLines([
      { key: 'nook-door:1', name: 'Door 1', role: 'front', count: 1 }, { key: 'nook-door:2', name: 'Door 2', role: 'front', count: 1 },
      { key: 'nook-shelves', name: 'Shelves', role: 'carcass', count: 6 }, { key: 'nook-door:play', name: 'Play door', role: 'front', count: 1 },
    ])).toEqual([['Doors', 2], ['Shelves', 6], ['Play door', 1]]);
  });

  it('speaks every language: a door by its number, a note with its height', () => {
    expect(translate('Door 3', 'ro')).toBe('Ușa 3');
    expect(translate('The dog\'s opening is lowered to 540 mm to fit under the slope.', 'ro')).toBe('Golul pentru câine e coborât la 540 mm ca să încapă sub pantă.');
    expect(translate('Under-stair space', 'de')).toBe('Raum unter der Treppe');
  });
});
