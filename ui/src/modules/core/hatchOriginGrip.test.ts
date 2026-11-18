import { describe, expect, it, vi } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { forgetOriginPoints, dropHatchOrigin, hatchOriginGrips, type OriginDeps } from './hatchOriginGrip';

const wall = (props: Record<string, unknown> = {}) => ({ id: 'w1', type: 'IfcWall', props } as unknown as NodeDto);
const flush = () => new Promise((r) => setTimeout(r, 0));

const deps = (over: Partial<OriginDeps> = {}): OriginDeps => ({
  point: vi.fn(async () => ({ point: [1.5, 2] as [number, number], origin: [0.5, 0.25] as [number, number], own: false })),
  at: vi.fn(async (_id, _view, p) => ({ origin: [p[0] - 1, p[1] - 1] as [number, number] })),
  set: vi.fn(async () => ({})),
  ...over,
});

describe('the hatch origin grip', () => {
  it('asks the engine once for the point, then stands there', async () => {
    forgetOriginPoints();
    const d = deps();
    expect(hatchOriginGrips(wall(), { view: 'v1' }, d)).toEqual([]);
    expect(hatchOriginGrips(wall(), { view: 'v1' }, d)).toEqual([]);
    await flush();
    expect(d.point).toHaveBeenCalledTimes(1);
    expect(d.point).toHaveBeenCalledWith('w1', 'v1');
    const [g] = hatchOriginGrips(wall(), { view: 'v1' }, d);
    expect(g.at).toEqual({ x: 1.5, y: 2 });
    expect(g.view).toBe('v1');
    // a changed origin asks again
    hatchOriginGrips(wall({ hatchOrigin: [1, 1] }), { view: 'v1' }, d);
    await flush();
    expect(d.point).toHaveBeenCalledTimes(2);
  });

  it('has no grip in 3D (no view), nor for a class that does not hatch', () => {
    forgetOriginPoints();
    const d = deps();
    expect(hatchOriginGrips(wall(), undefined, d)).toEqual([]);
    expect(hatchOriginGrips({ id: 's', type: 'IfcSpace', props: {} } as unknown as NodeDto, { view: 'v' }, d)).toEqual([]);
    expect(d.point).not.toHaveBeenCalled();
  });

  it('a drop asks the engine for the origin at the point, then stores it in one command', async () => {
    const d = deps();
    const origin = await dropHatchOrigin('w1', 'v1', { x: 3, y: 4 }, d);
    expect(d.at).toHaveBeenCalledWith('w1', 'v1', [3, 4]);
    expect(d.set).toHaveBeenCalledTimes(1);
    expect(d.set).toHaveBeenCalledWith({ id: 'w1', origin: [2, 3] });
    expect(origin).toEqual([2, 3]);
  });

  it('stores nothing when the engine cannot read the drop', async () => {
    const d = deps({ at: vi.fn(async () => null) });
    expect(await dropHatchOrigin('w1', 'v1', { x: 0, y: 0 }, d)).toBeNull();
    expect(d.set).not.toHaveBeenCalled();
  });

  it('the grip’s move does the same flow', async () => {
    forgetOriginPoints();
    const d = deps();
    hatchOriginGrips(wall(), { view: 'v2' }, d);
    await flush();
    const [g] = hatchOriginGrips(wall(), { view: 'v2' }, d);
    await g.move!({ x: 5, y: 6 });
    expect(d.at).toHaveBeenCalledWith('w1', 'v2', [5, 6]);
    expect(d.set).toHaveBeenCalledWith({ id: 'w1', origin: [4, 5] });
  });
});
