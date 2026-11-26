import { describe, expect, it } from 'vitest';
import { DEFAULT_FLOOR_SLOPE_MARKS, clearHeightLessThreshold, clearHeightLessThresholdArg, drawingLangArg, floorSlopeMarkChoices, floorSlopeMarksArg } from './drawingOptions';

describe('drawing options in the drafting standards', () => {
  it('offers the usual scales for floors\' slope marks, 1:50 the default kept as none', () => {
    expect(DEFAULT_FLOOR_SLOPE_MARKS).toBe(50);
    expect(floorSlopeMarkChoices().map((c) => c.value)).toEqual([20, 50, 100, 200, 100000]);
    expect(floorSlopeMarkChoices()[1].label).toBe('at 1:50 and larger');
    expect(floorSlopeMarksArg(50)).toBeNull();
    expect(floorSlopeMarksArg(100)).toBe(100);
  });
  it('shows a project\'s own threshold among them', () => {
    expect(floorSlopeMarkChoices(75).map((c) => c.value)).toEqual([20, 50, 75, 100, 200, 100000]);
    expect(floorSlopeMarkChoices(100).length).toBe(5);
  });
  it('takes a door\'s threshold off its clear height unless the project says not (forum topic 204), the default kept as none', () => {
    expect(clearHeightLessThreshold(undefined)).toBe(true);
    expect(clearHeightLessThreshold(null)).toBe(true);
    expect(clearHeightLessThreshold(false)).toBe(false);
    expect(clearHeightLessThresholdArg(true)).toBeNull();
    expect(clearHeightLessThresholdArg(false)).toBe(false);
  });
  it('keeps English drawings as none', () => {
    expect(drawingLangArg('en')).toBeNull();
    expect(drawingLangArg('ro')).toBe('ro');
  });
});

describe('the follow-ups\' words', async () => {
  const { loadAllLanguages, translate, LANGUAGES } = await import('../../i18n');
  await loadAllLanguages();
  it('call a channel a drainage channel, never the steel U', () => {
    expect(translate('Drainage channel', 'ro')).toBe('Rigolă de drenaj');
    expect(translate('Drainage channel schedule', 'ro')).toBe('Extrasul rigolelor de drenaj');
    expect(translate('Channel', 'ro')).toBe('U');
  });
  it('give a channel\'s grade its fall\'s meaning, a timber\'s grade its class', () => {
    expect(translate('Grade', 'ro', 'slope')).toBe('Panta');
    expect(translate('Grade', 'ro')).toBe('Clasa');
    for (const { id } of LANGUAGES) if (id !== 'en') expect(translate('Grade', id, 'slope'), id).not.toBeNull();
  });
  it('translate the clear height option and the threshold in plan in every language (forum topic 204)', () => {
    for (const { id } of LANGUAGES) if (id !== 'en') for (const w of ['Door clear height less the threshold', 'Threshold drawn in plan']) expect(translate(w, id), `${id} ${w}`).not.toBeNull();
  });
  it('translate the two drafting options in every language', () => {
    for (const { id } of LANGUAGES) if (id !== 'en') for (const w of ['Floor slope marks', 'Drawing language', 'at 1:50 and larger', 'at every scale']) expect(translate(w, id), `${id} ${w}`).not.toBeNull();
  });
});
