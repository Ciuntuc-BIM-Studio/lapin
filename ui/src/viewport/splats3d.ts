/**
 * Gaussian splats in the 3D view (docs/plan-gaussian-splats.md, G1), drawn by Lapin itself with three.js: each splat link's
 * Gaussians asked for page by page (`pointcloud.splats`, raw arrays, placed in the project by the engine), the largest and
 * most opaque first when the display quality's budget is smaller than the file. A cloud is one instanced quad a Gaussian;
 * its centre and its 3D covariance (Σ = R S² Rᵀ, worked out here once) live in an integer texture, two texels a Gaussian;
 * the vertex shader projects Σ to the screen (the EWA splatting of Zwicker et al., as Kerbl et al. 2023 use it: Σ' = J W Σ
 * Wᵀ Jᵀ, a 0.3 px² low-pass added) and spans the quad over its ellipse to 3σ; the fragment shader weighs the Gaussian and
 * blends it premultiplied. The quads are drawn back to front: a worker sorts the centres by depth (a 16-bit counting sort,
 * splatSort.ts) when the camera has turned or moved enough. Clipped by the view's planes (by the Gaussian's centre), greyed
 * for halftone, as the points are. No library: the clipping, the budget and the picking stay Lapin's.
 *
 * G3: the colour by direction. When a cloud is drawn in full colour, its spherical harmonics (degrees 1–3) come with its
 * pages, a byte each, into a second integer texture (1–3 texels a splat by degree); the vertex shader evaluates them along the
 * direction from the eye to the splat, turned into the file's frame by the page's `turn` (the link's rotation and up axis
 * undone), with the standard real basis (splatSh.ts). The degree is the display quality's (Fast 1, else 3), lowered by the
 * engine until the cloud's coefficients fit its share of the bytes (splatSh.shBytes).
 */

import * as THREE from 'three';
import type { DisplayQuality } from './lod';
import type { CloudSpec } from './pointClouds3d';
import { depthRow, sortBackToFront, type DepthRow } from './splatSort';
import { shBytes, shCoeffs, shDegreeCap, shTexels, writeSh } from './splatSh';

/**
 * A page of a link's Gaussians as the engine sends it (PointCloudLinks.Splats): rotations w x y z, colours RGBA a byte each;
 * when harmonics were asked, their degree, their bytes (packed in ints) and the turn of a project direction into the file's
 * (3 × 3, column by column).
 */
export interface SplatPage {
  link: string; start: number; next: number | null; total: number; kept: number;
  positions: Float32Array; scales: Float32Array; rotations: Float32Array; colors: Int32Array;
  shDegree?: number; sh?: Int32Array; turn?: Float32Array;
}

/** The Gaussians drawn at most, all splat clouds together, by the project's display quality (fewer in a browser). */
export function splatBudget(q: DisplayQuality, browser = false): number {
  const n = q === 'fast' ? 1_000_000 : q === 'precise' ? 4_000_000 : 2_000_000;
  return browser ? Math.round(n / 2) : n;
}

/** Σ = R · diag(s²) · Rᵀ of a unit quaternion (w, x, y, z), as its six numbers xx, xy, xz, yy, yz, zz. */
export function covariance(s: readonly [number, number, number], q: readonly [number, number, number, number]): [number, number, number, number, number, number] {
  const [w, x, y, z] = q;
  const r = [
    1 - 2 * (y * y + z * z), 2 * (x * y - w * z), 2 * (x * z + w * y),
    2 * (x * y + w * z), 1 - 2 * (x * x + z * z), 2 * (y * z - w * x),
    2 * (x * z - w * y), 2 * (y * z + w * x), 1 - 2 * (x * x + y * y),
  ];
  const s2 = [s[0] * s[0], s[1] * s[1], s[2] * s[2]];
  const e = (i: number, j: number) => r[3 * i] * s2[0] * r[3 * j] + r[3 * i + 1] * s2[1] * r[3 * j + 1] + r[3 * i + 2] * s2[2] * r[3 * j + 2];
  return [e(0, 0), e(0, 1), e(0, 2), e(1, 1), e(1, 2), e(2, 2)];
}

