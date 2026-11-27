import { describe, expect, it } from 'vitest';
import {
  arcFrame,
  arcFromCenterStartEnd,
  arcTangentFrom,
  arcWithEndAngle,
  arcWithRadius,
  circleAsTwoArcs,
  circleFromTwoTangentsAndPoint,
  middle,
  tangentAt,
} from './arc';
import { findSnap } from './cad/snaps';
import { arcShape } from './arc';

describe('extended curve drawing & editing (forum topic 293)', () => {
  it('constructs an arc from centre, start and end, and returns null when start and end coincide (full circle)', () => {
    const a = arcFromCenterStartEnd({ x: 0, y: 0 }, { x: 2, y: 0 }, { x: 0, y: 2 }, true)!;
    expect(a).not.toBeNull();
    const f = arcFrame(a);
    expect(f.centre.x).toBeCloseTo(0, 6);
    expect(f.centre.y).toBeCloseTo(0, 6);
    expect(f.r).toBeCloseTo(2, 6);
    expect(f.sweep).toBeCloseTo(Math.PI / 2, 6);
    // Double-click / same start point signals full circle.
    expect(arcFromCenterStartEnd({ x: 0, y: 0 }, { x: 2, y: 0 }, { x: 2, y: 0 }, true)).toBeNull();
    const pair = circleAsTwoArcs({ x: 1, y: 2 }, { x: 3, y: 2 })!;
    expect(pair).toHaveLength(2);
    expect(arcFrame(pair[0]).r).toBeCloseTo(2, 6);
    expect(arcFrame(pair[1]).r).toBeCloseTo(2, 6);
  });

  it('adjusts a curved element radius maintaining its centre location', () => {
    const orig = arcFromCenterStartEnd({ x: 5, y: 5 }, { x: 7, y: 5 }, { x: 5, y: 7 }, true)!;
    const resized = arcWithRadius(orig, 4)!;
    const f = arcFrame(resized);
    expect(f.centre.x).toBeCloseTo(5, 6);
    expect(f.centre.y).toBeCloseTo(5, 6);
    expect(f.r).toBeCloseTo(4, 6);
    expect(resized.a.x).toBeCloseTo(9, 6);
    expect(resized.a.y).toBeCloseTo(5, 6);
    expect(resized.b.x).toBeCloseTo(5, 6);
    expect(resized.b.y).toBeCloseTo(9, 6);
  });

  it('adjusts an arc angle maintaining centre and radius, and detects full circle when endpoints meet', () => {
    const orig = arcFromCenterStartEnd({ x: 0, y: 0 }, { x: 3, y: 0 }, { x: 0, y: 3 }, true)!;
    // Extend end to 180° (-3, 0).
    const ext = arcWithEndAngle(orig, 'end', { x: -3, y: 0 });
    expect(ext.fullCircle).toBe(false);
    const f = arcFrame(ext.arc);
    expect(f.centre.x).toBeCloseTo(0, 6);
    expect(f.centre.y).toBeCloseTo(0, 6);
    expect(f.r).toBeCloseTo(3, 6);
    expect(f.sweep).toBeCloseTo(Math.PI, 5);
    // Numerical angle override (60°).
    const num = arcWithEndAngle(orig, 'end', { x: 0, y: 0 }, 60);
    expect(arcFrame(num.arc).sweep).toBeCloseTo(Math.PI / 3, 5);
    // Dragging end onto start detects full circle.
    const full = arcWithEndAngle(orig, 'end', { x: 3, y: 0.01 });
    expect(full.fullCircle).toBe(true);
  });

  it('constructs an arc tangentially following a departing direction', () => {
    // Departing +X from (0,0) to (2,2): quarter circle centred at (0,2), R=2.
    const a = arcTangentFrom({ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 2, y: 2 })!;
    expect(a).not.toBeNull();
    const f = arcFrame(a);
    expect(f.centre.x).toBeCloseTo(0, 5);
    expect(f.centre.y).toBeCloseTo(2, 5);
    expect(f.r).toBeCloseTo(2, 5);
    const t0 = tangentAt(a, 0);
    expect(t0.x).toBeCloseTo(1, 5);
    expect(t0.y).toBeCloseTo(0, 5);
  });

  it('solves a circle tangent to two perpendicular lines and passing through a point', () => {
    // Tangent to X-axis (y=0) and Y-axis (x=0), passing through (2, 1) or (1, 1 - sqrt(2) ...):
    // Circle centred at (1, 1) with R=1 passes through (1, 0), (0, 1), and (1 + cos(45°), 1 + sin(45°)).
    const pt = { x: 1 + Math.SQRT1_2, y: 1 + Math.SQRT1_2 };
    const sol = circleFromTwoTangentsAndPoint(
      { a: { x: 0, y: 0 }, b: { x: 10, y: 0 } },
      { a: { x: 0, y: 0 }, b: { x: 0, y: 10 } },
      pt,
    )!;
    expect(sol).not.toBeNull();
    expect(Math.hypot(sol.c.x - pt.x, sol.c.y - pt.y)).toBeCloseTo(sol.r, 5);
    expect(Math.abs(sol.c.y)).toBeCloseTo(sol.r, 5);
    expect(Math.abs(sol.c.x)).toBeCloseTo(sol.r, 5);
  });

  it('snaps to center, quadrant, and radial perpendicular on model element curves', () => {
    const wallArc = arcFromCenterStartEnd({ x: 4, y: 4 }, { x: 6, y: 4 }, { x: 4, y: 6 }, true)!;
    const curve = arcShape(wallArc);
    const cenSnap = findSnap({ x: 4.02, y: 4.01 }, [], null, {
      tolerance: 0.2,
      modes: new Set(['center']),
      curves: [curve],
    });
    expect(cenSnap?.mode).toBe('center');
    expect(cenSnap?.point.x).toBeCloseTo(4, 5);
    expect(cenSnap?.point.y).toBeCloseTo(4, 5);

    const perpSnap = findSnap(middle(wallArc), [], null, {
      tolerance: 0.2,
      modes: new Set(['perpendicular']),
      from: { x: 8, y: 8 },
      curves: [curve],
    });
    expect(perpSnap?.mode).toBe('perpendicular');
  });
});
