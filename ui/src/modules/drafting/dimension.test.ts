/** The dimension tool: a string of points is previewed and made as one dimension, one engine command. */

import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { Vec2 } from '../../platform/module';

const engine = vi.hoisted(() => ({ execute: vi.fn(), undo: vi.fn(), query: vi.fn(), useSession: Object.assign(() => null, { getState: () => ({ nodes: new Map() }), setState: () => undefined }) }));
vi.mock('../../engine/session', () => engine);

import { cancel, feedPoint, finishStep, startCommand, submit, useCad } from '../../viewport/cad/session';
import { isSamePoint, pickDirection, segmentLabels, setContinuous, useDimTool } from './dimensionTool';
import { useUi } from '../../app/store';
import drafting from './index';
import type { NodeDto } from '../../engine/protocol';

const P = (x: number, y: number): Vec2 => ({ x, y });
const adds = () => engine.execute.mock.calls.filter(([c]) => c === 'drafting.addDimension').map(([, a]) => a as Record<string, unknown>);

beforeEach(() => {
  engine.execute.mockReset();
  engine.execute.mockImplementation(() => Promise.resolve({ id: 'd' }));
  cancel(true);
  setContinuous(false);
  useUi.setState({ notice: null });
  useCad.setState({ host: { viewId: 'v', pickDraft: () => null, pickAny: () => null, shapeOf: () => null }, history: [] });
});

describe('DIMLINEAR', () => {
  it('is single by default and asks for the next dimension after each', async () => {
    await startCommand('DLI');
    expect(useCad.getState().step?.prompt).toContain('First point');
    for (const p of [P(0, 0), P(3, 0), P(0, 2)]) await feedPoint(p);
    expect(adds()).toEqual([{ viewId: 'v', pts: [[0, 0], [3, 0]], at: [0, 2], dir: [1, 0] }]);
    expect(useCad.getState().step?.prompt).toContain('First point');
    for (const p of [P(0, 0), P(0, 4), P(-1, 2)]) await feedPoint(p);
    expect(adds()).toHaveLength(2);
    expect(adds()[1]).toMatchObject({ pts: [[0, 0], [0, 4]], dir: [0, 1] });
    cancel();
    expect(adds()).toHaveLength(2);
  });

  it('builds a string point by point when Continuous is on, and makes it once, on Enter', async () => {
    await startCommand('DLI');
    await submit('C');
    expect(useDimTool.getState().continuous).toBe(true);
    for (const p of [P(0, 0), P(3, 0), P(0, 2), P(7, 0), P(9, 0)]) await feedPoint(p);
    expect(adds()).toHaveLength(0);
    expect(useDimTool.getState().count).toBe(4);
    // U takes the last point back.
    await submit('U');
    await feedPoint(P(10, 0));
    await submit('');
    expect(adds()).toEqual([{ viewId: 'v', pts: [[0, 0], [3, 0], [7, 0], [10, 0]], at: [0, 2], dir: [1, 0] }]);
    expect(useCad.getState().step).toBeNull();
  });

  it('finishes a string with a right-click, or a click on its last point again', async () => {
    setContinuous(true);
    await startCommand('DIM');
    for (const p of [P(0, 0), P(3, 0), P(0, 2), P(7, 0)]) await feedPoint(p);
    expect(await finishStep()).toBe(true);
    expect(adds()).toHaveLength(1);
    expect(adds()[0].pts).toEqual([[0, 0], [3, 0], [7, 0]]);
    await startCommand('DIM');
    for (const p of [P(0, 0), P(3, 0), P(0, 2), P(7, 0), P(7, 0)]) await feedPoint(p);
    expect(adds()).toHaveLength(2);
    expect(adds()[1].pts).toEqual([[0, 0], [3, 0], [7, 0]]);
    expect(useCad.getState().step).toBeNull();
  });

  it('keeps the string on Esc, or when another command starts, and says so', async () => {
    setContinuous(true);
    await startCommand('DIM');
    for (const p of [P(0, 0), P(3, 0), P(0, 2), P(7, 0)]) await feedPoint(p);
    cancel();
    await new Promise((r) => setTimeout(r, 0));
    expect(adds()).toHaveLength(1);
    expect(useUi.getState().notice).toBe('Dimension string kept (3 points) · Undo removes it');
    await startCommand('DIM');
    for (const p of [P(0, 0), P(3, 0), P(0, 2)]) await feedPoint(p);
    await startCommand('LINE');
    await new Promise((r) => setTimeout(r, 0));
    expect(adds()).toHaveLength(2);
    expect(useUi.getState().notice).toBe('Dimension string kept (2 points) · Undo removes it');
  });

  it('makes nothing from fewer than two points, and S is single', async () => {
    setContinuous(true);
    await startCommand('DIM');
    await submit('S');
    expect(useDimTool.getState().continuous).toBe(false);
    for (const p of [P(0, 0), P(0, 4), P(-1, 0)]) await feedPoint(p);
    expect(adds()).toEqual([{ viewId: 'v', pts: [[0, 0], [0, 4]], at: [-1, 0] }]);
    cancel();
    await startCommand('DIM');
    await feedPoint(P(1, 1));
    cancel();
    expect(adds()).toHaveLength(1);
  });
});