/**
 * A Gaussian's covariance on the screen (px²), as the vertex shader works it out: its centre in the camera's space `c`
 * (three's: looking down −z), the view's rotation `w` (row-major 3 × 3, world to camera), the focal lengths in pixels;
 * an orthographic view's Jacobian is constant. The 0.3 px² low-pass keeps a Gaussian at least a pixel wide.
 */
export function projectCovariance(cov: readonly number[], c: readonly [number, number, number], w: readonly number[], fx: number, fy: number, ortho = false): { a: number; b: number; d: number } {
  const z = -c[2];
  // J's two rows (screen x, y by camera x, y, z).
  const j = ortho ? [fx, 0, 0, 0, fy, 0] : [fx / z, 0, (fx * c[0]) / (z * z), 0, fy / z, (fy * c[1]) / (z * z)];
  // T = J W (2 × 3).
  const t = [0, 0, 0, 0, 0, 0];
  for (let r = 0; r < 2; r++) for (let k = 0; k < 3; k++) t[3 * r + k] = j[3 * r] * w[k] + j[3 * r + 1] * w[3 + k] + j[3 * r + 2] * w[6 + k];
  const v = [[cov[0], cov[1], cov[2]], [cov[1], cov[3], cov[4]], [cov[2], cov[4], cov[5]]];
  const e = (r: number, s: number) => {
    let o = 0;
    for (let k = 0; k < 3; k++) for (let l = 0; l < 3; l++) o += t[3 * r + k] * v[k][l] * t[3 * s + l];
    return o;
  };
  return { a: e(0, 0) + 0.3, b: e(0, 1), d: e(1, 1) + 0.3 };
}

/** The ellipse of a screen covariance to 3σ: its two half-axes (px), the longer first, the other a quarter turn anticlockwise from it (the quad stays front-facing), at most 1024 px. */
export function ellipseAxes(a: number, b: number, d: number): { major: [number, number]; minor: [number, number] } {
  const mid = (a + d) / 2, radius = Math.hypot((a - d) / 2, b);
  const l1 = mid + radius, l2 = Math.max(mid - radius, 0.1);
  let dx = b, dy = l1 - a;
  const n = Math.hypot(dx, dy);
  if (n < 1e-12) { dx = a >= d ? 1 : 0; dy = a >= d ? 0 : 1; } else { dx /= n; dy /= n; }
  const m1 = Math.min(3 * Math.sqrt(l1), 1024), m2 = Math.min(3 * Math.sqrt(l2), 1024);
  return { major: [dx * m1, dy * m1], minor: [-dy * m2, dx * m2] };
}

/** A float as IEEE half bits (round to nearest; what GLSL's unpackHalf2x16 reads back). */
export function toHalf(v: number): number {
  F32[0] = v;
  const x = U32[0];
  const sign = (x >>> 16) & 0x8000;
  const exp = (x >>> 23) & 0xff;
  let mant = x & 0x7fffff;
  if (exp === 0xff) return sign | 0x7c00 | (mant ? 0x200 : 0);
  const e = exp - 127 + 15;
  if (e >= 0x1f) return sign | 0x7c00;
  if (e <= 0) {
    if (e < -10) return sign;
    mant |= 0x800000;
    const shift = 14 - e;
    let h = mant >> shift;
    if ((mant >> (shift - 1)) & 1) h++;
    return sign | h;
  }
  let h = (e << 10) | (mant >> 13);
  if (mant & 0x1000) { h++; if ((h & 0x7c00) === 0x7c00) return sign | 0x7c00; }
  return sign | h;
}
const F32 = new Float32Array(1);
const U32 = new Uint32Array(F32.buffer);

/** Two texels a Gaussian, 1024 Gaussians a row of the texture. */
const PER_ROW = 1024;

/**
 * A page written into a cloud's texture from Gaussian `at` on: texel 0 its centre (float bits) and colour (RGBA), texel 1 its
 * covariance as halves (xx|xy, xz|yy, yz|zz). Pure, for the tests.
 */
