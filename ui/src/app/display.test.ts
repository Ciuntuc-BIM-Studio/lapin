import { describe, expect, it } from 'vitest';
import { readDisplay, screenWidth } from './display';

describe('the display for the eyes', () => {
  it('shows the grid and the pens\' widths until the user says otherwise; anything unreadable left at that', () => {
    expect(readDisplay(null)).toEqual({ grid: true, thinLines: false, drafts3d: true, dxf3d: true, zoomToCursor: true, orbitSelection: true, drillSelect: false, sketchupKeys: false, dragMove3d: true });
    expect(readDisplay({ grid: false, thinLines: true, drafts3d: false })).toEqual({ grid: false, thinLines: true, drafts3d: false, dxf3d: true, zoomToCursor: true, orbitSelection: true, drillSelect: false, sketchupKeys: false, dragMove3d: true });
    expect(readDisplay({ grid: 'no', thinLines: 1, drafts3d: 0 })).toEqual({ grid: true, thinLines: false, drafts3d: true, dxf3d: true, zoomToCursor: true, orbitSelection: true, drillSelect: false, sketchupKeys: false, dragMove3d: true });
    expect(readDisplay('garbage')).toEqual({ grid: true, thinLines: false, drafts3d: true, dxf3d: true, zoomToCursor: true, orbitSelection: true, drillSelect: false, sketchupKeys: false, dragMove3d: true });
    // The 3D navigation (report #89): on unless turned off.
    expect(readDisplay({ zoomToCursor: false, orbitSelection: false })).toMatchObject({ zoomToCursor: false, orbitSelection: false });
    expect(readDisplay({ zoomToCursor: 'no', orbitSelection: 0 })).toMatchObject({ zoomToCursor: true, orbitSelection: true, drillSelect: false, sketchupKeys: false, dragMove3d: true });
    // Drill-down picking: off unless turned on.
    expect(readDisplay({ drillSelect: true })).toMatchObject({ drillSelect: true });
    expect(readDisplay({ drillSelect: 'yes' })).toMatchObject({ drillSelect: false, sketchupKeys: false, dragMove3d: true });
    // An imported DXF in 3D (decision 382): on unless turned off.
    expect(readDisplay({ dxf3d: false })).toMatchObject({ dxf3d: false, drafts3d: true });
    expect(readDisplay({ dxf3d: 0 })).toMatchObject({ dxf3d: true });
    // Kept from before drafts were drawn in 3D: shown.
    expect(readDisplay({ grid: false, thinLines: false })).toEqual({ grid: false, thinLines: false, drafts3d: true, dxf3d: true, zoomToCursor: true, orbitSelection: true, drillSelect: false, sketchupKeys: false, dragMove3d: true });
  });

  it('draws a pen at its paper width within what a screen shows well, every pen one pixel with thin lines', () => {
    expect(screenWidth(0.5, 2, false)).toBe(1);
    expect(screenWidth(0.1, 2, false)).toBe(0.5);
    expect(screenWidth(1, 10, false)).toBe(4);
    expect(screenWidth(1, 10, true)).toBe(1);
  });
});
