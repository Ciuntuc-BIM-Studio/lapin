import { describe, expect, it } from 'vitest';
import { clickStep, constrained, enterStep, isPointGesture, previewOf, typedPoint } from './gesture3d';

const p = (x: number, y: number) => ({ x, y });

describe('the placing tools in 3D', () => {
  it('take the gestures of points, and leave picks to the view', () => {
    expect(isPointGesture({ kind: 'twoPoints' })).toBe(true);
    expect(isPointGesture({ kind: 'polygon' })).toBe(true);
    expect(isPointGesture({ kind: 'pick', types: ['IfcWall'] })).toBe(false);
    expect(isPointGesture({ kind: 'face', types: ['*'] })).toBe(false);
    expect(isPointGesture(undefined)).toBe(false);
  });

  it('chain walls: the end of one is the start of the next', () => {
    const g = { kind: 'twoPoints', repeat: true } as const;
    const a = clickStep(g, [], p(0, 0), 0.1);
    expect(a).toEqual({ pts: [p(0, 0)], done: null });
    const b = clickStep(g, a.pts, p(4, 0), 0.1);
    expect(b).toEqual({ pts: [p(4, 0)], done: [p(0, 0), p(4, 0)] });
    // A beam is not chained.
    expect(clickStep({ kind: 'twoPoints' }, [p(0, 0)], p(4, 0), 0.1).pts).toEqual([]);
    expect(clickStep({ kind: 'point' }, [], p(1, 2), 0.1)).toEqual({ pts: [], done: [p(1, 2)] });
  });

  it('close a polygon back on its first point or by Enter, and end a polyline on its last', () => {
    const g = { kind: 'polygon' } as const;
    let pts = [p(0, 0), p(4, 0), p(4, 3)];
    expect(clickStep(g, pts, p(0.02, 0.01), 0.1)).toEqual({ pts: [], done: pts });
    expect(clickStep(g, pts, p(0, 3), 0.1).done).toBeNull();
    expect(enterStep(g, pts)?.done).toEqual(pts);
    expect(enterStep(g, pts.slice(0, 2))).toBeNull();
    const l = { kind: 'polyline' } as const;
    pts = [p(0, 0), p(2, 0)];
    expect(clickStep(l, pts, p(2.01, 0), 0.1).done).toEqual(pts);
    expect(enterStep(l, pts)?.done).toEqual(pts);
    expect(enterStep(l, [p(0, 0)])).toBeNull();
    const arc = { kind: 'threePoints', arc: true } as const;
    expect(clickStep(arc, [p(0, 0), p(4, 0)], p(2, 1), 0.1).done).toEqual([p(0, 0), p(4, 0), p(2, 1)]);
  });

  it('put a typed length from the last point the way the cursor points, a rectangle\'s size towards the cursor', () => {
    const wall = { kind: 'twoPoints', repeat: true } as const;
    const q = typedPoint(wall, [p(1, 1)], { kind: 'length', value: 3 }, p(1, 5))!;
    expect(q.x).toBeCloseTo(1, 9);
    expect(q.y).toBeCloseTo(4, 9);
    const r = typedPoint(wall, [p(0, 0)], { kind: 'lengthAngle', value: 2, deg: 90 }, null)!;
    expect(r.x).toBeCloseTo(0, 9);
    expect(r.y).toBeCloseTo(2, 9);
    expect(typedPoint({ kind: 'rectangle' }, [p(1, 1)], { kind: 'pair', a: 2, b: 3 }, p(-5, 0))).toEqual(p(-1, -2));
    expect(typedPoint(wall, [], { kind: 'length', value: 3 }, p(1, 5))).toBeNull();
    expect(typedPoint(wall, [p(0, 0)], { kind: 'radius', value: 3 }, p(1, 5))).toBeNull();
  });

  it('draw the outline the points and the cursor make, and keep a tool\'s own rule', () => {
    expect(previewOf({ kind: 'rectangle' }, [p(0, 0)], p(2, 1))).toEqual({ pts: [p(0, 0), p(2, 0), p(2, 1), p(0, 1)], closed: true });
    expect(previewOf({ kind: 'polygon' }, [p(0, 0), p(2, 0)], p(2, 2)).closed).toBe(true);
    expect(previewOf({ kind: 'polyline' }, [p(0, 0), p(2, 0)], p(2, 2)).closed).toBe(false);
    const arc = previewOf({ kind: 'threePoints', arc: true }, [p(-1, 0), p(1, 0)], p(0, 1)).pts;
    expect(arc.length).toBeGreaterThan(10);
    expect(Math.max(...arc.map((q) => q.y))).toBeCloseTo(1, 3);
    const pipe = { kind: 'polyline', constrain: (_: unknown, a: { x: number; y: number }) => ({ x: Math.round(a.x), y: Math.round(a.y) }) } as const;
    expect(constrained(pipe, [p(0, 0)], p(1.4, 2.6))).toEqual(p(1, 3));
    expect(constrained(pipe, [], p(1.4, 2.6))).toEqual(p(1.4, 2.6));
  });
});
