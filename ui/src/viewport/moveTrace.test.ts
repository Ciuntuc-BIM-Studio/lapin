/** The move trace (decision 165): the readout, the aim's state and its colour, the trace's lifecycle through MOVE and COPY. */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Vec2 } from '../platform/module';

const engine = vi.hoisted(() => ({ execute: vi.fn(), undo: vi.fn(), query: vi.fn() }));
vi.mock('../engine/session', () => engine);

import { carryBegin, carryBy, carryEnd, carrying, traceBegin, traceClass, traceEnd, traceOffset, traceReadout, traceState, useTrace } from './moveTrace';
import { resolveAim } from './cad/aim';
import { cancel, feedPoint, startCommand, submit, useCad } from './cad/session';
import { liveBegin, liveEnd, useLive } from './liveInput';
import { useStandards } from '../engine/unitsStore';
import { useUi } from '../app/store';
import type { Shape } from './cad/shapes';

const P = (x: number, y: number): Vec2 => ({ x, y });

describe('the readout', () => {
  afterEach(() => useStandards.setState({ units: 'mm' }));

  it('measures Δx, Δy, the distance and the angle from +X counter-clockwise', () => {
    const o = traceOffset(P(1, 1), P(4, 5));
    expect([o.dx, o.dy, o.dist]).toEqual([3, 4, 5]);
    expect(o.ang).toBeCloseTo(53.1301, 4);
    expect(traceOffset(P(0, 0), P(0, 2)).ang).toBe(90);
    expect(traceOffset(P(0, 0), P(-1, 0)).ang).toBe(180);
    expect(traceOffset(P(0, 0), P(0, -3)).ang).toBe(270);
    // Nothing moved yet: no angle; a hair under 360 reads 0.
    expect(traceOffset(P(2, 2), P(2, 2))).toEqual({ dx: 0, dy: 0, dist: 0, ang: 0 });
    expect(traceOffset(P(0, 0), P(1, -1e-5)).ang).toBe(0);
  });

  it('shows lengths in the project unit and the angle to a tenth of a degree', () => {
    expect(traceReadout(P(0, 0), P(3, -4))).toEqual({ dx: '3000', dy: '-4000', dist: '5000', ang: '306.9' });
    useStandards.setState({ units: 'm' });
    const m = traceReadout(P(0, 0), P(1.25, 0));
    expect(m.dx).toMatch(/^1[.,]25/);
    expect(m.ang).toBe('0.0');
  });
});

describe('the aim’s state and its colour', () => {
  it('reads a snap, the constraint the aim says holds, or free', () => {
    expect(traceState({ kind: 'point', mode: 'endpoint' })).toBe('snap');
    expect(traceState({ kind: 'point', mode: 'nearest', lock: 'ortho' })).toBe('snap');
    expect(traceState({ kind: 'point', lock: 'polar' })).toBe('polar');
    expect(traceState({ kind: 'point', lock: 'tracking' })).toBe('tracking');
    expect(traceState({ kind: 'free', lock: 'ortho' })).toBe('ortho');
    expect(traceState({ kind: 'grid', lock: 'ortho' })).toBe('ortho');
    expect(traceState({ kind: 'free' })).toBe('free');
    expect(traceState({ kind: 'grid' })).toBe('free');
    expect(traceState(null)).toBe('free');
  });

  // The aim's rule, as the views resolve it: what holds the point is said, not guessed from where it lands.
  const aimAt = (raw: Vec2, o: Partial<Parameters<typeof resolveAim>[1]> = {}) =>
    resolveAim(raw, { base: P(0, 0), ortho: false, polar: false, snap: null, acquired: [], tol: 0.1, ...o });
  it('says ortho only while ortho is on or Shift is held, and only from a base', () => {
    const on = aimAt(P(3, 0.7), { ortho: true });
    expect(on.point).toEqual(P(3, 0));
    expect(traceState(on)).toBe('ortho');
    // A grid point that happens to be square to the base, ortho off: free, not blue.
    const grid = aimAt(P(2.9, 0.2), { grid: 0.5 });
    expect(grid.point).toEqual(P(3, 0));
    expect(traceState(grid)).toBe('free');
    expect(traceState(aimAt(P(2.9, 0.2), { grid: 0.5, ortho: true }))).toBe('ortho');
    // Close to the base's own horizontal, ortho off: held by that tracking guide, so the tracking colour.
    expect(traceState(aimAt(P(2.98, 0.02), { grid: 0.5 }))).toBe('tracking');
    // No base: nothing to be square to.
    expect(traceState(aimAt(P(3, 0.7), { ortho: true, base: null }))).toBe('free');
  });
  it('says polar on a polar angle, tracking on a guide through an acquired point, snap over both', () => {
    const polar = aimAt(P(2, 2.05), { polar: true });
    expect(traceState(polar)).toBe('polar');
    expect(polar.point.x).toBeCloseTo(polar.point.y, 9);
    expect(traceState(aimAt(P(2, 2.3), { polar: true }))).toBe('free');
    expect(traceState(aimAt(P(5.03, 2.3), { acquired: [P(5, 9)] }))).toBe('tracking');
    // Ortho with a guide crossing its line: the ortho line holds.
    expect(traceState(aimAt(P(5.03, 0.4), { ortho: true, acquired: [P(5, 9)] }))).toBe('ortho');
    expect(traceState(aimAt(P(2, 2.05), { polar: true, snap: { point: P(2, 2), mode: 'endpoint' } }))).toBe('snap');
    // The nearest snap gives way to a guide.
    expect(traceState(aimAt(P(2, 2.05), { polar: true, snap: { point: P(2, 2.04), mode: 'nearest' } }))).toBe('polar');
  });
  it('gives ortho and polar the locked colour', () => {
    expect(traceClass('snap')).toBe('trace-snap');
    expect(traceClass('ortho')).toBe('trace-locked');
    expect(traceClass('polar')).toBe('trace-locked');
    expect(traceClass('tracking')).toBe('trace-tracking');
    expect(traceClass('free')).toBe('trace-free');
  });
});

