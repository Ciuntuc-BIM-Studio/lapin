/**
 * Intel Open Image Denoise (Apache-2.0) for the path tracer's still images, on the desktop (docs/plan-render.md, S4,
 * answer Î4: a separate program). The engine runs OIDN's own `oidnDenoise` beside it (Lapin.Host/Denoiser.cs, the binaries
 * fetched by scripts/fetch-oidn.mjs); the picture's light goes as half floats, its albedo and normals as bytes, and the
 * denoised light comes back as half floats. Where there is no OIDN (the browser's engine, a build without it) the shader
 * denoiser (denoise.ts) does the work.
 */

import * as THREE from 'three';
import { engine, engineInBrowser } from '../engine/session';
import { toBase64 } from '../engine/files';

/** Whether the engine has OIDN beside it (asked once a session). */
let known: Promise<boolean> | null = null;
export function oidnAvailable(): Promise<boolean> {
  if (engineInBrowser) return Promise.resolve(false);
  known ??= engine.call<{ oidn: boolean }>('render.denoiser', {}).then((r) => !!r?.oidn).catch(() => false);
  return known;
}

/** Floats as IEEE half floats (little-endian Uint16). */
export function toHalf(values: Float32Array): Uint16Array {
  const out = new Uint16Array(values.length);
  for (let i = 0; i < values.length; i++) out[i] = THREE.DataUtils.toHalfFloat(Math.min(65000, Math.max(0, values[i])));
  return out;
}

/** 0–1 values (an albedo; a normal mapped from −1–1) as bytes. */
export function toBytes(values: Float32Array, signed = false): Uint8Array {
  const out = new Uint8Array(values.length);
  for (let i = 0; i < values.length; i++) out[i] = Math.round(Math.min(1, Math.max(0, signed ? values[i] * 0.5 + 0.5 : values[i])) * 255);
  return out;
}

const bytesOf = (a: ArrayBufferView) => new Uint8Array(a.buffer, a.byteOffset, a.byteLength);

function fromBase64(text: string): Uint8Array {
  const bin = atob(text);
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
  return out;
}

/** The picture denoised by OIDN: its light as a half-float texture (RGBA) of the same size, rows bottom-up as WebGL's. */
export async function oidnDenoise(p: { width: number; height: number; color: Float32Array; albedo: Float32Array; normal: Float32Array }): Promise<THREE.DataTexture> {
  const r = await engine.call<{ width: number; height: number; color: string }>('render.denoise', {
    width: p.width, height: p.height,
    color: toBase64(bytesOf(toHalf(p.color))),
    albedo: toBase64(toBytes(p.albedo)),
    normal: toBase64(toBytes(p.normal, true)),
  }, { timeoutMs: 300_000 });
  const half = new Uint16Array(fromBase64(r.color).buffer);
  const n = p.width * p.height;
  if (half.length < n * 3) throw new Error('The denoiser sent back less than the picture.');
  const rgba = new Uint16Array(n * 4);
  const one = THREE.DataUtils.toHalfFloat(1);
  for (let i = 0; i < n; i++) { rgba[4 * i] = half[3 * i]; rgba[4 * i + 1] = half[3 * i + 1]; rgba[4 * i + 2] = half[3 * i + 2]; rgba[4 * i + 3] = one; }
  const tex = new THREE.DataTexture(rgba, p.width, p.height, THREE.RGBAFormat, THREE.HalfFloatType);
  tex.needsUpdate = true;
  return tex;
}
