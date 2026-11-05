/** The command line's state machine, with the engine mocked: steps, keywords, and points fed faster than the engine replies. */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Vec2 } from '../../platform/module';

const engine = vi.hoisted(() => ({ execute: vi.fn(), undo: vi.fn() }));
vi.mock('../../engine/session', () => engine);

import { cancel, feedPoint, startCommand, submit, useCad } from './session';
import { useStandards } from '../../engine/unitsStore';
import { useUi } from '../../app/store';

const P = (x: number, y: number): Vec2 => ({ x, y });
/** The segments drawn, as "x,y-x,y". */
const drawn = () => engine.execute.mock.calls
  .filter(([cmd]) => cmd === 'drafting.draw')
  .flatMap(([, args]) => (args as { shapes: { type: string; a?: number[]; b?: number[] }[] }).shapes)
  .map((s) => `${s.a?.join(',')}-${s.b?.join(',')}`);

beforeEach(() => {
  engine.execute.mockReset();
  engine.undo.mockReset();
  // The engine replies a few ticks later, as over the socket.
  engine.execute.mockImplementation(() => new Promise((r) => setTimeout(() => r({ ids: ['x'] }), 5)));
  engine.undo.mockImplementation(() => new Promise((r) => setTimeout(() => r(true), 5)));
  cancel(true);
  useCad.setState({ host: { viewId: 'v', pickDraft: () => null, pickAny: () => null, shapeOf: () => null }, history: [] });
});

async function lineThrough(...pts: Vec2[]) {
  await startCommand('LINE');
  for (const p of pts) await feedPoint(p);
}

describe('LINE', () => {
  it('chains points fed in the same tick: a→b, b→c, not a→b twice', async () => {
    await startCommand('LINE');
    await feedPoint(P(0, 0));
    await Promise.all([feedPoint(P(1, 0)), feedPoint(P(1, 1))]);
    expect(drawn()).toEqual(['0,0-1,0', '1,0-1,1']);
    expect(useCad.getState().step?.base).toEqual(P(1, 1));
  });

  it('U takes back the last segment and continues from the point before', async () => {
    await lineThrough(P(0, 0), P(1, 0), P(1, 1));
    await submit('U');
    expect(engine.undo).toHaveBeenCalledTimes(1);
    expect(useCad.getState().step?.base).toEqual(P(1, 0));
    await feedPoint(P(2, 0));
    expect(drawn()).toEqual(['0,0-1,0', '1,0-1,1', '1,0-2,0']);
  });

  it('a refused segment is not taken: the line goes on from where it was and U undoes nothing of before', async () => {
    await lineThrough(P(0, 0));
    engine.execute.mockImplementationOnce(() => Promise.resolve(null));
    await feedPoint(P(1, 0));
    expect(useCad.getState().step?.base).toEqual(P(0, 0));
    await submit('U');
    expect(engine.undo).not.toHaveBeenCalled();
  });

  it('C closes back to the first point and ends', async () => {
    await lineThrough(P(0, 0), P(1, 0), P(1, 1));
    await submit('C');
    expect(drawn()).toEqual(['0,0-1,0', '1,0-1,1', '1,1-0,0']);
    expect(useCad.getState().command).toBeNull();
  });

  it('Enter ends the command; Enter again repeats it', async () => {
    await lineThrough(P(0, 0), P(1, 0));
    await submit('');
    expect(useCad.getState().command).toBeNull();
    expect(useCad.getState().step).toBeNull();
    await submit('');
    expect(useCad.getState().command).toBe('LINE');
    expect(useCad.getState().step?.prompt).toBe('First point');
  });

  it('a typed relative point continues from the last one', async () => {
    await lineThrough(P(0, 0));
    // In the project's unit (millimetres): 2 m along x.
    await submit('@2000,0');
    expect(drawn()).toEqual(['0,0-2,0']);
  });

  it('Escape while the engine works does not bring the command back', async () => {
    await lineThrough(P(0, 0));
    const pending = feedPoint(P(1, 0));
    cancel();
    await pending;
    expect(useCad.getState().command).toBeNull();
    expect(useCad.getState().step).toBeNull();
  });
});

describe('PLINE', () => {
  it('collects points and draws one polyline on Enter', async () => {
    await startCommand('PLINE');
    await Promise.all([feedPoint(P(0, 0)), feedPoint(P(1, 0)), feedPoint(P(1, 1))]);
    await submit('');
    const shapes = engine.execute.mock.calls.filter(([c]) => c === 'drafting.draw').map(([, a]) => (a as { shapes: unknown[] }).shapes[0]);
    expect(shapes).toEqual([{ type: 'polyline', pts: [[0, 0], [1, 0], [1, 1]], closed: false }]);
  });
});

describe('typed values and the project\'s unit', () => {
  const offsets = () => engine.execute.mock.calls.filter(([c]) => c === 'drafting.offset').map(([, a]) => a as Record<string, unknown>);
  const pickable = () => useCad.setState({ host: { viewId: 'v', pickDraft: () => ({ id: 'l1', shape: { type: 'line', a: P(0, 0), b: P(10, 0) } }), pickAny: () => null, shapeOf: () => null } });
  afterEach(() => { useStandards.setState({ units: 'mm' }); useUi.setState({ selected: [] }); });

  it('reads an OFFSET distance in millimetres (500 is half a metre), with its own unit or in feet and inches, and shows it so', async () => {
    useStandards.setState({ units: 'mm' });
    pickable();
    await startCommand('OFFSET');
    await submit('500');
    expect(useCad.getState().offset).toBeCloseTo(0.5, 9);
    expect(useCad.getState().step?.prompt).toContain('500 mm');
    await submit('2m');
    expect(useCad.getState().offset).toBeCloseTo(2, 9);
    await submit('1500+250');
    expect(useCad.getState().offset).toBeCloseTo(1.75, 9);
    useStandards.setState({ units: 'ft-in' });
    await submit('1\'6"');
    expect(useCad.getState().offset).toBeCloseTo(0.4572, 9);
    await feedPoint(P(1, 1));
    await feedPoint(P(2, 3));
    expect(offsets()[0]).toEqual({ id: 'l1', side: [2, 3], distance: useCad.getState().offset });
  });

  it('offsets Through a point picked, again until Enter', async () => {
    pickable();
    await startCommand('OFFSET');
    await submit('T');
    expect(useCad.getState().step?.prompt).toContain('through a point');
    await feedPoint(P(1, 0));
    await feedPoint(P(3, 0.75));
    await feedPoint(P(1, 0));
    await feedPoint(P(3, -2));
    expect(offsets()).toEqual([{ id: 'l1', side: [3, 0.75], through: true }, { id: 'l1', side: [3, -2], through: true }]);
    await submit('');
    expect(useCad.getState().step).toBeNull();
    useCad.setState({ offsetThrough: false });
  });

  it('never reads an angle or a factor in the project\'s unit: ROTATE 90 is 90°, SCALE 2 is twice, in millimetres too', async () => {
    useStandards.setState({ units: 'mm' });
    useUi.setState({ selected: ['a'] });
    await startCommand('ROTATE');
    await feedPoint(P(0, 0));
    await submit('90');
    await startCommand('SCALE');
    await feedPoint(P(0, 0));
    await submit('2,5');
    const calls = engine.execute.mock.calls.map(([c, a]) => [c, a] as [string, Record<string, unknown>]);
    expect(calls.find(([c]) => c === 'element.transform')?.[1].angle).toBe(90);
    expect(calls.find(([c]) => c === 'drafting.transform')?.[1].factor).toBe(2.5);
  });
});
