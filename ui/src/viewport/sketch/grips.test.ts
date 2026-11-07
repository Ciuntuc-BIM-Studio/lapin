import { describe, expect, it } from 'vitest';
import { gripPoints, moveGrip } from './grips';
import { arcPts, bulgeOf, arcMiddle, offsetLoop, insideDistance, offsetOutcome, click, typed } from './direct';
import { edgesInPlane } from './edges';
import { levelFrame } from '../workPlane';

describe('stage B of the 3D sketch (decision 375)', () => {
  it('moves a sketch\'s grips: a line\'s end, a circle\'s radius, an arc through its points', () => {
    expect(moveGrip({ type: 'line', a: { x: 0, y: 0 }, b: { x: 1, y: 0 } }, 1, { x: 2, y: 1 })).toEqual({ type: 'line', a: { x: 0, y: 0 }, b: { x: 2, y: 1 } });
    expect(moveGrip({ type: 'circle', c: { x: 0, y: 0 }, r: 1 }, 1, { x: 0, y: 3 })).toEqual({ type: 'circle', c: { x: 0, y: 0 }, r: 3 });
    const arc = { type: 'arc' as const, c: { x: 0, y: 0 }, r: 1, start: 0, end: Math.PI };
    const g = gripPoints(arc);
    expect(g[1].x).toBeCloseTo(0, 9); expect(g[1].y).toBeCloseTo(1, 9);
    const moved = moveGrip(arc, 1, { x: 0, y: 2 });
    expect(moved?.type).toBe('arc');
    expect(moved && moved.type === 'arc' ? moved.r : 0).toBeCloseTo(1.25, 9);
  });

  it('a two-point arc takes its bulge from the chord', () => {
    expect(bulgeOf({ x: 0, y: 0 }, { x: 2, y: 0 }, { x: 1, y: 0.5 })).toBeCloseTo(0.5, 9);
    expect(arcMiddle({ x: 0, y: 0 }, { x: 2, y: 0 }, 1)).toEqual({ x: 1, y: 1 });
    expect(click('arc', [{ x: 0, y: 0 }], { x: 2, y: 0 })).toEqual({ kind: 'more', pts: [{ x: 0, y: 0 }, { x: 2, y: 0 }] });
    const o = click('arc', [{ x: 0, y: 0 }, { x: 2, y: 0 }], { x: 1, y: 1 });
    expect(o).toEqual({ kind: 'arc', start: { x: 0, y: 0 }, end: { x: 2, y: 0 }, through: { x: 1, y: 1 } });
    // The bulge typed, on the mouse's side (below the chord).
    const t = typed('arc', [{ x: 0, y: 0 }, { x: 2, y: 0 }], { kind: 'length', value: 0.5 }, { x: 1, y: -3 });
    expect(t.kind === 'arc' && t.through).toEqual({ x: 1, y: -0.5 });
    const pts = arcPts({ x: 0, y: 0 }, { x: 1, y: 1 }, { x: 2, y: 0 }, 4);
    expect(pts[2].x).toBeCloseTo(1, 9); expect(pts[2].y).toBeCloseTo(1, 9);
  });

  it('offsets a face\'s outline inward or outward through the cursor or by a typed distance', () => {
    const sq = [{ x: 0, y: 0 }, { x: 4, y: 0 }, { x: 4, y: 2 }, { x: 0, y: 2 }];
    expect(offsetLoop(sq, 0.5)).toEqual([{ x: 0.5, y: 0.5 }, { x: 3.5, y: 0.5 }, { x: 3.5, y: 1.5 }, { x: 0.5, y: 1.5 }]);
    expect(offsetLoop(sq, 1.5)).toBeNull();
    expect(insideDistance(sq, { x: 1, y: 0.3 })).toBeCloseTo(0.3, 9);
    expect(insideDistance(sq, { x: 5, y: 1 })).toBeCloseTo(-1, 9);
    const o = offsetOutcome(sq, { x: 1, y: 0.3 }, null);
    expect(o.kind === 'closed' && o.outline[0].x).toBeCloseTo(0.3, 9);
    const out = offsetOutcome(sq, { x: 5, y: 1 }, 0.2);
    expect(out.kind === 'closed' && out.outline[0]).toEqual({ x: -0.2, y: -0.2 });
  });

  it('takes other bodies\' edges lying in the plane, and where edges pass through it', () => {
    const f = levelFrame(3);
    const r = edgesInPlane([
      [{ x: 0, y: 0, z: 3 }, { x: 2, y: 0, z: 3 }],
      [{ x: 5, y: 5, z: 0 }, { x: 5, y: 5, z: 6 }],
      [{ x: 0, y: 0, z: 0 }, { x: 1, y: 0, z: 0 }],
    ], f);
    expect(r.edges).toEqual([{ a: { x: 0, y: 0 }, b: { x: 2, y: 0 } }]);
    expect(r.points).toEqual([{ x: 5, y: 5 }]);
  });
});
