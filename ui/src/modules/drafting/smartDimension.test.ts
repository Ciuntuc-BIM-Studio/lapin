/** The one Dimension tool (S9): the kind from what is clicked, the kinds of its bar, a baseline; the Tag tool's sources. */

import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { Vec2 } from '../../platform/module';
import type { Shape } from '../../viewport/cad/shapes';

const engine = vi.hoisted(() => ({ execute: vi.fn(), undo: vi.fn(), query: vi.fn(), useSession: Object.assign(() => null, { getState: () => ({ nodes: new Map() }), setState: () => undefined }) }));
vi.mock('../../engine/session', () => engine);

import { cancel, feedPoint, startCommand, submit, useCad } from '../../viewport/cad/session';
import { setStringMode, useDimTool } from './dimensionTool';
import { useSmartDim } from './smartDimension';
import { crossing, segmentAt, sidePoint } from './index';
import { tagContent } from './tag';

const P = (x: number, y: number): Vec2 => ({ x, y });
const adds = () => engine.execute.mock.calls.filter(([c]) => c === 'drafting.addDimension').map(([, a]) => a as Record<string, unknown>);

/** Drafted shapes the host finds under a point: each within 0.05 of the point. */
let shapes: { id: string; shape: Shape }[] = [];
const near = (s: Shape, p: Vec2): boolean => {
  if (s.type === 'circle' || s.type === 'arc') return Math.abs(Math.hypot(p.x - s.c.x, p.y - s.c.y) - s.r) < 0.05;
  const seg = segmentAt(s, p);
  if (!seg) return false;
  const dx = seg.b.x - seg.a.x, dy = seg.b.y - seg.a.y, l = Math.hypot(dx, dy);
  return Math.abs((p.x - seg.a.x) * dy - (p.y - seg.a.y) * dx) / l < 0.05;
};

beforeEach(() => {
  engine.execute.mockReset();
  engine.execute.mockImplementation(() => Promise.resolve({ id: 'd' }));
  cancel(true);
  setStringMode('single');
  shapes = [];
  useSmartDim.setState({ kind: 'smart', roundAs: 'radial', linear: false });
  useCad.setState({ aim: null, host: { viewId: 'v', pickDraft: (p) => shapes.find((s) => near(s.shape, p)) ?? null, pickAny: () => null, shapeOf: () => null }, history: [] });
});

describe('the smart Dimension tool', () => {
  it('makes an aligned dimension from two points, a linear one when the bar says so, and asks for the next', async () => {
    await startCommand('DIMENSION');
    for (const p of [P(0, 0), P(3, 1), P(0, 2)]) await feedPoint(p);
    expect(adds()).toEqual([{ viewId: 'v', pts: [[0, 0], [3, 1]], at: [0, 2] }]);
    expect(useCad.getState().command).toBe('DIMENSION');
    useSmartDim.setState({ linear: true });
    for (const p of [P(0, 0), P(3, 1), P(1, 4)]) await feedPoint(p);
    expect(adds()[1]).toMatchObject({ pts: [[0, 0], [3, 1]], dir: [1, 0] });
  });

  it('measures a drafted line clicked on, end to end', async () => {
    shapes = [{ id: 'l1', shape: { type: 'line', a: P(0, 0), b: P(4, 0) } }];
    await startCommand('DIM');
    await feedPoint(P(1.5, 0));
    expect(useCad.getState().step?.prompt).toContain('second line for the angle');
    await feedPoint(P(2, 1));
    expect(adds()).toEqual([{ viewId: 'v', pts: [[0, 0], [4, 0]], at: [2, 1] }]);
  });

  it('makes the angle between two lines clicked one after the other', async () => {
    shapes = [{ id: 'l1', shape: { type: 'line', a: P(0, 0), b: P(4, 0) } }, { id: 'l2', shape: { type: 'line', a: P(0, 0), b: P(0, 3) } }];
    await startCommand('DIMENSION');
    await feedPoint(P(2, 0));
    await feedPoint(P(0, 2));
    expect(useCad.getState().step?.prompt).toBe('Where the arc goes');
    await feedPoint(P(1, 1));
    expect(adds()).toEqual([{ viewId: 'v', dimKind: 'angular', c: [0, 0], a: [2, 0], b: [0, 2], at: [1, 1] }]);
  });

  it('gives a circle its radius, or its diameter, and an arc its length, as the bar says', async () => {
    shapes = [{ id: 'c1', shape: { type: 'circle', c: P(0, 0), r: 2 } }, { id: 'a1', shape: { type: 'arc', c: P(10, 0), r: 1, start: 0, end: Math.PI } }];
    await startCommand('DIMENSION');
    await feedPoint(P(2, 0));
    await feedPoint(P(3, 1));
    expect(adds()[0]).toMatchObject({ dimKind: 'radial', shape: 'c1', at: [3, 1] });
    useSmartDim.setState({ roundAs: 'diameter' });
    await feedPoint(P(0, 2));
    await feedPoint(P(1, 3));
    expect(adds()[1]).toMatchObject({ dimKind: 'diameter', shape: 'c1' });
    useSmartDim.setState({ roundAs: 'arc' });
    await feedPoint(P(10, 1));
    await feedPoint(P(10, 2));
    expect(adds()[2]).toMatchObject({ dimKind: 'arc', shape: 'a1', at: [10, 2] });
  });

  it('writes the first point\'s level on Enter', async () => {
    await startCommand('DIMENSION');
    await feedPoint(P(1, 1));
    await submit('');
    await feedPoint(P(2, 1));
    expect(adds()).toEqual([{ viewId: 'v', dimKind: 'level', a: [1, 1], at: [2, 1] }]);
  });

  it('takes a kind typed at its first point, and asks again in that kind', async () => {
    await startCommand('DIMENSION');
    await submit('AN');
    expect(useSmartDim.getState().kind).toBe('angular');
    expect(useCad.getState().step?.prompt).toBe('The angle\'s vertex');
    for (const p of [P(0, 0), P(1, 0), P(0, 1), P(1, 1)]) await feedPoint(p);
    expect(adds()[0]).toMatchObject({ dimKind: 'angular' });
    expect(useCad.getState().step?.prompt).toBe('The angle\'s vertex');
  });

  it('as a baseline, measures each next point from the first, each a dimension a step further out', async () => {
    setStringMode('baseline');
    await startCommand('DIMENSION');
    for (const p of [P(0, 0), P(2, 0), P(0, 1), P(5, 0), P(8, 0)]) await feedPoint(p);
    const a = adds();
    expect(a).toHaveLength(3);
    expect(a[0]).toEqual({ viewId: 'v', pts: [[0, 0], [2, 0]], at: [0, 1] });
    expect(a[1]).toMatchObject({ pts: [[0, 0], [5, 0]], dir: [1, 0] });
    // 7 mm on paper at 1:100 (no scale known): 0.7 m further out each.
    expect((a[1].at as number[])[1]).toBeCloseTo(1.7);
    expect((a[2].at as number[])[1]).toBeCloseTo(2.4);
    await submit('');
    expect(useCad.getState().step?.prompt).toContain('A point, a drafted line');
    expect(useDimTool.getState().baseline).toBe(true);
  });

  it('typed B, C or S choose the string\'s mode', async () => {
    await startCommand('DIMENSION');
    await submit('B');
    expect(useDimTool.getState()).toMatchObject({ baseline: true, continuous: false });
    await submit('C');
    expect(useDimTool.getState()).toMatchObject({ baseline: false, continuous: true });
    await submit('S');
    expect(useDimTool.getState()).toMatchObject({ baseline: false, continuous: false });
  });
});

