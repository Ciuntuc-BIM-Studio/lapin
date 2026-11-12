import { describe, expect, it } from 'vitest';
import { flagsJson, flagsOf, newWallFlags } from './layerFlags';

describe('a layer\'s category and extensions (topic #30)', () => {
  it('reads what the layer stores, nothing as its material\'s and off', () => {
    expect(flagsOf({ material: 'm', thickness: 0.02 })).toEqual({ category: null, down: false, up: false });
    expect(flagsOf({ material: 'm', thickness: 0.25, category: 'Finish', extendDown: true })).toEqual({ category: 'Finish', down: true, up: false });
  });

  it('sends only what is set, the extensions only for a wall', () => {
    expect(flagsJson({ category: null, down: false, up: false }, true)).toEqual({});
    expect(flagsJson({ category: 'Finish', down: true, up: true }, true)).toEqual({ category: 'Finish', extendDown: true, extendUp: true });
    expect(flagsJson({ category: 'Finish', down: true, up: true }, false)).toEqual({ category: 'Finish' });
  });

  it('gives a new wall type\'s core the way down onto the floor\'s structure', () => {
    expect(newWallFlags(true)).toEqual({ category: null, down: true, up: false });
    expect(newWallFlags(false).down).toBe(false);
  });
});
