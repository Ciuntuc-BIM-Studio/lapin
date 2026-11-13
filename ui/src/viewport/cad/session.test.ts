/** The command line's state machine, with the engine mocked: steps, keywords, and points fed faster than the engine replies. */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Vec2 } from '../../platform/module';

const engine = vi.hoisted(() => ({ execute: vi.fn(), undo: vi.fn(), useSession: { getState: vi.fn(() => ({ nodes: new Map(), error: null })), setState: vi.fn() } }));
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

  describe('ROTATE Reference and Copy', () => {
    const transforms = () => engine.execute.mock.calls.filter(([c]) => c === 'element.transform').map(([, a]) => a as Record<string, unknown>);
    beforeEach(() => { useUi.setState({ selected: ['a'] }); });

    it('Reference: hinge, reference point, new point; the turn is the difference', async () => {
      await startCommand('ROTATE');
      await feedPoint(P(0, 0));
      await submit('R');
      await feedPoint(P(1, 0));
      await feedPoint(P(0, 2));
      expect(transforms()).toHaveLength(1);
      expect(transforms()[0].angle).toBeCloseTo(90, 9);
      expect(transforms()[0].copy).toBeUndefined();
    });

    it('Reference with typed angles: 30 then 75 turns by 45', async () => {
      await startCommand('ROTATE');
      await feedPoint(P(0, 0));
      await submit('R');
      await submit('30');
      await submit('75');
      expect(transforms()[0].angle).toBeCloseTo(45, 9);
    });

    it('Reference: a reference point, then a typed new direction (mixed)', async () => {
      await startCommand('ROTATE');
      await feedPoint(P(1, 1));
      await submit('R');
      await feedPoint(P(2, 2));
      await submit('90');
      expect(transforms()[0].angle).toBeCloseTo(45, 9);
    });

    it('the ribbon\'s Rotate (ROTATEREF) goes hinge → reference point → target point', async () => {
      await startCommand('ROTATEREF');
      await feedPoint(P(0, 0));
      expect(useCad.getState().step?.prompt).toContain('Reference point');
      await feedPoint(P(2, 0));
      await feedPoint(P(0, 3));
      expect(transforms()).toHaveLength(1);
      expect(transforms()[0].angle).toBeCloseTo(90, 9);
    });

    it('ROTATEREF: an angle typed at the reference step is the turn itself, at the target step the new direction', async () => {
      await startCommand('ROTATEREF');
      await feedPoint(P(0, 0));
      await submit('30');
      expect(transforms()[0].angle).toBe(30);
      engine.execute.mockClear();
      await startCommand('ROTATEREF');
      await feedPoint(P(0, 0));
      await feedPoint(P(1, 1));          // reference 45°
      await submit('90');                // new direction 90°
      expect(transforms()[0].angle).toBeCloseTo(45, 9);
    });

    it('ROTATEREF keeps Copy', async () => {
      await startCommand('ROTATEREF');
      await feedPoint(P(0, 0));
      await submit('C');
      await submit('20');
      expect(transforms()[0]).toMatchObject({ angle: 20, copy: true });
    });

    it('Copy turns a copy', async () => {
      await startCommand('ROTATE');
      await feedPoint(P(0, 0));
      await submit('C');
      expect(useCad.getState().step?.prompt).toContain('Copy: yes');
      await submit('45');
      expect(transforms()[0]).toMatchObject({ op: 'rotate', angle: 45, copy: true });
    });

    it('the preview turns the selection with the cursor', async () => {
      useCad.setState({ host: { viewId: 'v', pickDraft: () => null, pickAny: () => null, shapeOf: () => ({ type: 'line', a: P(1, 0), b: P(2, 0) }) } });
      await startCommand('ROTATE');
      await feedPoint(P(0, 0));
      const pv = useCad.getState().step!.preview!(P(0, 5));
      const l = pv.find((sh) => sh.type === 'line' && Math.abs((sh as { a: Vec2 }).a.x) < 1e-9 && (sh as { a: Vec2 }).a.y > 0.5);
      expect(l).toBeTruthy();
    });
  });
});

