import { describe, expect, it } from 'vitest';
import { nearestOnShape } from '../../viewport/cad/shapes';
import { bodySketchName, isSketchBody, quickProblem, type FamilyDef } from './families/familyModel';
import {
  dragShapes, editedSketch, gripsOf, recordedConstraints, localShapes, poseForSketch, previewShapes, rayToSketch, shapeAt, shapeFrom, shapesPayload, sketchExtent, sketchToWorld, snapToGrip, statusOf, toolAllowed,
  withReplaced, withShape, withoutShape, worldToSketch, type SketchFrame,
} from './partSketchModel';
import { canConstrain, depthInput, localSketches, sketchBodySteps, sketchRows, stepRows, type PartTree } from './partTreeModel';

const XZ: SketchFrame = { origin: [1, 2, 0.5], right: [1, 0, 0], up: [0, 0, 1], normal: [0, -1, 0] };
const XY: SketchFrame = { origin: [0, 0, 0], right: [1, 0, 0], up: [0, 1, 0], normal: [0, 0, 1] };

describe('the sketch frame', () => {
  it('maps (u, v) to the world and back', () => {
    const w = sketchToWorld(XZ, { x: 0.3, y: 0.4 });
    expect(w).toEqual({ x: 1.3, y: 2, z: 0.9 });
    const p = worldToSketch(XZ, w);
    expect(p.x).toBeCloseTo(0.3); expect(p.y).toBeCloseTo(0.4);
  });
  it('finds where a ray meets the plane', () => {
    // From the front (y = -3) looking +y at the point (1.3, ?, 0.9): the ray hits the plane y = 2.
    const p = rayToSketch(XZ, { x: 1.3, y: -3, z: 0.9 }, { x: 0, y: 1, z: 0 });
    expect(p!.x).toBeCloseTo(0.3); expect(p!.y).toBeCloseTo(0.4);
    // Parallel, or the plane behind the eye: none.
    expect(rayToSketch(XZ, { x: 0, y: 0, z: 0 }, { x: 1, y: 0, z: 0 })).toBeNull();
    expect(rayToSketch(XZ, { x: 0, y: 5, z: 0 }, { x: 0, y: 1, z: 0 })).toBeNull();
  });
});

describe('the camera on the plane', () => {
  const fov = (45 * Math.PI) / 180;
  it('looks down on a plan sketch with its up up the screen', () => {
    const pose = poseForSketch(XY, { x: 1, y: 1 }, 2, fov);
    expect(pose.pitch).toBeLessThan(-1.5);
    expect(pose.azimuth).toBeCloseTo(Math.PI / 2);
    expect(pose.target).toEqual({ x: 1, y: 1, z: 0 });
  });
  it('looks along +y at a front sketch', () => {
    const pose = poseForSketch(XZ, { x: 0, y: 0 }, 1, fov);
    expect(pose.pitch).toBeCloseTo(0);
    expect(pose.azimuth).toBeCloseTo(Math.PI / 2);
    expect(pose.target).toEqual({ x: 1, y: 2, z: 0.5 });
  });
  it('stands farther for a larger sketch', () => {
    expect(poseForSketch(XY, { x: 0, y: 0 }, 4, fov).distance).toBeGreaterThan(poseForSketch(XY, { x: 0, y: 0 }, 1, fov).distance);
  });
  it('centres on the shapes', () => {
    const list = localShapes([{ id: 'p1', type: 'polyline', pts: [[0, 0], [2, 0], [2, 1], [0, 1]], closed: true }]);
    expect(sketchExtent(list)).toEqual({ centre: { x: 1, y: 0.5 }, extent: 2 });
    expect(sketchExtent([]).extent).toBe(1);
  });
});

