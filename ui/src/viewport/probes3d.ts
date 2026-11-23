/**
 * Reflection probes on rooms in the 3D view (docs/plan-render.md, S3), the GPU's part; where they go and when they are
 * baked is probes.ts's. The medium and realistic views only; the drawing is drawn as it always was.
 *
 * - A probe is the room seen from 1.70 m over its floor: six faces drawn with the view's own lights, sun, shadows and
 *   (realistic) sky, a few faces a frame, then blurred by roughness with three's PMREMGenerator (GGX, its mip levels).
 * - All probes in one texture: each a layer of an array texture holding its six faces and their blur levels side by side
 *   (layer 0 is the look outside rooms: the soft room glossy faces reflected before, materialLook.ts). A glossy face's
 *   shader reads that one texture in place of its environment map, whose sampler then goes unused: the probes take no
 *   texture unit more than before, which matters beside S2's lights (16 units in all).
 * - A glossy face reads the probes whose zone it lies in, its reflected ray projected on the room's box (parallax
 *   corrected: the window shows where the window is), blended across borders (probes.ts, blendAt: the same sums).
 * - Glossy: the materials that already reflected (materialLook.ts `reflects`: roughness under 0.3 or metalness over 0.3),
 *   opaque; glass keeps its own look (it would show the room's inside from the street). A project whose materials are
 *   all matt looks exactly as before.
 */

import * as THREE from 'three';
import { BakeQueue, assignSlots, sampleTriangles, type Probe, type ProbeBudget, type ProbeRoom, type Vec3, BLEND } from './probes';

/** A mesh's positions in the world (its own when it sits at the origin). */
function worldPositions(mesh: THREE.Mesh): ArrayLike<number> | null {
  const pos = mesh.geometry.getAttribute('position') as THREE.BufferAttribute | undefined;
  if (!pos) return null;
  mesh.updateWorldMatrix(true, false);
  if (mesh.matrixWorld.equals(IDENTITY)) return pos.array;
  const out = new Float32Array(pos.count * 3), v = new THREE.Vector3();
  for (let i = 0; i < pos.count; i++) { v.fromBufferAttribute(pos, i).applyMatrix4(mesh.matrixWorld); out[3 * i] = v.x; out[3 * i + 1] = v.y; out[3 * i + 2] = v.z; }
  return out;
}
const IDENTITY = new THREE.Matrix4();

/** Whether a plan point lies in one of these plan triangles (x0, y0, x1, y1, x2, y2 each). */
function inTriangles(tris: Float32Array, x: number, y: number): boolean {
  for (let i = 0; i + 5 < tris.length; i += 6) {
    const ax = tris[i], ay = tris[i + 1], bx = tris[i + 2], by = tris[i + 3], cx = tris[i + 4], cy = tris[i + 5];
    const d1 = (x - bx) * (ay - by) - (ax - bx) * (y - by);
    const d2 = (x - cx) * (by - cy) - (bx - cx) * (y - cy);
    const d3 = (x - ax) * (cy - ay) - (cx - ax) * (y - ay);
    const neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
    if (!(neg && pos)) return true;
  }
  return false;
}

/** A room as the probes read it, from its volume's meshes (an `IfcSpace` as the engine's Rooms build it): its box, its floor's outline. */
export function roomOfObject(id: string, object: THREE.Object3D): ProbeRoom | null {
  const lo: Vec3 = [Infinity, Infinity, Infinity], hi: Vec3 = [-Infinity, -Infinity, -Infinity];
  const floor: number[] = [];
  object.traverse((o) => {
    const m = o as THREE.Mesh;
    if (!m.isMesh) return;
    const p = worldPositions(m);
    if (!p) return;
    for (let i = 0; i + 2 < p.length; i += 3) for (let a = 0; a < 3; a++) { lo[a] = Math.min(lo[a], p[i + a]); hi[a] = Math.max(hi[a], p[i + a]); }
    // The faces looking down: the floor's outline in plan (an L-shaped room is not its box).
    const index = m.geometry.getIndex();
    const n = index ? index.count : p.length / 3;
    for (let t = 0; t + 2 < n; t += 3) {
      const a = (index ? index.getX(t) : t) * 3, b = (index ? index.getX(t + 1) : t + 1) * 3, c = (index ? index.getX(t + 2) : t + 2) * 3;
      const nz = (p[b] - p[a]) * (p[c + 1] - p[a + 1]) - (p[b + 1] - p[a + 1]) * (p[c] - p[a]);
      if (nz < -1e-9 && Math.abs(p[a + 2] - p[b + 2]) < 1e-3 && Math.abs(p[a + 2] - p[c + 2]) < 1e-3) floor.push(p[a], p[a + 1], p[b], p[b + 1], p[c], p[c + 1]);
    }
  });
  if (!(hi[0] > lo[0] && hi[1] > lo[1] && hi[2] > lo[2])) return null;
  const tris = Float32Array.from(floor);
  return { id, min: lo, max: hi, inside: tris.length ? (x, y) => inTriangles(tris, x, y) : undefined };
}

