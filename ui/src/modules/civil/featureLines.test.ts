import { describe, expect, it } from 'vitest';
import { bendSegment, defaultReach, dragFade, draggedContour, formatGrade, insertCorner, lineOf, moveCorner, payloadOf, readGrade, readHeights, readReach, removeCorner, saveLine } from './featureLines';

/** An open line of three corners: the first and last with a height of their own, the middle on the ground. */
const open = () => lineOf({
  points: [[0, 0, 1], [10, 0], [20, 0, 3]], arcs: [0, 0], grades: [null, null], closed: false,
  cornerZ: [1, 1.8, 3], ownZ: [true, false, true],
});

describe('a feature line read from its props', () => {
  it('takes corners, own heights, arcs and grades; the closed flag needs three corners', () => {
    const l = open();
    expect(l.pts).toHaveLength(3);
    expect(l.z).toEqual([1, null, 3]);
    expect(l.arcs).toEqual([0, 0]);
    expect(l.closed).toBe(false);
    expect(lineOf({ points: [[0, 0], [1, 0]], closed: true }).closed).toBe(false);
    expect(lineOf({ points: [[0, 0], [1, 0], [1, 1]], closed: true }).arcs).toHaveLength(3);
  });
  it('sends back the corners with their own heights only', () => {
    expect(payloadOf(open())).toEqual({ points: [[0, 0, 1], [10, 0], [20, 0, 3]], arcs: [0, 0], grades: [null, null], closed: false });
  });
});

describe('editing a feature line in plan', () => {
  it('moves a corner keeping its height, arcs and grades', () => {
    const l = moveCorner(open(), 0, { x: -2, y: 1 });
    expect(payloadOf(l).points[0]).toEqual([-2, 1, 1]);
  });
  it('inserts a corner in a segment: the line keeps its shape (the new corner takes the line\'s height there)', () => {
    const l = insertCorner(open(), 1);
    // The segment 10 → 20 runs from the ground's 1.8 to 3: its middle is 2.4, as a height of its own.
    expect(l.pts[2]).toEqual({ x: 15, y: 0 });
    expect(payloadOf(l).points).toEqual([[0, 0, 1], [10, 0], [15, 0, 2.4], [20, 0, 3]]);
    expect(l.arcs).toEqual([0, 0, 0]);
    expect(l.grades).toHaveLength(3);
  });
  it('gives a corner no height of its own when its segment\'s ends are the ground\'s', () => {
    const l = lineOf({ points: [[0, 0], [10, 0]], cornerZ: [1, 2], ownZ: [false, false] });
    expect(payloadOf(insertCorner(l, 0)).points).toEqual([[0, 0], [5, 0], [10, 0]]);
  });
  it('gives a grade to both halves of a segment it splits', () => {
    const l = lineOf({ points: [[0, 0, 1], [10, 0]], grades: [0.02], cornerZ: [1, 1.2], ownZ: [true, false] });
    const n = insertCorner(l, 0, { x: 4, y: 0 });
    expect(n.grades).toEqual([0.02, 0.02]);
    expect(payloadOf(n).points[1]).toEqual([4, 0, 1.08]);
  });
  it('inserts on a closed line\'s closing segment', () => {
    const l = lineOf({ points: [[0, 0], [10, 0], [10, 10]], closed: true, cornerZ: [0, 0, 0], ownZ: [true, true, true] });
    const n = insertCorner(l, 2);
    expect(n.pts).toHaveLength(4);
    expect(n.pts[3]).toEqual({ x: 5, y: 5 });
    expect(n.arcs).toHaveLength(4);
  });
  it('splits an arc into two arcs of its circle', () => {
    const l = lineOf({ points: [[0, 0, 0], [10, 0, 1]], arcs: [2], cornerZ: [0, 1], ownZ: [true, true] });
    const n = insertCorner(l, 0);
    expect(n.arcs).toHaveLength(2);
    expect(Math.abs(n.arcs[0])).toBeGreaterThan(0);
    expect(Math.sign(n.arcs[0])).toBe(Math.sign(2));
    // The arc is symmetrical: its middle is half way along it, so half way up.
    expect(n.z[1]).toBeCloseTo(0.5, 6);
  });
  it('takes a corner out, the segments it joined becoming one straight segment with no grade', () => {
    const l = lineOf({ points: [[0, 0], [5, 0], [10, 0], [15, 0]], arcs: [0, 3, 0], grades: [null, 0.01, 0.02] });
    const n = removeCorner(l, 1)!;
    expect(n.pts).toHaveLength(3);
    expect(n.arcs).toEqual([0, 0]);
    expect(n.grades).toEqual([null, 0.02]);
    expect(removeCorner(l, 0)!.arcs).toEqual([3, 0]);
    expect(removeCorner(l, 3)!.grades).toEqual([null, 0.01]);
  });
  it('takes a corner out of a closed line, never under three, and never an open one under two', () => {
    const c = lineOf({ points: [[0, 0], [10, 0], [10, 10], [0, 10]], closed: true, arcs: [0, 1, 0, 0] });
    const n = removeCorner(c, 2)!;
    expect(n.pts).toHaveLength(3);
    expect(n.arcs).toEqual([0, 0, 0]);
    expect(removeCorner(n, 0)).toBeNull();
    expect(removeCorner(lineOf({ points: [[0, 0], [1, 0]] }), 0)).toBeNull();
  });
  it('bends a segment', () => {
    expect(bendSegment(open(), 1, 0.5).arcs).toEqual([0, 0.5]);
  });
  it('writes the whole line as one update', () => {
    const s = saveLine('L1', moveCorner(open(), 1, { x: 9, y: 1 }));
    expect(s.command).toBe('civil.updateFeatureLine');
    expect(s.args).toMatchObject({ id: 'L1', closed: false, points: [[0, 0, 1], [9, 1], [20, 0, 3]] });
  });
});