describe('the smart tool\'s geometry', () => {
  it('finds a polyline\'s side nearest the click, and where two lines cross', () => {
    const pl: Shape = { type: 'polyline', pts: [P(0, 0), P(4, 0), P(4, 3)], closed: true };
    expect(segmentAt(pl, P(4.1, 1))).toEqual({ a: P(4, 0), b: P(4, 3) });
    expect(segmentAt(pl, P(2, 1.4))).toEqual({ a: P(4, 3), b: P(0, 0) });
    expect(segmentAt({ type: 'circle', c: P(0, 0), r: 1 }, P(1, 0))).toBeNull();
    expect(crossing(P(0, 0), P(1, 0), P(3, -1), P(3, 1))).toEqual({ x: 3, y: 0 });
    expect(crossing(P(0, 0), P(1, 0), P(0, 1), P(1, 1))).toBeNull();
  });
  it('takes a side\'s far end when the click is on the vertex itself', () => {
    expect(sidePoint(P(0, 0), { a: P(0, 0), b: P(5, 0) }, P(0.001, 0), 0.01)).toEqual(P(5, 0));
    expect(sidePoint(P(0, 0), { a: P(0, 0), b: P(5, 0) }, P(2, 0), 0.01)).toEqual(P(2, 0));
  });
});

describe('the Tag tool\'s sources', () => {
  const base = { source: 'element' as const, field: 'mark', tagType: '', roomField: 'name', parameter: 'FireRating', buildup: '', keynote: 'code' as const };
  it('says what each source shows', () => {
    expect(tagContent(base)).toEqual({ field: 'mark' });
    expect(tagContent({ ...base, tagType: 'door' })).toEqual({ tagType: 'door' });
    expect(tagContent({ ...base, source: 'room', roomField: 'area' })).toEqual({ field: 'area' });
    expect(tagContent({ ...base, source: 'parameter', parameter: ' {Fire rating} ' })).toEqual({ field: 'Fire rating' });
    expect(tagContent({ ...base, source: 'material', buildup: 'buildup' })).toEqual({ tagType: 'buildup' });
    expect(tagContent({ ...base, source: 'material' })).toEqual({ field: 'mark' });
    expect(tagContent({ ...base, source: 'keynote' })).toEqual({ field: 'classification.code' });
    expect(tagContent({ ...base, source: 'keynote', keynote: 'title' })).toEqual({ field: 'classification.title' });
  });
});

describe('a circle snapped to', () => {
  it('is the circle: its centre or a quadrant clicked gives the radius', async () => {
    const c: Shape = { type: 'circle', c: P(0, 0), r: 2 };
    useCad.setState({ host: { viewId: 'v', pickDraft: () => null, pickAny: () => null, shapeOf: (id) => (id === 'c1' ? c : null) } });
    await startCommand('DIMENSION');
    useCad.setState({ aim: { point: P(0, 0), kind: 'point', mode: 'center', guides: [], shapeId: 'c1' } });
    await feedPoint(P(0, 0));
    useCad.setState({ aim: null });
    await feedPoint(P(3, 1));
    expect(adds()[0]).toMatchObject({ dimKind: 'radial', shape: 'c1' });
  });
});