export function writePage(tex: Uint32Array, at: number, p: Pick<SplatPage, 'positions' | 'scales' | 'rotations' | 'colors'>): void {
  const n = p.colors.length;
  const f = new Float32Array(tex.buffer, tex.byteOffset, tex.length);
  for (let i = 0; i < n; i++) {
    const o = 8 * (at + i);
    f[o] = p.positions[3 * i]; f[o + 1] = p.positions[3 * i + 1]; f[o + 2] = p.positions[3 * i + 2];
    tex[o + 3] = p.colors[i] >>> 0;
    const c = covariance([p.scales[3 * i], p.scales[3 * i + 1], p.scales[3 * i + 2]], [p.rotations[4 * i], p.rotations[4 * i + 1], p.rotations[4 * i + 2], p.rotations[4 * i + 3]]);
    tex[o + 4] = (toHalf(c[0]) | (toHalf(c[1]) << 16)) >>> 0;
    tex[o + 5] = (toHalf(c[2]) | (toHalf(c[3]) << 16)) >>> 0;
    tex[o + 6] = (toHalf(c[4]) | (toHalf(c[5]) << 16)) >>> 0;
    tex[o + 7] = 0;
  }
}

const VERT = /* glsl */ `
precision highp float;
precision highp int;
precision highp usampler2D;
#include <common>
#include <clipping_planes_pars_vertex>
uniform usampler2D splatData;
uniform usampler2D shData;
uniform int shDegree;
uniform int shTexels;
uniform mat3 fileTurn;
uniform vec3 eye;
uniform vec3 viewDir;
uniform vec2 viewport;
uniform float halftone;
attribute uint splatIndex;
varying vec4 vColor;
varying vec2 vPos;
// A harmonic's byte k of the splat's texels t0–t2, as the value it stands for: (b − 128) / 128.
float shByte(uvec4 t0, uvec4 t1, uvec4 t2, int k) {
  int ti = k >> 4;
  uvec4 t = ti == 0 ? t0 : (ti == 1 ? t1 : t2);
  uint w = t[(k >> 2) & 3];
  return float((w >> uint(8 * (k & 3))) & 255u) / 128.0 - 1.0;
}
vec3 shCoeff(uvec4 t0, uvec4 t1, uvec4 t2, int j) {
  return vec3(shByte(t0, t1, t2, 3 * j), shByte(t0, t1, t2, 3 * j + 1), shByte(t0, t1, t2, 3 * j + 2));
}
// The colour seen from the eye (Kerbl et al. 2023: the real basis with the Condon–Shortley phase), in the file's frame.
vec3 shColour(vec3 base, vec3 centre, bool ortho) {
  if (shDegree == 0) return base;
  vec3 d = normalize(fileTurn * (ortho ? viewDir : centre - eye));
  ivec2 at = ivec2(int(splatIndex & 1023u) * shTexels, int(splatIndex >> 10u));
  uvec4 t0 = texelFetch(shData, at, 0);
  uvec4 t1 = shTexels > 1 ? texelFetch(shData, at + ivec2(1, 0), 0) : uvec4(0u);
  uvec4 t2 = shTexels > 2 ? texelFetch(shData, at + ivec2(2, 0), 0) : uvec4(0u);
  float x = d.x, y = d.y, z = d.z;
  vec3 c = base - 0.4886025119029199 * y * shCoeff(t0, t1, t2, 0) + 0.4886025119029199 * z * shCoeff(t0, t1, t2, 1) - 0.4886025119029199 * x * shCoeff(t0, t1, t2, 2);
  if (shDegree > 1) {
    float xx = x * x, yy = y * y, zz = z * z;
    c += 1.0925484305920792 * x * y * shCoeff(t0, t1, t2, 3) - 1.0925484305920792 * y * z * shCoeff(t0, t1, t2, 4)
      + 0.31539156525252005 * (2.0 * zz - xx - yy) * shCoeff(t0, t1, t2, 5) - 1.0925484305920792 * x * z * shCoeff(t0, t1, t2, 6)
      + 0.5462742152960396 * (xx - yy) * shCoeff(t0, t1, t2, 7);
    if (shDegree > 2) {
      c += -0.5900435899266435 * y * (3.0 * xx - yy) * shCoeff(t0, t1, t2, 8) + 2.890611442640554 * x * y * z * shCoeff(t0, t1, t2, 9)
        - 0.4570457994644658 * y * (4.0 * zz - xx - yy) * shCoeff(t0, t1, t2, 10) + 0.3731763325901154 * z * (2.0 * zz - 3.0 * xx - 3.0 * yy) * shCoeff(t0, t1, t2, 11)
        - 0.4570457994644658 * x * (4.0 * zz - xx - yy) * shCoeff(t0, t1, t2, 12) + 1.445305721320277 * z * (xx - yy) * shCoeff(t0, t1, t2, 13)
        - 0.5900435899266435 * x * (xx - 3.0 * yy) * shCoeff(t0, t1, t2, 14);
    }
  }
  return clamp(c, 0.0, 1.0);
}
void main() {
  ivec2 at = ivec2(int(splatIndex & 1023u) * 2, int(splatIndex >> 10u));
  uvec4 a = texelFetch(splatData, at, 0);
  uvec4 b = texelFetch(splatData, at + ivec2(1, 0), 0);
  vec3 centre = uintBitsToFloat(a.xyz);
  vec4 mvPosition = modelViewMatrix * vec4(centre, 1.0);
  vec4 clip = projectionMatrix * mvPosition;
  float bound = 1.2 * clip.w;
  bool ortho = projectionMatrix[3][3] == 1.0;
  if ((!ortho && mvPosition.z > -1e-3) || clip.x < -bound || clip.x > bound || clip.y < -bound || clip.y > bound) {
    gl_Position = vec4(0.0, 0.0, 2.0, 1.0);
    return;
  }
  #include <clipping_planes_vertex>
  vec2 u1 = unpackHalf2x16(b.x), u2 = unpackHalf2x16(b.y), u3 = unpackHalf2x16(b.z);
  mat3 V = mat3(u1.x, u1.y, u2.x, u1.y, u2.y, u3.x, u2.x, u3.x, u3.y);
  float fx = projectionMatrix[0][0] * viewport.x * 0.5;
  float fy = projectionMatrix[1][1] * viewport.y * 0.5;
  float z = -mvPosition.z;
  mat3 J = ortho
    ? mat3(fx, 0.0, 0.0, 0.0, fy, 0.0, 0.0, 0.0, 0.0)
    : mat3(fx / z, 0.0, 0.0, 0.0, fy / z, 0.0, fx * mvPosition.x / (z * z), fy * mvPosition.y / (z * z), 0.0);
  mat3 T = J * mat3(modelViewMatrix);
  mat3 S = T * V * transpose(T);
  float ca = S[0][0] + 0.3, cb = S[0][1], cd = S[1][1] + 0.3;
  float mid = 0.5 * (ca + cd), radius = length(vec2(0.5 * (ca - cd), cb));
  float l1 = mid + radius, l2 = max(mid - radius, 0.1);
  vec2 dir = vec2(cb, l1 - ca);
  float dl = length(dir);
  dir = dl < 1e-12 ? (ca >= cd ? vec2(1.0, 0.0) : vec2(0.0, 1.0)) : dir / dl;
  vec2 major = min(3.0 * sqrt(l1), 1024.0) * dir;
  // Across the major, a quarter turn anticlockwise: the quad keeps its winding (front-facing).
  vec2 minor = min(3.0 * sqrt(l2), 1024.0) * vec2(-dir.y, dir.x);
  vec4 rgba = vec4((a.w) & 255u, (a.w >> 8u) & 255u, (a.w >> 16u) & 255u, (a.w >> 24u) & 255u) / 255.0;
  vColor = vec4(mix(shColour(rgba.rgb, centre, ortho), vec3(0.62), halftone), rgba.a);
  vPos = position.xy;
  vec2 ndc = clip.xy / clip.w + (position.x * major + position.y * minor) * 2.0 / viewport;
  gl_Position = vec4(ndc, clip.z / clip.w, 1.0);
}`;

