import { describe, expect, it } from 'vitest';
import { withThresholdOffset, withThresholdWidth } from './joineryThreshold';

describe('a door threshold\'s width across the wall (forum topic 204)', () => {
  const th = { on: true, height: 0.02, material: 'Granite' };

  it('stores a width and keeps the rest', () => {
    expect(withThresholdWidth(th, 0.12)).toEqual({ on: true, height: 0.02, material: 'Granite', width: 0.12 });
  });

  it('clears to the whole wall on empty or 0, its offset with it', () => {
    const set = withThresholdOffset(withThresholdWidth(th, 0.12), 0.05);
    expect(set.offset).toBe(0.05);
    expect(withThresholdWidth(set, null)).toEqual(th);
    expect(withThresholdWidth(set, 0)).toEqual(th);
    expect(withThresholdWidth(set, 0.2)).toEqual({ ...th, width: 0.2, offset: 0.05 });
  });

  it('stores no offset when centred', () => {
    expect(withThresholdOffset({ ...th, width: 0.12, offset: 0.05 }, 0)).toEqual({ ...th, width: 0.12 });
    expect(withThresholdOffset({ ...th, width: 0.12 }, null)).toEqual({ ...th, width: 0.12 });
  });
});

describe('the threshold width\'s and the frame tokens\' words (forum topic 204, R8)', async () => {
  const { loadAllLanguages, translate, LANGUAGES } = await import('../../i18n');
  await loadAllLanguages();
  it('are in every language', () => {
    const words = ['Threshold width', 'Threshold offset', 'whole wall', 'Frame face width (mm)', 'Frame depth, across the wall (mm)',
      "The threshold's extent across the wall, centred on the frame. Empty: across the whole wall",
      "Moves the threshold across the wall from the frame's centre: + towards the exterior, − the interior; it stays within the wall's faces"];
    for (const { id } of LANGUAGES) if (id !== 'en') for (const w of words) expect(translate(w, id), `${id} ${w}`).not.toBeNull();
  });
});
