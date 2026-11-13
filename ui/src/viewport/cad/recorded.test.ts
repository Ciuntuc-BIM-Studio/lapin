import { describe, expect, it } from 'vitest';
import { drawConstrainedArgs, inferConstraints, newShapeId, newShapeIndex, sketchViews, type PlacedPoint } from './recorded';
import type { Shape } from './shapes';

const P = (x: number, y: number) => ({ x, y });
const existing: Shape = { type: 'polyline', pts: [P(0, 0), P(4, 0), P(4, 3), P(0, 3)], closed: true };
const world = new Map<string, Shape>([['rect', existing], ['circ', { type: 'circle', c: P(10, 0), r: 2 }]]);
const placed = (p: { x: number; y: number }, inference: PlacedPoint['inference'] = null, extra: Partial<PlacedPoint> = {}): PlacedPoint => ({ p, inference, free: false, ...extra });
const line = (a: { x: number; y: number }, b: { x: number; y: number }): Shape => ({ type: 'line', a, b });

describe('constraints recorded from inference', () => {
  it('an endpoint snap is a coincidence with the part it landed on', () => {
    const r = inferConstraints({ id: 'n', shape: line(P(4, 3), P(8, 5)) }, [placed(P(4, 3), { mode: 'endpoint', shapeId: 'rect' }), placed(P(8, 5))], world);
    expect(r).toEqual([{ kind: 'coincident', refs: [{ shape: 'n', part: 'start' }, { shape: 'rect', part: 'vertex:2' }] }]);
  });
  it('a centre snap is a coincidence with the centre', () => {
    const r = inferConstraints({ id: 'n', shape: line(P(10, 0), P(12, 5)) }, [placed(P(10, 0), { mode: 'center', shapeId: 'circ' }), placed(P(12, 5))], world);
    expect(r[0]).toEqual({ kind: 'coincident', refs: [{ shape: 'n', part: 'start' }, { shape: 'circ', part: 'center' }] });
  });
  it('a midpoint snap is the midpoint of the edge it lies on', () => {
    const r = inferConstraints({ id: 'n', shape: line(P(2, 0), P(2, -4)) }, [placed(P(2, 0), { mode: 'midpoint', shapeId: 'rect' }), placed(P(2, -4))], world);
    expect(r).toEqual([{ kind: 'midpoint', refs: [{ shape: 'n', part: 'start' }, { shape: 'rect', part: 'edge:0' }] }]);
  });
  it('on edge, a quadrant and an intersection put the point on the object', () => {
    const r = inferConstraints({ id: 'n', shape: line(P(4, 1.5), P(8, 1.5)) }, [placed(P(4, 1.5), { mode: 'nearest', shapeId: 'rect' }), placed(P(8, 1.5))], world);
    expect(r).toEqual([{ kind: 'pointOn', refs: [{ shape: 'n', part: 'start' }, { shape: 'rect', part: 'edge:1' }] }]);
    const q = inferConstraints({ id: 'n', shape: line(P(12, 0), P(15, 0)) }, [placed(P(12, 0), { mode: 'quadrant', shapeId: 'circ' }), placed(P(15, 0))], world);
    expect(q[0]).toEqual({ kind: 'pointOn', refs: [{ shape: 'n', part: 'start' }, { shape: 'circ', part: 'whole' }] });
  });
  it('a perpendicular snap is a point on the edge and a perpendicular side', () => {
    const r = inferConstraints({ id: 'n', shape: line(P(8, 5), P(2, 0)) }, [placed(P(8, 5)), placed(P(2, 0), { mode: 'perpendicular', shapeId: 'rect' })], world);
    expect(r.map((c) => c.kind)).toEqual(['pointOn', 'perpendicular']);
    expect(r[1].refs).toEqual([{ shape: 'n', part: 'whole' }, { shape: 'rect', part: 'edge:0' }]);
  });
  it('Ctrl places a point free: no constraint for it', () => {
    const r = inferConstraints({ id: 'n', shape: line(P(4, 3), P(8, 5)) }, [placed(P(4, 3), { mode: 'endpoint', shapeId: 'rect' }, { free: true }), placed(P(8, 5))], world);
    expect(r).toEqual([]);
  });
  it('a side drawn along an ortho or polar line is horizontal or vertical; typing it a length makes a dimension', () => {
    const h = inferConstraints({ id: 'n', shape: line(P(0, 5), P(6, 5)) }, [placed(P(0, 5)), placed(P(6, 5), { lock: 'ortho' }, { typed: { length: 6 } })], world);
    expect(h).toEqual([{ kind: 'horizontal', refs: [{ shape: 'n', part: 'whole' }] }, { kind: 'distance', refs: [{ shape: 'n', part: 'whole' }], value: 6 }]);
    const v = inferConstraints({ id: 'n', shape: line(P(0, 5), P(0, 8)) }, [placed(P(0, 5)), placed(P(0, 8), { lock: 'polar' })], world);
    expect(v).toEqual([{ kind: 'vertical', refs: [{ shape: 'n', part: 'whole' }] }]);
  });
  it('a side that happens to be level but was not held is left alone', () => {
    expect(inferConstraints({ id: 'n', shape: line(P(0, 5), P(6, 5)) }, [placed(P(0, 5)), placed(P(6, 5))], world)).toEqual([]);
  });
  it('a typed angle off the axes is a dimension; one on them is not repeated; one the plan does not share is dropped', () => {
    const a = Math.PI / 6;
    const s = line(P(0, 0), P(Math.cos(a) * 4, Math.sin(a) * 4));
    const r = inferConstraints({ id: 'n', shape: s }, [placed(P(0, 0)), placed(s.type === 'line' ? s.b : P(0, 0), null, { typed: { length: 4, angle: 30 } })], world);
    expect(r.map((c) => c.kind)).toEqual(['distance', 'angle']);
    expect(r[1].value).toBeCloseTo(30);
    const flat = inferConstraints({ id: 'n', shape: line(P(0, 5), P(6, 5)) }, [placed(P(0, 5)), placed(P(6, 5), { lock: 'ortho' }, { typed: { angle: 0 } })], world);
    expect(flat.map((c) => c.kind)).toEqual(['horizontal']);
    const turned = inferConstraints({ id: 'n', shape: s }, [placed(P(0, 0)), placed(s.type === 'line' ? s.b : P(0, 0), null, { typed: { angle: 45 } })], world);
    expect(turned).toEqual([]);
  });
  it('a polyline keeps each vertex and each side', () => {
    const s: Shape = { type: 'polyline', pts: [P(0, 5), P(4, 5), P(4, 9)], closed: false };
    const r = inferConstraints({ id: 'n', shape: s }, [placed(P(0, 5), { mode: 'endpoint', shapeId: 'rect' }), placed(P(4, 5), { lock: 'ortho' }), placed(P(4, 9), { lock: 'ortho' })], world);
    expect(r.map((c) => `${c.kind}:${c.refs[0].part}`)).toEqual(['horizontal:edge:0', 'vertical:edge:1']);
  });
  it('a rectangle is two horizontal and two vertical sides', () => {
    const rect: Shape = { type: 'polyline', pts: [P(0, 0), P(5, 0), P(5, 2), P(0, 2)], closed: true };
    const r = inferConstraints({ id: 'n', shape: rect }, [placed(P(0, 0)), placed(P(5, 2))], world, { hints: { rect: true } });
    expect(r.map((c) => c.kind)).toEqual(['horizontal', 'vertical', 'horizontal', 'vertical']);
    expect(inferConstraints({ id: 'n', shape: { type: 'polyline', pts: [P(0, 0), P(5, 0), P(5, 0), P(0, 0)], closed: true } }, [], world, { hints: { rect: true } })).toEqual([]);
  });
  it('a line begun where the last one ended is joined to it', () => {
    const prev = { id: 'p', shape: line(P(0, 0), P(3, 0)) };
    const r = inferConstraints({ id: 'n', shape: line(P(3, 0), P(3, 4)) }, [placed(P(3, 0)), placed(P(3, 4))], world, { prev });
    expect(r).toEqual([{ kind: 'coincident', refs: [{ shape: 'n', part: 'start' }, { shape: 'p', part: 'end' }] }]);
  });
  it('a typed radius is a driving dimension', () => {
    const r = inferConstraints({ id: 'c', shape: { type: 'circle', c: P(0, 0), r: 1.5 } }, [placed(P(0, 0))], world, { hints: { radius: 1.5 } });
    expect(r).toEqual([{ kind: 'radius', refs: [{ shape: 'c', part: 'whole' }], value: 1.5 }]);
  });
});

