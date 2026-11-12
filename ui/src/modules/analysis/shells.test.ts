import { describe, expect, it } from 'vitest';
import {
  bandColours, bandEdges, bandPieces, flipped, shellExtremes, shownRange, shownScale, shownUnit, shownValues, signNote, triangles,
  type ContoursDto, type SurfaceDto,
} from './shells';

const surface = (kind: string, extra: Partial<SurfaceDto>): SurfaceDto => ({
  id: kind, sourceId: kind, name: kind, kind, normal: [0, 0, 1], positions: [0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0], faces: [0, 1, 2, 3], ...extra,
});

const area = (poly: [number, number][]) => Math.abs(poly.reduce((a, p, i) => { const q = poly[(i + 1) % poly.length]!; return a + p[0] * q[1] - q[0] * p[1]; }, 0)) / 2;

describe('the shells\' contours', () => {
  it('turn a slab\'s moments over (its bottom in tension positive) and leave the rest as the engine gives them', () => {
    expect(flipped('slab', 'mx')).toBe(true);
    expect(flipped('slab', 'mxy')).toBe(true);
    expect(flipped('slab', 'w')).toBe(false);
    expect(flipped('slab', 'nx')).toBe(false);
    expect(flipped('wall', 'mx')).toBe(false);
    expect(shownValues(surface('slab', { values: [-2, 1, 0, 3] }), 'my')).toEqual([2, -1, -0, -3]);
    expect(shownValues(surface('wall', { values: [-2, 1, 0, 3] }), 'my')).toEqual([-2, 1, 0, 3]);
  });

  it('show an envelope\'s larger in size at each node, turned over on a slab', () => {
    const s = surface('slab', { min: [-5, -1, 0, -2], max: [1, 4, 0, 2] });
    expect(shownValues(s, 'mx')).toEqual([5, -4, -0, -2]);
    expect(shownValues({ ...s, kind: 'wall' }, 'nx')).toEqual([-5, 4, 0, 2]);
  });

  it('share the range into equal bands, one when the values are equal', () => {
    expect(bandEdges(0, 10, 5)).toEqual([0, 2, 4, 6, 8, 10]);
    expect(bandEdges(3, 3, 10)).toHaveLength(2);
    const c: ContoursDto = { kind: 'case', key: 'q', quantity: 'my', unit: 'kN·m/m', min: -3, max: 1, surfaces: [surface('slab', { values: [-3, 1, 0, 0] })] };
    expect(shownRange(c)).toEqual({ min: -1, max: 3 });
    expect(bandColours(4)).toHaveLength(4);
    expect(new Set(bandColours(8)).size).toBe(8);
  });

  it('show displacements in millimetres', () => {
    expect(shownUnit('m')).toBe('mm');
    expect(shownScale('m')).toBe(1000);
    expect(shownUnit('kN/m')).toBe('kN/m');
  });

  it('split a quadrilateral face in two triangles and keep a triangle', () => {
    expect(triangles([0, 1, 2, 3, 4, 5, 6, 6])).toEqual([[0, 1, 2], [0, 2, 3], [4, 5, 6]]);
  });

  it('cut a triangle into the bands its values cross, the pieces covering it', () => {
    const tri: [number, number][] = [[0, 0], [4, 0], [0, 4]];
    const pieces = bandPieces(tri, [0, 4, 4], [0, 1, 2, 3, 4]);
    expect(pieces.map((p) => p.band)).toEqual([0, 1, 2, 3]);
    expect(pieces.reduce((a, p) => a + area(p.poly), 0)).toBeCloseTo(8, 9);
    // The lowest band: the corner where the value is under 1 (a quarter of the way along both sides).
    expect(area(pieces[0]!.poly)).toBeCloseTo(0.5, 9);
    // One band only: the whole triangle in it.
    expect(bandPieces(tri, [1.2, 1.5, 1.9], [0, 1, 2, 3])).toEqual([{ band: 1, poly: tri }]);
  });

  it('give the largest and least over the surfaces in the engineer\'s sign', () => {
    const m = shellExtremes([
      { id: 's', sourceId: 's', name: 'Floor', kind: 'slab', extremes: { mx: { max: 2, atMax: [0, 0, 3], min: -10, atMin: [3, 2, 3] } } },
      { id: 'w', sourceId: 'w', name: 'Wall', kind: 'wall', extremes: { mx: { max: 4, atMax: [0, 0, 1], min: -1, atMin: [0, 0, 2] } } },
    ], 'mx');
    expect(m?.max).toEqual({ value: 10, at: [3, 2, 3], name: 'Floor' });
    expect(m?.min).toEqual({ value: -2, at: [0, 0, 3], name: 'Floor' });
    expect(shellExtremes([], 'w')).toBeNull();
  });

  it('say the sign of what is shown', () => {
    expect(signNote('mx', ['slab'])).toBe('Positive: the bottom face in tension.');
    expect(signNote('nx', ['slab', 'wall'])).toBe('Positive: tension.');
    expect(signNote('vx', ['slab'])).toBeNull();
  });
});
