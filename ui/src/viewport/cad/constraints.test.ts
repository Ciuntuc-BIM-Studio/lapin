import { describe, expect, it } from 'vitest';
import {
  autoPlace, candidatesAt, canExtend, choose, chipOf, classify, constraintState, dimensionDraw, dimensionFor, placeAt, dimensionText, glyphAnchor, isComplete, isFinal,
  measure, parseDimensionInput, sketchPartOf, pointOf, segmentOf, shapeStates, spreadGlyphs, wanted, freePoints, type ConstraintDto, type SketchStatus,
} from './constraints';
import type { Shape } from './shapes';

const line: Shape = { type: 'line', a: { x: 0, y: 0 }, b: { x: 4, y: 0 } };
const rect: Shape = { type: 'polyline', pts: [{ x: 0, y: 0 }, { x: 4, y: 0 }, { x: 4, y: 3 }, { x: 0, y: 3 }], closed: true };
const circle: Shape = { type: 'circle', c: { x: 1, y: 1 }, r: 2 };
const len = (m: number) => `${m} m`;
const parseLen = (s: string) => { const m = /^(-?[\d.]+)\s*(mm|m)?$/.exec(s); return m ? Number(m[1]) / (m[2] === 'mm' ? 1000 : 1) : null; };

describe('which picks a kind takes', () => {
  it('follows the engine table: prefixes, whole patterns, the other way round', () => {
    expect(canExtend('coincident', [])).toBe(true);
    expect(canExtend('coincident', ['point'])).toBe(true);
    expect(canExtend('coincident', ['line'])).toBe(false);
    expect(isComplete('coincident', ['point', 'point'])).toBe(true);
    expect(isComplete('pointOn', ['line', 'point'])).toBe(true);
    expect(isComplete('pointOn', ['point', 'curve'])).toBe(true);
    expect(isComplete('parallel', ['line', 'point'])).toBe(false);
    expect(canExtend('symmetric', ['point', 'point'])).toBe(true);
    expect(isComplete('symmetric', ['point', 'point', 'line'])).toBe(true);
  });
  it('applies at once only when the picks cannot grow', () => {
    expect(isFinal('horizontal', ['line'])).toBe(true);
    expect(isFinal('horizontal', ['point'])).toBe(false);
    expect(isFinal('horizontal', ['point', 'point'])).toBe(true);
    expect(isFinal('distanceX', ['point'])).toBe(false);
    expect(isComplete('distanceX', ['point'])).toBe(true);
    expect(isFinal('radius', ['curve'])).toBe(true);
    expect(isFinal('angle', ['line'])).toBe(false);
    expect(isFinal('fix', ['shape'])).toBe(true);
  });
  it('says what comes next', () => {
    expect(wanted('tangent', [])).toBe('a line or a circle or an arc or a point');
    expect(wanted('pointOn', ['point'])).toBe('a line or a circle or an arc');
    expect(wanted('radius', ['curve'])).toBe('nothing');
  });
});

