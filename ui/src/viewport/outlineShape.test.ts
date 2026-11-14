import { describe, expect, it } from 'vitest';
import { arrivingDir, circleOutline, rectangleOutline, tangentRise } from './outlineShape';

describe('outline tools\' shapes (forum topic 97, stage 2)', () => {
  it('draws a rectangle anticlockwise whichever corners are clicked', () => {
    expect(rectangleOutline({ x: 0, y: 0 }, { x: 4, y: 3 })).toEqual([{ x: 0, y: 0 }, { x: 4, y: 0 }, { x: 4, y: 3 }, { x: 0, y: 3 }]);
    const back = rectangleOutline({ x: 4, y: 0 }, { x: 0, y: 3 });
    let a = 0;
    back.forEach((p, i) => { const q = back[(i + 1) % 4]; a += p.x * q.y - q.x * p.y; });
    expect(a).toBeGreaterThan(0);
  });

  it('makes a circle of four quarter arcs bulging out, from its centre and a point on it', () => {
    const c = circleOutline({ x: 1, y: 1 }, { x: 3, y: 1 })!;
    expect(c.pts).toHaveLength(4);
    expect(c.pts[1].x).toBeCloseTo(1);
    expect(c.pts[1].y).toBeCloseTo(3);
    c.arcs.forEach((r) => expect(r).toBeCloseTo(-2 * (1 - Math.SQRT1_2)));
    expect(circleOutline({ x: 1, y: 1 }, { x: 1, y: 1 })).toBeNull();
  });

  it('goes on from the edge before with a tangent arc', () => {
    // Along +x, then to (1, 1): a quarter circle turning left, bulging to the right of its chord (a negative rise).
    expect(tangentRise({ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 1, y: 1 })).toBeCloseTo(-(Math.SQRT2 / 2) * Math.tan(Math.PI / 8));
    expect(tangentRise({ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 1, y: -1 })).toBeCloseTo((Math.SQRT2 / 2) * Math.tan(Math.PI / 8));
    expect(tangentRise({ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 3, y: 0 })).toBe(0);
    // Arriving along its last edge, or its arc's end tangent.
    expect(arrivingDir([{ x: 0, y: 0 }], [])).toBeNull();
    const d = arrivingDir([{ x: 0, y: 0 }, { x: 2, y: 0 }], [-1])!;
    expect(d.x).toBeCloseTo(0);
    expect(d.y).toBeCloseTo(1);
  });
});