const FRAG = /* glsl */ `
precision highp float;
#include <clipping_planes_pars_fragment>
varying vec4 vColor;
varying vec2 vPos;
void main() {
  #include <clipping_planes_fragment>
  float r2 = dot(vPos, vPos);
  if (r2 > 1.0) discard;
  float alpha = exp(-4.5 * r2) * vColor.a;
  if (alpha < 1.0 / 255.0) discard;
  gl_FragColor = vec4(vColor.rgb * alpha, alpha);
}`;

/** A 1 × 1 texture standing in for the harmonics of a cloud drawn in its base colour (a sampler must have one). */
let noSh: THREE.DataTexture | null = null;
function emptySh(): THREE.DataTexture {
  if (noSh) return noSh;
  noSh = new THREE.DataTexture(new Uint32Array(4), 1, 1, THREE.RGBAIntegerFormat, THREE.UnsignedIntType);
  noSh.internalFormat = 'RGBA32UI';
  noSh.minFilter = noSh.magFilter = THREE.NearestFilter;
  noSh.needsUpdate = true;
  return noSh;
}

function material(data: THREE.DataTexture): THREE.ShaderMaterial {
  return new THREE.ShaderMaterial({
    vertexShader: VERT, fragmentShader: FRAG, clipping: true, transparent: true, depthWrite: false, depthTest: true, side: THREE.DoubleSide,
    blending: THREE.CustomBlending, blendEquation: THREE.AddEquation, blendSrc: THREE.OneFactor, blendDst: THREE.OneMinusSrcAlphaFactor,
    blendSrcAlpha: THREE.OneFactor, blendDstAlpha: THREE.OneMinusSrcAlphaFactor,
    uniforms: {
      splatData: { value: data }, viewport: { value: new THREE.Vector2(1, 1) }, halftone: { value: 0 },
      shData: { value: emptySh() }, shDegree: { value: 0 }, shTexels: { value: 1 }, fileTurn: { value: new THREE.Matrix3() },
      eye: { value: new THREE.Vector3() }, viewDir: { value: new THREE.Vector3(0, 0, -1) },
    },
  });
}