describe('direction of a linear dimension', () => {
  const a = P(0, 0), b = P(4, 0);
  it('is horizontal above or below the middle, vertical beside it', () => {
    expect(pickDirection(a, b, P(2, 3), null)).toBe('h');
    expect(pickDirection(a, b, P(9, 1), null)).toBe('v');
  });
  it('holds until the cursor is clearly on the other side', () => {
    // 45° from the middle: whichever was chosen stays.
    expect(pickDirection(a, b, P(4, 2), 'h')).toBe('h');
    expect(pickDirection(a, b, P(4, 2), 'v')).toBe('v');
    // Below 30°: vertical; above 60°: horizontal.
    expect(pickDirection(a, b, P(5, 0.5), 'h')).toBe('v');
    expect(pickDirection(a, b, P(2.4, 3), 'v')).toBe('h');
    // On the middle: unchanged.
    expect(pickDirection(a, b, P(2, 0), 'v')).toBe('v');
  });
  it('is not changed by the cursor once Tab chose it', () => {
    expect(pickDirection(a, b, P(2, 5), 'v', true)).toBe('v');
  });
  it('is kept once the line is placed', async () => {
    setContinuous(true);
    await startCommand('DLI');
    for (const p of [P(0, 0), P(4, 0), P(2, 1)]) await feedPoint(p);
    expect(useDimTool.getState().orient).toBe('h');
    expect(useDimTool.getState().forced).toBe(true);
  });
});

describe('the dimension preview', () => {
  it('tells each segment its value, in the project unit', () => {
    const labels = segmentLabels([P(0, 0), P(3, 0), P(7, 0)], P(0, 2), P(1, 0));
    expect(labels).toHaveLength(2);
    expect(labels[0].at).toEqual({ x: 1.5, y: 2 });
    expect(labels.map((l) => parseFloat(l.text.replace(',', '.')))).toEqual([expect.any(Number), expect.any(Number)]);
    expect(labels[0].text).not.toBe(labels[1].text);
  });
  it('finds the last point again within a tolerance', () => {
    expect(isSamePoint(P(1, 1), P(1.001, 1), 0.01)).toBe(true);
    expect(isSamePoint(P(1, 1), P(1.5, 1), 0.01)).toBe(false);
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
    // A segment's middle: dragged, the line goes through the point; Ctrl/⌘-dragged, a point goes there.
    expect(gs[4].step?.({ x: 5, y: 3 })).toEqual({ command: 'drafting.moveDimension', args: { id: 'd', at: [5, 3] } });
    expect(gs[4].insertAt?.({ x: 5, y: 0 })).toEqual({ command: 'drafting.insertDimensionPoint', args: { id: 'd', point: [5, 0] } });
    expect(gs[6].at).toEqual({ x: 7, y: 2 });
    expect(gs[6].step?.({ x: 7, y: 3 })).toEqual({ command: 'drafting.moveDimension', args: { id: 'd', at: [7, 3] } });
    expect(gs.every((g) => g.view === 'v')).toBe(true);
  });
  it('take no point out of a two-point dimension', () => {
    const two = { ...node, props: { ...node.props, pts: [[0, 0], [3, 0]] } } as NodeDto;
    expect(grips(two)[0].remove?.()).toBeNull();
  });
});
