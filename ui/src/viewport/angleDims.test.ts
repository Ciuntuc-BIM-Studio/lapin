/** Decision 738: angular temporary dimensions, the angle a segment drawn makes at its start and the one a selected wall or beam makes where it meets another. */

import { describe, expect, it } from 'vitest';
import { angleFrom, directionAt, elementAt, meetingAngles, previousRef, rotated, rotationFor, segmentArc, toDeg, wayAlong, X_REF } from './angleDims';
import type { DimLineInfo } from './lineDims';

const P = (x: number, y: number) => ({ x, y });
const wall = (id: string, a: [number, number], b: [number, number], t = 0.3, joins?: Partial<DimLineInfo['joins']>): DimLineInfo => ({
  id, name: id.toUpperCase(), kind: 'wall', a: P(...a), b: P(...b), faces: [-t / 2, t / 2], core: [-t / 2, t / 2],
  joins: { start: [], end: [], path: [], ...joins },
});
const c60 = Math.cos(Math.PI / 3), s60 = Math.sin(Math.PI / 3);

describe('the angle of a segment being drawn', () => {
  it('to the previous segment is the angle inside the corner: 90° square, 180° straight on', () => {
    // The previous segment ran (0,0) → (5,0); the next starts at (5,0).
    const ref = previousRef(P(5, 0), P(0, 0))!;
    expect(ref.what).toBe('previous');
    expect(segmentArc(ref, P(5, 0), P(5, 3))!.value).toBeCloseTo(90, 9);
    expect(segmentArc(ref, P(5, 0), P(5, -3))!.value).toBeCloseTo(90, 9);
    expect(segmentArc(ref, P(5, 0), P(9, 0))!.value).toBeCloseTo(180, 9);
    expect(segmentArc(ref, P(5, 0), P(5 - 2 * c60, 2 * s60))!.value).toBeCloseTo(60, 9);
    expect(previousRef(P(1, 1), P(1, 1))).toBeNull();
  });

  it('to the wall it starts on is the smaller angle to its line, either way along it', () => {
    const lines = [wall('w', [0, 0], [6, 0]), { ...wall('c', [3, 3], [3, 3.4]), kind: 'column' }];
    const ref = elementAt(P(2, 0.1), lines)!;
    expect(ref).toMatchObject({ what: 'element', id: 'w', ray: false });
    expect(segmentArc(ref, P(2, 0.1), P(2 + 3 * c60, 0.1 + 3 * s60))!.value).toBeCloseTo(60, 9);
    expect(segmentArc(ref, P(2, 0.1), P(2 - 3 * c60, 0.1 + 3 * s60))!.value).toBeCloseTo(60, 9);
    // Off the wall (past its thickness), or on a column: none.
    expect(elementAt(P(2, 0.5), lines)).toBeNull();
    expect(elementAt(P(3, 3.2), lines)).toBeNull();
  });

  it('with nothing at its start, from the X axis counter-clockwise, as the ∠ field reads it', () => {
    expect(segmentArc(X_REF, P(0, 0), P(0, 2))!.value).toBeCloseTo(90, 9);
    expect(segmentArc(null, P(0, 0), P(0, -2))!.value).toBeCloseTo(270, 9);
  });

  it('typed, sets the direction on the side of the reference the cursor is', () => {
    const ref = previousRef(P(5, 0), P(0, 0))!;
    const m = angleFrom(ref, P(0.3, 1))!;
    expect(m.side).toBe(-1);
    const d = directionAt(m.ray, m.side, 90);
    expect(d.x).toBeCloseTo(0, 9);
    expect(d.y).toBeCloseTo(1, 9);
  });
});

describe('the angle a selected wall makes where it meets another', () => {
  it('is the smaller angle at the corner, and typed turns the wall about the corner to read it', () => {
    const a = wall('a', [0, 0], [6, 0], 0.3, { start: ['b'] });
    const b = wall('b', [0, 0], [4 * c60, 4 * s60], 0.3, { start: ['a'] });
    const [m] = meetingAngles(b, [a, b]);
    expect(m).toMatchObject({ end: 'start', other: { id: 'a' } });
    expect(m.at.x).toBeCloseTo(0, 9);
    expect(m.deg).toBeCloseTo(60, 9);
    // Typed 75: the wall turns 15° counter-clockwise about the corner, and then reads 75.
    const turn = rotationFor(m, 75)!;
    expect(turn).toBeCloseTo(15, 9);
    const end = rotated(b.b, m.at, turn);
    expect(toDeg(Math.atan2(end.y, end.x))).toBeCloseTo(75, 9);
    expect(rotationFor(m, 60)).toBeNull();
    expect(rotationFor(m, 180)).toBeNull();
    // A wider corner (120° inside) reads its smaller angle, 60°, to the other's line beyond the corner.
    const wide = wall('w', [0, 0], [-4 * c60, 4 * s60], 0.3, { start: ['a'] });
    expect(meetingAngles(wide, [a, wide])[0].deg).toBeCloseTo(60, 9);
  });

  it('shows where its end stands on another without a join, not by a parallel one or one it does not reach', () => {
    const host = wall('h', [3, 0.15], [3 + 3 * c60, 0.15 + 3 * s60]);
    const base = wall('base', [0, 0], [6, 0]);
    const parallel = wall('p', [3, 5], [3 + c60, 5 + s60]);
    const far = wall('far', [10, -5], [10, 5]);
    const ms = meetingAngles(host, [host, base, parallel, far]);
    expect(ms.map((m) => m.other.id)).toEqual(['base']);
    expect(ms[0].deg).toBeCloseTo(60, 9);
    // Where their axes cross: half the base's thickness below the host's end.
    expect(ms[0].at.y).toBeCloseTo(0, 6);
  });

  it('holds a permanent angle on each element by the way its ray runs along it', () => {
    expect(wayAlong(P(1, 0), P(0, 0), P(6, 0))).toBe(1);
    expect(wayAlong(P(-1, 0), P(0, 0), P(6, 0))).toBe(-1);
  });
});
