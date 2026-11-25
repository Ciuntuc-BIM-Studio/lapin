import { describe, expect, it } from 'vitest';
import { splitHintTokens } from './ViewportHud';
import { fuzzyWord, matchScore } from './CommandPalette';

describe('Phase 1 game-like UX helpers', () => {
  it('splits keycap words out of tool hints while keeping exact textContent', () => {
    const src = 'Wall: click its start, then its end · Shift squares it · Space: core · Enter ends the chain, Esc cancels';
    const tokens = splitHintTokens(src);
    expect(tokens.map((t) => t.text).join('')).toBe(src);
    expect(tokens.filter((t) => t.key).map((t) => t.key)).toEqual(['shift', 'space', 'enter', 'esc']);
  });

  it('matches command palette queries by prefix, substring and fuzzy subsequence', () => {
    expect(fuzzyWord('section box', 'secb')).toBe(true);
    expect(fuzzyWord('wall', 'wll')).toBe(true);
    expect(fuzzyWord('wall', 'xyz')).toBe(false);
    expect(matchScore('wall architecture', ['wall'])).toBe(0);
    expect(matchScore('curtain wall', ['wall'])).toBe(1);
    expect(matchScore('section box', ['secb'])).toBe(2);
    expect(matchScore('section box', ['nope'])).toBe(-1);
  });
});