/** Points on an object's glossy faces (the materials marked for the probes), a few a square metre. */
export function glossySamples(object: THREE.Object3D, out: Vec3[], step = 0.5): void {
  object.traverseVisible((o) => {
    const m = o as THREE.Mesh;
    if (!m.isMesh || (m as unknown as THREE.InstancedMesh).isInstancedMesh) return;
    const mat = m.material as THREE.Material | THREE.Material[];
    if (!(Array.isArray(mat) ? mat.some((x) => x.userData.probes) : mat.userData.probes)) return;
    const p = worldPositions(m);
    const index = m.geometry.getIndex();
    if (p) sampleTriangles(p, index ? index.array : null, step, out);
  });
}

/** The most probes a frame reads (the realistic view's slots); the shader's arrays are this long. */
export const PROBE_MAX = 10;

/** The atlas of one probe: its faces' size at level 0, three faces across and two down a level, each level half the one before, down to 8 px. */
export function atlasLayout(size: number): { width: number; height: number; levels: number } {
  const levels = Math.max(1, Math.log2(size) - 2);
  return { width: 3 * size, height: 4 * size, levels };
}

/** A level's place in the atlas: its faces' size and the height it starts at. */
export function levelOrigin(size: number, level: number): { face: number; y: number } {
  return { face: size / 2 ** level, y: 4 * size * (1 - 2 ** -level) };
}

/** The direction a face's point looks along (the probes' own face order, the same both ways: +x, −x, +y, −y, +z, −z). */
export function faceDirection(face: number, u: number, v: number): Vec3 {
  const a = u * 2 - 1, b = v * 2 - 1;
  switch (face) {
    case 0: return [1, a, b];
    case 1: return [-1, a, b];
    case 2: return [a, 1, b];
    case 3: return [a, -1, b];
    case 4: return [a, b, 1];
    default: return [a, b, -1];
  }
}

/** The face a direction falls on and its point there (u, v in 0–1): faceDirection's inverse. */
export function directionFace(d: Vec3): { face: number; u: number; v: number } {
  const [x, y, z] = d, ax = Math.abs(x), ay = Math.abs(y), az = Math.abs(z);
  let face: number, a: number, b: number;
  if (ax >= ay && ax >= az) { face = x > 0 ? 0 : 1; a = y / ax; b = z / ax; }
  else if (ay >= az) { face = y > 0 ? 2 : 3; a = x / ay; b = z / ay; }
  else { face = z > 0 ? 4 : 5; a = x / az; b = y / az; }
  return { face, u: a * 0.5 + 0.5, v: b * 0.5 + 0.5 };
}

const FACE_GLSL = /* glsl */ `
vec3 probeFaceDir( float face, vec2 uv ) {
  vec2 p = uv * 2.0 - 1.0;
  if ( face < 0.5 ) return vec3( 1.0, p.x, p.y );
  if ( face < 1.5 ) return vec3( -1.0, p.x, p.y );
  if ( face < 2.5 ) return vec3( p.x, 1.0, p.y );
  if ( face < 3.5 ) return vec3( p.x, -1.0, p.y );
  if ( face < 4.5 ) return vec3( p.x, p.y, 1.0 );
  return vec3( p.x, p.y, -1.0 );
}`;