describe('the shape list sent to the engine', () => {
  const list = localShapes([
    { id: 'p1', type: 'line', a: [0, 0], b: [1, 0] },
    { id: 'p2', type: 'circle', c: [0.5, 0.5], r: 0.2, inProfile: true },
    { id: 'p3', type: 'nonsense' },
  ]);
  it('reads the tree\'s shapes, leaving one that does not read', () => {
    expect(list.map((s) => s.id)).toEqual(['p1', 'p2']);
    expect(list[1].inProfile).toBe(true);
  });
  it('keeps the ids of the shapes it has and gives a new one none', () => {
    const next = withShape(list, { type: 'line', a: { x: 0, y: 0 }, b: { x: 0, y: 1 } });
    expect(shapesPayload(next)).toEqual([
      { id: 'p1', type: 'line', a: [0, 0], b: [1, 0] },
      { id: 'p2', type: 'circle', c: [0.5, 0.5], r: 0.2 },
      { type: 'line', a: [0, 0], b: [0, 1] },
    ]);
  });
  it('removes one without touching the others\' ids', () => {
    expect(shapesPayload(withoutShape(list, 'p1')).map((s) => s.id)).toEqual(['p2']);
  });
  it('replaces one by id', () => {
    const next = withReplaced(list, 'p2', { type: 'circle', c: { x: 0, y: 0 }, r: 1 });
    expect(shapesPayload(next)[1]).toEqual({ id: 'p2', type: 'circle', c: [0, 0], r: 1 });
  });
});

describe('the drawing tools', () => {
  const P = (x: number, y: number) => ({ x, y });
  it('makes a line, a rectangle, a circle and a 3-point arc', () => {
    expect(shapeFrom('line', [P(0, 0), P(1, 0)])).toEqual({ type: 'line', a: P(0, 0), b: P(1, 0) });
    const r = shapeFrom('rectangle', [P(0, 0), P(2, 1)]);
    expect(r).toMatchObject({ type: 'polyline', closed: true });
    expect(r && r.type === 'polyline' ? r.pts : []).toEqual([P(0, 0), P(2, 0), P(2, 1), P(0, 1)]);
    expect(shapeFrom('circle', [P(1, 1), P(1, 3)])).toEqual({ type: 'circle', c: P(1, 1), r: 2 });
    const a = shapeFrom('arc', [P(1, 0), P(0, 1), P(-1, 0)]);
    expect(a).toMatchObject({ type: 'arc', r: 1 });
  });
  it('makes nothing from too few points or degenerate ones', () => {
    expect(shapeFrom('line', [P(0, 0)])).toBeNull();
    expect(shapeFrom('line', [P(1, 1), P(1, 1)])).toBeNull();
    expect(shapeFrom('rectangle', [P(0, 0), P(2, 0)])).toBeNull();
    expect(shapeFrom('arc', [P(0, 0), P(1, 1), P(2, 2)])).toBeNull();
  });
  it('previews the shape the cursor would make, or the line to it', () => {
    expect(previewShapes('circle', [P(0, 0)], P(2, 0))).toEqual([{ type: 'circle', c: P(0, 0), r: 2 }]);
    expect(previewShapes('arc', [P(0, 0), P(1, 1)], P(2, 2))[0].type).toBe('polyline');
    expect(previewShapes('line', [], P(1, 1))).toEqual([]);
  });
  it('allows drawing and deleting, and the constraint tools; not the model\'s tools', () => {
    expect(toolAllowed('line', { constraintCommand: false })).toBe(true);
    expect(toolAllowed('delete', { constraintCommand: false })).toBe(true);
    expect(toolAllowed('line', { constraintCommand: true })).toBe(false);
    expect(toolAllowed('CNCOINCIDENT', { constraintCommand: true })).toBe(true);
    expect(toolAllowed('CDIM', { constraintCommand: false })).toBe(true);
    expect(toolAllowed('move', { constraintCommand: false })).toBe(false);
    expect(toolAllowed('studio.extrude', { constraintCommand: false })).toBe(false);
  });
});

describe('picking and grips', () => {
  const list = localShapes([{ id: 'p1', type: 'line', a: [0, 0], b: [1, 0] }, { id: 'p2', type: 'circle', c: [0.5, 0.5], r: 0.2 }, { id: 'p3', type: 'polyline', pts: [[2, 0], [3, 0], [3, 1]], closed: false }]);
  it('lists every point that can be dragged', () => {
    const g = gripsOf(list);
    expect(g.map((x) => `${x.shape}:${x.part}`)).toEqual(['p1:start', 'p1:end', 'p2:center', 'p3:vertex:0', 'p3:vertex:1', 'p3:vertex:2']);
  });
  it('snaps a point to the nearest grip within reach', () => {
    const g = gripsOf(list);
    expect(snapToGrip(g, { x: 1.02, y: 0.01 }, 0.05)?.part).toBe('end');
    expect(snapToGrip(g, { x: 1.5, y: 1.5 }, 0.05)).toBeNull();
  });
  it('picks the shape nearest a click', () => {
    const near = (s: Parameters<typeof nearestOnShape>[0], p: { x: number; y: number }) => nearestOnShape(s, p).dist;
    expect(shapeAt(list, { x: 0.5, y: 0.01 }, 0.05, near)?.id).toBe('p1');
    expect(shapeAt(list, { x: 5, y: 5 }, 0.05, near)).toBeNull();
  });
  it('reads the engine\'s drag answer, flat shapes or wrapped', () => {
    const r = dragShapes([{ id: 'p1', type: 'line', a: [0, 0], b: [2, 0] }, { id: 'p2', shape: { type: 'circle', c: [0, 0], r: 1 } }, { type: 'line' }]);
    expect(r.map((x) => x.id)).toEqual(['p1', 'p2']);
  });
});

