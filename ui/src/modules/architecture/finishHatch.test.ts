import { describe, expect, it } from 'vitest';
import { hatchOf, withHatch } from './finishHatch';

describe("a room finish's plan hatch", () => {
  it('is from the material when the type keeps none', () => {
    expect(hatchOf({}, 'floor')).toEqual({ mode: 'material', pattern: null, scale: 1, angle: 0, color: null });
    expect(hatchOf({ ceilingHatch: { mode: 'none' } }, 'ceiling').mode).toBe('none');
  });

  it('keeps the default as nothing, and an own hatch takes a pattern', () => {
    expect(withHatch({ floorHatch: { mode: 'none' } }, 'floor', { mode: 'material' })).toEqual({ floorHatch: null });
    expect(withHatch({}, 'floor', { mode: 'own' }, 'TILES')).toEqual({ floorHatch: { mode: 'own', pattern: 'TILES', scale: 1, angle: 0, color: null } });
    expect(withHatch({}, 'ceiling', { mode: 'own' }).ceilingHatch?.pattern).toBe('LINES45');
    // A scale kept on the material's own pattern; the own pattern and colour dropped when it goes back to the material.
    expect(withHatch({ floorHatch: { mode: 'own', pattern: 'CROSS', color: '#336699' } }, 'floor', { mode: 'material', scale: 2 })).toEqual({ floorHatch: { mode: 'material', pattern: null, scale: 2, angle: 0, color: null } });
  });
});
