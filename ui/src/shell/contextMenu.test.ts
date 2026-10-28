import { describe, expect, it } from 'vitest';
import { placeMenu } from './ContextMenu';

describe('the right-click menu', () => {
  const win = { w: 1000, h: 800 };
  it('opens at the cursor when it fits', () => {
    expect(placeMenu({ x: 100, y: 100 }, { w: 200, h: 120 }, win)).toEqual({ x: 100, y: 100 });
  });
  it('flips to the cursor\'s other side near the window\'s right and bottom edges', () => {
    expect(placeMenu({ x: 950, y: 750 }, { w: 200, h: 120 }, win)).toEqual({ x: 750, y: 630 });
  });
  it('is pushed in when it fits neither way', () => {
    expect(placeMenu({ x: 100, y: 50 }, { w: 200, h: 900 }, win)).toEqual({ x: 100, y: 4 });
  });
});
