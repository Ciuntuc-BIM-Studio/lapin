import { describe, expect, it } from 'vitest';
import {
  sketchSources, isClosedOutline, addBody, addParam, addPoint, blankFamily, cellText, className, formulaNames, freeKey, isoPoint, keyProblem, normaliseFamily, parseCell, parseValue, quickProblem,
  removeParam, removePoint, renameInFormula, renameKey, planEdges, placedEdges, setBodyOptional, setOffset, setParam, setPoint, sizeOf, valuesOf, wireframe,
  type FamilyDef, type PreviewBody,
} from './familyModel';

describe('a family cell', () => {
  it('is a number when it reads as one (a comma is a decimal point) and a formula otherwise', () => {
    expect(parseCell('0.5')).toBe(0.5);
    expect(parseCell(' 1,25 ')).toBe(1.25);
    expect(parseCell('-2')).toBe(-2);
    expect(parseCell('W - T')).toBe('W - T');
    expect(parseCell('2*H')).toBe('2*H');
    expect(parseCell('')).toBe(0);
  });
  it('is written back as typed', () => {
    expect(cellText(0.5)).toBe('0.5');
    expect(cellText('W - T')).toBe('W - T');
    expect(cellText(1 / 3)).toBe('0.333333');
    expect(parseCell(cellText('W / 2'))).toBe('W / 2');
  });
});

describe('the values typed for an object', () => {
  const def = blankFamily();
  it('keep numbers of the family\'s parameters only; empty is the default', () => {
    expect(parseValue('', 'length')).toBeNull();
    expect(parseValue('1,2', 'length')).toBe(1.2);
    expect(parseValue('abc', 'length')).toBe('bad');
    expect(parseValue('yes', 'bool')).toBe(1);
    expect(parseValue('no', 'bool')).toBe(0);
    expect(valuesOf(def, { W: '2', H: '', D: 'x', Z: '9' })).toEqual({ W: 2 });
    // A length goes through the project's unit reader when given.
    expect(valuesOf(def, { W: '1600' }, (t) => Number(t) / 1000)).toEqual({ W: 1.6 });
    expect(parseValue('abc', 'length', () => null)).toBe('bad');
  });
});

