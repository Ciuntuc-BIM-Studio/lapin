import { describe, expect, it } from 'vitest';
import { changeSteps, extentsOf, newExtent, setArgs } from './layerExtents';

const layers = [{ material: 'm1', thickness: 0.015 }, { material: 'm2', thickness: 0 }, { material: 'm3', thickness: 0.25, core: true }];

describe('layer extents (forum topic 89)', () => {
  it('reads what the element holds, and nothing from one without them', () => {
    expect(extentsOf({})).toEqual([]);
    expect(extentsOf({ layerExtents: [{ layer: 0, edge: 'bottom', offset: 0.02 }, { layer: 1, edge: 'edge', index: 2, offset: -0.1, cuts: true }, { edge: 'x' }] }))
      .toEqual([{ layer: 0, edge: 'bottom', offset: 0.02 }, { layer: 1, edge: 'edge', index: 2, offset: -0.1, cuts: true }]);
  });

  it('sets one, an edge of a floor with its index', () => {
    expect(setArgs('w', { layer: 0, edge: 'top', offset: 0.03 })).toEqual({ id: 'w', layer: 0, edge: 'top', offset: 0.03, cuts: false });
    expect(setArgs('s', { layer: 0, edge: 'edge', offset: 0.03, cuts: true })).toEqual({ id: 's', layer: 0, edge: 'edge', offset: 0.03, cuts: true, index: 0 });
  });

  it('changes an offset in one step, a layer or an edge in two (the old one taken away), never onto another', () => {
    const was = { layer: 0, edge: 'bottom', offset: 0.02 };
    expect(changeSteps('w', [was], was, { ...was, offset: 0.05 })).toHaveLength(1);
    const moved = changeSteps('w', [was], was, { ...was, edge: 'top' });
    expect(moved.map((s) => s.args.offset)).toEqual([0, 0.02]);
    expect(changeSteps('w', [was, { layer: 0, edge: 'top', offset: 0.01 }], was, { ...was, edge: 'top' })).toEqual([]);
  });

  it('starts a new one on the first free place of a layer with a thickness', () => {
    expect(newExtent(true, layers, [])).toEqual({ layer: 0, edge: 'bottom', offset: 0.02 });
    expect(newExtent(true, layers, [{ layer: 0, edge: 'bottom', offset: 0.02 }])).toEqual({ layer: 0, edge: 'top', offset: 0.02 });
    expect(newExtent(false, layers, [{ layer: 0, edge: 'outline', offset: 0.02 }])).toEqual({ layer: 2, edge: 'outline', offset: 0.02 });
    expect(newExtent(false, [layers[1]], [])).toBeNull();
  });
});
