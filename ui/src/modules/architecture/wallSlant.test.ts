import { describe, expect, it } from 'vitest';
import { batter } from './WallLeanFields';
import { wallSlantArgs } from './WallLeanFields';

describe('the wall tool\'s lean and thickness at top', () => {
  const none = { lean: null, leanFrom: null, openingCut: null, thicknessTop: null };
  it('tells addWall nothing when empty (the type decides)', () => {
    expect(wallSlantArgs(none)).toEqual({});
  });
  it('passes the lean with its level and openings, and the thickness at top', () => {
    expect(wallSlantArgs({ lean: 10, leanFrom: 'top', openingCut: 'vertical', thicknessTop: 0.25 })).toEqual({ lean: 10, leanFrom: 'top', openingCut: 'vertical', thicknessTop: 0.25 });
  });
  it('leaves leanFrom and openingCut out when there is no lean', () => {
    expect(wallSlantArgs({ ...none, leanFrom: 'top', openingCut: 'wall', thicknessTop: 0.2 })).toEqual({ thicknessTop: 0.2 });
  });
  it('shows the lean as n:1', () => {
    expect(batter(Math.atan(1 / 5) * 180 / Math.PI)).toBe('5.0:1');
    expect(batter(null)).toBe('');
  });
});
