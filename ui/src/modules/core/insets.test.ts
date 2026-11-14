import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { insetGrips, insetSources } from './insets';

const node = (id: string, type: string, props: Record<string, unknown> = {}, name = id) => ({ id, type, name, parentId: null, props }) as unknown as NodeDto;
const inset = node('ins', 'LapinInset', { viewId: 'plan:s1', source: 'sec', at: [20, 0] });
// The engine's mark: the point, the mapping (1:20 in a 1:100 plan) and a crop in the section's coordinates.
const mark = { id: 'ins', segments: [], data: { at: [20, 0], centre: [0, 1.5], factor: 5, crop: { min: [-1, 0], max: [1, 3] } } };

describe('an inset view (decision 576)', () => {
  it('is moved by its point, on its host', () => {
    const [g] = insetGrips(inset);
    expect(g.at).toEqual({ x: 20, y: 0 });
    expect(g.view).toBe('plan:s1');
    expect(g.step!({ x: 22, y: 1 })).toEqual({ command: 'view.updateInset', args: { id: 'ins', at: [22, 1] } });
  });

  it("drags its crop's corners where the inset shows them, the crop kept in the source's coordinates", () => {
    const grips = insetGrips(inset, { mark });
    expect(grips.map((g) => g.key)).toEqual(['at', 'crop-sw', 'crop-se', 'crop-ne', 'crop-nw']);
    const ne = grips.find((g) => g.key === 'crop-ne')!;
    expect(ne.at).toEqual({ x: 25, y: 7.5 });
    // Dragged 5 m right in the plan: 1 m in the section.
    expect(ne.step!({ x: 30, y: 7.5 })).toEqual({ command: 'view.updateInset', args: { id: 'ins', crop: { min: [-1, 0], max: [2, 3] } } });
    // Without a crop, its point only.
    expect(insetGrips(inset, { mark: { ...mark, data: { ...mark.data, crop: undefined } } }).length).toBe(1);
  });

  it("offers every 2D view and the levels' own plans, not the host nor a 3D view", () => {
    const nodes = new Map([
      ['sec', node('sec', 'LapinView', { kind: 'section' }, 'Section A')],
      ['el', node('el', 'LapinView', { kind: 'elevation' }, 'South')],
      ['v3', node('v3', 'LapinView', { kind: '3d' }, '3D')],
      ['s1', node('s1', 'IfcBuildingStorey', {}, 'Ground floor')],
      ['s2', node('s2', 'IfcBuildingStorey', {}, 'First floor')],
      ['p2', node('p2', 'LapinView', { kind: 'plan', storeyId: 's2' }, 'First floor plan')],
    ]);
    expect(insetSources(nodes, 'sec').map((v) => v.id)).toEqual(['el', 'p2', 'plan:s1']);
    expect(insetSources(nodes, 'plan:s1').map((v) => v.id)).toEqual(['sec', 'el', 'p2']);
  });
});
