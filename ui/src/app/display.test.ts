import { describe, expect, it } from 'vitest';
import { readDisplay, screenWidth } from './display';

describe('the display for the eyes', () => {
  it('shows the grid and the pens\' widths until the user says otherwise; anything unreadable left at that', () => {
    expect(readDisplay(null)).toEqual({ grid: true, thinLines: false, drafts3d: true });
    expect(readDisplay({ grid: false, thinLines: true, drafts3d: false })).toEqual({ grid: false, thinLines: true, drafts3d: false });
    expect(readDisplay({ grid: 'no', thinLines: 1, drafts3d: 0 })).toEqual({ grid: true, thinLines: false, drafts3d: true });
    expect(readDisplay('garbage')).toEqual({ grid: true, thinLines: false, drafts3d: true });
    // Kept from before drafts were drawn in 3D: shown.
    expect(readDisplay({ grid: false, thinLines: false })).toEqual({ grid: false, thinLines: false, drafts3d: true });
  });

  it('draws a pen at its paper width within what a screen shows well, every pen one pixel with thin lines', () => {
    expect(screenWidth(0.5, 2, false)).toBe(1);
    expect(screenWidth(0.1, 2, false)).toBe(0.5);
    expect(screenWidth(1, 10, false)).toBe(4);
    expect(screenWidth(1, 10, true)).toBe(1);
  });
});
