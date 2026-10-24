/** The command line's state machine, with the engine mocked: steps, keywords, and points fed faster than the engine replies. */

import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { Vec2 } from '../../platform/module';

const engine = vi.hoisted(() => ({ execute: vi.fn(), undo: vi.fn() }));
vi.mock('../../engine/session', () => engine);

import { cancel, feedPoint, startCommand, submit, useCad } from './session';

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
    await submit('@2,0');
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
