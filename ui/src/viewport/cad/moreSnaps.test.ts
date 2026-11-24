/** Forum topic 151: the Wall axis and Tangent snaps, their place in the snaps' order, the element curves and axes they take, the preference that turns them on. */

import { describe, expect, it } from 'vitest';
import type { Vec2 } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { ALL_MODES, findSnap, type SnapMode } from './snaps';
import type { Shape } from './shapes';
import { elementCurves, wallAxes } from './elementSnaps';
import { resolveAim } from './aim';
import { arcShape } from '../arc';
import { readPrefs, SNAP_MODES } from '../../app/prefs';

const P = (x: number, y: number): Vec2 => ({ x, y });
const nearPt = (p: Vec2, q: Vec2, digits = 7) => { expect(p.x).toBeCloseTo(q.x, digits); expect(p.y).toBeCloseTo(q.y, digits); };
const modes = (...m: SnapMode[]) => new Set<SnapMode>(m);
const node = (id: string, type: string, props: Record<string, unknown>): NodeDto => ({ id, type, name: id, props } as unknown as NodeDto);

describe('the Wall axis snap', () => {
  // A wall 0.3 thick along y = 0 from x = 0 to x = 10: its faces at y = ±0.15, its axis at y = 0.
  const faces: Shape[] = [{ type: 'polyline', pts: [P(0, -0.15), P(10, -0.15), P(10, 0.15), P(0, 0.15)], closed: true }];
  const axis: Shape[] = [{ type: 'line', a: P(0, 0), b: P(10, 0) }];

  it('takes the point on the axis nearest the cursor', () => {
    const s = findSnap(P(3.3, 0.04), faces, null, { tolerance: 0.1, modes: ALL_MODES, wallAxes: axis });
    expect(s?.mode).toBe('wallAxis');
    expect(s?.along).toBe(true);
    nearPt(s!.point, P(3.3, 0));
    expect(s?.on).toEqual(axis[0]);
  });

  it("takes the axis' ends and middle as points, before a midpoint", () => {
    const end = findSnap(P(0.05, 0.02), faces, null, { tolerance: 0.1, modes: ALL_MODES, wallAxes: axis });
    expect(end?.mode).toBe('wallAxis');
    expect(end?.along).toBeFalsy();
    nearPt(end!.point, P(0, 0));
    const mid = findSnap(P(5.04, 0.03), faces, null, { tolerance: 0.2, modes: ALL_MODES, wallAxes: axis });
    expect(mid?.mode).toBe('wallAxis');
    expect(mid?.along).toBeFalsy();
    nearPt(mid!.point, P(5, 0));
  });

  it('never takes priority over an endpoint or an intersection of the drawn lines', () => {
    // The face's corner (0, 0.15) and the axis' end (0, 0) both within reach: the corner wins.
    const s = findSnap(P(0.02, 0.1), faces, null, { tolerance: 0.2, modes: ALL_MODES, wallAxes: axis });
    expect(s?.mode).toBe('endpoint');
    nearPt(s!.point, P(0, 0.15));
    // A line crossing the face at (4, 0.15): the intersection beats the axis' middle-less line.
    const cross: Shape[] = [...faces, { type: 'line', a: P(4, -1), b: P(4, 1) }];
    const x = findSnap(P(4.02, 0.1), cross, null, { tolerance: 0.2, modes: ALL_MODES, wallAxes: axis });
    expect(x?.mode).toBe('intersection');
  });

  it('on a line, the nearer of the axis and a drawn line wins', () => {
    // 0.03 from the face, 0.12 from the axis: nearest on the face.
    const face = findSnap(P(3, 0.12), faces, null, { tolerance: 0.2, modes: ALL_MODES, wallAxes: axis });
    expect(face?.mode).toBe('nearest');
    nearPt(face!.point, P(3, 0.15));
    // 0.04 from the axis, 0.11 from the face: the axis.
    const ax = findSnap(P(3, -0.04), faces, null, { tolerance: 0.2, modes: ALL_MODES, wallAxes: axis });
    expect(ax?.mode).toBe('wallAxis');
  });

  it('turned off, the axes give nothing', () => {
    const s = findSnap(P(3.3, 0.04), faces, null, { tolerance: 0.1, modes: modes('endpoint', 'nearest'), wallAxes: axis });
    expect(s).toBeNull();
  });

  it('a curved axis: on the arc, its middle on the arc', () => {
    const arc = arcShape({ a: P(0, 0), b: P(10, 0), rise: 2 });
    expect(arc.type).toBe('arc');
    const mid = findSnap(P(5.05, 2.02), [], null, { tolerance: 0.2, modes: ALL_MODES, wallAxes: [arc] });
    expect(mid?.mode).toBe('wallAxis');
    nearPt(mid!.point, P(5, 2));
    // r = (25 + 4) / 4 = 7.25, centre (5, −5.25): a point on the arc at 60° from the centre's right.
    const on = { x: 5 + 7.25 * Math.cos(Math.PI / 3), y: -5.25 + 7.25 * Math.sin(Math.PI / 3) };
    const s = findSnap(P(on.x + 0.05 * Math.cos(Math.PI / 3), on.y + 0.05 * Math.sin(Math.PI / 3)), [], null, { tolerance: 0.2, modes: ALL_MODES, wallAxes: [arc] });
    expect(s?.along).toBe(true);
    nearPt(s!.point, on);
  });
});

