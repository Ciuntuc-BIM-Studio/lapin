import { describe, expect, it } from 'vitest';
import { KNEE_ANGLES, leaderShapeOf, rotationFor, snapKnee, snapTurn, tagLeaderShape, turnAngle } from './tagLeader';

const deg = (end: { x: number; y: number }, p: { x: number; y: number }) => ((Math.atan2(p.y - end.y, p.x - end.x) * 180) / Math.PI + 360) % 360;

describe('a tag\'s knee', () => {
  it('keeps 30°, 45°, 60° or 90° from the leader\'s end, every way round', () => {
    expect(KNEE_ANGLES).toHaveLength(14);
    const end = { x: 1, y: 2 };
    expect(deg(end, snapKnee(end, { x: 3, y: 4.1 }, false))).toBeCloseTo(45, 9);
    expect(deg(end, snapKnee(end, { x: 1.1, y: 5 }, false))).toBeCloseTo(90, 9);
    expect(deg(end, snapKnee(end, { x: 4, y: 3.6 }, false))).toBeCloseTo(30, 9);
    expect(deg(end, snapKnee(end, { x: -1, y: 5.2 }, false))).toBeCloseTo(120, 9);
    expect(deg(end, snapKnee(end, { x: -2, y: 0 }, false))).toBeCloseTo(210, 9);
    expect(deg(end, snapKnee(end, { x: 3, y: -1.3 }, false))).toBeCloseTo(300, 9);
  });

  it('goes as far along the angle as the cursor, and where it is with Shift', () => {
    const end = { x: 0, y: 0 };
    const k = snapKnee(end, { x: 2, y: 2.2 }, false);
    expect(k.x).toBeCloseTo(k.y, 9);
    expect(Math.hypot(k.x, k.y)).toBeCloseTo((2 + 2.2) / Math.SQRT2, 9);
    expect(snapKnee(end, { x: 2, y: 2.2 }, true)).toEqual({ x: 2, y: 2.2 });
    expect(snapKnee(end, end, false)).toEqual(end);
  });
});

describe('a tag\'s leader shape', () => {
  it('is an elbow unless its type or the tag says straight, and straight when bent by hand', () => {
    expect(leaderShapeOf(undefined)).toBe('elbow');
    expect(leaderShapeOf({ leaderShape: 'straight' })).toBe('straight');
    expect(tagLeaderShape({}, { leaderShape: 'straight' })).toBe('straight');
    expect(tagLeaderShape({ leaderShape: 'elbow' }, { leaderShape: 'straight' })).toBe('elbow');
    expect(tagLeaderShape({ elbow: [1, 2] }, null)).toBe('straight');
    expect(tagLeaderShape({ elbow: [1, 2], leaderShape: 'elbow' }, null)).toBe('elbow');
  });
});

describe('a tag\'s turning grip (report #129)', () => {
  const centre = { x: 2, y: 3 };
  it('reads the text\'s angle from a grip that stands square over it', () => {
    expect(turnAngle(centre, { x: 2, y: 5 })).toBeCloseTo(0, 9);
    expect(turnAngle(centre, { x: 0, y: 3 })).toBeCloseTo(90, 9);
    expect(turnAngle(centre, { x: 4, y: 3 })).toBeCloseTo(-90, 9);
  });
  it('keeps to steps of 15° (45° and 90° among them) and Shift frees it', () => {
    const at = (a: number, d = 2) => ({ x: centre.x + Math.cos(((a + 90) * Math.PI) / 180) * d, y: centre.y + Math.sin(((a + 90) * Math.PI) / 180) * d });
    expect(turnAngle(centre, snapTurn(centre, at(43), false))).toBeCloseTo(45, 9);
    expect(turnAngle(centre, snapTurn(centre, at(88), false))).toBeCloseTo(90, 9);
    expect(turnAngle(centre, snapTurn(centre, at(19), false))).toBeCloseTo(15, 9);
    expect(Math.hypot(snapTurn(centre, at(43, 3), false).x - centre.x, snapTurn(centre, at(43, 3), false).y - centre.y)).toBeCloseTo(3, 9);
    expect(snapTurn(centre, at(43), true)).toEqual(at(43));
  });
  it('stores the turn from the element\'s direction when aligned, from level otherwise; none when upright', () => {
    expect(rotationFor(30)).toBe(30);
    expect(rotationFor(30, 90)).toBe(-60);
    expect(rotationFor(0)).toBe(0);
    expect(rotationFor(90, 90)).toBe(0);
    expect(rotationFor(-170, 170)).toBe(20);
  });
});
