import { describe, expect, it } from 'vitest';
import { parallelHold, relatables, tangentHold } from './relations';
import { resolveAim } from './aim';
import { inferConstraints, type PlacedPoint } from './recorded';
import type { Shape } from './shapes';

const P = (x: number, y: number) => ({ x, y });
const drafts = new Map<string, Shape[]>([
  ['wall', [{ type: 'line', a: P(0, 0), b: P(10, 5) }]],
  ['ring', [{ type: 'circle', c: P(20, 0), r: 2 }]],
]);
const { sides, curves } = relatables(drafts);

describe('parallel and tangent while aiming', () => {
  it('locks a line to a nearby drafted one when its direction is within the tolerance', () => {
    const r = parallelHold(P(0, 3), P(6, 6.1), sides, 3, 0.1)!;
    expect(r.lock).toBe('parallel');
    expect(r.to).toEqual({ shape: 'wall', part: 'whole' });
    expect((r.point.y - 3) / (r.point.x - 0)).toBeCloseTo(0.5, 9);
  });
  it('leaves a direction off by more than the tolerance, and a line too far from the cursor', () => {
    expect(parallelHold(P(0, 3), P(6, 7), sides, 5, 0.1)).toBeNull();
    expect(parallelHold(P(0, 40), P(6, 43), sides, 3, 0.1)).toBeNull();
  });
  it('does not call a continuation of the same line parallel', () => {
    expect(parallelHold(P(10, 5), P(14, 7), sides, 3, 0.1)).toBeNull();
  });
  it('holds the tangent at a base on a circle', () => {
    const r = tangentHold(P(20, 2), P(23, 2.05), curves, 0.1)!;
    expect(r.lock).toBe('tangent');
    expect(r.point.y).toBeCloseTo(2, 9);
  });
  it('snaps to the point where a line from outside just touches the circle', () => {
    const base = P(26, 0);
    const a = Math.acos(2 / 6);
    const q = P(20 + 2 * Math.cos(a), 2 * Math.sin(a));
    const r = tangentHold(base, P(q.x + 0.02, q.y - 0.02), curves, 0.1)!;
    expect(r.point.x).toBeCloseTo(q.x, 9);
    expect(r.point.y).toBeCloseTo(q.y, 9);
    expect(tangentHold(base, P(q.x + 1, q.y), curves, 0.1)).toBeNull();
  });
  it('resolveAim takes it after the object snaps and never with ortho on', () => {
    const c = { base: P(0, 3), ortho: false, polar: false, snap: null, acquired: [], tol: 0.1, relate: { sides, curves, reach: 3 } };
    const a = resolveAim(P(6, 6.1), c);
    expect(a.lock).toBe('parallel');
    expect(a.guides[0].kind).toBe('parallel');
    expect(resolveAim(P(6, 6.1), { ...c, ortho: true }).lock).toBe('ortho');
    expect(resolveAim(P(6, 6.1), { ...c, snap: { point: P(6, 6.1), mode: 'endpoint' } }).mode).toBe('endpoint');
  });
});

const placed = (p: { x: number; y: number }, inference: PlacedPoint['inference'] = null, extra: Partial<PlacedPoint> = {}): PlacedPoint => ({ p, inference, free: false, ...extra });
const world = new Map<string, Shape>([['wall', drafts.get('wall')![0]], ['ring', drafts.get('ring')![0]], ['arc', { type: 'arc', c: P(0, 0), r: 2, start: 0, end: Math.PI / 2 }]]);

describe('parallel and tangent recorded', () => {
  it('a side held parallel is a parallel constraint', () => {
    const s: Shape = { type: 'line', a: P(0, 8), b: P(6, 11) };
    const r = inferConstraints({ id: 'n', shape: s }, [placed(P(0, 8)), placed(P(6, 11), { lock: 'parallel', to: { shape: 'wall', part: 'whole' } })], world);
    expect(r).toEqual([{ kind: 'parallel', refs: [{ shape: 'n', part: 'whole' }, { shape: 'wall', part: 'whole' }] }]);
  });
  it('a parallel to a horizontal line is not also recorded as horizontal', () => {
    const s: Shape = { type: 'line', a: P(0, 8), b: P(6, 8) };
    const r = inferConstraints({ id: 'n', shape: s }, [placed(P(0, 8)), placed(P(6, 8), { lock: 'parallel', to: { shape: 'wall', part: 'whole' } })], world);
    expect(r.map((c) => c.kind)).toEqual(['parallel']);
  });
  it('a line ending where it touches a circle is tangent to it and on it', () => {
    const a = Math.acos(2 / 6);
    const q = P(20 + 2 * Math.cos(a), 2 * Math.sin(a));
    const r = inferConstraints({ id: 'n', shape: { type: 'line', a: P(26, 0), b: q } }, [placed(P(26, 0)), placed(q, { lock: 'tangent', to: { shape: 'ring', part: 'whole' } })], world);
    expect(r).toEqual([
      { kind: 'pointOn', refs: [{ shape: 'n', part: 'end' }, { shape: 'ring', part: 'whole' }] },
      { kind: 'tangent', refs: [{ shape: 'n', part: 'whole' }, { shape: 'ring', part: 'whole' }] },
    ]);
  });
  it('a line begun at an arc end and held tangent is tangent end to end', () => {
    const r = inferConstraints({ id: 'n', shape: { type: 'line', a: P(0, 2), b: P(-3, 2) } }, [placed(P(0, 2), { mode: 'endpoint', shapeId: 'arc' }), placed(P(-3, 2), { lock: 'tangent', to: { shape: 'arc', part: 'whole' } })], world);
    expect(r.map((c) => c.kind)).toEqual(['coincident', 'tangent']);
    expect(r[1].refs).toEqual([{ shape: 'n', part: 'start' }, { shape: 'arc', part: 'end' }]);
  });
  it('a rectangle typed as @w,h keeps both sizes as driving distances', () => {
    const s: Shape = { type: 'polyline', pts: [P(0, 0), P(4, 0), P(4, 3), P(0, 3)], closed: true };
    const r = inferConstraints({ id: 'n', shape: s }, [placed(P(0, 0)), placed(P(4, 3), null, { typed: { size: { w: 4, h: 3 } } })], world, { hints: { rect: true } });
    expect(r.filter((c) => c.kind === 'distance')).toEqual([
      { kind: 'distance', refs: [{ shape: 'n', part: 'edge:0' }], value: 4 },
      { kind: 'distance', refs: [{ shape: 'n', part: 'edge:1' }], value: 3 },
    ]);
  });
});
