import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { arcLength, pointAt, type Arc } from '../../viewport/arc';
import { railingArcs, railingGrips, railingLine, removePoint, splitSegment } from './Railing';

const node = (points: number[][], arcs?: number[]): NodeDto => ({ id: 'r', type: 'LapinRailing', name: 'Railing 1', props: { points, ...(arcs ? { arcs } : {}) } }) as unknown as NodeDto;

describe('a railing whose segments may be arcs', () => {
  it('reads its arcs one a segment, straight where it has none', () => {
    expect(railingArcs(undefined, 2)).toEqual([0, 0]);
    expect(railingArcs([0.5], 3)).toEqual([0.5, 0, 0]);
  });

  it('a point put on an arc splits it into two arcs of the same circle', () => {
    const pts = [{ x: 0, y: 0 }, { x: 4, y: 0 }];
    const r = splitSegment(pts, [-2], 0);
    expect(r.pts[1].x).toBeCloseTo(2, 9);
    expect(r.pts[1].y).toBeCloseTo(-2, 9);
    // Each half a quarter of the circle of radius 2: as long as half the arc.
    const whole = arcLength({ a: pts[0], b: pts[1], rise: -2 });
    const halves = [0, 1].map((i) => arcLength({ a: r.pts[i], b: r.pts[i + 1], rise: r.arcs[i] } as Arc));
    halves.forEach((h) => expect(h).toBeCloseTo(whole / 2, 9));
    const q = pointAt({ a: r.pts[0], b: r.pts[1], rise: r.arcs[0] }, halves[0] / 2);
    expect(Math.hypot(q.x - 2, q.y)).toBeCloseTo(2, 9);
  });

  it('a point taken out joins its two segments in a straight one', () => {
    const pts = [{ x: 0, y: 0 }, { x: 3, y: 0 }, { x: 4, y: 1 }, { x: 4, y: 3 }];
    expect(removePoint(pts, [0, -0.3, 0], 1).arcs).toEqual([0, 0]);
    expect(removePoint(pts, [0.1, -0.3, 0.2], 0).arcs).toEqual([-0.3, 0.2]);
    expect(removePoint(pts, [0.1, -0.3, 0.2], 3).arcs).toEqual([0.1, -0.3]);
  });

  it('its grips: an arc grip on a curved segment, Make arc / Make straight, and a corner to fillet between two straight ones', () => {
    const g = railingGrips(node([[0, 0], [3, 0], [4, 1], [4, 3]], [0, -0.3, 0]));
    expect(g.filter((x) => x.kind === 'radius').map((x) => x.key)).toEqual(['a1']);
    expect(g.find((x) => x.key === 'm1')?.straighten).toBeTruthy();
    expect(g.find((x) => x.key === 'm0')?.bend).toBeTruthy();
    expect(g.find((x) => x.key === 'v0')?.corner).toBeUndefined();
    expect(g.find((x) => x.key === 'v1')?.corner?.straight).toBe(false);
    const flat = railingGrips(node([[0, 0], [4, 0], [4, 3]]));
    expect(flat.find((x) => x.key === 'v1')?.corner?.fillet(1)).toEqual({ command: 'architecture.roundRailingCorner', args: { id: 'r', index: 1, radius: 1 } });
    // Dragging a point keeps the arcs.
    expect(g.find((x) => x.key === 'v0')!.step!({ x: 0, y: -1 })).toMatchObject({ command: 'architecture.updateRailing', args: { arcs: [0, -0.3, 0] } });
    expect(railingLine([{ x: 0, y: 0 }, { x: 4, y: 0 }], [-2]).length).toBeGreaterThan(10);
  });
});
