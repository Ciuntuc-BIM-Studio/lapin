import { describe, expect, it } from 'vitest';
import { readDisplay, screenWidth } from './display';

describe('the display for the eyes', () => {
  it('shows the grid and the pens\' widths until the user says otherwise; anything unreadable left at that', () => {
    expect(readDisplay(null)).toEqual({ grid: true, thinLines: false });
    expect(readDisplay({ grid: false, thinLines: true })).toEqual({ grid: false, thinLines: true });
    expect(readDisplay({ grid: 'no', thinLines: 1 })).toEqual({ grid: true, thinLines: false });
    expect(readDisplay('garbage')).toEqual({ grid: true, thinLines: false });
  });

  it('draws a pen at its paper width within what a screen shows well, every pen one pixel with thin lines', () => {
    expect(screenWidth(0.5, 2, false)).toBe(1);
    expect(screenWidth(0.1, 2, false)).toBe(0.5);
    expect(screenWidth(1, 10, false)).toBe(4);
    expect(screenWidth(1, 10, true)).toBe(1);
  });
});
