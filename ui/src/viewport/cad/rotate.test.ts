import { describe, expect, it } from 'vitest';
import { directionFrom, normalizeTurn, rotatedShape, rotationFromReference, turned } from './rotate';
import { parseCoordinateInput } from './input';

const P = (x: number, y: number) => ({ x, y });

describe('rotation from three points', () => {
  it('is the new direction minus the reference direction', () => {
    expect(rotationFromReference(P(0, 0), P(1, 0), P(0, 1))).toBeCloseTo(90, 9);
    expect(rotationFromReference(P(0, 0), P(1, 1), P(0, 3))).toBeCloseTo(45, 9);
    expect(rotationFromReference(P(2, 2), P(3, 2), P(2, 1))).toBeCloseTo(-90, 9);
  });
  it('takes the short way round and 0 for the same direction', () => {
    expect(rotationFromReference(P(0, 0), P(-1, 0.001), P(-1, -0.001))).toBeCloseTo(0.1146, 3);
    expect(normalizeTurn(270)).toBeCloseTo(-90, 9);
    expect(normalizeTurn(-180)).toBeCloseTo(180, 9);
    expect(rotationFromReference(P(0, 0), P(1, 0), P(5, 0))).toBe(0);
  });
  it('reads typed angles from the UCS axis, points as they lie (decision 445)', () => {
    // A UCS turned 30°: typed 0 is its X axis, 30° in the plan.
    expect(directionFrom(P(0, 0), 0, 30)).toBe(30);
    // Both typed: the UCS cancels.
    expect(rotationFromReference(P(0, 0), 10, 40, 30)).toBeCloseTo(30, 9);
    // A point reference at 0° in the plan, a typed new direction 0 in a 30° UCS: a turn of 30.
    expect(rotationFromReference(P(0, 0), P(1, 0), 0, 30)).toBeCloseTo(30, 9);
  });
  it('agrees with points typed in the UCS: the turn is the same in any frame', () => {
    const frame = { origin: P(5, 5), angle: 90 };
    const hinge = P(5, 5);
    const ref = parseCoordinateInput('@1,0', { last: hinge, frame });
    const tgt = parseCoordinateInput('@0,1', { last: hinge, frame });
    expect(ref?.kind === 'point' && tgt?.kind === 'point').toBe(true);
    if (ref?.kind === 'point' && tgt?.kind === 'point') expect(rotationFromReference(hinge, ref.pt, tgt.pt)).toBeCloseTo(90, 9);
  });
  it('turns points and shapes about the hinge', () => {
    const p = turned(P(2, 1), P(1, 1), 90);
    expect(p.x).toBeCloseTo(1, 9); expect(p.y).toBeCloseTo(2, 9);
    const a = rotatedShape({ type: 'arc', c: P(1, 0), r: 1, start: 0, end: Math.PI / 2 }, P(0, 0), 90);
    expect(a.type === 'arc' && a.c.y).toBeCloseTo(1, 9);
    expect(a.type === 'arc' && a.start).toBeCloseTo(Math.PI / 2, 9);
  });
});