describe('the Tangent snap', () => {
  const circle: Shape = { type: 'circle', c: P(0, 0), r: 5 };
  // From (10, 0) the circle of radius 5 is touched at ±60°: (2.5, ±4.330127).
  const touch = P(2.5, 5 * Math.sin(Math.PI / 3));

  it('takes the point where a line from the previous point touches a circle', () => {
    const s = findSnap(P(2.55, 4.3), [circle], null, { tolerance: 0.2, modes: ALL_MODES, from: P(10, 0) });
    expect(s?.mode).toBe('tangent');
    nearPt(s!.point, touch);
    const below = findSnap(P(2.45, -4.36), [circle], null, { tolerance: 0.2, modes: ALL_MODES, from: P(10, 0) });
    nearPt(below!.point, P(2.5, -touch.y));
  });

  it('needs a previous point, and one outside the curve', () => {
    expect(findSnap(P(2.55, 4.3), [circle], null, { tolerance: 0.2, modes: modes('tangent') })).toBeNull();
    expect(findSnap(P(2.55, 4.3), [circle], null, { tolerance: 0.2, modes: modes('tangent'), from: P(1, 0) })).toBeNull();
  });

  it('on an arc only where the arc runs', () => {
    const upper: Shape = { type: 'arc', c: P(0, 0), r: 5, start: 0, end: Math.PI };
    expect(findSnap(P(2.55, 4.3), [upper], null, { tolerance: 0.2, modes: modes('tangent'), from: P(10, 0) })?.mode).toBe('tangent');
    expect(findSnap(P(2.45, -4.36), [upper], null, { tolerance: 0.2, modes: modes('tangent'), from: P(10, 0) })).toBeNull();
  });

  it('on an ellipse, exactly', () => {
    // x²/16 + y²/4 = 1 seen from (0, 10): touched at (∓3.919184, 0.4).
    const e: Shape = { type: 'ellipse', c: P(0, 0), m: P(4, 0), ratio: 0.5, start: 0, end: 0 };
    const s = findSnap(P(-3.9, 0.45), [e], null, { tolerance: 0.2, modes: modes('tangent', 'nearest'), from: P(0, 10) });
    expect(s?.mode).toBe('tangent');
    nearPt(s!.point, P(-16 * Math.sqrt(6) / 10, 0.4));
  });

  it('ahead of the centre taken from the curve and of the quadrant, behind a midpoint', () => {
    // From (0, 10), a circle of radius 5 at the origin is touched at (±4.330127, 2.5): near it, no quadrant within 0.2.
    const s = findSnap(P(4.3, 2.55), [circle], null, { tolerance: 0.2, modes: ALL_MODES, from: P(0, 10) });
    expect(s?.mode).toBe('tangent');
    // From (0, 1000) the circle is touched at (4.99994, 0.025), beside the quadrant (5, 0): the tangent, which needs the line drawn.
    const q = findSnap(P(5, 0.01), [circle], null, { tolerance: 0.1, modes: ALL_MODES, from: P(0, 1000) });
    expect(q?.mode).toBe('tangent');
    nearPt(q!.point, P(5 * Math.cos(Math.asin(0.005)), 0.025));
    // Without a line drawn (no previous point) the circle gives its centre, as before.
    expect(findSnap(P(5, 0.01), [circle], null, { tolerance: 0.1, modes: ALL_MODES })?.mode).toBe('center');
    // A line's midpoint near the tangent point (a line outside the circle, along it) goes first.
    const mid = findSnap(P(4.3, 2.55), [circle, { type: 'line', a: P(4.6255, 2.175), b: P(4.1145, 3.035) }], null, { tolerance: 0.2, modes: ALL_MODES, from: P(0, 10) });
    expect(mid?.mode).toBe('midpoint');
  });

  it("an element's own curves (a curved wall's faces) are touched too", () => {
    const s = findSnap(P(2.55, 4.3), [], null, { tolerance: 0.2, modes: ALL_MODES, from: P(10, 0), curves: [circle] });
    expect(s?.mode).toBe('tangent');
    nearPt(s!.point, touch);
    expect(s?.on).toEqual(circle);
  });

  it('holds the line tangent: a drafted curve is named, as the tangent inference names it', () => {
    const a = resolveAim(P(2.55, 4.3), {
      base: P(10, 0), ortho: false, polar: false, acquired: [], tol: 0.2,
      snap: { point: touch, mode: 'tangent', shape: circle },
      relate: { sides: [], curves: [{ ref: { shape: 'c1', part: 'whole' }, c: P(0, 0), r: 5 }], reach: 1 },
    });
    expect(a.mode).toBe('tangent');
    expect(a.lock).toBe('tangent');
    expect(a.to).toEqual({ shape: 'c1', part: 'whole' });
    nearPt(a.point, touch);
  });
});