describe('the sketch status', () => {
  it('fills what the engine leaves out', () => {
    expect(statusOf({ status: 'solved', dof: 0 })).toEqual({ status: 'solved', dof: 0, freeShapes: [], redundant: [], conflicting: [] });
  });
});

const tree = (over: Partial<PartTree> = {}): PartTree => ({
  id: 'part1', name: 'Shelf', kind: 'formula', partTypeId: 't1', planes: ['xy', 'xz', 'yz'], params: [],
  sketches: [
    { id: 's1', name: 'Side', plane: 'xz', local: true, shapeIds: [], frame: XZ, shapes: [], constraints: [], constraintCount: 2, status: 'solved', dof: 0, bodies: ['Body 1'] },
    { id: 's2', name: 'Top', plane: 'xy', local: true, shapeIds: [], frame: XY, shapes: [], constraints: [], status: 'under', dof: 3, bodies: [] },
  ],
  steps: [{ index: 0, kind: 'extrude', label: 'Body 1', sketch: 's1' }, { index: 1, kind: 'extrude', label: 'Old', sketch: 'body:2' }],
  ...over,
});

describe('the part tree with local sketches', () => {
  it('opens a local sketch in place, with its degrees of freedom and bodies', () => {
    const rows = sketchRows(tree());
    expect(rows.map((r) => [r.id, r.opens, r.dof, r.bodies])).toEqual([['s1', 'sketch', 0, ['Body 1']], ['s2', 'sketch', 3, []]]);
    expect(localSketches(tree()).map((s) => s.id)).toEqual(['s1', 's2']);
  });
  it('offers Constrain part only while a body is still corners', () => {
    expect(canConstrain(tree())).toBe(false);
    const withCorners = tree({ sketches: [...tree().sketches, { id: 'body:2', plane: 'xy', shapeIds: [], points: [[0, 0], [1, 0], [1, 1]] }] });
    expect(canConstrain(withCorners)).toBe(true);
    expect(canConstrain(tree({ kind: 'recipe', sketches: withCorners.sketches }))).toBe(false);
    expect(canConstrain(null)).toBe(false);
  });
  it('tells the bodies drawn from a sketch', () => {
    const t = tree();
    expect(sketchBodySteps(t).map((x) => [x.step.label, x.sketch.id])).toEqual([['Body 1', 's1']]);
    expect(stepRows(t).map((s) => s.sketch)).toEqual(['s1', 'body:2']);
  });
  it('reads a depth typed as a length or a formula', () => {
    const len = (s: string) => (/^\d+(\.\d+)?$/.test(s) ? Number(s) : null);
    expect(depthInput('0.4', len)).toBe(0.4);
    expect(depthInput('= H - 0.04', len)).toBe('H - 0.04');
    expect(depthInput('H / 2', len)).toBe('H / 2');
    expect(depthInput('  ', len)).toBeNull();
    expect(depthInput('=', len)).toBeNull();
  });
});

describe('the families dialog on a body drawn from a sketch', () => {
  const def: FamilyDef = {
    ifcClass: 'IfcBuildingElementProxy', params: [],
    bodies: [{ name: 'Body 1', plane: 'xy', points: [], depth: 0.4, sketch: 's1' }, { name: 'Box', plane: 'xy', points: [[0, 0], [1, 0], [1, 1]], depth: 1 }],
    sketches: [{ id: 's1', name: 'Side' }],
  };
  it('names the sketch and needs no corners of its own', () => {
    expect(isSketchBody(def.bodies[0])).toBe(true);
    expect(isSketchBody(def.bodies[1])).toBe(false);
    expect(bodySketchName(def, def.bodies[0])).toBe('Side');
    expect(bodySketchName({ ...def, sketches: [] }, def.bodies[0])).toBe('s1');
    expect(quickProblem(def)).toBeNull();
  });
});

