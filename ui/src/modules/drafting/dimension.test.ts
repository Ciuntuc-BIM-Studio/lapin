/** The dimension tool: a string of points is previewed and made as one dimension, one engine command. */

import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { Vec2 } from '../../platform/module';

const engine = vi.hoisted(() => ({ execute: vi.fn(), undo: vi.fn(), query: vi.fn(), useSession: Object.assign(() => null, { getState: () => ({ nodes: new Map() }), setState: () => undefined }) }));
vi.mock('../../engine/session', () => engine);

import { cancel, feedPoint, startCommand, submit, useCad } from '../../viewport/cad/session';
import drafting from './index';
import type { NodeDto } from '../../engine/protocol';

const P = (x: number, y: number): Vec2 => ({ x, y });
const adds = () => engine.execute.mock.calls.filter(([c]) => c === 'drafting.addDimension').map(([, a]) => a as Record<string, unknown>);

beforeEach(() => {
  engine.execute.mockReset();
  engine.execute.mockImplementation(() => Promise.resolve({ id: 'd' }));
  cancel(true);
  useCad.setState({ host: { viewId: 'v', pickDraft: () => null, pickAny: () => null, shapeOf: () => null }, history: [] });
});

describe('DIMLINEAR', () => {
  it('builds a string point by point and makes it once, on Enter', async () => {
    await startCommand('DLI');
    for (const p of [P(0, 0), P(3, 0), P(0, 2), P(7, 0), P(9, 0)]) await feedPoint(p);
    expect(adds()).toHaveLength(0);
    // U takes the last point back.
    await submit('U');
    await feedPoint(P(10, 0));
    await submit('');
    expect(adds()).toEqual([{ viewId: 'v', pts: [[0, 0], [3, 0], [7, 0], [10, 0]], at: [0, 2], dir: [1, 0] }]);
    expect(useCad.getState().step).toBeNull();
  });

  it('makes a single dimension at once after S', async () => {
    await startCommand('DIM');
    await submit('S');
    for (const p of [P(0, 0), P(0, 4), P(-1, 0)]) await feedPoint(p);
    expect(adds()).toEqual([{ viewId: 'v', pts: [[0, 0], [0, 4]], at: [-1, 0] }]);
  });
});

describe('dimension grips', () => {
  const node = { id: 'd', type: 'IfcAnnotation', name: 'Dimension', props: { predefinedType: 'DIMENSION', viewId: 'v', pts: [[0, 0], [3, 0], [7, 0]], at: [0, 2] } } as unknown as NodeDto;
  const grips = (n: NodeDto, view?: Parameters<NonNullable<typeof drafting.grips>[string]>[1]) => drafting.grips?.IfcAnnotation(n, view) ?? [];
  it('stand on each point as drawn, in each segment, and at the ends of the line; each is one command', () => {
    // The view draws the second point where its wall went (3 to 4).
    const mark = { id: 'd', segments: [], data: { points: [[0, 0], [4, 0], [7, 0]], line: [[0, 2], [4, 2], [7, 2]] } };
    const gs = grips(node, { mark });
    expect(gs.map((g) => g.key)).toEqual(['p0', 'p1', 'p2', 'e0', 'e1', 'l0', 'l1']);
    expect(gs[1].at).toEqual({ x: 4, y: 0 });
    expect(gs[1].step?.({ x: 5, y: 0 })).toEqual({ command: 'drafting.moveDimensionPoint', args: { id: 'd', index: 1, point: [5, 0] } });
    expect(gs[0].remove?.()).toEqual({ command: 'drafting.removeDimensionPoint', args: { id: 'd', index: 0 } });
    expect(gs[4].insert?.()).toEqual({ command: 'drafting.insertDimensionPoint', args: { id: 'd', index: 2, point: [5.5, 0] } });
    expect(gs[4].step?.({ x: 5, y: 0 })).toEqual({ command: 'drafting.insertDimensionPoint', args: { id: 'd', point: [5, 0] } });
    expect(gs[6].at).toEqual({ x: 7, y: 2 });
    expect(gs[6].step?.({ x: 7, y: 3 })).toEqual({ command: 'drafting.moveDimension', args: { id: 'd', at: [7, 3] } });
    expect(gs.every((g) => g.view === 'v')).toBe(true);
  });
  it('take no point out of a two-point dimension', () => {
    const two = { ...node, props: { ...node.props, pts: [[0, 0], [3, 0]] } } as NodeDto;
    expect(grips(two)[0].remove?.()).toBeNull();
  });
});
