import { describe, expect, it } from 'vitest';
import { DARK_INK, screenInk } from './drawing';

describe('screenInk (report #36: a plan in the dark theme)', () => {
  it('draws black and near-black pens in the theme ink on a dark plan', () => {
    expect(screenInk('#000000', true)).toBe(DARK_INK);
    expect(screenInk('#000', true)).toBe(DARK_INK);
    expect(screenInk('#1a1a1a', true)).toBe(DARK_INK);
    expect(screenInk('black', true)).toBe(DARK_INK);
  });

  it('keeps greys and colours, and nothing changes on paper', () => {
    expect(screenInk('#888888', true)).toBe('#888888');
    expect(screenInk('#d33f2f', true)).toBe('#d33f2f');
    expect(screenInk('#000000', false)).toBe('#000000');
    expect(screenInk(null, true)).toBeNull();
  });
});