describe('editing the part, not the object', () => {
  const sq = (w: number) => [{ id: 'sq', type: 'polyline', pts: [[0, 0], [w, 0], [w, 1], [0, 1]], closed: true }];
  const F2: SketchFrame = { origin: [5, 0, 0], right: [1, 0, 0], up: [0, 1, 0], normal: [0, 0, 1] };
  it('takes the stored sketch (the defaults) and tells when the object\'s values move it', () => {
    const sk = {
      shapes: sq(1.6), constraints: [{ id: 'c1', value: 1.6 }], status: 'underconstrained', dof: 1, frame: XY,
      stored: { shapes: sq(1), constraints: [{ id: 'c1', value: 1 }], status: 'underconstrained', dof: 1, frame: F2 },
    };
    const e = editedSketch(sk);
    expect(e.differs).toBe(true);
    expect(localShapes(e.source.shapes)[0].shape).toMatchObject({ pts: [{ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 1, y: 1 }, { x: 0, y: 1 }] });
    expect((e.source.constraints![0] as { value: number }).value).toBe(1);
    expect(e.source.frame).toBe(F2);
    // The same values: nothing to tell; no stored one (an older engine): the object's own.
    expect(editedSketch({ ...sk, stored: { ...sk.stored, shapes: sq(1.6) } }).differs).toBe(false);
    expect(editedSketch({ shapes: sq(2), frame: XY }).source.shapes).toEqual(sq(2));
    // A stored one without its frame takes the object's.
    expect(editedSketch({ shapes: sq(1), frame: XY, stored: { shapes: sq(1) } }).source.frame).toBe(XY);
  });
});

describe('what drawing records', () => {
  const grip = (shape: string, part: string, x: number, y: number) => ({ shape, part, at: { x, y } });
  it('makes a point put down on an existing one coincident, unless Ctrl placed it free', () => {
    const line = shapeFrom('line', [{ x: 1, y: 1 }, { x: 2, y: 2 }])!;
    expect(recordedConstraints('line', line, [{ p: { x: 1, y: 1 }, grip: grip('sq', 'vertex:2', 1, 1) }, { p: { x: 2, y: 2 }, grip: null }])).toEqual([
      { kind: 'coincident', refs: [{ new: 0, part: 'start' }, { shape: 'sq', part: 'vertex:2' }] },
    ]);
    expect(recordedConstraints('line', line, [{ p: { x: 1, y: 1 }, grip: grip('sq', 'vertex:2', 1, 1), free: true }, { p: { x: 2, y: 2 }, grip: grip('a', 'end', 2, 2) }])).toEqual([
      { kind: 'coincident', refs: [{ new: 0, part: 'end' }, { shape: 'a', part: 'end' }] },
    ]);
  });
  it('holds a rectangle\'s sides and its corners on points', () => {
    const rect = shapeFrom('rectangle', [{ x: 0, y: 0 }, { x: 2, y: 1 }])!;
    const r = recordedConstraints('rectangle', rect, [{ p: { x: 0, y: 0 }, grip: grip('sq', 'vertex:0', 0, 0) }, { p: { x: 2, y: 1 }, grip: grip('l', 'end', 2, 1) }]);
    expect(r.filter((c) => c.kind === 'coincident').map((c) => c.refs[0])).toEqual([{ new: 0, part: 'vertex:0' }, { new: 0, part: 'vertex:2' }]);
    expect(r.filter((c) => c.kind !== 'coincident').map((c) => `${c.kind}:${(c.refs[0] as { part: string }).part}`)).toEqual(['horizontal:edge:0', 'vertical:edge:1', 'horizontal:edge:2', 'vertical:edge:3']);
  });
  it('takes an arc\'s ends, not its middle point', () => {
    const arc = shapeFrom('arc', [{ x: 1, y: 0 }, { x: 0, y: 1 }, { x: -1, y: 0 }])!;
    const r = recordedConstraints('arc', arc, [
      { p: { x: 1, y: 0 }, grip: grip('a', 'end', 1, 0) }, { p: { x: 0, y: 1 }, grip: grip('b', 'end', 0, 1) }, { p: { x: -1, y: 0 }, grip: grip('c', 'start', -1, 0) },
    ]);
    expect(r.map((c) => (c.refs[1] as { shape: string }).shape).sort()).toEqual(['a', 'c']);
  });
});
