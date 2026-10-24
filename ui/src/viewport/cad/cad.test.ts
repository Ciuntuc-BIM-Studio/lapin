/** Snaps, typed input and guides: ported from the old Lapin's snaps-input.test.ts (same cases and numbers), then the guide lines. */

import { describe, expect, it } from 'vitest';
import type { Vec2 } from '../../platform/module';
import { ALL_MODES, SnapIndex, findSnap, type SnapMode } from './snaps';
import { applyOrtho, applyPolar, parseCoordinateInput, pointAlong } from './input';
import { arcThrough, type Shape } from './shapes';
import { track, trackOnOrtho } from './tracking';

const P = (x: number, y: number): Vec2 => ({ x, y });
const nearPt = (p: Vec2, q: Vec2) => { expect(p.x).toBeCloseTo(q.x, 7); expect(p.y).toBeCloseTo(q.y, 7); };

describe('SnapIndex', () => {
  it('returns only shapes near the query box', () => {
    const shapes: Shape[] = [];
    for (let i = 0; i < 100; i++) shapes.push({ type: 'line', a: P(i * 10, 0), b: P(i * 10 + 5, 0) });
    shapes.push({ type: 'circle', c: P(500, 500), r: 2 });
    const idx = new SnapIndex(shapes);
    const hit = idx.query(P(201, -1), P(204, 1));
    expect(hit).toContain(20);
    expect(hit).not.toContain(0);
    expect(hit).not.toContain(99);
    expect(hit).not.toContain(100);
    expect(hit.length).toBeLessThan(20);
    expect(new SnapIndex(shapes, 1).query(P(201, -1), P(204, 1))).toEqual([20]);
    expect(idx.query(P(499.9, 499.9), P(500.1, 500.1))).toContain(100);
    expect(idx.query(P(5000, 5000), P(5001, 5001))).toEqual([]);
  });

  it('handles 50,000 segments', () => {
    const shapes: Shape[] = [];
    for (let i = 0; i < 50000; i++) { const x = (i % 250) * 2, y = Math.floor(i / 250) * 2; shapes.push({ type: 'line', a: P(x, y), b: P(x + 1, y) }); }
    const idx = new SnapIndex(shapes);
    const ids = idx.query(P(10.4, 19.9), P(10.6, 20.1));
    expect(ids.filter((i) => { const s = shapes[i]; return s.type === 'line' && s.a.x <= 10.6 && s.b.x >= 10.4 && Math.abs(s.a.y - 20) < 0.2; })).toEqual([10 * 250 + 5]);
    const snap = findSnap(P(10.95, 20.02), shapes, idx, { tolerance: 0.1, modes: ALL_MODES });
    expect(snap?.mode).toBe('endpoint');
    nearPt(snap!.point, P(11, 20));
  });
});

describe('findSnap', () => {
  const shapes: Shape[] = [{ type: 'line', a: P(0, 0), b: P(10, 0) }, { type: 'line', a: P(5, -5), b: P(5, 5) }, { type: 'circle', c: P(20, 0), r: 3 }];
  it('endpoint beats nearest', () => {
    const s = findSnap(P(9.8, 0.1), shapes, null, { tolerance: 0.5, modes: ALL_MODES });
    expect(s?.mode).toBe('endpoint');
    nearPt(s!.point, P(10, 0));
  });
  it('falls back to nearest when the endpoint mode is disabled', () => {
    const s = findSnap(P(9.8, 0.1), shapes, null, { tolerance: 0.5, modes: new Set<SnapMode>(['nearest']) });
    expect(s?.mode).toBe('nearest');
    nearPt(s!.point, P(9.8, 0));
  });
  it('finds intersections and midpoints with priority', () => {
    const s = findSnap(P(5.1, 0.1), shapes, null, { tolerance: 0.5, modes: ALL_MODES });
    expect(s?.mode).toBe('intersection');
    nearPt(s!.point, P(5, 0));
    expect(findSnap(P(2.6, 0.1), shapes, null, { tolerance: 0.5, modes: ALL_MODES })?.mode).toBe('nearest');
  });
  it('snaps to a centre from the curve or near the centre', () => {
    const modes = new Set<SnapMode>(['center', 'nearest']);
    const onCurve = findSnap(P(23.1, 0), shapes, null, { tolerance: 0.2, modes });
    expect(onCurve?.mode).toBe('center');
    nearPt(onCurve!.point, P(20, 0));
    expect(findSnap(P(20.1, 0.1), shapes, new SnapIndex(shapes), { tolerance: 0.2, modes })?.mode).toBe('center');
  });
  it('quadrant and perpendicular', () => {
    expect(findSnap(P(20.05, 3.05), shapes, null, { tolerance: 0.2, modes: ALL_MODES })?.mode).toBe('center');
    const q = findSnap(P(20.05, 3.05), shapes, null, { tolerance: 0.2, modes: new Set<SnapMode>(['quadrant', 'nearest']) });
    expect(q?.mode).toBe('quadrant');
    nearPt(q!.point, P(20, 3));
    const p = findSnap(P(3.1, 0.05), shapes, null, { tolerance: 0.2, modes: new Set<SnapMode>(['perpendicular', 'nearest']), from: P(3, 7) });
    expect(p?.mode).toBe('perpendicular');
    nearPt(p!.point, P(3, 0));
  });
  it('returns null when nothing is within tolerance', () => {
    expect(findSnap(P(50, 50), shapes, null, { tolerance: 0.5, modes: ALL_MODES })).toBeNull();
  });
});