describe('grades', () => {
  it('reads what the engine reads', () => {
    expect(readGrade('2 %')).toBeCloseTo(0.02, 9);
    expect(readGrade('-1,5 %')).toBeCloseTo(-0.015, 9);
    expect(readGrade('1:50')).toBeCloseTo(0.02, 9);
    expect(readGrade('-1:40')).toBeCloseTo(-0.025, 9);
    expect(readGrade('0.02')).toBeCloseTo(0.02, 9);
    expect(readGrade('')).toBeNull();
    expect(readGrade('steep')).toBeUndefined();
    expect(readGrade('1:0')).toBeUndefined();
  });
  it('shows a fraction as a percentage', () => {
    expect(formatGrade(0.02)).toBe('2.00 %');
    expect(formatGrade(-0.0125)).toBe('-1.25 %');
    expect(formatGrade(-1e-9)).toBe('0.00 %');
  });
});

describe('dragging a contour', () => {
  it('fades the drag by Shepard weighting: 1 at the grab, ½ halfway, 0 at the reach', () => {
    expect(dragFade(0, 10)).toBe(1);
    expect(dragFade(5, 10)).toBeCloseTo(0.5, 12);
    expect(dragFade(10, 10)).toBe(0);
    expect(dragFade(12, 10)).toBe(0);
    expect(dragFade(2, 10)).toBeCloseTo(64 / (64 + 4), 12);
    // Falling monotonically.
    let last = 1;
    for (let r = 0.5; r < 10; r += 0.5) { const w = dragFade(r, 10); expect(w).toBeLessThan(last); last = w; }
  });
  it('has a default reach of 2 m or three times the drag', () => {
    expect(defaultReach(0.5)).toBe(2);
    expect(defaultReach(4)).toBe(12);
  });
  it('moves the points within the reach, faded, and leaves the others', () => {
    const line = [{ x: 0, y: 0 }, { x: 5, y: 0 }, { x: 10, y: 0 }, { x: 30, y: 0 }];
    const { line: out, reach } = draggedContour(line, { x: 0, y: 0 }, { x: 0, y: 2 }, 10);
    expect(reach).toBe(10);
    expect(out[0]).toEqual({ x: 0, y: 2 });
    expect(out[1].y).toBeCloseTo(1, 12);
    expect(out[2]).toEqual({ x: 10, y: 0 });
    expect(out[3]).toEqual({ x: 30, y: 0 });
  });
  it('takes the default reach when none is given', () => {
    expect(draggedContour([{ x: 0, y: 0 }], { x: 0, y: 0 }, { x: 3, y: 4 }).reach).toBe(15);
  });
});

describe('what the tools read from their bars', () => {
  it('reads the corners\' heights in order, an empty one taking the ground\'s', () => {
    expect(readHeights('1.5; ;3', 3)).toEqual([1.5, null, 3]);
    expect(readHeights('1 2 3', 3)).toEqual([1, 2, 3]);
    expect(readHeights('', 2)).toEqual([null, null]);
    expect(readHeights('2;', 3)).toEqual([2, null, null]);
    expect(readHeights('2; abc', 2)).toBeNull();
  });
  it('reads a reach: empty for the default, nothing that does not read or is not positive', () => {
    expect(readReach('')).toBeUndefined();
    expect(readReach('7 m')).toBe(7);
    expect(readReach('abc')).toBeNull();
    expect(readReach('-3 m')).toBeNull();
  });
});