describe('the part of a shape a click means', () => {
  it('names parts the way the engine does', () => {
    expect(classify(line, 'edge:0')).toEqual({ kind: 'line', part: 'whole' });
    expect(classify(rect, 'edge:3')).toEqual({ kind: 'line', part: 'edge:3' });
    expect(classify(rect, 'edge:4')).toBeNull();
    expect(classify({ type: 'polyline', pts: [{ x: 0, y: 0 }, { x: 1, y: 0 }], closed: false }, 'end')).toEqual({ kind: 'point', part: 'vertex:1' });
    expect(classify(circle, 'center')).toEqual({ kind: 'point', part: 'center' });
    expect(classify(circle, 'start')).toBeNull();
  });
  it('prefers a point over an edge near it, and the nearest edge of a polyline', () => {
    const c = candidatesAt(rect, { x: 3.98, y: 0.02 }, 0.1);
    expect(c[0]).toMatchObject({ part: 'vertex:1', kind: 'point' });
    expect(c.map((x) => x.part)).toContain('edge:0');
    const e = candidatesAt(rect, { x: 2, y: 0.02 }, 0.1);
    expect(e[0]).toMatchObject({ part: 'edge:0', kind: 'line' });
    expect(candidatesAt(circle, { x: 3.02, y: 1 }, 0.1)[0]).toMatchObject({ part: 'whole', kind: 'curve' });
    expect(candidatesAt(circle, { x: 1.02, y: 1 }, 0.1)[0]).toMatchObject({ part: 'center', kind: 'point' });
    expect(candidatesAt(line, { x: 2, y: 2 }, 0.1)).toEqual([]);
  });
  it('chooses the first part the kind can take and skips one already picked', () => {
    const found = [{ shape: 'a', candidates: candidatesAt(rect, { x: 4, y: 0 }, 0.1) }, { shape: 'b', candidates: candidatesAt(line, { x: 4, y: 0 }, 0.1) }];
    const first = choose('coincident', [], found);
    expect(first).toMatchObject({ ref: { shape: 'a', part: 'vertex:1' }, kind: 'point' });
    // The second click on the same spot meets the other shape's end, not the same vertex again.
    const second = choose('coincident', [{ ref: first!.ref, kind: 'point' }], found);
    expect(second).toMatchObject({ ref: { shape: 'b', part: 'end' } });
    // Parallel takes lines: a click on a corner falls through to an edge.
    expect(choose('parallel', [], found)).toMatchObject({ ref: { shape: 'a', part: 'edge:0' }, kind: 'line' });
  });
  it('reads points and edges', () => {
    expect(pointOf(rect, 'vertex:2')).toEqual({ x: 4, y: 3 });
    expect(segmentOf(rect, 'edge:3')).toEqual([{ x: 0, y: 3 }, { x: 0, y: 0 }]);
    expect(pointOf({ type: 'arc', c: { x: 0, y: 0 }, r: 1, start: 0, end: Math.PI / 2 }, 'end')!.y).toBeCloseTo(1);
  });
});

describe('the point a grip drags', () => {
  it('maps drafting grip keys to parts', () => {
    expect(sketchPartOf(line, 'end')).toBe('end');
    expect(sketchPartOf(line, 'move')).toBeNull();
    expect(sketchPartOf(rect, 'v2')).toBe('vertex:2');
    expect(sketchPartOf(rect, 'e1')).toBeNull();
    expect(sketchPartOf(rect, 'v9')).toBeNull();
    expect(sketchPartOf(circle, 'c')).toBe('center');
    expect(sketchPartOf(circle, 'q0')).toBeNull();
    expect(sketchPartOf({ type: 'arc', c: { x: 0, y: 0 }, r: 1, start: 0, end: 1 }, 's')).toBe('start');
  });
});

describe('the dimension a pick implies', () => {
  it('infers the kind', () => {
    expect(dimensionFor(['point', 'point'])).toEqual({ kind: 'distance', alternatives: ['distanceX', 'distanceY'] });
    expect(dimensionFor(['line'])?.kind).toBe('distance');
    expect(dimensionFor(['line', 'line'])?.kind).toBe('angle');
    expect(dimensionFor(['curve'])).toEqual({ kind: 'radius', alternatives: ['diameter'] });
    expect(dimensionFor(['point', 'curve'])).toBeNull();
  });
  it('measures as the engine does', () => {
    expect(measure('distance', [{ shape: line, part: 'whole' }])).toBe(4);
    expect(measure('distanceX', [{ shape: rect, part: 'vertex:0' }, { shape: rect, part: 'vertex:2' }])).toBe(4);
    expect(measure('distanceY', [{ shape: rect, part: 'vertex:0' }, { shape: rect, part: 'vertex:2' }])).toBe(3);
    expect(measure('distance', [{ shape: rect, part: 'vertex:3' }, { shape: rect, part: 'edge:0' }])).toBe(3);
    expect(measure('radius', [{ shape: circle, part: 'whole' }])).toBe(2);
    expect(measure('diameter', [{ shape: circle, part: 'whole' }])).toBe(4);
    expect(measure('angle', [{ shape: rect, part: 'edge:0' }, { shape: rect, part: 'edge:1' }])).toBeCloseTo(90);
    expect(measure('angle', [{ shape: rect, part: 'edge:1' }, { shape: rect, part: 'edge:0' }])).toBeCloseTo(-90);
    expect(measure('distance', [{ shape: circle, part: 'whole' }])).toBeNull();
  });
});

