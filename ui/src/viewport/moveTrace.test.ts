/** The move trace (decision 165): the readout, the aim's state and its colour, the trace's lifecycle through MOVE and COPY. */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Vec2 } from '../platform/module';

const engine = vi.hoisted(() => ({ execute: vi.fn(), undo: vi.fn(), query: vi.fn() }));
vi.mock('../engine/session', () => engine);

import { traceBegin, traceClass, traceEnd, traceOffset, traceReadout, traceState, useTrace } from './moveTrace';
import { cancel, feedPoint, startCommand, submit, useCad } from './cad/session';
import { useLive } from './liveInput';
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
  const free = { kind: 'free' as const, guides: [] };
  it('reads a snap, a polar or tracking guide, ortho, or free', () => {
    expect(traceState({ kind: 'point', mode: 'endpoint', guides: [] }, false)).toBe('snap');
    expect(traceState({ kind: 'point', mode: 'nearest', guides: [] }, true)).toBe('snap');
    expect(traceState({ kind: 'point', guides: [{ kind: 'polar' }] }, false)).toBe('polar');
    expect(traceState({ kind: 'point', guides: [{ kind: 'h' }] }, false)).toBe('tracking');
    expect(traceState({ kind: 'point', guides: [{ kind: 'v' }] }, true)).toBe('ortho');
    expect(traceState(free, true)).toBe('ortho');
    expect(traceState(free, false)).toBe('free');
    expect(traceState({ kind: 'grid', guides: [] }, false)).toBe('free');
    expect(traceState(null, true)).toBe('free');
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
