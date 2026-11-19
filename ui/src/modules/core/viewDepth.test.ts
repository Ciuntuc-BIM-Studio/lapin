import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { elevationFarGrips, sectionFarGrips, sectionFarLine, viewDepth } from './ViewDepth';

const section = (props: Record<string, unknown>) => ({ id: 's', type: 'LapinView', name: 'Section 1', parentId: null, props: { kind: 'section', start: [0, 4], end: [10, 4], ...props } }) as unknown as NodeDto;

describe("a section's far clip in plan (issue #93, decision 721)", () => {
  it('puts its grip on the far line, looking left of the section line', () => {
    const [far] = sectionFarGrips(section({ depth: 3 }));
    expect([far.key, far.at.x, far.at.y]).toEqual(['far', 5, 7]);
    expect(sectionFarLine(section({ depth: 3 }))[0].points).toEqual([{ x: 0, y: 7 }, { x: 10, y: 7 }]);
  });

  it('sets the depth where it is dragged, along the look, as one command', () => {
    const [far] = sectionFarGrips(section({ depth: 3 }));
    expect(far.step!({ x: 8, y: 5.5 })).toEqual({ command: 'view.setDepth', args: { id: 's', depth: 1.5 } });
    // Never behind the cut.
    expect(far.step!({ x: 5, y: 1 })!.args.depth).toBe(0.01);
    // Ctrl/⌘-click: without end again.
    expect(far.remove!()).toEqual({ command: 'view.setDepth', args: { id: 's', depth: null } });
  });

  it('offers a grip to drag in when it sees without end, and no far line', () => {
    const [far] = sectionFarGrips(section({}));
    expect(far.at).toEqual({ x: 5, y: 9 });
    expect(far.remove!()).toBeNull();
    expect(sectionFarLine(section({}))).toEqual([]);
    expect(viewDepth(section({}))).toBeNull();
  });

  it('reads an across section without a depth of its own as its margin, and gives a curved one none', () => {
    expect(viewDepth(section({ across: 'w', margin: 0.8 }))).toBe(0.8);
    expect(sectionFarGrips(section({ curve: 'arc', pts: [[0, 0], [10, 0], [5, 2]] }))).toEqual([]);
  });
});

describe("an elevation's far clip in plan", () => {
  const elevation = { id: 'e', type: 'LapinView', name: 'South elevation', parentId: null, props: { kind: 'elevation', direction: 'south', depth: 3 } } as unknown as NodeDto;
  const data = { ends: [[-0.5, -2], [10.5, -2]], right: [1, 0], min: -0.5, max: 10.5, symbol: [5, -2.5], depth: -2, along: 0 };

  it('has a grip only once the engine tells where its far line is', () => {
    expect(elevationFarGrips(elevation, { mark: { id: 'e', segments: [], data } as never })).toEqual([]);
    const [far] = elevationFarGrips(elevation, { mark: { id: 'e', segments: [], data: { ...data, seeFrom: -0.15, far: 2.85 } } as never });
    expect(far.at.x).toBeCloseTo(5, 9);
    expect(far.at.y).toBeCloseTo(2.85, 9);
    expect(far.step!({ x: 5, y: 4.85 })).toEqual({ command: 'view.setDepth', args: { id: 'e', depth: 5 } });
  });
});
