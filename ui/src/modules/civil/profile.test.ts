import { describe, expect, it } from 'vitest';
import {
  capabilities, dragPoint, gradeTarget, heightAtPx, heightRange, hitPoint, hitSegment, makeFrame, niceStep, planCrossMove, planCrossPoint, planDelete, planGrade,
  planInsert, planMove, pointAtStation, segmentsOf, seriesPath, stationAtPoint, stationAtPx, stationName, sx, sy, ticks, valueAt, withGround, type ProfileData, type ProfilePoint,
} from './profile';

const pt = (station: number, z: number, o: Partial<ProfilePoint> = {}): ProfilePoint => ({ station, z, designed: true, locked: false, curve: 0, movable: true, grade: null, ...o });
const fl = (points: ProfilePoint[], extra: Partial<ProfileData> = {}): ProfileData => ({
  kind: 'featureLine', id: 'L1', name: 'Line', start: 0, length: 40, closed: false, path: [[0, 0, 0], [40, 0, 40]], line: points.map((p) => [p.station, p.z]),
  points, existing: [], design: [], ...extra,
});
const three = [pt(0, 10, { movable: false }), pt(20, 11), pt(40, 10, { movable: false })];

describe('the frame', () => {
  const spec = { s0: 0, s1: 100, z0: 0, z1: 10, width: 552, height: 264, exaggeration: 1 };
  it('maps stations and heights to pixels and back', () => {
    const f = makeFrame(spec);
    expect(stationAtPx(f, sx(f, 37.5))).toBeCloseTo(37.5, 9);
    expect(heightAtPx(f, sy(f, 6.25))).toBeCloseTo(6.25, 9);
    expect(sy(f, 8)).toBeLessThan(sy(f, 2));
  });
  it('is true at 1x and steeper by the exaggeration', () => {
    const one = makeFrame({ ...spec, z1: 2 });
    expect(one.kz).toBeCloseTo(one.k, 9);
    const five = makeFrame({ ...spec, z1: 2, exaggeration: 5 });
    expect(five.kz).toBeCloseTo(five.k * 5, 9);
  });
  it('keeps everything inside the box at any exaggeration', () => {
    for (const ex of [1, 2, 5, 10]) {
      const f = makeFrame({ ...spec, exaggeration: ex });
      expect(sx(f, 100)).toBeLessThanOrEqual(spec.width - 16 + 1e-6);
      expect(sy(f, 10)).toBeGreaterThanOrEqual(14 - 1e-6);
      expect(sy(f, 0)).toBeLessThanOrEqual(spec.height - 30 + 1e-6);
    }
  });
  it('pads the height range and survives an empty one', () => {
    const [lo, hi] = heightRange([[[0, 1], [1, 3]]], [0]);
    expect(lo).toBeCloseTo(-0.24, 9); expect(hi).toBeCloseTo(3.24, 9);
    expect(heightRange([[]])).toEqual([0, 1]);
  });
  it('picks round grid steps', () => {
    expect(niceStep(97, 5)).toBe(20);
    expect(niceStep(7, 5)).toBe(2);
    expect(ticks(0, 100, 25)).toEqual([0, 25, 50, 75, 100]);
  });
  it('draws a gap where the ground is off', () => {
    const f = makeFrame(spec);
    expect(seriesPath(f, [[0, 1], [10, null], [20, 3], [30, 3]]).match(/M/g)).toHaveLength(2);
  });
  it('reads a series between its samples', () => {
    expect(valueAt([[0, 0], [10, 5]], 4)).toBeCloseTo(2, 9);
    expect(valueAt([[0, 0], [10, null]], 4)).toBeNull();
    expect(valueAt([[0, 0], [10, 5]], 11)).toBeNull();
  });
});

describe('hit testing', () => {
  const f = makeFrame({ s0: 0, s1: 40, z0: 9, z1: 12, width: 440, height: 200, exaggeration: 1 });
  it('finds the point under the cursor, the nearest within the radius', () => {
    expect(hitPoint(f, three, sx(f, 20) + 3, sy(f, 11) - 2)).toBe(1);
    expect(hitPoint(f, three, sx(f, 10), sy(f, 10.5))).toBe(-1);
  });
  it('finds the segment under the cursor', () => {
    const segs = segmentsOf(three, false, 40);
    expect(segs).toHaveLength(2);
    expect(hitSegment(f, segs, sx(f, 10), sy(f, 10.5))).toBe(0);
    expect(hitSegment(f, segs, sx(f, 30), sy(f, 10.5) + 1)).toBe(1);
    expect(hitSegment(f, segs, sx(f, 10), sy(f, 10.5) - 40)).toBe(-1);
  });
  it('closes a closed line back to its first height', () => {
    const segs = segmentsOf([pt(0, 5), pt(10, 6), pt(20, 7)], true, 30);
    expect(segs).toHaveLength(3);
    expect(segs[2]).toMatchObject({ from: 2, to: 0, s0: 20, z0: 7, s1: 30, z1: 5 });
  });
});