describe('coordinate input', () => {
  const last = P(1, 1);
  it('parses absolute and relative cartesian points', () => {
    expect(parseCoordinateInput('3,4', { last: null })).toEqual({ kind: 'point', pt: P(3, 4) });
    expect(parseCoordinateInput(' -1.5 , 2. ', { last: null })).toEqual({ kind: 'point', pt: P(-1.5, 2) });
    expect(parseCoordinateInput('@2,-3', { last })).toEqual({ kind: 'point', pt: P(3, -2) });
    expect(parseCoordinateInput('@', { last })).toEqual({ kind: 'point', pt: P(1, 1) });
  });
  it('parses polar forms', () => {
    const r = parseCoordinateInput('@2<90', { last });
    nearPt(r?.kind === 'point' ? r.pt : P(NaN, NaN), P(1, 3));
    const a = parseCoordinateInput('2 < 180', { last });
    nearPt(a?.kind === 'point' ? a.pt : P(NaN, NaN), P(-2, 0));
    const n = parseCoordinateInput('@1<-45', { last });
    nearPt(n?.kind === 'point' ? n.pt : P(NaN, NaN), P(1 + Math.SQRT1_2, 1 - Math.SQRT1_2));
  });
  it('parses a bare distance', () => {
    expect(parseCoordinateInput('2.5', { last: null })).toEqual({ kind: 'distance', d: 2.5 });
    expect(parseCoordinateInput('-.5', { last: null })).toEqual({ kind: 'distance', d: -0.5 });
  });
  it('rejects garbage and relative input without a last point', () => {
    for (const bad of ['', 'abc', '1,2,3', '1;2', '1,', '<5', '1<2<3', '@5', '1 2', '1e', '0x10']) expect(parseCoordinateInput(bad, { last })).toBeNull();
    expect(parseCoordinateInput('@1,1', { last: null })).toBeNull();
    expect(parseCoordinateInput('@1<30', { last: null })).toBeNull();
  });
});

describe('ortho / polar constraints', () => {
  it('applyOrtho snaps to the dominant axis', () => {
    expect(applyOrtho(P(0, 0), P(5, 1))).toEqual(P(5, 0));
    expect(applyOrtho(P(1, 1), P(0, -4))).toEqual(P(1, -4));
  });
  it('applyPolar rounds the angle and keeps the distance', () => {
    nearPt(applyPolar(P(0, 0), P(10, 1), 45), P(Math.hypot(10, 1), 0));
    const d = Math.hypot(1, 0.9);
    nearPt(applyPolar(P(0, 0), P(1, 0.9), 45), P(d * Math.SQRT1_2, d * Math.SQRT1_2));
  });
  it('pointAlong moves a distance toward a target', () => {
    nearPt(pointAlong(P(0, 0), P(3, 4), 10), P(6, 8));
    nearPt(pointAlong(P(1, 1), P(1, 1), 2), P(3, 1));
  });
});

describe('guide lines', () => {
  it('hold the cursor on the orthogonal lines through acquired points, and where two cross', () => {
    const acquired = [P(0, 0), P(5, 3)];
    const h = track(P(2, 0.04), acquired, 0.1)!;
    nearPt(h.point, P(2, 0));
    expect(h.guides[0].kind).toBe('h');
    const both = track(P(5.05, 0.03), acquired, 0.1)!;
    nearPt(both.point, P(5, 0));
    expect(both.guides.map((g) => g.kind)).toEqual(['h', 'v']);
    expect(track(P(2, 2), acquired, 0.1)).toBeNull();
  });
  it('polar angles from the base, and ortho lines meeting an acquired point\'s guide', () => {
    const p = track(P(3, 3.05), [], 0.1, { base: P(0, 0), polarStep: 45 })!;
    nearPt(p.point, P(Math.hypot(3, 3.05) * Math.SQRT1_2, Math.hypot(3, 3.05) * Math.SQRT1_2));
    expect(p.guides[0].kind).toBe('polar');
    const o = trackOnOrtho(P(4.96, 0), P(0, 0), [P(0, 0), P(5, 3)], 0.1)!;
    nearPt(o.point, P(5, 0));
  });
  it('an arc through three points runs from the first to the third by the second', () => {
    const a = arcThrough(P(1, 0), P(0, 1), P(-1, 0));
    expect(a?.type).toBe('arc');
    if (a?.type === 'arc') { nearPt(a.c, P(0, 0)); expect(a.start).toBeCloseTo(0, 9); expect(a.end).toBeCloseTo(Math.PI, 9); }
    expect(arcThrough(P(0, 0), P(1, 1), P(2, 2))).toBeNull();
  });
});
