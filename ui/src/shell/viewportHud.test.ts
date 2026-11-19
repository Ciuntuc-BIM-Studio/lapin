import { describe, expect, it } from 'vitest';
import { hudHidesOver } from './ViewportHud';

describe('hudHidesOver (reports #65, #170)', () => {
  it('keeps the mode card off views with a toolbar across the top', () => {
    expect(hudHidesOver('SHEET')).toBe(true);
    expect(hudHidesOver('SCHEDULE')).toBe(true);
    expect(hudHidesOver('PLAN')).toBe(false);
  });
});
