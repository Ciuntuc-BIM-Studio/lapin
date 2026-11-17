import { describe, expect, it } from 'vitest';
import { joinerySize, sizeArgs } from './joinerySize';
import { joineryJambGrips } from './wallOpeningGrips';

const spec = { width: 1.2, height: 1.2 };

describe('a door or window of its own size (forum topic 140)', () => {
  it('is its type’s size unless it has its own, dimension by dimension', () => {
    expect(joinerySize({}, spec)).toEqual({ width: 1.2, height: 1.2, own: false });
    expect(joinerySize({ width: 0.9 }, spec)).toEqual({ width: 0.9, height: 1.2, own: true });
    expect(joinerySize({ width: 1.8, height: 1.5 }, spec)).toEqual({ width: 1.8, height: 1.5, own: true });
    expect(joinerySize({ width: 'x' }, undefined)).toEqual({ width: 0, height: 0, own: false });
  });

  it('sets a size, or goes back to its type’s', () => {
    expect(sizeArgs(['w1', 'w2'], { width: 0.9 })).toEqual({ ids: ['w1', 'w2'], width: 0.9 });
    expect(sizeArgs(['w1'], 'reset')).toEqual({ ids: ['w1'], reset: true });
  });

  it('is widened by a jamb of its opening, the other jamb staying put', () => {
    const wall = { a: { x: 0, y: 0 }, b: { x: 6, y: 0 }, rise: 0 };
    const grips = joineryJambGrips('opening', wall, 2, 1.2);
    expect(grips.map((g) => g.key)).toEqual(['jamb-', 'jamb+']);
    const step = grips[1].step!({ x: 3.2, y: 0 })!;
    expect(step.command).toBe('architecture.updateWallOpening');
    expect(step.args.id).toBe('opening');
    expect(step.args.width).toBeCloseTo(1.8, 9);
    expect(step.args.along).toBeCloseTo(2.3, 9);
  });
});