/** Copies a PMREM cube (its blur levels) into one layer of the atlas. */
function copyMaterial(): THREE.RawShaderMaterial {
  return new THREE.RawShaderMaterial({
    glslVersion: THREE.GLSL3,
    depthTest: false,
    depthWrite: false,
    uniforms: { cube: { value: null }, size: { value: 256 }, levels: { value: 6 }, shift: { value: 0 }, maxLod: { value: 5 } },
    vertexShader: 'in vec3 position; void main() { gl_Position = vec4( position.xy, 0.0, 1.0 ); }',
    fragmentShader: /* glsl */ `
precision highp float;
uniform samplerCube cube;
uniform float size, levels, shift, maxLod;
out vec4 outColor;
${FACE_GLSL}
void main() {
  vec2 f = gl_FragCoord.xy;
  float y0 = 0.0, s = size;
  for ( int l = 0; l < 12; l ++ ) {
    if ( float( l ) >= levels ) break;
    if ( f.y < y0 + 2.0 * s ) {
      if ( f.x >= 3.0 * s ) break;
      float fx = floor( f.x / s ), fy = floor( ( f.y - y0 ) / s );
      vec2 uv = vec2( f.x - fx * s, f.y - y0 - fy * s ) / s;
      outColor = vec4( textureLod( cube, probeFaceDir( fx + 3.0 * fy, uv ), min( float( l ) + shift, maxLod ) ).rgb, 1.0 );
      return;
    }
    y0 += 2.0 * s;
    s *= 0.5;
  }
  outColor = vec4( 0.0 );
}`,
  });
}

