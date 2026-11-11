import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { elevationGrips, sectionHeadGrips } from './sections';

const elevation = { id: 'e', type: 'LapinView', name: 'South elevation', parentId: null, props: { kind: 'elevation', direction: 'south' } } as unknown as NodeDto;
const mark = { id: 'e', segments: [], data: { ends: [[-0.5, -2], [10.5, -2]], right: [1, 0], min: -0.5, max: 10.5, own: false, symbol: [5, -2.5], depth: -2, along: 0 } };

describe("an elevation's grips in plan (decision 456)", () => {
  it('puts one on each end of its marker line', () => {
    const grips = elevationGrips(elevation, { mark });
    expect(grips.map((g) => [g.key, g.at.x, g.at.y])).toEqual([['extentMin', -0.5, -2], ['extentMax', 10.5, -2], ['marker', 5, -2.5]]);
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

describe('markers moved in plan (decision 460)', () => {
  it("puts an elevation's marker where its symbol is dragged: its line along the look, its symbol along the line (decision 460)", () => {
    const marker = elevationGrips(elevation, { mark }).find((g) => g.key === 'marker')!;
    // The south elevation looks north (+y): dragged 1 m east and 2 m south, its line 2 m further out.
    expect(marker.step!({ x: 6, y: -4.5 })).toEqual({ command: 'view.moveMarker', args: { id: 'e', depth: -4, along: 1 } });
    // Dragged into the house.
    expect(marker.step!({ x: 5, y: 3.5 })!.args).toEqual({ id: 'e', depth: 4, along: 0 });
  });

  it("moves a section's head off its end, and puts it back", () => {
    const section = { ...elevation, id: 's', props: { kind: 'section', start: [6, -2], end: [6, 10] } } as unknown as NodeDto;
    const heads = { id: 's', segments: [], data: { heads: [{ end: 'a', at: [5.3, -2], offset: [0, 0] }, { end: 'b', at: [4.3, 9.5], offset: [-1, -0.5] }] } };
    const [a, b] = sectionHeadGrips(section, { mark: heads });
    expect(a.step!({ x: 4.3, y: -2.5 })).toEqual({ command: 'view.moveHead', args: { id: 's', end: 'a', offset: [-1, -0.5] } });
    expect(a.remove!()).toBeNull();
    expect(b.remove!()).toEqual({ command: 'view.moveHead', args: { id: 's', end: 'b', offset: null } });
  });
});
