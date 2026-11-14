import { describe, expect, it } from 'vitest';
import { DARK_INK, DARK_PAPER, drawsOnDark, screenFill, screenInk, splitAlpha, withOpacity } from './drawing';

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

  it('fills of the paper\'s white become the dark paper on a dark plan, and stay white on paper', () => {
    expect(screenFill('#ffffff', true)).toBe(DARK_PAPER);
    expect(screenFill('#fff', true)).toBe(DARK_PAPER);
    expect(screenFill('white', true)).toBe(DARK_PAPER);
    expect(screenFill('#ffffff', false)).toBe('#ffffff');
    expect(screenFill('#d9c7a3', true)).toBe('#d9c7a3');
    expect(screenFill('#000000', true)).toBe(DARK_INK);
  });
});

describe('drawsOnDark (report #90: sections and elevations follow the dark theme as plans do)', () => {
  it('is the graphite theme only', () => {
    expect(drawsOnDark('graphite')).toBe(true);
    expect(drawsOnDark('paper')).toBe(false);
    expect(drawsOnDark(undefined)).toBe(false);
  });
});

describe('a fill with an opacity (forum #79: room colours)', () => {
  it('reads #rrggbbaa as its colour and opacity, any other colour as opaque', () => {
    expect(splitAlpha('#f4c7a180')).toEqual({ color: '#f4c7a1', opacity: 0.502 });
    expect(splitAlpha('#f4c7a1')).toEqual({ color: '#f4c7a1', opacity: 1 });
    expect(splitAlpha(null)).toEqual({ color: null, opacity: 1 });
  });

  it('writes an opacity in per cent as the engine does, the colour alone at 100 % or none', () => {
    expect(withOpacity('#f4c7a1', 50)).toBe('#f4c7a180');
    expect(withOpacity('#a7c7e7', 25)).toBe('#a7c7e740');
    expect(withOpacity('#a7c7e7', 100)).toBe('#a7c7e7');
    expect(withOpacity('#a7c7e740', null)).toBe('#a7c7e7');
  });
});