/** What the layer asks of the engine. */
export interface SplatSource {
  status(id: string): Promise<{ state: string } | null>;
  /** `sh`: the highest degree of harmonics wanted (0: none), `shBytes`: the bytes they may take for this cloud. */
  page(id: string, start: number, budget: number, sh?: number, shBytes?: number): Promise<SplatPage | null>;
}

/** What a cloud's Gaussians were asked with: another asks for them again. */
interface Ask { budget: number; sh: number; shBytes: number }

interface Cloud {
  spec: CloudSpec;
  /** What its Gaussians were asked with (the budget, the harmonics); another asking loads them again. */
  ask: Ask;
  loading: boolean;
  /** Bumped when the cloud is asked for again, so a late page of the earlier asking is dropped. */
  gen: number;
  capacity: number;
  loaded: number;
  tex: Uint32Array | null;
  texture: THREE.DataTexture | null;
  /** Its harmonics' texture (G3), `texels` a splat, at the degree the first page came with; none in base colour. */
  shTex: Uint32Array | null;
  shTexture: THREE.DataTexture | null;
  shDegree: number;
  texels: number;
  positions: Float32Array;
  mesh: THREE.Mesh<THREE.InstancedBufferGeometry, THREE.ShaderMaterial> | null;
  order: THREE.InstancedBufferAttribute | null;
  box: THREE.Box3;
  /** The view it was last sorted for, the loaded count then; a sort in flight. */
  sortedRow: DepthRow | null; sortedEye: THREE.Vector3; sortedN: number; sorting: boolean; seq: number;
}

/** The view must have turned more than about 1°, or the eye moved more than 1 % of its distance to the cloud, to sort again. */
const TURN = Math.cos(Math.PI / 180);

/** The 3D view's splat clouds: one instanced mesh a cloud, under one group. */
export class SplatLayer {
  readonly group = new THREE.Group();
  private readonly clouds = new Map<string, Cloud>();
  private worker: Worker | null = null;
  private workerTried = false;
  private budget = 2_000_000;
  private shCap = 3;
  private shRoom = shBytes();
  /** Called with how many Gaussians a cloud draws, for the panels (−1: it is no longer drawn as splats). */
  onDrawn: (id: string, drawn: number) => void = () => undefined;

  constructor(private readonly source: SplatSource, private readonly invalidate: () => void, private readonly maxTexture = 8192) {
    this.group.name = 'gaussian-splats';
  }

