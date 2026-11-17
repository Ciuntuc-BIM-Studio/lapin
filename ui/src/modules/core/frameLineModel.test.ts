import { describe, expect, it } from 'vitest';
import { FRAME_WEIGHTS, inheritedFrame, lapinFrame, ownFrameLine, ownPatch, readFrameLine, standardPatch, weightChoices } from './frameLineModel';

describe('callout boundaries and inset frames (issue #73)', () => {
  it("falls back on Lapin's own: a callout 0.35 black (grey with no view), an inset 0.18 grey", () => {
    expect(lapinFrame('callout')).toEqual({ weight: 0.35, color: '#000000', lineType: 'continuous' });
    expect(lapinFrame('callout', false).color).toBe('#8f98a3');
    expect(lapinFrame('inset')).toEqual({ weight: 0.18, color: '#555555', lineType: 'continuous' });
  });

  it("takes the project's fields where it has them, Lapin's where it does not", () => {
    expect(inheritedFrame('callout', { lineType: 'dashdot' })).toEqual({ weight: 0.35, color: '#000000', lineType: 'dashdot' });
    expect(inheritedFrame('inset', { weight: 0.5, color: '#ff0000' })).toEqual({ weight: 0.5, color: '#ff0000', lineType: 'continuous' });
  });

  it('reads the standards and an element tolerantly', () => {
    expect(readFrameLine({ weight: 0.5, color: '#ff0000', lineType: 'Fence' })).toEqual({ weight: 0.5, color: '#ff0000', lineType: 'Fence' });
    expect(readFrameLine(undefined)).toEqual({ weight: undefined, color: undefined, lineType: undefined });
    expect(readFrameLine({ weight: 'x', color: '', lineType: 3 })).toEqual({ weight: undefined, color: undefined, lineType: undefined });
    expect(ownFrameLine({ lineWeight: 0.7, lineColor: '#00aa00', lineType: 'dashed', shape: 'rect' })).toEqual({ weight: 0.7, color: '#00aa00', lineType: 'dashed' });
  });

  it('keeps a weight that is not offered in the list', () => {
    expect(weightChoices()).toBe(FRAME_WEIGHTS);
    expect(weightChoices(0.35)).toBe(FRAME_WEIGHTS);
    expect(weightChoices(0.6)).toEqual([0.13, 0.18, 0.25, 0.35, 0.5, 0.6, 0.7, 1.0]);
  });

  it('sets a field or clears it (null: the next default)', () => {
    expect(ownPatch('weight', 0.5)).toEqual({ lineWeight: 0.5 });
    expect(ownPatch('color', '#ff0000')).toEqual({ lineColor: '#ff0000' });
    expect(ownPatch('lineType', '')).toEqual({ lineType: null });
    expect(standardPatch('inset', 'lineType', 'dashdot')).toEqual({ kind: 'inset', lineType: 'dashdot' });
    expect(standardPatch('callout', 'weight', null)).toEqual({ kind: 'callout', weight: null });
  });
});
