import { describe, expect, it } from 'vitest';
import { cleanRow, commonRow, elementGraphicsArgs, frontViewIdNow, hasOwnGraphics } from './elementGraphics';

describe('an element\'s graphics in a view', () => {
  it('sends only what is set: empty fields, halftone off and empty lines left out', () => {
    expect(cleanRow({ cutHatch: '', cutFill: 'none', cutHatchScale: 0, cutHatchAngle: 0, hatchHalftone: false, cutLine: { color: undefined }, projectionLine: { weight: 0.35 } }))
      .toEqual({ cutFill: 'none', cutHatchAngle: 0, projectionLine: { weight: 0.35 } });
    expect(cleanRow({ halftone: false })).toBeNull();
  });

  it('starts a selection from what its elements share', () => {
    expect(commonRow([{ cutHatch: 'none', cutFill: '#000000' }, { cutHatch: 'none', cutFill: '#ffffff' }])).toEqual({ cutHatch: 'none' });
    expect(commonRow([{ cutHatch: 'none' }, undefined])).toEqual({});
    expect(commonRow([])).toEqual({});
  });

  it('goes to the view in front or every view, and null clears it', () => {
    expect(elementGraphicsArgs('v1', ['a'], { surfaceHatch: 'none' }, 'view')).toEqual({ id: 'v1', ids: ['a'], value: { surfaceHatch: 'none' } });
    expect(elementGraphicsArgs('v1', ['a', 'b'], null, 'all')).toEqual({ scope: 'all', ids: ['a', 'b'], value: null });
  });

  it('knows the view in front and whether an element has its own row there', () => {
    expect(frontViewIdNow({ view: 'plan', planViewId: 'p1' })).toBe('p1');
    expect(frontViewIdNow({ view: '3d' })).toBe('3d');
    expect(frontViewIdNow({ view: 'sheet' })).toBeNull();
    expect(hasOwnGraphics({ props: { elementGraphics: { a: { halftone: true } } } }, 'a')).toBe(true);
    expect(hasOwnGraphics({ props: {} }, 'a')).toBe(false);
  });
});