describe('what is typed for a dimension', () => {
  it('reads a number, a number with its unit, a formula', () => {
    expect(parseDimensionInput('250 mm', 'distance', parseLen)).toEqual({ value: 0.25 });
    expect(parseDimensionInput('= W / 2', 'distance', parseLen)).toEqual({ formula: 'W / 2' });
    expect(parseDimensionInput('45°', 'angle', parseLen)).toEqual({ value: 45 });
    expect(parseDimensionInput('270', 'angle', parseLen)).toEqual({ value: -90 });
    expect(parseDimensionInput('=', 'distance', parseLen)).toHaveProperty('error');
    expect(parseDimensionInput('', 'distance', parseLen)).toHaveProperty('error');
    expect(parseDimensionInput('abc', 'distance', parseLen)).toHaveProperty('error');
    expect(parseDimensionInput('0', 'radius', parseLen)).toHaveProperty('error');
    expect(parseDimensionInput('-1', 'distance', parseLen)).toHaveProperty('error');
    expect(parseDimensionInput('-1', 'distanceX', parseLen)).toEqual({ value: -1 });
  });
  it('writes a dimension: R, Ø, degrees, driven in parentheses', () => {
    expect(dimensionText({ kind: 'distance', value: 4, driving: true }, len)).toBe('4 m');
    expect(dimensionText({ kind: 'radius', value: 2, driving: true }, len)).toBe('R 2 m');
    expect(dimensionText({ kind: 'diameter', value: 4, driving: true }, len)).toBe('Ø 4 m');
    expect(dimensionText({ kind: 'angle', value: 90, driving: true }, len)).toBe('90°');
    expect(dimensionText({ kind: 'distance', measured: 3, driving: false }, len)).toBe('(3 m)');
    expect(dimensionText({ kind: 'distance', driving: true }, len)).toBe('?');
  });
});

const dto = (o: Partial<ConstraintDto> & { id: string; kind: string }): ConstraintDto => ({ refs: [], driving: true, ...o });

