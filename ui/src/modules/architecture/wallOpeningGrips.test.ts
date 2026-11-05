import { describe, expect, it } from 'vitest';
import { jambTo, wallOpeningGrips } from './wallOpeningGrips';

// A straight wall 6 m along +X; an opening 1.0 m wide, its middle 2 m along.
const wall = { a: { x: 0, y: 0 }, b: { x: 6, y: 0 }, rise: 0 };

describe('a wall opening edited by its grips, as a window is', () => {
  it('slides along its wall by its middle, kept on the wall', () => {
    const g = wallOpeningGrips('o', wall, 2, 1).find((x) => x.key === 'along')!;
    expect(g.at).toEqual({ x: 2, y: 0 });
    expect(g.step!({ x: 3.5, y: 0.7 })).toEqual({ command: 'architecture.moveOpening', args: { id: 'o', along: 3.5 } });
  });

  it('widens by a jamb, the other jamb staying put', () => {
    const grips = wallOpeningGrips('o', wall, 2, 1);
    expect(grips.find((x) => x.key === 'jamb+')!.at).toEqual({ x: 2.5, y: 0 });
    expect(grips.find((x) => x.key === 'jamb-')!.at).toEqual({ x: 1.5, y: 0 });
    // The far jamb from 2.5 to 3.0: 1.5 wide, its middle at 2.25, the near jamb still at 1.5.
    const step = grips.find((x) => x.key === 'jamb+')!.step!({ x: 3, y: -0.4 })!;
    expect(step.command).toBe('architecture.updateWallOpening');
    expect(step.args.width).toBeCloseTo(1.5, 9);
    expect(step.args.along).toBeCloseTo(2.25, 9);
    // The near jamb from 1.5 to 1.8: 0.7 wide, the far jamb at 2.5.
    const r = jambTo(wall, 2, 1, -1, { x: 1.8, y: 0 })!;
    expect(r.width).toBeCloseTo(0.7, 9);
    expect(r.along + r.width / 2).toBeCloseTo(2.5, 9);
  });

  it('refuses a jamb dragged past the other one, and keeps the opening on the wall', () => {
    expect(jambTo(wall, 2, 1, 1, { x: 1, y: 0 })).toBeNull();
    const r = jambTo(wall, 2, 1, 1, { x: 9, y: 0 })!;
    expect(r.along + r.width / 2).toBeCloseTo(6, 9);
  });
});
