import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../engine/protocol';
import { copyArrays, creasedNormals, displayQualityOf, levelFor, pixelsPerMetre, type LodCamera } from './lod';

const persp = (z: number): LodCamera => ({ kind: 'perspective', eye: [0, 0, z], fovDeg: 45, heightPx: 1000 });

describe('choosing a coarser copy', () => {
  it('counts the pixels a metre covers at the part, in perspective and orthographic', () => {
    // 10 m away (a sphere of 1 m at 11 m), 45°: 500 / (10 · tan 22.5°) ≈ 120.7 px a metre.
    expect(pixelsPerMetre(persp(11), [0, 0, 0], 1)).toBeCloseTo(500 / (10 * Math.tan(Math.PI / 8)), 6);
    // The eye inside the part: as near as can be.
    expect(pixelsPerMetre(persp(0.5), [0, 0, 0], 1)).toBe(Infinity);
    // Orthographic: 50 m high on 1000 px, wherever the part is.
    expect(pixelsPerMetre({ kind: 'ortho', metresHigh: 50, heightPx: 1000 }, [100, 0, 0], 1)).toBe(20);
  });

  it('takes the coarsest copy whose error stays under the budget', () => {
    const errors = [0.01, 0.04, 0.16];
    // 120 px a metre: 1 cm is 1.2 px, over balanced's pixel: the part itself.
    expect(levelFor(errors, 120, 'balanced')).toBe(0);
    // 25 px a metre: 1 cm is a quarter pixel, 4 cm a pixel: the second copy.
    expect(levelFor(errors, 25, 'balanced')).toBe(2);
    // Fast allows 3 px: 16 cm at 15 px a metre is 2.4 px, the coarsest.
    expect(levelFor(errors, 15, 'fast')).toBe(3);
    // Precise draws the part itself however far it is.
    expect(levelFor(errors, 0.01, 'precise')).toBe(0);
    expect(levelFor(errors, Infinity, 'fast')).toBe(0);
  });

  it("reads the project's quality, balanced by default", () => {
    const project = (props: Record<string, unknown>) => new Map<string, NodeDto>([['p', { id: 'p', type: 'IfcProject', name: 'P', parentId: null, px: 0, py: 0, props, editable: true, module: null, storeyId: null } as NodeDto]]);
    expect(displayQualityOf(project({}))).toBe('balanced');
    expect(displayQualityOf(project({ displayQuality: 'fast' }))).toBe('fast');
    expect(displayQualityOf(project({ displayQuality: 'ultra' }))).toBe('balanced');
  });

  it("numbers a bare copy's corners in order and shades it smooth under the crease, sharp past it", () => {
    // Two triangles sharing the edge (0,0,0)–(1,0,0): folded 30°, then 90°.
    const pair = (deg: number) => {
      const a = (deg * Math.PI) / 180;
      return new Float32Array([0, 0, 0, 1, 0, 0, 0, 1, 0, /* second, turned about x */ 1, 0, 0, 0, 0, 0, 0, -Math.cos(a), Math.sin(a)]);
    };
    const l = { error: 0.01, positions: pair(30), normals: new Float32Array(0), indices: new Int32Array(0), edges: new Float32Array(0) };
    const m = copyArrays(l);
    expect([...m.indices]).toEqual([0, 1, 2, 3, 4, 5]);
    expect(copyArrays(l).normals).toBe(m.normals);
    // At the shared corner (0,0,0): one normal for both triangles, between their own.
    expect([...m.normals.slice(0, 3)].map((v) => +v.toFixed(5))).toEqual([...m.normals.slice(12, 15)].map((v) => +v.toFixed(5)));
    // Folded 90°: each keeps its own, the first +z, the second +y.
    const sharp = creasedNormals(pair(90));
    expect(sharp[2]).toBeCloseTo(1, 6);
    expect(sharp[13]).toBeCloseTo(1, 6);
    expect(sharp[14]).toBeCloseTo(0, 6);
  });
});