describe('editing a definition', () => {
  it('leaves the original alone', () => {
    const d = blankFamily();
    const before = JSON.stringify(d);
    setPoint(d, 0, 1, 0, 'W / 2'); addParam(d); addBody(d); removeParam(d, 0);
    expect(JSON.stringify(d)).toBe(before);
  });
  it('adds parameters with free keys and checks keys', () => {
    let d = blankFamily();
    d = addParam(d); d = addParam(d);
    expect(d.params.map((p) => p.key)).toEqual(['W', 'H', 'D', 'P', 'P2']);
    expect(keyProblem(setParam(d, 3, { key: 'w' }), 3)).toMatch(/twice|two/i);
    expect(keyProblem(setParam(d, 3, { key: '2x' }), 3)).toMatch(/letters/);
    expect(keyProblem(d, 3)).toBeNull();
    expect(formulaNames(d)).toContain('P2');
    expect(freeKey(['Body', 'Body 2'], 'Body')).toBe('Body2');
  });
  it('sets a corner to a number or a formula, adds one between its neighbours and takes one out', () => {
    let d = blankFamily();
    d = setPoint(d, 0, 2, 0, 'W - 0.1');
    expect(d.bodies[0].points[2]).toEqual(['W - 0.1', 'H']);
    d = addPoint(d, 0, 0);
    expect(d.bodies[0].points).toHaveLength(5);
    expect(d.bodies[0].points[1]).toEqual([0, 0]);
    d = removePoint(d, 0, 1);
    expect(d.bodies[0].points).toHaveLength(4);
    expect(quickProblem(removePoint(removePoint(d, 0, 0), 0, 0))).toMatch(/three corners/);
  });
  it('keeps the optional condition, material and offset out of the definition when empty', () => {
    let d = blankFamily();
    d = setBodyOptional(d, 0, 'when', 'HasShelf'); expect(d.bodies[0].when).toBe('HasShelf');
    d = setBodyOptional(d, 0, 'when', '  '); expect('when' in d.bodies[0]).toBe(false);
    d = setBodyOptional(d, 0, 'material', 'Oak'); expect(d.bodies[0].material).toBe('Oak');
    d = setOffset(d, 0, 2, '0.4'); expect(d.bodies[0].offset).toEqual([0, 0, 0.4]);
    d = setOffset(d, 0, 2, '0'); expect('offset' in d.bodies[0]).toBe(false);
  });
  it('makes a definition safe from what the engine gives', () => {
    const d = normaliseFamily({ params: [{ key: 'W', label: '', kind: 'length', default: 1 }], bodies: [{ name: 'A', plane: 'xz', points: [[0, 0], [1, 0], [1, 1]], depth: 'W' }] });
    expect(d.ifcClass).toBe('IfcBuildingElementProxy');
    expect(d.params[0].label).toBe('W');
    expect(normaliseFamily(null).bodies).toEqual([]);
    expect(quickProblem(normaliseFamily(null))).toMatch(/one body/);
  });
  it('keeps a parameter\'s table fields (formula, instance, published, group, bounds) and the text and integer kinds', () => {
    const d = normaliseFamily({ params: [
      { key: 'A', label: 'A', kind: 'length', default: 1, min: 0.1, max: 3, unit: 'm', group: 'Size', description: 'd', instance: false, published: false },
      { key: 'B', label: 'B', kind: 'length', default: 2, formula: 'A * 2' },
      { key: 'T', label: 'T', kind: 'text', default: 'oak' },
      { key: 'N', label: 'N', kind: 'integer', default: 3 },
    ], bodies: [] });
    expect(d.params[0]).toEqual({ key: 'A', label: 'A', kind: 'length', default: 1, min: 0.1, max: 3, unit: 'm', group: 'Size', description: 'd', instance: false, published: false });
    expect(d.params[1].formula).toBe('A * 2');
    expect(d.params[2]).toMatchObject({ kind: 'text', default: 'oak' });
    expect(d.params[3]).toMatchObject({ kind: 'integer', default: 3 });
  });
  it('names a class plainly', () => {
    expect(className('IfcFlowTerminal')).toBe('Flow terminal');
    expect(className('IfcBuildingElementProxy')).toBe('Building element proxy');
  });
});

describe('the wireframe', () => {
  // A unit cube's edges, each twice as two faces give them.
  const square = (z: number): number[][] => [[0, 0, z, 1, 0, z], [1, 0, z, 1, 1, z], [1, 1, z, 0, 1, z], [0, 1, z, 0, 0, z]];
  const up = [[0, 0, 0, 0, 0, 1], [1, 0, 0, 1, 0, 1], [1, 1, 0, 1, 1, 1], [0, 1, 0, 0, 1, 1]];
  const cube = [{ name: 'c', edges: [...square(0), ...square(1), ...up, ...square(0)], volume: 1 }];
  it('projects up as up on screen and keeps each edge once', () => {
    const [, sy0] = isoPoint(0, 0, 0), [, sy1] = isoPoint(0, 0, 1);
    expect(sy1).toBeGreaterThan(sy0);
    const w = wireframe(cube);
    expect(w.lines).toHaveLength(12);
  });
  it('fits its box with the margin, y down, and is empty without edges', () => {
    const w = wireframe(cube, 300, 200, 10);
    const xs = w.lines.flatMap((l) => [l[0], l[2]]), ys = w.lines.flatMap((l) => [l[1], l[3]]);
    expect(Math.min(...xs)).toBeGreaterThanOrEqual(10 - 0.01); expect(Math.max(...xs)).toBeLessThanOrEqual(290.01);
    expect(Math.min(...ys)).toBeGreaterThanOrEqual(10 - 0.01); expect(Math.max(...ys)).toBeLessThanOrEqual(190.01);
    // The top of the cube is higher on the screen (a smaller y) than its bottom.
    const vertical = w.lines.filter((l) => Math.abs(l[0] - l[2]) < 0.01 && Math.abs(l[1] - l[3]) > 1);
    expect(vertical.length).toBe(4);
    expect(wireframe([]).lines).toEqual([]);
  });
  it('tells the size of the bodies', () => {
    expect(sizeOf(cube)).toEqual([1, 1, 1]);
    expect(sizeOf([])).toBeNull();
  });
});

