import { describe, expect, it } from 'vitest';
import type { PenDto } from '../../../viewport/drawing';
import {
  attributeKey, byCategory, defBounds, defaultValues, ghostFactor, ghostShapes, missingAttributes, normaliseDef, packText, parsePack, previewImage, shiftPens, symBounds,
  symDragHandle, symHandles, symItemAt, symMove, symSnap, symKeyPoints, sweepDeg, toggled, turned, type SymDef, type SymItem,
} from './symbolModel';

const circle: SymItem = { type: 'circle', c: [10, 0], r: 5 };
const arc: SymItem = { type: 'arc', c: [0, 0], r: 10, start: 270, end: 90 };

describe('symbol items', () => {
  it('bounds a circle and an arc (the arc through its quadrants)', () => {
    expect(symBounds(circle)).toEqual([5, -5, 15, 5]);
    const [x0, y0, x1, y1] = symBounds(arc);
    expect([Math.abs(Math.round(x0)), Math.round(y0), Math.round(x1), Math.round(y1)]).toEqual([0, -10, 10, 10]);
  });
  it('bounds all the items, a small box round the origin when empty', () => {
    expect(defBounds([])).toEqual([-5, -5, 5, 5]);
    expect(defBounds([circle, { type: 'line', a: [-3, -4], b: [0, 0] }])).toEqual([-3, -5, 15, 5]);
  });
  it('turns the sweep counter-clockwise, a full circle when the ends meet', () => {
    expect(sweepDeg(270, 90)).toBe(180);
    expect(sweepDeg(0, 0)).toBe(360);
    expect(sweepDeg(350, 10)).toBe(20);
  });
  it('moves a circle by its centre and drags its radius', () => {
    expect(symMove(circle, 1, 2)).toMatchObject({ c: [11, 2], r: 5 });
    expect(symDragHandle(circle, 0, [10, 8])).toMatchObject({ r: 8 });
  });
  it('drags an arc end to the angle of the pointer', () => {
    expect(symDragHandle(arc, 1, [0, 7])).toMatchObject({ end: 90 });
    expect(symDragHandle(arc, 0, [7, 0])).toMatchObject({ start: 0 });
    expect(symHandles(arc)).toHaveLength(3);
  });
  it('picks a circle on its line, or inside when it is filled', () => {
    expect(symItemAt([circle], [15, 0], 0.5)).toBe(0);
    expect(symItemAt([circle], [10, 0], 0.5)).toBe(-1);
    expect(symItemAt([{ ...circle, fill: 'byInstance' } as SymItem], [10, 0], 0.5)).toBe(0);
    expect(symItemAt([arc], [10, 0], 0.5)).toBe(0);
    expect(symItemAt([arc], [-10, 0], 0.5)).toBe(-1);
  });
  it('picks the other items as the title block designer does', () => {
    expect(symItemAt([{ type: 'line', a: [0, 0], b: [10, 0] }], [5, 0.2], 0.5)).toBe(0);
    expect(symItemAt([{ type: 'line', a: [0, 0], b: [10, 0] }], [5, 3], 0.5)).toBe(-1);
  });
  it('snaps to the origin, to key points, then to the grid', () => {
    expect(symSnap([0.2, -0.1], [circle], { grid: 1, tol: 0.5 })).toEqual({ p: [0, 0], on: 'origin' });
    expect(symSnap([15.1, 0.2], [circle], { grid: 1, tol: 0.5 })).toEqual({ p: [15, 0], on: 'point' });
    expect(symSnap([7.4, 3.6], [circle], { grid: 1, tol: 0.1 })).toEqual({ p: [7, 4], on: 'grid' });
    expect(symKeyPoints(circle)).toHaveLength(5);
  });
});