describe('which views are sketches', () => {
  it('are the views whose drafted shapes build a solid', () => {
    const nodes = [
      { id: 'd1', props: { predefinedType: 'DRAFT', viewId: 'plan:a' } }, { id: 'd2', props: { predefinedType: 'DRAFT', viewId: 'plan:b' } },
      { id: 's1', props: { operation: 'extrude', profile: 'd1' } }, { id: 's2', props: { operation: '', profile: 'd2' } }, { id: 'w', props: {} },
    ];
    expect([...sketchViews(nodes)]).toEqual(['plan:a']);
  });
});

describe('a tangent arc continued from a line', () => {
  it('is joined to the line\'s end, end to end, and tangent there (the arc\'s start counter-clockwise, its end clockwise)', () => {
    const tf = { ref: { shape: 'ln', part: 'end' }, p: P(0, 0) };
    const ccw: Shape = { type: 'arc', c: P(0, 1), r: 1, start: 1.5 * Math.PI, end: 0 };
    expect(inferConstraints({ id: newShapeId(0), shape: ccw }, [placed(P(1, 1))], world, { hints: { tangentFrom: tf } })).toEqual([
      { kind: 'coincident', refs: [{ shape: newShapeId(0), part: 'start' }, { shape: 'ln', part: 'end' }] },
      { kind: 'tangent', refs: [{ shape: 'ln', part: 'end' }, { shape: newShapeId(0), part: 'start' }] },
    ]);
    // Clockwise: the point leaving the line is the arc's own end.
    const cw: Shape = { type: 'arc', c: P(0, -2), r: 2, start: 0, end: Math.PI / 2 };
    const r = inferConstraints({ id: newShapeId(0), shape: cw }, [placed(P(2, -2))], world, { hints: { tangentFrom: tf, cw: true } });
    expect(r.map((c) => c.refs[c.refs.length - 1].part)).toEqual(['end', 'end']);
    const args = drawConstrainedArgs({}, r);
    expect(args.constraints[1]).toEqual({ kind: 'tangent', refs: [{ shape: 'ln', part: 'end' }, { new: 0, part: 'end' }] });
  });
});

