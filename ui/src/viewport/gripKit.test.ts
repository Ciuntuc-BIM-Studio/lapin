import { describe, expect, it } from 'vitest';
import { nearestOnLine, nearestOnSegment, outlineGrips } from './gripKit';

const square = [{ x: 0, y: 0 }, { x: 4, y: 0 }, { x: 4, y: 4 }, { x: 0, y: 4 }];
const save = (o: { pts: { x: number; y: number }[] }) => ({ command: 'x.update', args: { points: o.pts.map((p) => [p.x, p.y]) } });

describe('an outline\'s corners (forum topic 97)', () => {
  it('finds the point of an edge nearest to a click, clamped to the edge', () => {
    expect(nearestOnSegment({ x: 0, y: 0 }, { x: 4, y: 0 }, { x: 1, y: 0.5 })).toEqual({ at: { x: 1, y: 0 }, dist: 0.5, t: 0.25 });
    expect(nearestOnSegment({ x: 0, y: 0 }, { x: 4, y: 0 }, { x: 9, y: 0 }).t).toBe(1);
    expect(nearestOnSegment({ x: 1, y: 1 }, { x: 1, y: 1 }, { x: 3, y: 1 }).dist).toBe(2);
  });

  it('puts a corner on the edge at the point clicked, as one update of the element', () => {
    const grips = outlineGrips({ pts: square }, save);
    const edge = grips.find((g) => g.key === 'e0')!;
    expect(edge.edge).toEqual([square[0], square[1]]);
    expect(edge.addCornerAt!({ x: 1, y: 0.4 })).toEqual({ command: 'x.update', args: { points: [[0, 0], [1, 0], [4, 0], [4, 4], [0, 4]] } });
    // Ctrl/⌘-click on the middle puts it halfway.
    expect(edge.insert!()).toEqual({ command: 'x.update', args: { points: [[0, 0], [2, 0], [4, 0], [4, 4], [0, 4]] } });
  });

  it('takes a corner out, never under three, and says so in the grips\' titles', () => {
    const four = outlineGrips({ pts: square }, save);
    expect(four.find((g) => g.key === 'v1')!.remove!()).toEqual({ command: 'x.update', args: { points: [[0, 0], [4, 4], [0, 4]] } });
    expect(four.find((g) => g.key === 'v1')!.title).toContain('remove this corner');
    expect(four.find((g) => g.key === 'e1')!.title).toContain('add a corner');
    const three = outlineGrips({ pts: square.slice(0, 3) }, save);
    expect(three.find((g) => g.key === 'v0')!.remove!()).toBeNull();
    expect(three.find((g) => g.key === 'v0')!.title).not.toContain('remove');
  });
});

describe('curved edges and rounded corners on outlines (forum topic 97, stage 2)', () => {
  const curved = {
    fillet: (index: number, radius: number) => ({ command: 'x.round', args: { index, radius } }),
    chamfer: (index: number, distance: number) => ({ command: 'x.chamfer', args: { index, distance } }),
  };
  const saveArcs = (o: { pts: { x: number; y: number }[]; arcs?: number[] }) => ({ command: 'x.update', args: { points: o.pts.map((p) => [p.x, p.y]), arcs: o.arcs } });

  it('bends a straight edge out of the outline and straightens a curved one', () => {
    const grips = outlineGrips({ pts: square }, saveArcs, curved);
    // Anticlockwise: out of it is to the right of each edge, a negative rise, an eighth of the edge.
    expect(grips.find((g) => g.key === 'e0')!.bend!()).toEqual({ command: 'x.update', args: { points: square.map((p) => [p.x, p.y]), arcs: [-0.5, 0, 0, 0] } });
    expect(grips.find((g) => g.key === 'e0')!.straighten).toBeUndefined();
    const bent = outlineGrips({ pts: square, arcs: [-1, 0, 0, 0] }, saveArcs, curved);
    expect(bent.find((g) => g.key === 'e0')!.straighten!()).toEqual({ command: 'x.update', args: { points: square.map((p) => [p.x, p.y]), arcs: [0, 0, 0, 0] } });
    // Its round grip at the arc's middle, dragged through a point, bends it through the point.
    const arc = bent.find((g) => g.key === 'a0')!;
    expect(arc.at.x).toBeCloseTo(2);
    expect(arc.at.y).toBeCloseTo(-1);
    expect((arc.step!({ x: 2, y: -2 })!.args.arcs as number[])[0]).toBeCloseTo(-2);
    // The edge's line for "Add corner here" follows the arc.
    expect(bent.find((g) => g.key === 'e0')!.edge!.length).toBeGreaterThan(2);
    // Without `curved` (a roof), none of it.
    expect(outlineGrips({ pts: square }, saveArcs).find((g) => g.key === 'e0')!.bend).toBeUndefined();
  });

  it('offers a fillet and a chamfer on a corner, as its own commands, only between straight edges', () => {
    const grips = outlineGrips({ pts: square, arcs: [0, -1, 0, 0] }, saveArcs, curved);
    const v0 = grips.find((g) => g.key === 'v0')!.corner!;
    expect(v0.straight).toBe(true);
    expect(v0.fillet(0.3)).toEqual({ command: 'x.round', args: { index: 0, radius: 0.3 } });
    expect(v0.chamfer(0.2)).toEqual({ command: 'x.chamfer', args: { index: 0, distance: 0.2 } });
    expect(grips.find((g) => g.key === 'v1')!.corner!.straight).toBe(false);
    expect(grips.find((g) => g.key === 'v2')!.corner!.straight).toBe(false);
  });

  it('finds the nearest point of a polyline and how far along it lies', () => {
    const line = [{ x: 0, y: 0 }, { x: 2, y: 0 }, { x: 2, y: 2 }];
    const h = nearestOnLine(line, { x: 2.5, y: 1 });
    expect(h.at).toEqual({ x: 2, y: 1 });
    expect(h.t).toBeCloseTo(0.75);
  });
});
