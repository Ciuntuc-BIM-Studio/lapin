/** ALIGN (Revit's Align, forum topics 109, 170, 171): the reference line, then the parallel line on the element to move. */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Vec2 } from '../../platform/module';

const engine = vi.hoisted(() => ({ execute: vi.fn(), undo: vi.fn(), useSession: { getState: vi.fn(() => ({ nodes: new Map(), error: null })), setState: vi.fn() } }));
vi.mock('../../engine/session', () => engine);

import { cancel, feedPoint, startCommand, submit, useCad } from './session';
import { nearestLine, parallel, useAlign } from './align';

const P = (x: number, y: number): Vec2 => ({ x, y });
const nodes = new Map<string, unknown>([
  ['w1', { id: 'w1', type: 'IfcWall', props: { start: [0, 0], end: [0, 6] } }],
  ['w2', { id: 'w2', type: 'IfcWall', props: { start: [3, 1], end: [3, 4] } }],
  ['c1', { id: 'c1', type: 'IfcColumn', props: { at: [5, 5] } }],
  ['g', { id: 'g', type: 'IfcGrid', props: {} }],
]);
// The plan's modelling lines: each wall's cut, 0.3 m thick round its axis; the column's square.
const outlines: Record<string, unknown[]> = {
  w1: [{ type: 'polyline', pts: [P(-0.15, 0), P(0.15, 0), P(0.15, 6), P(-0.15, 6)], closed: true }],
  w2: [{ type: 'polyline', pts: [P(2.85, 1), P(3.15, 1), P(3.15, 4), P(2.85, 4)], closed: true }],
  c1: [{ type: 'polyline', pts: [P(4.8, 4.8), P(5.2, 4.8), P(5.2, 5.2), P(4.8, 5.2)], closed: true }],
  'g#A': [{ type: 'line', a: P(8, -1), b: P(8, 10) }],
};
let under: string | null = null;
const pickAt = (id: string | null) => { under = id; };

beforeEach(() => {
  engine.execute.mockReset();
  engine.execute.mockImplementation(() => new Promise((r) => setTimeout(() => r({ ids: ['x'] }), 5)));
  engine.useSession.getState.mockImplementation(() => ({ nodes, error: null }) as never);
  cancel(true);
  useAlign.setState({ multiple: false, lock: false });
  useCad.setState({
    host: { viewId: 'v', isPlan: () => true, pickDraft: () => null, pickAny: () => under, shapeOf: () => null, outlinesOf: (id: string) => (outlines[id] ?? []) as never },
    history: [],
  });
});
afterEach(() => { engine.useSession.getState.mockImplementation(() => ({ nodes: new Map(), error: null })); });

describe('ALIGN', () => {
  it('brings the face picked on a wall onto the face picked on another, then asks for a new reference', async () => {
    await startCommand('AL');
    pickAt('w1'); await feedPoint(P(0.15, 3));
    pickAt('w2'); await feedPoint(P(2.86, 2));
    expect(engine.execute).toHaveBeenCalledWith('element.align', {
      ids: ['w2'], reference: [[0.15, 0], [0.15, 6]], line: [[2.85, 4], [2.85, 1]], referenceId: 'w1',
    });
    expect(useCad.getState().step?.prompt).toMatch(/reference line/);
  });

  it('takes a wall\'s axis when the click is nearer it than a face', async () => {
    await startCommand('ALIGN');
    pickAt('g#A'); await feedPoint(P(8, 2));
    pickAt('w2'); await feedPoint(P(3.01, 2));
    expect(engine.execute).toHaveBeenCalledWith('element.align', expect.objectContaining({ ids: ['w2'], reference: [[8, -1], [8, 10]], line: [[3, 1], [3, 4]], referenceId: 'g#A' }));
  });

  it('turns a line not parallel to the reference onto it in a plan, about the point picked (forum topic 234)', async () => {
    await startCommand('ALIGN');
    pickAt('w1'); await feedPoint(P(0.15, 3));
    pickAt('c1'); await feedPoint(P(5, 4.81));
    expect(engine.execute).toHaveBeenCalledWith('element.align', {
      ids: ['c1'], reference: [[0.15, 0], [0.15, 6]], line: [[4.8, 4.8], [5.2, 4.8]], referenceId: 'w1', rotate: true, pivot: [5, 4.81],
    });
  });

  it('in a section or an elevation, refuses a line not parallel to the reference and waits for another', async () => {
    const host = useCad.getState().host!;
    useCad.setState({ host: { ...host, isPlan: () => false, frameAt: () => ({ origin: [0, 0, 0], right: [1, 0, 0], up: [0, 0, 1] }) as never } });
    await startCommand('ALIGN');
    pickAt('w1'); await feedPoint(P(0.15, 3));
    pickAt('c1'); await feedPoint(P(5, 4.81));
    expect(engine.execute).not.toHaveBeenCalled();
    expect(useCad.getState().step?.prompt).toMatch(/line on the element/);
    await feedPoint(P(4.81, 5));
    expect(engine.execute).toHaveBeenCalledWith('element.align', expect.objectContaining({ ids: ['c1'], line: [[4.8, 5.2], [4.8, 4.8]] }));
  });

  it('with Multiple keeps the reference for one element after another until Enter; Lock goes only to a grid axis', async () => {
    await startCommand('ALIGN');
    await submit('M');
    await submit('L');
    pickAt('g#A'); await feedPoint(P(8, 2));
    pickAt('w1'); await feedPoint(P(0, 2));
    pickAt('w2'); await feedPoint(P(3, 2));
    expect(engine.execute).toHaveBeenCalledTimes(2);
    expect(engine.execute).toHaveBeenLastCalledWith('element.align', expect.objectContaining({ ids: ['w2'], referenceId: 'g#A', lock: true }));
    expect(useCad.getState().step?.prompt).toMatch(/next element/);
    await submit('');
    expect(useCad.getState().command).toBeNull();
    // A wall as the reference: aligned, not locked.
    await startCommand('ALIGN');
    pickAt('w1'); await feedPoint(P(0.15, 3));
    pickAt('w2'); await feedPoint(P(2.86, 2));
    expect(engine.execute).toHaveBeenLastCalledWith('element.align', expect.not.objectContaining({ lock: true }));
  });

  it('the reference itself is not moved', async () => {
    await startCommand('ALIGN');
    pickAt('w1'); await feedPoint(P(0.15, 3));
    await feedPoint(P(-0.15, 3));
    expect(engine.execute).not.toHaveBeenCalled();
  });
});

describe('nearestLine and parallel', () => {
  it('take the straight piece nearest the click, and parallel either way round', () => {
    expect(nearestLine([{ type: 'circle', c: P(0, 0), r: 1 } as never, { type: 'line', a: P(0, 2), b: P(4, 2) }], P(1, 1.9))).toEqual([P(0, 2), P(4, 2)]);
    expect(parallel([P(0, 0), P(1, 0)], [P(5, 3), P(2, 3)])).toBe(true);
    expect(parallel([P(0, 0), P(1, 0)], [P(0, 0), P(1, 0.01)])).toBe(false);
  });
});