describe('the trace’s lifecycle', () => {
  const square: Shape = { type: 'polyline', pts: [P(0, 0), P(1, 0), P(1, 1), P(0, 1)], closed: true };
  beforeEach(() => {
    engine.execute.mockReset();
    engine.execute.mockImplementation(() => new Promise((r) => setTimeout(() => r({ ids: ['x'] }), 5)));
    cancel(true);
    traceEnd();
    useUi.setState({ selected: ['wall'], selection: 'wall' });
    useCad.setState({ host: { viewId: 'v', pickDraft: () => null, pickAny: () => null, shapeOf: () => null, outlinesOf: (id) => (id === 'wall' ? [square] : []) }, history: [] });
  });

  it('MOVE: no trace before the base point; then the base and the ghost where it was; gone once moved', async () => {
    await startCommand('MOVE');
    expect(useTrace.getState().trace).toBeNull();
    await feedPoint(P(1, 1));
    const t = useTrace.getState().trace!;
    expect(t.owner).toBe('cad');
    expect(t.base).toEqual(P(1, 1));
    expect(t.ghost).toEqual([square]);
    expect(t.carry).toBe(false);
    expect(useLive.getState().delta).toBe(true);
    // The preview carries the ghost to the cursor.
    const pv = useCad.getState().step!.preview!(P(3, 1)) as Extract<Shape, { type: 'polyline' }>[];
    expect(pv[0].pts[0]).toEqual(P(2, 0));
    await feedPoint(P(3, 1));
    expect(engine.execute).toHaveBeenCalledWith('element.transform', { ids: ['wall'], op: 'move', by: [2, 0] });
    expect(useTrace.getState().trace).toBeNull();
    expect(useLive.getState().delta).toBe(false);
  });

  it('COPY keeps its trace between copies; Esc takes it away', async () => {
    await startCommand('COPY');
    await feedPoint(P(0, 0));
    await feedPoint(P(2, 0));
    expect(useTrace.getState().trace?.base).toEqual(P(0, 0));
    cancel();
    expect(useTrace.getState().trace).toBeNull();
  });

  it('Enter ends a COPY and its trace', async () => {
    await startCommand('COPY');
    await feedPoint(P(0, 0));
    await submit('');
    expect(useTrace.getState().trace).toBeNull();
  });

  it('a drawing step (LINE) has no trace, and another owner’s end leaves it', async () => {
    await startCommand('LINE');
    await feedPoint(P(0, 0));
    expect(useTrace.getState().trace).toBeNull();
    expect(useLive.getState().delta).toBe(false);
    cancel(true);
    traceBegin('grip', P(1, 2), [square], true);
    traceEnd('cad');
    expect(useTrace.getState().trace?.owner).toBe('grip');
    traceEnd('grip');
    expect(useTrace.getState().trace).toBeNull();
  });
});

describe('dragging a selection by its body', () => {
  const square: Shape = { type: 'polyline', pts: [P(0, 0), P(1, 0), P(1, 1), P(0, 1)], closed: true };
  beforeEach(() => { traceEnd(); liveEnd(); });

  it('shows the trace from the press point, carrying the ghost, with the readout’s Δx and Δy', () => {
    const commit = vi.fn();
    carryBegin(P(1, 1), [square], commit);
    const t = useTrace.getState().trace!;
    expect([t.owner, t.base, t.ghost, t.carry]).toEqual(['drag', P(1, 1), [square], true]);
    expect(carrying()).toBe(true);
    expect(useLive.getState()).toMatchObject({ owner: 'drag', base: P(1, 1), delta: true });
    // Enter with a typed length: the live input's commit is the drag's.
    useLive.getState().commit!(P(3, 1));
    expect(commit).toHaveBeenCalledWith(P(3, 1));
  });

  it('lets go at the cursor, or at the typed length along the cursor’s direction; nothing when it did not move', () => {
    carryBegin(P(1, 1), [square], () => {});
    expect(carryBy(P(1, 1), P(4, 5))).toEqual([3, 4]);
    expect(carryBy(P(1, 1), P(1, 1))).toBeNull();
    useLive.setState({ len: '2000' });
    const by = carryBy(P(1, 1), P(4, 1))!;
    expect(by[0]).toBeCloseTo(2, 9);
    expect(by[1]).toBeCloseTo(0, 9);
  });

  it('ends (release or Esc) without touching another owner’s live input', () => {
    carryBegin(P(0, 0), [square], () => {});
    carryEnd();
    expect(useTrace.getState().trace).toBeNull();
    expect(carrying()).toBe(false);
    expect(useLive.getState().owner).toBeNull();
    liveBegin('tool', P(0, 0), () => {});
    useLive.setState({ len: '500' });
    // A tool's typed length is not the drag's.
    expect(carryBy(P(0, 0), P(3, 0))).toEqual([3, 0]);
    carryEnd();
    expect(useLive.getState().owner).toBe('tool');
  });
});