describe('constraints recorded while drawing (decision 520, C2)', () => {
  const sketch = () => engine.useSession.getState.mockReturnValue({
    error: null,
    nodes: new Map([
      ['d1', { id: 'd1', props: { predefinedType: 'DRAFT', viewId: 'v', shape: { type: 'line', a: [0, 0], b: [4, 0] } } }],
      ['s1', { id: 's1', props: { operation: 'extrude', profile: 'd1' } }],
    ]),
  });
  const added = () => engine.execute.mock.calls.filter(([c]) => c === 'studio.addConstraint').map(([, a]) => a as { constraints?: { kind: string; refs: { shape: string; part: string }[]; value?: number }[] });
  const aim = (p: Vec2, mode?: string, shapeId?: string, lock?: string) => useCad.setState({ aim: { point: p, kind: 'point', guides: [], ...(mode ? { mode } : {}), ...(shapeId ? { shapeId } : {}), ...(lock ? { lock } : {}) } as never });
  beforeEach(async () => {
    sketch();
    const { setPrefs } = await import('../../app/prefs');
    setPrefs({ constrainWhileDrawing: true });
  });

  it('an endpoint snap and a held ortho line become a coincidence and a horizontal side, in one batch', async () => {
    await startCommand('LINE');
    aim(P(4, 0), 'endpoint', 'd1');
    await feedPoint(P(4, 0));
    aim(P(9, 0), undefined, undefined, 'ortho');
    await feedPoint(P(9, 0));
    const [call] = added();
    expect(call.constraints?.map((c) => c.kind)).toEqual(['coincident', 'horizontal']);
    expect(call.constraints?.[0].refs).toEqual([{ shape: 'x', part: 'start' }, { shape: 'd1', part: 'end' }]);
  });

  it('a typed length is a driving dimension of the side', async () => {
    await startCommand('LINE');
    aim(P(0, 5));
    await feedPoint(P(0, 5));
    aim(P(0.5, 5.1));
    await feedPoint(P(3, 5), { length: 3 });
    expect(added()[0].constraints).toEqual([{ kind: 'distance', refs: [{ shape: 'x', part: 'whole' }], value: 3 }]);
  });

  it('a rectangle is two horizontal and two vertical sides; a typed radius a radius', async () => {
    await startCommand('RECTANGLE');
    await feedPoint(P(0, 0));
    await feedPoint(P(5, 2));
    expect(added()[0].constraints?.map((c) => c.kind)).toEqual(['horizontal', 'vertical', 'horizontal', 'vertical']);
    engine.execute.mockClear();
    await startCommand('CIRCLE');
    await feedPoint(P(10, 10));
    await submit('2');
    expect(added()[0].constraints).toEqual([{ kind: 'radius', refs: [{ shape: 'x', part: 'whole' }], value: 0.002 }]);
  });

  it('records nothing with the setting off, in a view that is no sketch, or with the point placed free', async () => {
    const { setPrefs } = await import('../../app/prefs');
    setPrefs({ constrainWhileDrawing: false });
    await startCommand('RECTANGLE');
    await feedPoint(P(0, 0)); await feedPoint(P(5, 2));
    expect(added()).toEqual([]);
    setPrefs({ constrainWhileDrawing: true });
    engine.useSession.getState.mockReturnValue({ nodes: new Map(), error: null });
    await startCommand('RECTANGLE');
    await feedPoint(P(0, 0)); await feedPoint(P(5, 2));
    expect(added()).toEqual([]);
  });

  it('tries the constraints one by one when the solver refuses the batch', async () => {
    await startCommand('RECTANGLE');
    engine.execute.mockImplementation((cmd: string, args: { constraints?: unknown[] }) => new Promise((r) => setTimeout(() => r(cmd === 'studio.addConstraint' && args.constraints ? null : { ids: ['x'] }), 2)));
    await feedPoint(P(0, 0)); await feedPoint(P(5, 2));
    const calls = engine.execute.mock.calls.filter(([c]) => c === 'studio.addConstraint');
    expect(calls.length).toBe(5);
  });
});