describe('the drawConstrained request', () => {
  it('sends the drawing as drafting.draw takes it, with refs to the new shapes written {new: i, part}', () => {
    const draw = { viewId: 'v', layer: '0', shapes: [{ type: 'line', a: [0, 0], b: [4, 0] }, { type: 'line', a: [4, 0], b: [4, 3] }] };
    const specs = [
      ...inferConstraints({ id: newShapeId(0), shape: line(P(4, 3), P(8, 3)) }, [placed(P(4, 3), { mode: 'endpoint', shapeId: 'rect' }), placed(P(8, 3), { lock: 'ortho' }, { typed: { length: 4 } })], world),
      { kind: 'coincident', refs: [{ shape: newShapeId(1), part: 'start' }, { shape: newShapeId(0), part: 'end' }] },
    ];
    expect(drawConstrainedArgs(draw, specs)).toEqual({
      ...draw,
      constraints: [
        { kind: 'coincident', refs: [{ new: 0, part: 'start' }, { shape: 'rect', part: 'vertex:2' }] },
        { kind: 'horizontal', refs: [{ new: 0, part: 'whole' }] },
        { kind: 'distance', refs: [{ new: 0, part: 'whole' }], value: 4 },
        { kind: 'coincident', refs: [{ new: 1, part: 'start' }, { new: 0, part: 'end' }] },
      ],
    });
  });
  it('tells a placeholder id from a drafted shape\'s own', () => {
    expect(newShapeIndex(newShapeId(3))).toBe(3);
    expect(newShapeIndex('rect')).toBeNull();
  });
});
