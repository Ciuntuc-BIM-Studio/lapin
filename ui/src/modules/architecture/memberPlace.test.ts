import { describe, expect, it } from 'vitest';
import { boxRing, isDefaultPlace, placeJson, placeRings, type Ring } from './memberPlace';

/** The engine's test profile: 50 wide, 100 deep, a notch at its +y, −x corner. */
const alu: Ring[] = [[[-0.025, -0.05], [0.025, -0.05], [0.025, 0.05], [0.005, 0.05], [0.005, 0.03], [-0.025, 0.03]]];
const box = (r: Ring[]) => {
  const p = r.flat();
  return { x0: Math.min(...p.map((q) => q[0])), x1: Math.max(...p.map((q) => q[0])), y0: Math.min(...p.map((q) => q[1])), y1: Math.max(...p.map((q) => q[1])) };
};
const area = (r: Ring) => r.reduce((s, [x, y], i) => s + (x * r[(i + 1) % r.length][1] - r[(i + 1) % r.length][0] * y), 0) / 2;

describe('a profile placed on its member (report #128)', () => {
  it('leaves a default place as it is', () => {
    expect(isDefaultPlace({})).toBe(true);
    expect(placeRings(alu, null)).toBe(alu);
    expect(placeJson({ rotate: 0, across: 'center' })).toBeNull();
  });

  it('turns a quarter, exchanging its width and depth', () => {
    const b = box(placeRings(alu, { rotate: 90 }));
    expect(b.x1 - b.x0).toBeCloseTo(0.1, 9);
    expect(b.y1 - b.y0).toBeCloseTo(0.05, 9);
  });

  it('mirrors across x, keeping its outside anticlockwise', () => {
    const [r] = placeRings(alu, { mirror: true });
    expect(area(r)).toBeGreaterThan(0);
    expect(r.some(([x, y]) => Math.abs(x - 0.025) < 1e-9 && Math.abs(y - 0.03) < 1e-9)).toBe(true);
  });

  it('aligns its front on the member\'s plane and its start on its line, then moves it', () => {
    const b = box(placeRings(alu, { across: 'front', along: 'start' }));
    expect(b.y1).toBeCloseTo(0, 9);
    expect(b.x0).toBeCloseTo(0, 9);
    const m = box(placeRings(alu, { dx: 0.02, dy: -0.01 }));
    expect((m.x0 + m.x1) / 2).toBeCloseTo(0.02, 9);
    expect((m.y0 + m.y1) / 2).toBeCloseTo(-0.01, 9);
  });

  it('writes only what is not default, and a plain member is a centred rectangle', () => {
    expect(placeJson({ rotate: 450, mirror: true, across: 'back', dx: 0.01 })).toEqual({ rotate: 90, mirror: true, across: 'back', dx: 0.01 });
    expect(box([boxRing(0.05, 0.15)])).toEqual({ x0: -0.025, x1: 0.025, y0: -0.075, y1: 0.075 });
  });
});