  /** The splat clouds as the links and the view now have them (the specs drawn as splats). */
  sync(specs: readonly CloudSpec[]): void {
    const wanted = specs.filter((s) => s.splats);
    const keep = new Set(wanted.map((s) => s.id));
    for (const [id, c] of this.clouds) if (!keep.has(id)) this.drop(id, c);
    for (const spec of wanted) {
      let c = this.clouds.get(spec.id);
      if (c && c.spec.rev !== spec.rev) { this.drop(spec.id, c); c = undefined; }
      if (!c) {
        c = {
          spec, ask: { budget: 0, sh: -1, shBytes: 0 }, loading: false, gen: 0, capacity: 0, loaded: 0, tex: null, texture: null,
          shTex: null, shTexture: null, shDegree: 0, texels: 0, positions: new Float32Array(0), mesh: null, order: null,
          box: new THREE.Box3(), sortedRow: null, sortedEye: new THREE.Vector3(), sortedN: 0, sorting: false, seq: 0,
        };
        this.clouds.set(spec.id, c);
      }
      c.spec = spec;
      if (c.mesh) {
        c.mesh.visible = spec.shown;
        c.mesh.material.uniforms.halftone.value = spec.halftone ? 0.6 : 0;
      }
    }
    this.invalidate();
  }

  /** What a cloud asks for: the quality's budget and harmonics' bytes, shared by the clouds shown; no harmonics in base colour. */
  private share(c: Cloud): Ask {
    const shown = Math.max(1, [...this.clouds.values()].filter((x) => x.spec.shown).length);
    const full = c.spec.splatColor !== 'base';
    return { budget: Math.max(50_000, Math.floor(this.budget / shown)), sh: full ? this.shCap : 0, shBytes: full ? Math.floor(this.shRoom / shown) : 0 };
  }

  /** A cloud's Gaussians asked for, page by page, once its index is ready. */
  private async load(c: Cloud, ask: Ask): Promise<void> {
    c.loading = true;
    const gen = ++c.gen;
    c.ask = ask;
    const budget = ask.budget;
    const live = () => this.clouds.get(c.spec.id) === c && c.gen === gen;
    try {
      for (let k = 0; ; k++) {
        const st = await this.source.status(c.spec.id);
        if (!live()) return;
        if (st?.state === 'ready') break;
        if (st?.state !== 'indexing' || k > 2000) return;
        await new Promise((r) => setTimeout(r, 600));
      }
      let start: number | null = 0;
      let first = true;
      while (start != null) {
        const p = await this.source.page(c.spec.id, start, budget, ask.sh, ask.shBytes);
        if (!live() || !p) return;
        if (first) { this.allocate(c, Math.min(p.kept, this.maxTexture * PER_ROW), p.sh ? p.shDegree ?? 0 : 0, p.turn); first = false; }
        const room = Math.max(0, c.capacity - c.loaded);
        const n = Math.min(room, p.colors.length);
        if (n > 0) {
          const part = n === p.colors.length ? p : { positions: p.positions.subarray(0, 3 * n), scales: p.scales.subarray(0, 3 * n), rotations: p.rotations.subarray(0, 4 * n), colors: p.colors.subarray(0, n) };
          writePage(c.tex!, c.loaded, part);
          if (c.shTex && p.sh && (p.shDegree ?? 0) === c.shDegree) {
            writeSh(c.shTex, c.loaded, c.texels, p.sh, shCoeffs(c.shDegree) * 3, n);
            c.shTexture!.needsUpdate = true;
          }
          c.positions.set(part.positions, 3 * c.loaded);
          for (let i = 0; i < n; i++) c.box.expandByPoint(new THREE.Vector3(part.positions[3 * i], part.positions[3 * i + 1], part.positions[3 * i + 2]));
          this.sendPositions(c, c.loaded, part.positions);
          c.loaded += n;
          c.texture!.needsUpdate = true;
          c.sortedRow = null;
        }
        this.onDrawn(c.spec.id, c.loaded);
        this.invalidate();
        start = p.next;
      }
    } catch {
      // A page that failed leaves what came; the next budget or link change asks again.
    } finally {
      if (c.gen === gen) c.loading = false;
    }
  }