describe('where glyphs and dimensions stand', () => {
  const horizontalEdge = dto({ id: 'c1', kind: 'horizontal', refs: [{ shape: 'a', part: 'whole', a: [0, 0], b: [4, 0] }] });
  it('puts a glyph on its edge, point, or midway between two', () => {
    expect(glyphAnchor(horizontalEdge)).toEqual({ x: 2, y: 0 });
    expect(glyphAnchor(dto({ id: 'c', kind: 'coincident', refs: [{ shape: 'a', part: 'start', at: [1, 1] }, { shape: 'b', part: 'end', at: [1, 1] }] }))).toEqual({ x: 1, y: 1 });
    expect(glyphAnchor(dto({ id: 'c', kind: 'parallel', refs: [{ shape: 'a', part: 'whole', a: [0, 0], b: [4, 0] }, { shape: 'b', part: 'whole', a: [0, 2], b: [4, 2] }] }))).toEqual({ x: 2, y: 1 });
    expect(glyphAnchor(dto({ id: 'c', kind: 'fix', refs: [{ shape: 'a', part: 'start', missing: true }] }))).toBeNull();
  });
  it('fans out glyphs that stand together', () => {
    const o = spreadGlyphs([{ x: 10, y: 10 }, { x: 12, y: 11 }, { x: 11, y: 10 }, { x: 200, y: 10 }]);
    expect(o.map((x) => x.dx)).toEqual([0, 15, 30, 0]);
    expect(o.every((x) => x.dy === -12)).toBe(true);
  });
  it('draws a distance parallel to what it measures, offset', () => {
    const d = dimensionDraw(dto({ id: 'd', kind: 'distance', refs: [{ shape: 'a', part: 'whole', a: [0, 0], b: [4, 0] }] }), 0.5)!;
    expect(d.text).toEqual({ x: 2, y: 0.5 });
    expect(d.lines).toHaveLength(3);
    const x = dimensionDraw(dto({ id: 'd', kind: 'distanceX', refs: [{ shape: 'a', part: 'vertex:0', at: [0, 0] }, { shape: 'a', part: 'vertex:2', at: [4, 3] }] }), 0.5)!;
    expect(x.text).toEqual({ x: 2, y: 3.5 });
    const r = dimensionDraw(dto({ id: 'd', kind: 'radius', refs: [{ shape: 'a', part: 'whole', c: [1, 1], r: 2 }] }), 0.5)!;
    expect(r.lines[0][0]).toEqual({ x: 1, y: 1 });
    const g = dimensionDraw(dto({ id: 'd', kind: 'angle', value: 90, refs: [{ shape: 'a', part: 'edge:0', a: [0, 0], b: [4, 0] }, { shape: 'a', part: 'edge:1', a: [4, 0], b: [4, 3] }] }), 0.5)!;
    expect(g.lines[0].length).toBeGreaterThan(6);
    expect(dimensionDraw(dto({ id: 'd', kind: 'radius', refs: [{ shape: 'a', part: 'whole', missing: true }] }), 0.5)).toBeNull();
  });
});

describe('the solved state as colours and a chip', () => {
  const status = (o: Partial<SketchStatus>): SketchStatus => ({ status: 'underconstrained', dof: 3, freeShapes: [], redundant: [], conflicting: [], ...o });
  const cs = [dto({ id: 'c1', kind: 'horizontal', refs: [{ shape: 'a', part: 'whole' }] }), dto({ id: 'c2', kind: 'equal', refs: [{ shape: 'b', part: 'whole' }, { shape: 'c', part: 'whole' }] })];
  it('colours shapes free, fixed or in conflict', () => {
    const m = shapeStates(['a', 'b', 'c', 'd'], cs, status({ freeShapes: [{ shape: 'a', part: 'end' }, { shape: 'd', part: 'whole' }], conflicting: ['c2'] }));
    expect([...m]).toEqual([['a', 'free'], ['b', 'conflict'], ['c', 'conflict'], ['d', 'free']]);
    expect(shapeStates(['a'], cs, status({ dof: 0 })).get('a')).toBe('fixed');
    expect(freePoints(status({ freeShapes: [{ shape: 'a', part: 'end' }, { shape: 'a', part: 'whole' }, { shape: 'a', part: 'vertex:2' }] })).map((p) => p.part)).toEqual(['end', 'vertex:2']);
  });
  it('marks a glyph red when its constraint conflicts', () => {
    expect(constraintState(cs[1], status({ conflicting: ['c2'] }))).toBe('conflict');
    expect(constraintState(cs[0], status({ redundant: ['c1'] }))).toBe('redundant');
    expect(constraintState(cs[0], null)).toBe('ok');
  });
  it('says how free the sketch is', () => {
    expect(chipOf(status({ dof: 3 }))).toEqual({ text: '3 degrees of freedom', tone: 'free' });
    expect(chipOf(status({ dof: 1 })).text).toBe('1 degree of freedom');
    expect(chipOf(status({ dof: 0, status: 'solved' }))).toEqual({ text: 'Fully constrained', tone: 'fixed' });
    expect(chipOf(status({ conflicting: ['c1'] })).tone).toBe('conflict');
    expect(chipOf(status({ redundant: ['c1'], dof: 0 })).tone).toBe('redundant');
    expect(chipOf(null).tone).toBe('unknown');
  });
});