/** What a glossy face's shader reads its environment with: the probes' atlas, their places and zones. */
const PARS = /* glsl */ `
#ifdef USE_ENVMAP
uniform highp sampler2DArray probeMaps;
uniform vec4 probeAtlas; // size, 1/width, 1/height, -
uniform vec3 probeLod; // the PMREM's last level, the atlas's shift under it, the atlas's last level
uniform int probeCount;
uniform float probeOutside; // the look outside rooms: 1; none while a probe is captured (nor any probe then)
uniform vec4 probeA[ ${PROBE_MAX} ]; // position, shown (1) or not yet baked (0)
uniform vec4 probeB[ ${PROBE_MAX} ]; // the room's box: min, layer
uniform vec4 probeC[ ${PROBE_MAX} ]; // the room's box: max
uniform vec4 probeD[ ${PROBE_MAX} ]; // the zone in plan: x0, y0, x1, y1
varying vec3 vProbeWorld;
${FACE_GLSL}
vec3 probeLevel( float layer, vec3 d, float level ) {
  vec3 a = abs( d );
  float face; vec2 uv;
  if ( a.x >= a.y && a.x >= a.z ) { face = d.x > 0.0 ? 0.0 : 1.0; uv = d.yz / a.x; }
  else if ( a.y >= a.z ) { face = d.y > 0.0 ? 2.0 : 3.0; uv = d.xz / a.y; }
  else { face = d.z > 0.0 ? 4.0 : 5.0; uv = d.xy / a.z; }
  uv = uv * 0.5 + 0.5;
  float s = probeAtlas.x * exp2( - level );
  float y0 = 4.0 * probeAtlas.x * ( 1.0 - exp2( - level ) );
  vec2 px = clamp( uv * s, vec2( 0.5 ), vec2( s - 0.5 ) ) + vec2( mod( face, 3.0 ) * s, y0 + floor( face / 3.0 ) * s );
  return textureLod( probeMaps, vec3( px * probeAtlas.yz, layer ), 0.0 ).rgb;
}
vec3 probeSample( float layer, vec3 d, float roughness ) {
  float r = clamp( roughness, 0.0, 1.0 );
  float lod = clamp( probeLod.x * r * ( 2.0 - r ) - probeLod.y, 0.0, probeLod.z );
  float l0 = floor( lod ), l1 = min( l0 + 1.0, probeLod.z );
  return mix( probeLevel( layer, d, l0 ), probeLevel( layer, d, l1 ), lod - l0 );
}
float probeZone( vec2 p, vec4 z ) {
  vec2 d = max( z.xy - p, p - z.zw );
  return ( d.x > 0.0 || d.y > 0.0 ) ? length( max( d, 0.0 ) ) : max( d.x, d.y );
}
vec3 probeProject( vec3 p, vec3 r, vec3 lo, vec3 hi, vec3 c ) {
  p = clamp( p, lo + 0.01, hi - 0.01 );
  vec3 t = max( ( hi - p ) / r, ( lo - p ) / r );
  return p + r * min( min( t.x, t.y ), t.z ) - c;
}
vec3 probeRadiance( vec3 dir, float roughness ) {
  vec3 p = vProbeWorld, sum = vec3( 0.0 );
  float total = 0.0;
  for ( int i = 0; i < ${PROBE_MAX}; i ++ ) {
    if ( i >= probeCount ) break;
    if ( probeA[ i ].w < 0.5 || p.z < probeB[ i ].z - 0.35 || p.z > probeC[ i ].z + 0.05 ) continue;
    float w = clamp( 0.5 - probeZone( p.xy, probeD[ i ] ) / ${BLEND.toFixed(3)}, 0.0, 1.0 );
    if ( w <= 0.0 ) continue;
    sum += w * probeSample( probeB[ i ].w, probeProject( p, dir, probeB[ i ].xyz, probeC[ i ].xyz, probeA[ i ].xyz ), roughness );
    total += w;
  }
  float cover = min( 1.0, 2.0 * total );
  vec3 inside = total > 0.0 ? sum / total : vec3( 0.0 );
  if ( cover >= 1.0 || probeOutside <= 0.0 ) return cover * inside;
  return cover * inside + ( 1.0 - cover ) * probeOutside * probeSample( 0.0, dir, roughness ) * envMapIntensity;
}
vec3 getIBLIrradiance( const in vec3 normal ) {
  #ifdef ENVMAP_TYPE_PMREM
    return PI * probeRadiance( transformDirectionByInverseViewMatrix( normal, viewMatrix ), 1.0 );
  #else
    return vec3( 0.0 );
  #endif
}
vec3 getIBLRadiance( const in vec3 viewDir, const in vec3 normal, const in float roughness ) {
  #ifdef ENVMAP_TYPE_PMREM
    vec3 reflectVec = normalize( mix( reflect( - viewDir, normal ), normal, pow4( roughness ) ) );
    return probeRadiance( transformDirectionByInverseViewMatrix( reflectVec, viewMatrix ), roughness );
  #else
    return vec3( 0.0 );
  #endif
}
#ifdef USE_RETROREFLECTION
vec3 getIBLRetroRadiance( const in vec3 viewDir, const in vec3 normal, const in float roughness ) {
  vec3 retroVec = normalize( mix( viewDir, normal, pow4( roughness ) ) );
  return probeRadiance( transformDirectionByInverseViewMatrix( retroVec, viewMatrix ), roughness );
}
#endif
#ifdef USE_ANISOTROPY
vec3 getIBLAnisotropyRadiance( const in vec3 viewDir, const in vec3 normal, const in float roughness, const in vec3 bitangent, const in float anisotropy ) {
  vec3 bentNormal = cross( bitangent, viewDir );
  bentNormal = normalize( cross( bentNormal, bitangent ) );
  bentNormal = normalize( mix( bentNormal, normal, pow2( pow2( 1.0 - anisotropy * ( 1.0 - roughness ) ) ) ) );
  return getIBLRadiance( viewDir, bentNormal, roughness );
}
#ifdef USE_RETROREFLECTION
vec3 getIBLAnisotropyRetroRadiance( const in vec3 viewDir, const in vec3 normal, const in float roughness, const in vec3 bitangent, const in float anisotropy ) {
  vec3 bentNormal = cross( bitangent, viewDir );
  bentNormal = normalize( cross( bentNormal, bitangent ) );
  bentNormal = normalize( mix( bentNormal, normal, pow2( pow2( 1.0 - anisotropy * ( 1.0 - roughness ) ) ) ) );
  return getIBLRetroRadiance( viewDir, bentNormal, roughness );
}
#endif
#endif
#endif
`;

/** A glossy material's shader reading the probes: its world position passed on, its environment's functions replaced. */
export function patchShader(vertex: string, fragment: string): { vertex: string; fragment: string } {
  const v = vertex
    .replace('#include <common>', '#include <common>\n#ifdef USE_ENVMAP\nvarying vec3 vProbeWorld;\n#endif')
    .replace('#include <worldpos_vertex>', '#include <worldpos_vertex>\n#ifdef USE_ENVMAP\nvProbeWorld = worldPosition.xyz;\n#endif');
  const f = fragment.replace('#include <envmap_physical_pars_fragment>', PARS);
  return { vertex: v, fragment: f };
}

