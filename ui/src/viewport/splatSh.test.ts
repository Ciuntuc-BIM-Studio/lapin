import { describe, expect, it } from 'vitest';
import { shBasis, shBytes, shCoeffs, shColour, shDegreeCap, shTexels, writeSh } from './splatSh';
import { cloudFormat } from './pointClouds';

describe("a splat's colour by direction (G3)", () => {
  it('uses the real basis of 3D Gaussian splatting, the same numbers as the engine', () => {
    const z = shBasis(0, 0, 1);
    expect(z[1]).toBeCloseTo(0.4886025119029199, 12);
    expect(z[0]).toBeCloseTo(0, 12);
    expect(z[5]).toBeCloseTo(2 * 0.31539156525252005, 12);
    expect(z[11]).toBeCloseTo(2 * 0.3731763325901154, 12);
    const x = shBasis(1, 0, 0);
    expect(x[2]).toBeCloseTo(-0.4886025119029199, 12);
    expect(x[7]).toBeCloseTo(0.5462742152960396, 12);
    expect(x[14]).toBeCloseTo(-0.5900435899266435, 12);
    // Orthonormal over the sphere (a Fibonacci sphere's mean × 4π).
    const m = 20000;
    const gram = Array.from({ length: 15 }, () => new Array<number>(15).fill(0));
    for (let i = 0; i < m; i++) {
      const zz = 1 - (2 * i + 1) / m, r = Math.sqrt(1 - zz * zz), phi = i * Math.PI * (3 - Math.sqrt(5));
      const b = shBasis(r * Math.cos(phi), r * Math.sin(phi), zz);
      for (let p = 0; p < 15; p++) for (let q = 0; q < 15; q++) gram[p][q] += (b[p] * b[q] * 4 * Math.PI) / m;
    }
    for (let p = 0; p < 15; p++) for (let q = 0; q < 15; q++) expect(Math.abs(gram[p][q] - (p === q ? 1 : 0))).toBeLessThan(2e-3);
  });

  it('turns red from one side and blue from the other, and stays its base colour at degree 0', () => {
    // Degree 1's x coefficient (−C1·x): −0.5 for red, +0.5 for blue, as bytes (b − 128)/128.
    const sh = new Array<number>(9).fill(128);
    sh[6] = 64; sh[8] = 192;
    const seenFromMinusX = shColour([0.5, 0.5, 0.5], sh, 1, [1, 0, 0]);
    const seenFromPlusX = shColour([0.5, 0.5, 0.5], sh, 1, [-1, 0, 0]);
    expect(seenFromMinusX[0]).toBeGreaterThan(0.7);
    expect(seenFromMinusX[2]).toBeLessThan(0.3);
    expect(seenFromPlusX[2]).toBeGreaterThan(0.7);
    expect(seenFromPlusX[0]).toBeLessThan(0.3);
    // Any length of direction: the same.
    expect(shColour([0.5, 0.5, 0.5], sh, 1, [7, 0, 0])).toEqual(seenFromMinusX);
    expect(shColour([0.5, 0.4, 0.3], sh, 0, [1, 0, 0])).toEqual([0.5, 0.4, 0.3]);
    // Clamped to 0–1.
    sh[6] = 0;
    expect(shColour([0.9, 0.5, 0.5], sh, 1, [1, 0, 0])[0]).toBe(1);
  });

  it('is drawn at the degree the display quality affords, in 1 to 3 texels a splat', () => {
    expect(shDegreeCap('fast')).toBe(1);
    expect(shDegreeCap('balanced')).toBe(3);
    expect(shDegreeCap('precise')).toBe(3);
    expect(shBytes()).toBe(96 * 1024 * 1024);
    expect(shBytes(true)).toBe(48 * 1024 * 1024);
    expect([0, 1, 2, 3].map(shCoeffs)).toEqual([0, 3, 8, 15]);
    expect([0, 1, 2, 3].map(shTexels)).toEqual([0, 1, 2, 3]);
    // Balanced's 2 million at degree 3 fit the desktop's bytes (45 a splat); Precise's 4 million do not.
    expect(2_000_000 * 45).toBeLessThanOrEqual(shBytes());
    expect(4_000_000 * 45).toBeGreaterThan(shBytes());
  });

  it("writes a page's bytes at the start of each splat's texels, as the shader reads them", () => {
    // Degree 2: 24 bytes a splat, two texels (32 bytes); the engine packs the bytes four to an int.
    const bytes = new Uint8Array(2 * 24).map((_, i) => i + 1);
    const ints = new Int32Array(bytes.buffer);
    const tex = new Uint32Array(4 * 2 * 4);
    writeSh(tex, 1, 2, ints, 24, 2);
    const t = new Uint8Array(tex.buffer);
    // Splat 1 at byte 32, splat 2 at byte 64; byte k of a splat is (word k >> 2) >> 8·(k & 3).
    expect(t[32]).toBe(1);
    expect(t[32 + 23]).toBe(24);
    expect(t[64]).toBe(25);
    expect((tex[8 + 1] >>> 8) & 255).toBe(6);
    expect(t[0]).toBe(0);
  });

  it("names a cloud's format in a few words for its row in Manage → Links", () => {
    expect(cloudFormat('SOG version 3 Gaussian splats, harmonics of degree 3')).toBe('SOG v3 · SH 3');
    expect(cloudFormat('SPZ version 4 Gaussian splats')).toBe('SPZ v4');
    expect(cloudFormat('Compressed PLY Gaussian splats, harmonics of degree 1')).toBe('SuperSplat PLY · SH 1');
    expect(cloudFormat('PLY Gaussian splats')).toBe('3DGS PLY');
    expect(cloudFormat('LAS 1.4, point format 7')).toBe('LAS 1.4, point format 7');
  });
});