describe('a family from a sketch', () => {
  const draft = (view: string, shape: object) => ({ type: 'IfcAnnotation', props: { viewId: view, predefinedType: 'DRAFT', shape } });
  const nodes = new Map<string, { type: string; props: Record<string, unknown> }>([
    ['a', draft('plan:1', { type: 'polyline', closed: true, pts: [] })],
    ['b', draft('plan:1', { type: 'circle', c: [0, 0], r: 1 })],
    ['c', draft('plan:1', { type: 'polyline', closed: false, pts: [] })],
    ['d', draft('plan:2', { type: 'polyline', closed: true, pts: [] })],
    ['e', { type: 'IfcWall', props: {} }],
  ]);
  it('takes closed polylines and circles only', () => {
    expect(['a', 'b', 'c', 'd', 'e'].map((id) => isClosedOutline(nodes.get(id)))).toEqual([true, true, false, true, false]);
  });
  it('is made from one view\'s drafting', () => {
    expect(sketchSources(['c', 'e'], nodes)).toBeNull();
    expect(sketchSources(['a', 'c', 'b', 'd'], nodes)).toEqual({ viewId: 'plan:1', ids: ['a', 'b'] });
  });
});

describe('renameInFormula', () => {
  const r = (t: string) => renameInFormula(t, 'W', 'Width');
  it('renames whole names', () => {
    expect(r('W')).toBe('Width');
    expect(r('W*2-T')).toBe('Width*2-T');
    expect(r('T-W')).toBe('T-Width');
    expect(r('if(W>1;W;0)')).toBe('if(Width>1;Width;0)');
    expect(r('(W+W)/2')).toBe('(Width+Width)/2');
    expect(r('max(W, 2)')).toBe('max(Width, 2)');
  });
  it('is case-insensitive like the evaluator', () => { expect(r('w + W')).toBe('Width + Width'); });
  it('leaves longer names, dotted names and functions alone', () => {
    expect(r('W2')).toBe('W2');
    expect(r('Width')).toBe('Width');
    expect(r('attr.W')).toBe('attr.W');
    expect(r('W.x')).toBe('W.x');
    expect(r('W_x * 2')).toBe('W_x * 2');
    expect(r('_W')).toBe('_W');
    expect(r('2W')).toBe('2Width');
  });
  it('leaves functions and numbers alone', () => {
    expect(renameInFormula('max(1;2)', 'max', 'Top')).toBe('max(1;2)');
    expect(renameInFormula('max + 1', 'max', 'Top')).toBe('Top + 1');
    expect(renameInFormula('2e3 + E', 'e', 'Eps')).toBe('2e3 + Eps');
    expect(renameInFormula('1.5E-2*E', 'E', 'Eps')).toBe('1.5E-2*Eps');
    expect(renameInFormula('0.5', 'W', 'X')).toBe('0.5');
  });
});