const CACHE_KEY = `lapin-probes-${PROBE_MAX}`;
const plainCompile = THREE.Material.prototype.onBeforeCompile;
const plainKey = THREE.Material.prototype.customProgramCacheKey;

/** What a capture hides and shows round a probe (lines, room volumes, the grid; the realistic sky at the probe): the restore after. */
export type CapturePrepare = (at: Vec3) => () => void;

/** The probes' look in the 3D view now, for its corner and the guide's checks. */
export interface ProbeStats { probes: number; slots: number; baked: number; waiting: number; size: number }

/**
 * The rooms' reflection probes of one 3D view: the atlas, the capture, the glossy materials patched to read them.
 * `configure` by the profile, `place` when the rooms or the glossy faces change, `stale` when the light does, `step`
 * each frame before the view is drawn (true while there is more to bake).
 */
export class ReflectionProbes {
  readonly uniforms = {
    probeMaps: { value: null as THREE.Texture | null },
    probeAtlas: { value: new THREE.Vector4(256, 1, 1, 0) },
    probeLod: { value: new THREE.Vector3(5, 0, 5) },
    probeCount: { value: 0 },
    probeOutside: { value: 1 },
    probeA: { value: Array.from({ length: PROBE_MAX }, () => new THREE.Vector4()) },
    probeB: { value: Array.from({ length: PROBE_MAX }, () => new THREE.Vector4()) },
    probeC: { value: Array.from({ length: PROBE_MAX }, () => new THREE.Vector4()) },
    probeD: { value: Array.from({ length: PROBE_MAX }, () => new THREE.Vector4()) },
  };
  private budget: ProbeBudget | null = null;
  private probes: Probe[] = [];
  private byKey = new Map<string, Probe>();
  private slots: (string | null)[] = [];
  private queue = new BakeQueue();
  private atlas: THREE.WebGLArrayRenderTarget | null = null;
  private cube: THREE.WebGLCubeRenderTarget | null = null;
  private cubeCamera: THREE.CubeCamera | null = null;
  private pmrem: THREE.PMREMGenerator | null = null;
  private pmremTarget: THREE.WebGLCubeRenderTarget | null = null;
  private readonly copy = copyMaterial();
  private readonly quad: THREE.Mesh;
  private readonly quadCamera = new THREE.OrthographicCamera(-1, 1, 1, -1, 0, 1);
  private readonly patched = new Set<THREE.Material>();
  private fallbackDone = false;
  private failed = false;

