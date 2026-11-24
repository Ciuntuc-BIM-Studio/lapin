/**
 * The colour by direction of Gaussian splats (docs/plan-gaussian-splats.md, G3): the real spherical harmonics of degrees
 * 1–3 with the Condon–Shortley phase, as 3D Gaussian splatting uses them (Kerbl et al. 2023; the engine's Splat.ShBasis), a
 * colour evaluated from them as the vertex shader does (splats3d.ts), and how much of them a view affords: the degree by the
 * display quality, the texture's bytes by quality and host. Pure, for the tests.
 */

import type { DisplayQuality } from './lod';

/** The coefficients a colour channel has above degree 0: 0, 3, 8, 15 for degrees 0–3. */
export const shCoeffs = (degree: number): number => [0, 3, 8, 15][Math.max(0, Math.min(3, degree))];

/** The basis at a unit direction (x, y, z): 15 values, band by band. */
export function shBasis(x: number, y: number, z: number): number[] {
  const C1 = 0.4886025119029199;
  const xx = x * x, yy = y * y, zz = z * z, xy = x * y, yz = y * z, xz = x * z;
  return [
    -C1 * y, C1 * z, -C1 * x,
    1.0925484305920792 * xy, -1.0925484305920792 * yz, 0.31539156525252005 * (2 * zz - xx - yy), -1.0925484305920792 * xz, 0.5462742152960396 * (xx - yy),
    -0.5900435899266435 * y * (3 * xx - yy), 2.890611442640554 * xy * z, -0.4570457994644658 * y * (4 * zz - xx - yy),
    0.3731763325901154 * z * (2 * zz - 3 * xx - 3 * yy), -0.4570457994644658 * x * (4 * zz - xx - yy), 1.445305721320277 * z * (xx - yy),
    -0.5900435899266435 * x * (xx - 3 * yy),
  ];
}

/**
 * A Gaussian's colour seen along `dir` (from the eye to it, in the file's frame; any length): its base colour (0–1) plus its
 * harmonics (bytes as the engine sends them, (b − 128)/128, coefficient by coefficient, colours inner) up to `degree`,
 * clamped to 0–1 as the shader does.
 */
export function shColour(base: readonly [number, number, number], sh: ArrayLike<number>, degree: number, dir: readonly [number, number, number]): [number, number, number] {
  const n = Math.min(shCoeffs(degree), Math.floor(sh.length / 3));
  const len = Math.hypot(dir[0], dir[1], dir[2]);
  if (n === 0 || !(len > 0)) return [base[0], base[1], base[2]];
  const b = shBasis(dir[0] / len, dir[1] / len, dir[2] / len);
  const o: [number, number, number] = [base[0], base[1], base[2]];
  for (let j = 0; j < n; j++) for (let c = 0; c < 3; c++) o[c] += b[j] * (sh[3 * j + c] - 128) / 128;
  return [Math.min(1, Math.max(0, o[0])), Math.min(1, Math.max(0, o[1])), Math.min(1, Math.max(0, o[2]))];
}

/**
 * The highest degree a display quality draws: Fast 1 (the broad change with the view, 16 bytes a splat), Balanced and
 * Precise 3 (all of it, 48 bytes a splat); held further to the bytes `shBytes` gives.
 */
export function shDegreeCap(q: DisplayQuality): number {
  return q === 'fast' ? 1 : 3;
}

/**
 * The bytes the harmonics of all splat clouds together may take (their coefficients, a byte each; the texture is a little
 * larger, rounded up to 16 bytes a splat): 96 MB on the desktop, 48 MB in a browser. The engine lowers the degree of a cloud
 * whose kept splats would take more (SplatStore.DegreeFor): at Balanced's 2 million, degree 3 (90 MB); at Precise's 4
 * million, degree 2 (96 MB).
 */
export function shBytes(browser = false): number {
  return (browser ? 48 : 96) * 1024 * 1024;
}

/** Texels (16 bytes each) a splat's harmonics take in the texture at a degree: 0, 1, 2, 3. */
export const shTexels = (degree: number): number => Math.ceil(shCoeffs(degree) * 3 / 16);

/**
 * A page's harmonics written into a cloud's texture from Gaussian `at` on (`texels` a Gaussian; the bytes, `per` a Gaussian,
 * packed in the engine's ints): each Gaussian's bytes at the start of its texels, little-endian, as the shader reads them.
 */
export function writeSh(tex: Uint32Array, at: number, texels: number, sh: Int32Array | Uint8Array, per: number, count: number): void {
  const src = sh instanceof Uint8Array ? sh : new Uint8Array(sh.buffer, sh.byteOffset, sh.byteLength);
  const dst = new Uint8Array(tex.buffer, tex.byteOffset, tex.byteLength);
  for (let i = 0; i < count; i++) dst.set(src.subarray(i * per, (i + 1) * per), (at + i) * texels * 16);
}
