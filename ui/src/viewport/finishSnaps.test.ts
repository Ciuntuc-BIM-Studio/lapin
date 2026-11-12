import { describe, expect, it } from 'vitest';
import { drawnInPlan } from './finishSnaps';

const cut = (sub?: string) => ({ kind: 'cut', closed: true, points: [0, 0, 1, 0, 1, 1], sub });

describe('snapping to what a plan draws (topic #30)', () => {
  it('leaves out the walls\' finish pieces where the plan leaves out their finishes', () => {
    expect(drawnInPlan('IfcWall', cut('finishes'), ['walls'])).toBe(false);
    expect(drawnInPlan('IfcWall', cut('core'), ['walls'])).toBe(true);
    expect(drawnInPlan('IfcWall', cut(), ['walls'])).toBe(true);
  });

  it('keeps them where the plan draws them, and another category\'s', () => {
    expect(drawnInPlan('IfcWall', cut('finishes'), [])).toBe(true);
    expect(drawnInPlan('IfcWall', cut('finishes'), undefined)).toBe(true);
    expect(drawnInPlan('IfcWall', cut('finishes'), ['slabs'])).toBe(true);
    expect(drawnInPlan('IfcColumn', cut('finishes'), ['walls', 'slabs'])).toBe(true);
  });
});