  constructor(private readonly renderer: THREE.WebGLRenderer, private readonly scene: THREE.Scene, private readonly fallback: THREE.Texture | null) {
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(new Float32Array([-1, -1, 0, 3, -1, 0, -1, 3, 0]), 3));
    this.quad = new THREE.Mesh(g, this.copy);
    this.quad.frustumCulled = false;
  }

  /** Whether the probes are on (medium and realistic, a renderer that can draw them). */
  get on(): boolean { return this.budget != null && !this.failed; }

  /** The budget by the profile (null: off, the drawing); the atlas made again when its size or slots change. */
  configure(budget: ProbeBudget | null): void {
    const same = budget && this.budget && budget.size === this.budget.size && budget.slots === this.budget.slots && budget.facesPerFrame === this.budget.facesPerFrame;
    if (same) return;
    this.budget = budget;
    this.atlas?.dispose(); this.atlas = null;
    this.cube?.dispose(); this.cube = null;
    this.cubeCamera = null;
    this.fallbackDone = false;
    this.slots = [];
    this.queue = new BakeQueue(budget?.facesPerFrame ?? 2);
    this.uniforms.probeCount.value = 0;
    if (budget && !this.failed) {
      const { width, height } = atlasLayout(budget.size);
      this.atlas = new THREE.WebGLArrayRenderTarget(width, height, budget.slots + 1, { type: THREE.HalfFloatType, depthBuffer: false, generateMipmaps: false, minFilter: THREE.LinearFilter, magFilter: THREE.LinearFilter });
      this.cube = new THREE.WebGLCubeRenderTarget(budget.size, { type: THREE.HalfFloatType, generateMipmaps: false, minFilter: THREE.LinearFilter });
      this.cubeCamera = new THREE.CubeCamera(0.05, 4500, this.cube);
      this.pmrem ??= new THREE.PMREMGenerator(this.renderer);
      this.uniforms.probeMaps.value = this.atlas.texture;
      this.uniforms.probeAtlas.value.set(budget.size, 1 / width, 1 / height, 0);
    }
    this.patchAll(this.scene);
  }

  /** The probes placed now (probes.ts placeProbes); those kept keep their pictures until the light changes. */
  place(probes: Probe[]): void {
    const before = this.byKey;
    this.probes = probes;
    this.byKey = new Map(probes.map((p) => [p.key, p]));
    // A probe whose room changed shape is a new one.
    for (let i = 0; i < this.slots.length; i++) {
      const k = this.slots[i];
      if (k == null) continue;
      const a = before.get(k), b = this.byKey.get(k);
      if (!b || !a || a.min.join() !== b.min.join() || a.max.join() !== b.max.join() || a.position.join() !== b.position.join()) { this.slots[i] = null; this.queue.set(i, null); }
    }
    this.queue.stale();
  }

  /** The light changed (the sun, a lamp, the night, the model): every picture baked again, the old ones shown meanwhile. */
  stale(): void { this.queue.stale(); }

  /** The glossy materials under `root` reading the probes (or their plain look again, when off); new ones adopted the same way. */
  patchAll(root: THREE.Object3D): void {
    const on = this.on;
    root.traverse((o) => {
      const mat = (o as THREE.Mesh).material as THREE.Material | THREE.Material[] | undefined;
      for (const m of Array.isArray(mat) ? mat : mat ? [mat] : []) if (m.userData.probes) this.patch(m, on);
    });
  }

  private patch(m: THREE.Material, on: boolean): void {
    if (on === this.patched.has(m)) return;
    if (on) {
      const uniforms = this.uniforms;
      m.onBeforeCompile = (shader) => {
        Object.assign(shader.uniforms, uniforms);
        const p = patchShader(shader.vertexShader, shader.fragmentShader);
        shader.vertexShader = p.vertex;
        shader.fragmentShader = p.fragment;
      };
      m.customProgramCacheKey = () => CACHE_KEY;
      this.patched.add(m);
      m.addEventListener('dispose', this.forget);
    } else {
      m.onBeforeCompile = plainCompile;
      m.customProgramCacheKey = plainKey;
      this.patched.delete(m);
      m.removeEventListener('dispose', this.forget);
    }
    m.needsUpdate = true;
  }

  private forget = (e: { target: THREE.Material }) => { this.patched.delete(e.target); };

  stats(): ProbeStats {
    const c = this.queue.counts();
    return { probes: this.probes.length, slots: this.slots.filter((k) => k != null).length, baked: c.baked, waiting: c.waiting, size: this.budget?.size ?? 0 };
  }

  /**
   * Before a frame: the slots given to the probes nearest the eye, a few faces baked, the shader's uniforms set. True
   * while more is to bake (the view asks for another frame).
   */
  step(eye: Vec3, prepare: CapturePrepare): boolean {
    const budget = this.budget;
    if (!budget || this.failed || !this.atlas || !this.cube || !this.cubeCamera || !this.pmrem) return false;
    try {
      if (!this.fallbackDone) {
        if (this.fallback) this.toLayer(this.fallback as THREE.CubeTexture, 0);
        this.fallbackDone = true;
      }
      const slots = assignSlots(this.slots, this.probes, eye, budget.slots);
      for (let i = 0; i < slots.length; i++) {
        if (slots[i] === (this.slots[i] ?? null)) continue;
        const p = slots[i] != null ? this.byKey.get(slots[i]!) : undefined;
        this.queue.set(i, p ? p.position : null);
      }
      this.slots = slots;
      const step = this.queue.next(eye);
      if (step) {
        const p = this.byKey.get(this.slots[step.slot] ?? '');
        if (p) this.bake(p, step.faces, step.finish ? step.slot + 1 : -1, prepare);
      }
      this.upload();
      return this.queue.pending;
    } catch (e) {
      // A chip that cannot draw them (no half-float targets): the plain look kept.
      console.warn('Reflection probes are off:', e);
      this.failed = true;
      this.patchAll(this.scene);
      return false;
    }
  }

  private bake(p: Probe, faces: number[], layer: number, prepare: CapturePrepare): void {
    const r = this.renderer, cam = this.cubeCamera!, cube = this.cube!;
    // The faces turned for the renderer first, then placed: a face's camera draws from its world matrix.
    if (cam.coordinateSystem !== r.coordinateSystem) { cam.coordinateSystem = r.coordinateSystem; cam.updateCoordinateSystem(); }
    cam.position.set(p.position[0], p.position[1], p.position[2]);
    cam.updateMatrixWorld(true);
    const restore = prepare(p.position);
    const target = r.getRenderTarget(), face0 = r.getActiveCubeFace(), level0 = r.getActiveMipmapLevel();
    const shadows = r.shadowMap.autoUpdate;
    // The shadows are the frame's own (drawn for the view): not drawn again for each face. Glossy faces in the capture
    // reflect nothing: a floor reading its own probe's last picture (the day's, at night) would bake it in again and again,
    // and the soft room outside would light a room at night as if by day.
    r.shadowMap.autoUpdate = false;
    const count = this.uniforms.probeCount.value;
    this.uniforms.probeOutside.value = 0;
    this.uniforms.probeCount.value = 0;
    try {
      for (const f of faces) {
        r.setRenderTarget(cube, f);
        r.render(this.scene, cam.children[f] as THREE.Camera);
      }
    } finally {
      r.shadowMap.autoUpdate = shadows;
      this.uniforms.probeOutside.value = 1;
      this.uniforms.probeCount.value = count;
      r.setRenderTarget(target, face0, level0);
      restore();
    }
    if (layer >= 0) {
      this.pmremTarget = this.pmrem!.fromCubemap(cube.texture, this.pmremTarget);
      this.toLayer(this.pmremTarget.texture, layer);
    }
  }

  /** A blurred cube (a PMREM: its levels the roughness) laid into a layer of the atlas. */
  private toLayer(cube: THREE.Texture, layer: number): void {
    const budget = this.budget!, r = this.renderer;
    const pmremSize = (cube.image as { width?: number }[] | undefined)?.[0]?.width ?? 256;
    const maxLod = Math.max(0, (cube.mipmaps?.length ?? 1) - 1);
    const shift = Math.max(0, Math.log2(pmremSize / budget.size));
    const { levels } = atlasLayout(budget.size);
    const u = this.copy.uniforms;
    u.cube.value = cube; u.size.value = budget.size; u.levels.value = Math.min(levels, maxLod - shift + 1); u.shift.value = shift; u.maxLod.value = maxLod;
    this.uniforms.probeLod.value.set(maxLod, shift, Math.min(levels, maxLod - shift + 1) - 1);
    const target = r.getRenderTarget(), face0 = r.getActiveCubeFace(), level0 = r.getActiveMipmapLevel();
    const clipping = r.clippingPlanes;
    r.clippingPlanes = [];
    try {
      r.setRenderTarget(this.atlas!, layer);
      r.render(this.quad, this.quadCamera);
    } finally {
      r.clippingPlanes = clipping;
      r.setRenderTarget(target, face0, level0);
    }
  }

  private upload(): void {
    const u = this.uniforms;
    let n = 0;
    for (let i = 0; i < this.slots.length && n < PROBE_MAX; i++) {
      const p = this.slots[i] != null ? this.byKey.get(this.slots[i]!) : undefined;
      if (!p) continue;
      u.probeA.value[n].set(p.position[0], p.position[1], p.position[2], this.queue.baked(i) ? 1 : 0);
      u.probeB.value[n].set(p.min[0], p.min[1], p.min[2], i + 1);
      u.probeC.value[n].set(p.max[0], p.max[1], p.max[2], 0);
      u.probeD.value[n].set(p.zone[0], p.zone[1], p.zone[2], p.zone[3]);
      n++;
    }
    u.probeCount.value = n;
  }

  dispose(): void {
    this.budget = null;
    this.patchAll(this.scene);
    this.atlas?.dispose();
    this.cube?.dispose();
    this.pmremTarget?.dispose();
    this.pmrem?.dispose();
    this.copy.dispose();
    this.quad.geometry.dispose();
  }
}
