import { describe, expect, it } from 'vitest';
import { nearestOnSegment, outlineGrips } from './gripKit';

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