describe('where a dimension stands', () => {
  const rect = { type: 'polyline' as const, pts: [{ x: 0, y: 0 }, { x: 5, y: 0 }, { x: 5, y: 2 }, { x: 0, y: 2 }], closed: true };
  const edge = (i: number) => { const a = rect.pts[i], b = rect.pts[(i + 1) % 4]; return { shape: 'r', part: `edge:${i}`, a: [a.x, a.y], b: [b.x, b.y] }; };
  const side = (i: number, kind = 'distance') => dto({ id: 'd', kind, refs: [edge(i)] });
  it('stands outside a shape, on the side of the edge away from its middle', () => {
    // The bottom side runs +x (its left is up, into the rectangle): the dimension goes below; the top side runs -x: above.
    const bottom = dimensionDraw(side(0), 0.5, { shapes: [rect], scale: 100 })!;
    expect(bottom.text.y).toBeCloseTo(-0.5);
    const top = dimensionDraw(side(2), 0.5, { shapes: [rect], scale: 100 })!;
    expect(top.text.y).toBeCloseTo(2.5);
    const right = dimensionDraw(side(1), 0.5, { shapes: [rect], scale: 100 })!;
    expect(right.text.x).toBeCloseTo(5.5);
  });
  it('clears a small shape whole: a diagonal is dimensioned outside it, a large one only past its own ends', () => {
    const diag = dto({ id: 'd', kind: 'distance', refs: [{ shape: 'r', part: 'vertex:0', at: [0, 0] }, { shape: 'r', part: 'vertex:2', at: [5, 2] }] });
    // Small on screen (5 m at 20 px/m): the line clears the corner (0, 2), 10/√29 to the diagonal's left.
    expect(autoPlace(diag, 0.5, [rect], 20)!.offset).toBeCloseTo(10 / Math.sqrt(29) + 0.5);
    // Large (5 m at 1000 px/m): only past the dimension's own ends.
    const large = autoPlace(side(0), 0.05, [rect], 1000)!;
    expect(large.offset).toBeCloseTo(-0.05);
  });
  it('with no shapes keeps the left, as before', () => {
    expect(autoPlace(side(0), 0.5)!.offset).toBeCloseTo(0.5);
  });
  it('a place the user gave wins; a drag makes the place through the cursor', () => {
    const c = { ...side(0), place: { offset: 1.25 } };
    expect(dimensionDraw(c, 0.5, { shapes: [rect], scale: 100 })!.text).toEqual({ x: 2.5, y: 1.25 });
    expect(placeAt(side(0), { x: 1, y: -0.75 }, 0.5)).toEqual({ offset: -0.75 });
    const vertical = dto({ id: 'd', kind: 'distanceY', refs: [edge(1)] });
    // A vertical distance's offset is to the left of +y: a line at x = 7 is 2 m right of its first end, offset -2.
    expect(placeAt(vertical, { x: 7, y: 1 }, 0.5)).toEqual({ offset: -2 });
    expect(dimensionDraw({ ...vertical, place: { offset: -2 } }, 0.5)!.text).toEqual({ x: 7, y: 1 });
    const radius = dto({ id: 'r', kind: 'radius', refs: [{ shape: 'c', part: 'whole', c: [0, 0], r: 1 }] });
    const p = placeAt(radius, { x: 0, y: 3 }, 0.5)!;
    expect(p.offset).toBeCloseTo(2);
    expect(p.angle).toBeCloseTo(90);
    const t = dimensionDraw({ ...radius, place: p }, 0.5)!.text;
    expect(t.x).toBeCloseTo(0); expect(t.y).toBeCloseTo(3);
  });
});