describe('symbol attributes', () => {
  it('makes a key of a label', () => {
    expect(attributeKey('Door number')).toBe('doorNumber');
    expect(attributeKey('  3 floor-level ')).toBe('floorLevel');
    expect(attributeKey('###')).toBe('');
  });
  it('finds the attributes the texts read and the list lacks', () => {
    const def: SymDef = normaliseDef({
      items: [{ type: 'text', at: [0, 0], text: 'No {attr.no} {= attr.w * 2 | 0.00} {view.scale}' }, { type: 'text', at: [0, 5], text: '{attr.Name}' }],
      attributes: [{ key: 'name', label: 'Name', default: 'x' }],
    });
    expect(missingAttributes(def)).toEqual(['no', 'w']);
    expect(defaultValues(def.attributes)).toEqual({ name: 'x' });
  });
  it('fills a definition as the engine sent it', () => {
    expect(normaliseDef({ items: [] })).toEqual({ scale: 'annotation', category: 'other', items: [], attributes: [] });
    expect(normaliseDef({ scale: 'model', category: 'fire', items: [], attributes: [{ key: 'a' } as never] }).attributes).toEqual([{ key: 'a', label: 'a', default: '' }]);
  });
});

describe('symbol ghosts', () => {
  it('works out metres per millimetre: paper millimetres at the view scale, the building\'s as they are', () => {
    expect(ghostFactor('annotation', 100)).toBeCloseTo(0.1);
    expect(ghostFactor('annotation', 50)).toBeCloseTo(0.05);
    expect(ghostFactor('model', 100)).toBeCloseTo(0.001);
    expect(ghostFactor('annotation', 100, 2)).toBeCloseTo(0.2);
  });
  const pens: PenDto[] = [{ layer: 'a', stroke: '#000', width: 0.25, fill: null, segments: [0, 0, 10, 0], loops: [[0, 0, 4, 0, 4, -4]] }];
  it('puts the pens (paper mm, y down) at the cursor in the view (metres, y up)', () => {
    const shapes = ghostShapes(pens, { x: 1, y: 2 }, 0.1);
    expect(shapes[0]).toMatchObject({ type: 'line', a: { x: 1, y: 2 }, b: { x: 2, y: 2 } });
    const loop = shapes[1];
    expect(loop.type === 'polyline' && loop.closed && loop.pts[2]).toMatchObject({ x: 1.4, y: 2.4 });
  });
  it('shifts pens and pads a preview for a thumbnail', () => {
    const moved = shiftPens([{ ...pens[0], texts: [{ x: 1, y: 2, text: 't', height: 2, angle: 0, anchor: 'start', baseline: 'alphabetic', bold: false, font: 'sans' }] }], 5, 6);
    expect(moved[0].segments).toEqual([5, 6, 15, 6]);
    expect(moved[0].texts?.[0]).toMatchObject({ x: 6, y: 8 });
    const img = previewImage({ box: [-2, -3, 8, 3], pens }, 1);
    expect(img.w).toBe(12);
    expect(img.h).toBe(8);
    expect(img.pens[0].segments).toEqual([3, 4, 13, 4]);
  });
});

describe('symbol turns and flips', () => {
  it('toggles each flip alone', () => {
    expect(toggled({ flipX: false, flipY: false }, 'x')).toEqual({ flipX: true, flipY: false });
    expect(toggled({ flipX: true, flipY: true }, 'y')).toEqual({ flipX: true, flipY: false });
  });
  it('turns a quarter, round to 0', () => {
    expect(turned(0)).toBe(90);
    expect(turned(270)).toBe(0);
    expect(turned(10, -90)).toBe(280);
  });
});

describe('symbol libraries', () => {
  it('writes and reads a .lapinsymbols file', () => {
    const def = normaliseDef({ items: [circle], category: 'fire' });
    const back = parsePack(packText([{ name: 'Ring', def }]));
    expect(back).toEqual([{ name: 'Ring', def }]);
    expect(parsePack('{"format":"other"}')).toBeNull();
    expect(parsePack('not json')).toBeNull();
  });
  it('groups by category in the engine\'s order, then by name', () => {
    const items = [{ name: 'B', category: 'fire' }, { name: 'A', category: 'north' }, { name: 'A', category: 'fire' }, { name: 'Z', category: 'mine' }];
    expect(byCategory(items, ['north', 'fire']).map(([c, l]) => [c, l.map((i) => i.name)])).toEqual([['north', ['A']], ['fire', ['A', 'B']], ['mine', ['Z']]]);
  });
});