describe('the Wall axis on a line, with the grid', () => {
  it('slides along the axis to the nearest grid line, as the nearest snap does', () => {
    const axis: Shape = { type: 'line', a: P(0, 0), b: P(10, 5) };
    const a = resolveAim(P(3.1, 1.6), { base: null, ortho: false, polar: false, acquired: [], tol: 0.2, grid: 1, snap: { point: P(3.12, 1.56), mode: 'wallAxis', shape: axis, along: true } });
    expect(a.mode).toBe('wallAxis');
    nearPt(a.point, P(3, 1.5));
    // slid along a line: not a point of its own, so a door placed there still takes the grid along the wall (forum topic 226)
    expect(a.exact).toBeUndefined();
    const mid = resolveAim(P(5.1, 2.6), { base: null, ortho: false, polar: false, acquired: [], tol: 0.2, grid: 1, snap: { point: P(5, 2.5), mode: 'midpoint', shape: axis } });
    expect(mid.exact).toBe(true);
    nearPt(mid.point, P(5, 2.5));
  });
});

describe('what the plan gives the new snaps', () => {
  const straight = node('w1', 'IfcWall', { start: [0, 0], end: [10, 0] });
  const curved = node('w2', 'IfcWall', { start: [0, 0], end: [10, 0], arc: 2 });
  const beam = node('b1', 'IfcBeam', { start: [0, 5], end: [10, 5] });

  it("wall axes: each shown wall's axis, straight or curved", () => {
    const axes = wallAxes([{ nodeId: 'w1' }, { nodeId: 'w2' }, { nodeId: 'b1' }], new Map([[straight.id, straight], [curved.id, curved], [beam.id, beam]]));
    expect(axes.length).toBe(2);
    expect(axes[0]).toEqual({ type: 'line', a: P(0, 0), b: P(10, 0) });
    expect(axes[1].type).toBe('arc');
    if (axes[1].type === 'arc') { expect(axes[1].r).toBeCloseTo(7.25, 9); nearPt(axes[1].c, P(5, -5.25)); }
  });

  it("a curved wall's faces, from its cut's points on circles round its axis' centre", () => {
    // The axis' circle: centre (5, −5.25), radius 7.25; the faces 0.15 either side, chorded every 10°.
    const c = P(5, -5.25), r = 7.25;
    const a0 = Math.atan2(5.25, -5), a1 = Math.atan2(5.25, 5);
    const ring = (rad: number) => Array.from({ length: 8 }, (_, i) => { const t = a1 + ((a0 - a1) * i) / 7; return [c.x + rad * Math.cos(t), c.y + rad * Math.sin(t)]; });
    const outer = ring(r + 0.15), inner = ring(r - 0.15).reverse();
    const item = { nodeId: 'w2', plan: [{ kind: 'cut', closed: true, points: [...outer, ...inner].flat() }] };
    const curves = elementCurves([item], new Map([[curved.id, curved]]));
    const radii = curves.map((s) => (s.type === 'arc' ? s.r : 0)).sort((x, y) => x - y);
    expect(radii.length).toBe(3);
    expect(radii[0]).toBeCloseTo(7.1, 9);
    expect(radii[1]).toBeCloseTo(7.25, 9);
    expect(radii[2]).toBeCloseTo(7.4, 9);
  });

  it("an outline's curved edges", () => {
    const slab = node('s1', 'IfcSlab', { contour: [[0, 0], [10, 0], [10, 6], [0, 6]], arcs: [0, 0, 2, 0] });
    const curves = elementCurves([{ nodeId: 's1', plan: [] }], new Map([[slab.id, slab]]));
    expect(curves.length).toBe(1);
    expect(curves[0].type).toBe('arc');
  });
});

describe('the snap preferences', () => {
  it('lists Tangent and Wall axis, both on by default', () => {
    expect(SNAP_MODES).toContain('tangent');
    expect(SNAP_MODES).toContain('wallAxis');
    expect(readPrefs(null).snapModes).toEqual(SNAP_MODES.filter((m) => m !== 'division' && m !== 'distance')); // Divisions and Distance are off until ticked
  });
  it('turns the new snaps on for settings kept before they existed, and keeps a choice made since', () => {
    const before = readPrefs({ snapModes: ['endpoint', 'midpoint', 'intersection', 'center', 'quadrant', 'perpendicular', 'nearest'] });
    expect(before.snapModes).toContain('tangent');
    expect(before.snapModes).toContain('wallAxis');
    const off = readPrefs({ snapModes: ['endpoint', 'tangent'], snapModesKnown: [...SNAP_MODES] });
    expect(off.snapModes).toEqual(['endpoint', 'tangent']);
  });
});