describe('renameKey', () => {
  const def: FamilyDef = {
    ifcClass: 'IfcBuildingElementProxy',
    params: [{ key: 'W', label: 'Width', kind: 'length', default: 1 }, { key: 'T', label: 'T', kind: 'length', default: 0.1 }, { key: 'W2', label: 'W2', kind: 'length', default: 2 }],
    bodies: [{
      name: 'B', plane: 'xy', points: [[0, 0], ['W', 0], ['W*2-T', 'W2'], [0, 5]], holes: [[['W/2', 1], [2, 'w'], [3, 3]]],
      depth: 'W - T', offset: [0, 'W', 0.5], when: 'W > 1 and W2 > 1',
    }],
  };
  it('renames the key and every formula that uses it', () => {
    const n = renameKey(def, 'W', 'Width');
    expect(n.params.map((p) => p.key)).toEqual(['Width', 'T', 'W2']);
    const b = n.bodies[0];
    expect(b.points).toEqual([[0, 0], ['Width', 0], ['Width*2-T', 'W2'], [0, 5]]);
    expect(b.holes).toEqual([[['Width/2', 1], [2, 'Width'], [3, 3]]]);
    expect(b.depth).toBe('Width - T');
    expect(b.offset).toEqual([0, 'Width', 0.5]);
    expect(b.when).toBe('Width > 1 and W2 > 1');
  });
  it('does not mutate the original and keeps numbers numbers', () => {
    const copy = JSON.stringify(def);
    const n = renameKey(def, 'W', 'Width');
    expect(JSON.stringify(def)).toBe(copy);
    expect(n.bodies[0].points[0]).toEqual([0, 0]);
  });
  it('does nothing for the same key, an unknown key or a key another parameter has', () => {
    expect(renameKey(def, 'W', 'W')).toBe(def);
    expect(renameKey(def, 'Nope', 'X')).toBe(def);
    expect(renameKey(def, 'W', 'w2')).toBe(def);
    expect(renameKey(def, 'W', 't')).toBe(def);
  });
  it('can change only the case of a key', () => {
    const n = renameKey(def, 'T', 't');
    expect(n.params[1].key).toBe('t');
    expect(n.bodies[0].depth).toBe('W - t');
  });
});

describe('the ghost in a plan', () => {
  const box: PreviewBody = { name: 'b', volume: 1, edges: [
    [0, 0, 0, 2, 0, 0], [2, 0, 0, 2, 1, 0], [2, 1, 0, 0, 1, 0], [0, 1, 0, 0, 0, 0],
    [0, 0, 1, 2, 0, 1], [2, 0, 1, 2, 1, 1], [2, 1, 1, 0, 1, 1], [0, 1, 1, 0, 0, 1],
    [0, 0, 0, 0, 0, 1], [2, 0, 0, 2, 0, 1], [2, 1, 0, 2, 1, 1], [0, 1, 0, 0, 1, 1],
  ] };
  it('projects the edges, each once, and drops the vertical ones', () => {
    const e = planEdges([box]);
    expect(e).toHaveLength(4);
  });
  it('counts an edge once whichever way it runs', () => {
    expect(planEdges([{ name: 'a', volume: 0, edges: [[0, 0, 0, 1, 0, 0], [1, 0, 5, 0, 0, 5]] }])).toHaveLength(1);
  });
  it('moves, turns and mirrors', () => {
    const e: [number, number, number, number][] = [[0, 0, 2, 0]];
    expect(placedEdges(e, { x: 10, y: 5 })).toEqual([[{ x: 10, y: 5 }, { x: 12, y: 5 }]]);
    const t = placedEdges(e, { x: 10, y: 5 }, 90)[0];
    expect(t[1].x).toBeCloseTo(10); expect(t[1].y).toBeCloseTo(7);
    const m = placedEdges(e, { x: 10, y: 5 }, 0, true)[0];
    expect(m[1].x).toBeCloseTo(8); expect(m[1].y).toBeCloseTo(5);
    const mt = placedEdges(e, { x: 0, y: 0 }, 90, true)[0];
    expect(mt[1].x).toBeCloseTo(0); expect(mt[1].y).toBeCloseTo(-2);
  });
});
