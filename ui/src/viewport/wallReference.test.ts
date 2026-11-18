import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../engine/protocol';
import { arcLength, isArc, pointAt } from './arc';
import { wallAxes, wallAxisPoints } from './cad/elementSnaps';
import { axisFromReference, keptReference, offsetArc, referenceArc } from './wallReference';
import { wallLineArgs } from '../modules/architecture/prefs';
import { argsOf, choiceOf } from '../modules/architecture/WallReferenceField';

// The engine's test wall (WallReferenceLineTests): 100 insulation outside, a 250 core, 15 plaster; drawn along +x, the axis at
// y = 0.225 and its exterior face kept as its reference line, 0.225 right of the axis, at y = 0.
const wall = (props: Record<string, unknown>): NodeDto => ({ id: 'w', type: 'IfcWall', name: 'W', parent: null, props } as unknown as NodeDto);
const kept = { start: [0, 0.225], end: [10, 0.225], referenceLine: { kind: 'exteriorFace' }, referenceAxis: { start: [0, 0], end: [10, 0], left: -0.225 } };

describe('a wall\'s reference line (decision 685)', () => {
  it('reads the line the engine worked out, and none on a wall that keeps none', () => {
    const r = referenceArc(kept)!;
    expect(r.left).toBe(-0.225);
    expect(r.arc.a).toEqual({ x: 0, y: 0 });
    expect(r.arc.rise).toBe(0);
    expect(keptReference(kept)).toEqual({ kind: 'exteriorFace' });
    expect(referenceArc({ start: [0, 0], end: [10, 0] })).toBeNull();
    expect(keptReference({ referenceLine: { kind: 'coreCentre' } })).toBeNull();
  });

  it('puts the axis back at its distance from a reference line dragged', () => {
    // The reference line dragged to y = 1 along +x: the axis 0.225 left of it.
    const axis = axisFromReference({ a: { x: 0, y: 1 }, b: { x: 10, y: 1 }, rise: 0 }, -0.225);
    expect(axis.a.y).toBeCloseTo(1.225, 9);
    expect(axis.b.x).toBeCloseTo(10, 9);
  });

  it('sets an arc off concentric with it', () => {
    // A half circle of radius 5 round the origin, anticlockwise from (5, 0) through (0, 5) (a rise right of the chord): left is towards the centre.
    const c = { a: { x: 5, y: 0 }, b: { x: -5, y: 0 }, rise: -5 };
    const inner = offsetArc(c, 0.225);
    expect(isArc(inner)).toBe(true);
    expect(inner.a.x).toBeCloseTo(4.775, 9);
    expect(inner.rise).toBeCloseTo(-4.775, 9);
    expect(arcLength(inner)).toBeCloseTo(Math.PI * 4.775, 6);
    expect(Math.hypot(pointAt(inner, 1).x, pointAt(inner, 1).y)).toBeCloseTo(4.775, 9);
    // And back: the line 0.225 left of its axis gives that axis.
    expect(axisFromReference(inner, 0.225).a.x).toBeCloseTo(5, 9);
  });

  it('lends the Wall axis snap its line and its ends beside the axis', () => {
    const nodes = new Map([['w', wall(kept)]]);
    const shapes = wallAxes([{ nodeId: 'w' }], nodes);
    expect(shapes).toHaveLength(2);
    expect(shapes[1]).toEqual({ type: 'line', a: { x: 0, y: 0 }, b: { x: 10, y: 0 } });
    expect(wallAxisPoints([{ nodeId: 'w' }], nodes)).toEqual([{ x: 0, y: 0.225 }, { x: 10, y: 0.225 }, { x: 0, y: 0 }, { x: 10, y: 0 }]);
    // A wall without one: its axis alone, as before.
    expect(wallAxes([{ nodeId: 'w' }], new Map([['w', wall({ start: [0, 0], end: [10, 0] })]]))).toHaveLength(1);
  });

  it('tells addWall the line chosen and whether the wall keeps it', () => {
    expect(wallLineArgs({ line: 'core', lineOffset: 0, remember: true })).toEqual({});
    expect(wallLineArgs({ line: 'exterior', lineOffset: 0, remember: false })).toEqual({ reference: 'exterior' });
    expect(wallLineArgs({ line: 'exterior', lineOffset: 0, remember: true })).toEqual({ reference: 'exterior', rememberReference: true });
    expect(wallLineArgs({ line: 'offset', lineOffset: 0.15, remember: true })).toEqual({ reference: { kind: 'offset', offset: 0.15 }, rememberReference: true });
    expect(wallLineArgs({ line: 'offset', lineOffset: 0, remember: true })).toEqual({});
  });

  it('shows and sets the reference in Properties', () => {
    expect(choiceOf({})).toBe('coreCentre');
    expect(choiceOf({ referenceLine: { kind: 'layer', layer: 1 } })).toBe('layer:1');
    expect(choiceOf({ referenceLine: { kind: 'offset', offset: 0.3 } })).toBe('offset');
    expect(argsOf('layer:2', 0)).toEqual({ kind: 'layer', layer: 2 });
    expect(argsOf('offset', 0.3)).toEqual({ kind: 'offset', offset: 0.3 });
    expect(argsOf('exteriorFace', 0.3)).toEqual({ kind: 'exteriorFace' });
  });
});
