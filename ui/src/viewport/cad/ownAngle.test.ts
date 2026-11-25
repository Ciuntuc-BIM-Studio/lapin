import { describe, expect, it } from 'vitest';
import { resolveAim, type AimInput } from './aim';
import { axisGrips, outlineGrips } from '../gripKit';
import { ownGuideOf } from '../gripLock';

const P = (x: number, y: number) => ({ x, y });
const a = (37 * Math.PI) / 180;
const dir = P(Math.cos(a), Math.sin(a));
const along = { from: P(0, 0), dir };
const base = P(5 * dir.x, 5 * dir.y);
const c: AimInput = { base, ortho: false, polar: false, snap: null, acquired: [], tol: 0.1 };
const cross = (p: { x: number; y: number }) => p.x * dir.y - p.y * dir.x;

describe('aim with an end grip kept on its own line (decision 865)', () => {
  it('projects a free cursor onto the line and draws the guide', () => {
    const r = resolveAim(P(7, 2), { ...c, along: { ...along, locked: true } });
    expect(cross(r.point)).toBeCloseTo(0, 9);
    expect(r.lock).toBe('own');
    expect(r.guides[0].kind).toBe('own');
  });
  it('projects a point snap onto the line', () => {
    const r = resolveAim(P(7, 2), { ...c, along: { ...along, locked: true }, snap: { point: P(6, 6), mode: 'endpoint' } });
    expect(cross(r.point)).toBeCloseTo(0, 9);
    expect(r.point.x).toBeCloseTo((6 * dir.x + 6 * dir.y) * dir.x, 9);
  });
  it('takes where the element\'s line meets a line snap', () => {
    const wall = { type: 'line' as const, a: P(8, -10), b: P(8, 10) };
    const r = resolveAim(P(7.9, 5), { ...c, along: { ...along, locked: true }, snap: { point: P(8, 5), mode: 'nearest', shape: wall } });
    expect(r.point.x).toBeCloseTo(8, 9);
    expect(r.point.y).toBeCloseTo(8 * Math.tan(a), 9);
  });
  it('wins over Shift\'s ortho', () => {
    const r = resolveAim(P(7, 2), { ...c, ortho: true, along: { ...along, locked: true } });
    expect(cross(r.point)).toBeCloseTo(0, 9);
    expect(r.lock).toBe('own');
  });
  it('offers the own angle as a soft guide with polar on, not when it is off or far', () => {
    const near = P(7 * dir.x - 0.03 * dir.y, 7 * dir.y + 0.03 * dir.x);
    const soft = { ...c, polar: true, polarStep: 90, along: { ...along, locked: false } };
    const r = resolveAim(near, soft);
    expect(r.guides.some((g) => g.kind === 'own')).toBe(true);
    expect(cross(r.point)).toBeCloseTo(0, 9);
    expect(resolveAim(near, { ...soft, polar: false }).guides.some((g) => g.kind === 'own')).toBe(false);
    expect(resolveAim(P(7, 0.5), soft).guides.some((g) => g.kind === 'own')).toBe(false);
  });
});

describe('grips that know their own line', () => {
  const step = (s: { x: number; y: number }, e: { x: number; y: number }) => ({ command: 'x', args: { s, e } });
  it('fills `along` on the ends, pointing from the other end, and not on the move grip', () => {
    const g = axisGrips(P(0, 0), P(4, 3), step);
    const end = g.find((x) => x.key === 'end')!, start = g.find((x) => x.key === 'start')!, move = g.find((x) => x.key === 'move')!;
    expect(end.along).toEqual({ from: P(0, 0), dir: P(0.8, 0.6) });
    expect(start.along!.from).toEqual(P(4, 3));
    expect(start.along!.dir.x).toBeCloseTo(-0.8, 12);
    expect(move.along).toBeUndefined();
  });
  it('gives no line to a zero-length axis', () => {
    expect(axisGrips(P(1, 1), P(1, 1), step).every((g) => !g.along)).toBe(true);
  });
  it('locks an outline vertex to the edge before it', () => {
    const v = outlineGrips({ pts: [P(0, 0), P(4, 0), P(4, 3), P(0, 3)] }, (o) => ({ command: 'o', args: { n: o.pts.length } })).filter((g) => g.kind === 'vertex');
    expect(v[1].along).toEqual({ from: P(0, 0), dir: P(1, 0) });
    expect(v[0].along).toEqual({ from: P(0, 3), dir: P(0, -1) });
  });
  it('the soft guide and the lock are offered for an end grip only, and not for a copy or Alt-moved', () => {
    const end = axisGrips(P(0, 0), P(4, 3), step)[1];
    expect(ownGuideOf(end, { ctrl: true, alt: false, copy: false })).toEqual({ ...end.along!, locked: true });
    expect(ownGuideOf(end, { ctrl: false, alt: false, copy: false })).toEqual({ ...end.along!, locked: false });
    // A copy grip (Ctrl on a move grip) is unaffected.
    const move = axisGrips(P(0, 0), P(4, 3), step)[2];
    expect(ownGuideOf(move, { ctrl: true, alt: false, copy: true })).toBeNull();
    // Alt+Ctrl still moves one element only: no lock is asked of it.
    expect(ownGuideOf(end, { ctrl: true, alt: true, copy: false })).toEqual({ ...end.along!, locked: true });
    expect(ownGuideOf({ key: 'x', at: P(0, 0) }, { ctrl: true, alt: false, copy: false })).toBeNull();
  });
});
