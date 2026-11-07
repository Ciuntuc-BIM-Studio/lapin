import { describe, expect, it } from 'vitest';
import { categoryName, clean, moveRow } from './VisibilityDialog';

describe('Visibility / Graphics rows', () => {
  it('drop what says nothing, keeping a shown that is not the default', () => {
    expect(clean({ hidden: false, lineColor: '', fillColor: '#ff0000' }, false)).toEqual({ fillColor: '#ff0000' });
    expect(clean({ hidden: false }, true)).toEqual({ hidden: false });
    expect(clean({ hidden: true, sub: {} }, false)).toEqual({ hidden: true });
    expect(clean({ sub: { finishes: { hidden: true } } }, false)).toEqual({ sub: { finishes: { hidden: true } } });
  });

  it('name the categories and subcategories the engine sends', () => {
    expect(categoryName('steelConnections')).toBe('Steel connections');
    expect(categoryName('otherLayers')).toBe('Other layers');
    expect(categoryName('mep')).toBe('MEP');
    expect(categoryName('somethingNew')).toBe('SomethingNew');
  });

  it('move a view\'s filters up and down, never past the ends', () => {
    expect(moveRow(['a', 'b', 'c'], 2, -1)).toEqual(['a', 'c', 'b']);
    expect(moveRow(['a', 'b', 'c'], 0, -1)).toEqual(['a', 'b', 'c']);
    expect(moveRow(['a', 'b', 'c'], 2, 1)).toEqual(['a', 'b', 'c']);
    expect(categoryName('roomTags')).toBe('Room tags');
  });
});