describe('dragging a point', () => {
  it('moves it vertically and makes it designed', () => {
    const next = dragPoint([pt(0, 10), pt(20, 11, { designed: false }), pt(40, 10)], 1, { z: 12.5 }, 40)!;
    expect(next[1]).toMatchObject({ station: 20, z: 12.5, designed: true });
  });
  it('slides a movable point along the station, between its neighbours', () => {
    expect(dragPoint(three, 1, { station: 25, z: 11 }, 40)![1].station).toBe(25);
    expect(dragPoint(three, 1, { station: 99 }, 40)![1].station).toBeCloseTo(40 - 0.05, 9);
    expect(dragPoint(three, 1, { station: -9 }, 40)![1].station).toBeCloseTo(0.05, 9);
  });
  it('keeps a point that may not slide at its station, and the ends', () => {
    expect(dragPoint([pt(0, 10), pt(20, 11, { movable: false }), pt(40, 10)], 1, { station: 25, z: 12 }, 40)![1]).toMatchObject({ station: 20, z: 12 });
    expect(dragPoint(three, 0, { station: 5, z: 9 }, 40)![0]).toMatchObject({ station: 0, z: 9 });
    expect(dragPoint(three, 2, { station: 30 }, 40)![2].station).toBe(40);
  });
  it('refuses a locked point', () => {
    expect(dragPoint([pt(0, 10), pt(20, 11, { locked: true }), pt(40, 10)], 1, { z: 12 }, 40)).toBeNull();
  });
  it('drops the typed grade of the run before it, which the new height replaces', () => {
    const next = dragPoint([pt(0, 10, { grade: 0.02 }), pt(20, 11), pt(40, 10)], 1, { z: 13 }, 40)!;
    expect(next[0].grade).toBeNull();
  });
});

describe('typing a grade', () => {
  const seg = { from: 1, to: 2, s0: 20, z0: 11, s1: 40, z1: 10 };
  it('reads 2 %, 1:50, -2,5% and a decimal fraction', () => {
    expect(gradeTarget(seg, '2 %')).toEqual({ index: 2, z: expect.closeTo(11.4, 9) });
    expect(gradeTarget(seg, '1:50')).toEqual({ index: 2, z: expect.closeTo(11.4, 9) });
    expect(gradeTarget(seg, '-2,5%')).toEqual({ index: 2, z: expect.closeTo(10.5, 9) });
    expect(gradeTarget(seg, '0.01')).toEqual({ index: 2, z: expect.closeTo(11.2, 9) });
  });
  it('keeps the end when asked: the start takes the height', () => {
    expect(gradeTarget(seg, '2 %', 'end')).toEqual({ index: 1, z: expect.closeTo(9.6, 9) });
  });
  it('tells a typo, and an empty text', () => {
    expect(gradeTarget(seg, 'steep')).toBeUndefined();
    expect(gradeTarget(seg, '  ')).toBeNull();
  });
});

