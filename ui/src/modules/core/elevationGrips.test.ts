import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { elevationGrips } from './sections';

const elevation = { id: 'e', type: 'LapinView', name: 'South elevation', parentId: null, props: { kind: 'elevation', direction: 'south' } } as unknown as NodeDto;
const mark = { id: 'e', segments: [], data: { ends: [[-0.5, -2], [10.5, -2]], right: [1, 0], min: -0.5, max: 10.5, own: false } };

describe("an elevation's grips in plan (decision 456)", () => {
  it('puts one on each end of its marker line', () => {
    const grips = elevationGrips(elevation, { mark });
    expect(grips.map((g) => [g.key, g.at.x, g.at.y])).toEqual([['extentMin', -0.5, -2], ['extentMax', 10.5, -2]]);
  });

  it('sets how far it reaches on the side dragged, along its line, the other end where it is drawn', () => {
    const [lo, hi] = elevationGrips(elevation, { mark });
    expect(lo.step!({ x: 2, y: -5 })).toEqual({ command: 'view.setExtent', args: { id: 'e', min: 2, max: 10.5 } });
    expect(hi.step!({ x: 6.25, y: 3 })).toEqual({ command: 'view.setExtent', args: { id: 'e', min: -0.5, max: 6.25 } });
    // Never past the other end.
    expect(hi.step!({ x: -3, y: 0 })!.args.max).toBeCloseTo(-0.4, 9);
  });

  it('has none without the line the engine drew, nor on a section', () => {
    expect(elevationGrips(elevation)).toEqual([]);
    expect(elevationGrips({ ...elevation, props: { kind: 'section' } } as NodeDto, { mark })).toEqual([]);
  });
});