  private allocate(c: Cloud, n: number, shDegree = 0, turn?: Float32Array): void {
    this.free(c);
    c.capacity = n;
    c.shDegree = shDegree;
    c.texels = shTexels(shDegree);
    c.loaded = 0;
    c.box.makeEmpty();
    const rows = Math.max(1, Math.ceil(n / PER_ROW));
    c.tex = new Uint32Array(rows * PER_ROW * 8);
    c.positions = new Float32Array(n * 3);
    const texture = new THREE.DataTexture(c.tex, PER_ROW * 2, rows, THREE.RGBAIntegerFormat, THREE.UnsignedIntType);
    texture.internalFormat = 'RGBA32UI';
    texture.minFilter = texture.magFilter = THREE.NearestFilter;
    texture.generateMipmaps = false;
    texture.needsUpdate = true;
    c.texture = texture;
    const g = new THREE.InstancedBufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(new Float32Array([-1, -1, 0, 1, -1, 0, 1, 1, 0, -1, 1, 0]), 3));
    g.setIndex([0, 1, 2, 0, 2, 3]);
    const order = new THREE.InstancedBufferAttribute(new Uint32Array(Math.max(1, n)), 1);
    order.gpuType = THREE.IntType;
    order.setUsage(THREE.DynamicDrawUsage);
    g.setAttribute('splatIndex', order);
    g.instanceCount = 0;
    const mesh = new THREE.Mesh(g, material(texture));
    if (shDegree > 0 && PER_ROW * c.texels <= this.maxTexture) {
      c.shTex = new Uint32Array(rows * PER_ROW * c.texels * 4);
      const sht = new THREE.DataTexture(c.shTex, PER_ROW * c.texels, rows, THREE.RGBAIntegerFormat, THREE.UnsignedIntType);
      sht.internalFormat = 'RGBA32UI';
      sht.minFilter = sht.magFilter = THREE.NearestFilter;
      sht.generateMipmaps = false;
      sht.needsUpdate = true;
      c.shTexture = sht;
      const u = mesh.material.uniforms;
      u.shData.value = sht;
      u.shDegree.value = shDegree;
      u.shTexels.value = c.texels;
      if (turn && turn.length === 9) (u.fileTurn.value as THREE.Matrix3).fromArray(Array.from(turn));
    } else c.shDegree = 0;
    mesh.frustumCulled = false;
    mesh.renderOrder = 10;
    mesh.visible = c.spec.shown;
    mesh.material.uniforms.halftone.value = c.spec.halftone ? 0.6 : 0;
    mesh.userData = { splatCloud: c.spec.id };
    // Picked and snapped by its centres, the cloud's points (pointClouds3d.ts), never by three's raycaster.
    mesh.raycast = () => undefined;
    c.mesh = mesh;
    c.order = order;
    this.group.add(mesh);
    this.post({ kind: 'drop', id: c.spec.id });
  }

  private free(c: Cloud): void {
    if (c.mesh) { this.group.remove(c.mesh); c.mesh.geometry.dispose(); c.mesh.material.dispose(); }
    c.texture?.dispose();
    c.shTexture?.dispose();
    c.mesh = null; c.texture = null; c.tex = null; c.order = null; c.sortedRow = null; c.sortedN = 0;
    c.shTexture = null; c.shTex = null; c.shDegree = 0;
  }

  private drop(id: string, c: Cloud): void {
    c.gen++;
    this.onDrawn(id, -1);
    this.free(c);
    this.clouds.delete(id);
    this.post({ kind: 'drop', id });
  }

  /** The sort's worker, made the first time it is needed; none where there are no workers (the tests): the sort runs here. */
  private sorter(): Worker | null {
    if (this.workerTried) return this.worker;
    this.workerTried = true;
    if (typeof Worker === 'undefined') return null;
    try {
      const w = new Worker(new URL('./splatSort.worker.ts', import.meta.url), { type: 'module' });
      w.onmessage = (e: MessageEvent<{ id: string; seq: number; n: number; order: Uint32Array }>) => this.sorted(e.data.id, e.data.seq, e.data.n, e.data.order);
      this.worker = w;
    } catch { this.worker = null; }
    return this.worker;
  }

  private post(m: unknown, transfer: Transferable[] = []): void {
    this.worker?.postMessage(m, transfer);
  }

  private sendPositions(c: Cloud, at: number, positions: Float32Array): void {
    const w = this.sorter();
    if (!w) return;
    const copy = positions.slice();
    w.postMessage({ kind: 'positions', id: c.spec.id, at, positions: copy, capacity: c.capacity }, [copy.buffer]);
  }

  private sorted(id: string, seq: number, n: number, order: Uint32Array): void {
    const c = this.clouds.get(id);
    if (!c) return;
    if (seq === c.seq) c.sorting = false;
    if (!c.order || n > c.capacity) return;
    (c.order.array as Uint32Array).set(order.subarray(0, n));
    c.order.addUpdateRange(0, n);
    c.order.needsUpdate = true;
    c.mesh!.geometry.instanceCount = n;
    this.invalidate();
  }

  /** A frame: the budget by the quality, the clouds asked for again if it changed, a sort asked for when the view has moved enough. */
  update(camera: THREE.Camera, renderer: { getDrawingBufferSize(v: THREE.Vector2): THREE.Vector2 }, quality: DisplayQuality, browser = false): void {
    this.budget = splatBudget(quality, browser);
    this.shCap = shDegreeCap(quality);
    this.shRoom = shBytes(browser);
    camera.updateMatrixWorld();
    const size = renderer.getDrawingBufferSize(new THREE.Vector2());
    const eye = new THREE.Vector3().setFromMatrixPosition(camera.matrixWorld);
    const row = depthRow(camera.matrixWorldInverse.elements);
    for (const c of this.clouds.values()) {
      if (!c.spec.shown) continue;
      const ask = this.share(c);
      if (!c.loading && (c.ask.budget !== ask.budget || c.ask.sh !== ask.sh || c.ask.shBytes !== ask.shBytes)) void this.load(c, ask);
      if (!c.mesh || c.loaded === 0) continue;
      const u = c.mesh.material.uniforms;
      u.viewport.value.set(size.x, size.y);
      if (c.shDegree > 0) {
        // The eye and the view's direction in the cloud's frame (the project's: its mesh is not moved).
        c.mesh.updateMatrixWorld();
        const inv = new THREE.Matrix4().copy(c.mesh.matrixWorld).invert();
        (u.eye.value as THREE.Vector3).copy(eye).applyMatrix4(inv);
        (u.viewDir.value as THREE.Vector3).set(0, 0, -1).transformDirection(camera.matrixWorld).transformDirection(inv);
      }
      if (c.sorting || !this.moved(c, row, eye)) continue;
      c.sortedRow = row;
      c.sortedEye.copy(eye);
      c.sortedN = c.loaded;
      const w = this.sorter();
      if (w) {
        c.sorting = true;
        w.postMessage({ kind: 'sort', id: c.spec.id, row, n: c.loaded, seq: ++c.seq });
      } else {
        this.sorted(c.spec.id, ++c.seq, c.loaded, sortBackToFront(c.positions, c.loaded, row, new Uint32Array(c.loaded)));
      }
    }
  }

  private moved(c: Cloud, row: DepthRow, eye: THREE.Vector3): boolean {
    if (!c.sortedRow || c.sortedN !== c.loaded) return true;
    const a = c.sortedRow, dot = a[0] * row[0] + a[1] * row[1] + a[2] * row[2];
    if (dot < TURN) return true;
    const far = c.box.isEmpty() ? 1 : Math.max(1e-3, c.box.distanceToPoint(eye) + c.box.getSize(new THREE.Vector3()).length() / 2);
    return eye.distanceTo(c.sortedEye) > 0.01 * far;
  }

  /** The box round every splat cloud drawn (for Zoom to fit), null when none is here. */
  box(): THREE.Box3 | null {
    const b = new THREE.Box3();
    for (const c of this.clouds.values()) if (c.spec.shown && !c.box.isEmpty()) b.union(c.box);
    return b.isEmpty() ? null : b;
  }

  /** The highest degree of harmonics drawn now (0: every cloud in its base colour), for the guide's checks. */
  shDrawn(): number {
    let d = 0;
    for (const c of this.clouds.values()) if (c.mesh?.visible && c.loaded > 0) d = Math.max(d, c.shDegree);
    return d;
  }

  /** How many Gaussians are drawn now, all clouds. */
  drawn(): number {
    let n = 0;
    for (const c of this.clouds.values()) if (c.mesh?.visible) n += c.mesh.geometry.instanceCount;
    return n;
  }

  dispose(): void {
    for (const [id, c] of this.clouds) this.drop(id, c);
    this.worker?.terminate();
    this.worker = null;
  }
}