describe('the command a kind of line is edited with', () => {
  it('moves a feature line point with one command for height and station', () => {
    expect(planMove(fl(three), 1, { z: 12, station: 25 })).toEqual({ call: { command: 'civil.moveProfilePoint', args: { id: 'L1', index: 1, z: 12, station: 25 } } });
    expect(planMove(fl(three), 1, { z: 12 })).toEqual({ call: { command: 'civil.moveProfilePoint', args: { id: 'L1', index: 1, z: 12 } } });
  });
  it('types a grade onto a feature line as the engine takes it', () => {
    expect(planGrade(fl(three), { from: 0, to: 1, s0: 0, z0: 10, s1: 20, z1: 11 }, '2 %', 'start')).toEqual({ call: { command: 'civil.setFeatureLineGrade', args: { id: 'L1', segment: 0, grade: 0.02, keep: 'start' } } });
    expect(planGrade(fl(three), { from: 0, to: 1, s0: 0, z0: 10, s1: 20, z1: 11 }, 'x', 'start')).toHaveProperty('refused');
  });
  it('sends a hardscape every corner height, the edited one changed', () => {
    const h = fl([pt(0, 1, { movable: false }), pt(5, 1, { movable: false }), pt(10, 1, { movable: false })], { kind: 'hardscape', id: 'H', closed: true });
    expect(planMove(h, 1, { z: 1.4 })).toEqual({ call: { command: 'civil.updateHardscape', args: { id: 'H', cornerZ: [1, 1.4, 1] } } });
    const seg = segmentsOf(h.points, true, 15)[0];
    expect(planGrade(h, seg, '2 %', 'start')).toEqual({ call: { command: 'civil.updateHardscape', args: { id: 'H', cornerZ: [1, 1.1, 1] } } });
    expect(planInsert(h, 3)).toHaveProperty('refused');
    expect(planDelete(h, 1)).toHaveProperty('refused');
    expect(capabilities(h)).toMatchObject({ insert: false, remove: false, curve: false, lock: false, edit: true });
  });
  it('edits a corridor through its PVIs, in design mode only', () => {
    const c = fl([pt(100, 5), pt(140, 6), pt(180, 5)], { kind: 'corridor', id: 'C', start: 100, length: 80, mode: 'design' });
    expect(planMove(c, 1, { z: 7 })).toEqual({ call: { command: 'civil.setCorridor', args: { id: 'C', profile: { mode: 'design', pvis: [[100, 5, 0], [140, 7, 0], [180, 5, 0]] } } } });
    expect(planInsert(c, 120)).toEqual({ call: { command: 'civil.setCorridor', args: { id: 'C', profile: { mode: 'design', pvis: [[100, 5, 0], [120, 5.5, 0], [140, 6, 0], [180, 5, 0]] } } } });
    expect(planDelete(c, 1)).toEqual({ call: { command: 'civil.setCorridor', args: { id: 'C', profile: { mode: 'design', pvis: [[100, 5, 0], [180, 5, 0]] } } } });
    expect(planMove({ ...c, mode: 'ground' }, 1, { z: 7 })).toHaveProperty('refused');
  });
  it('sets a cross section point by the axis, and slides one on the cross line', () => {
    const c = { station: 20, label: '0+020.00', width: 10, at: [0, 0], from: [0, 10], to: [0, -10], z: 11, existing: [], design: [], crossId: 'X' } as never;
    expect(planCrossPoint('L1', c, -3, 10.5)).toEqual({ command: 'civil.setCrossPoint', args: { id: 'L1', station: 20, width: 10, offset: -3, z: 10.5 } });
    expect(planCrossMove(c, 2, -3, 10.5)).toEqual({ command: 'civil.moveProfilePoint', args: { id: 'X', index: 2, z: 10.5, station: 7 } });
  });
});

describe('plan and graph', () => {
  const path: [number, number, number][] = [[0, 0, 100], [10, 0, 110], [10, 10, 120]];
  it('finds the place at a station, with the direction', () => {
    expect(pointAtStation(path, 105)).toMatchObject({ x: 5, y: 0, angle: 0 });
    const p = pointAtStation(path, 115)!;
    expect(p.x).toBeCloseTo(10, 9); expect(p.y).toBeCloseTo(5, 9); expect(p.angle).toBeCloseTo(Math.PI / 2, 9);
    expect(pointAtStation(path, 500)).toMatchObject({ x: 10, y: 10 });
  });
  it('finds the station nearest a point of the plan', () => {
    const r = stationAtPoint(path, 4, 3)!;
    expect(r.station).toBeCloseTo(104, 9); expect(r.distance).toBeCloseTo(3, 9);
    expect(stationAtPoint(path, 12, 6)!.station).toBeCloseTo(116, 9);
  });
  it('writes stations as 0+020.00', () => {
    expect(stationName(20)).toBe('0+020.00');
    expect(stationName(1234.5)).toBe('1+234.50');
  });
});

describe('a line with no terrain under it (forum 258: a retaining wall drawn on a bare site)', () => {
  it('has empty ground series, which every plot can draw and read', () => {
    // The engine leaves `existing` and `design` out with no terrain; a plot looping over undefined threw and blanked the window.
    const raw: { line: [number, number][]; existing?: [number, number][]; design?: [number, number][] } = { line: [[0, 0], [10, 0]] };
    const bare = withGround(raw);
    expect(bare.existing).toEqual([]);
    expect(bare.design).toEqual([]);
    const fr = makeFrame({ s0: 0, s1: 10, z0: 0, z1: 1, width: 500, height: 200, exaggeration: 1 });
    expect(seriesPath(fr, bare.existing)).toBe('');
    expect(valueAt(bare.design, 5)).toBeNull();
    expect(heightRange([bare.existing, bare.design, bare.line])).toEqual([-0.1, 0.1]);
    const rough: { existing?: [number, number][] } = { existing: [[0, 1]] };
    const kept = withGround(rough);
    expect(kept.existing).toEqual([[0, 1]]);
  });
});
