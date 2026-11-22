/**
 * The 3D view's path tracer (docs/plan-render.md, S4): three-gpu-pathtracer (Garrett Johnson, MIT) over three-mesh-bvh
 * (MIT) on the view's own WebGL renderer, fed the scene pathTrace.ts builds from what the view draws, the view's sky and
 * sun, its exposure and white balance.
 *
 * - The sky: the view's own (sky.ts) drawn into a cube and laid out as the tracer's equirectangular map, its light
 *   importance-sampled; the sun a disc in it, of the live sun's colour and light (its irradiance the view's DirectionalLight
 *   gives), half a degree across and a little more (a real sun's 0.53°, so shadows have the soft edge they have outdoors).
 *   The visible sky (camera rays) is the view's backdrop sky, the light (rays that bounce) its light-giving sky.
 * - The picture: the tracer's linear light, denoised or not (denoise.ts, guided by albedo, normals and depth rendered from
 *   the same camera), white-balanced, exposed and tone-mapped as the view (AgX for the realistic profile, Neutral for the
 *   medium) and written in sRGB: to the screen, or to an image of any size up to the GPU's (still).
 * - Never blocking: one tile of a sample per frame (the frame stays short), restarted by a camera move.
 *
 * Built lazily (its shaders and three-gpu-pathtracer load the first time Render is pressed).
 */

import * as THREE from 'three';
import { PhysicalCamera, WebGLPathTracer } from 'three-gpu-pathtracer';
import { FullScreenQuad } from 'three/examples/jsm/postprocessing/Pass.js';
import { Sky } from './sky';
import { ALBEDO_FLOOR, ATROUS_FRAGMENT, DEMODULATE_FRAGMENT, NOISE_FRAGMENT, paramsFor, SPREAD, STEPS } from './denoise';
import { addSun } from './pathTrace';

const VERTEX = 'varying vec2 vUv; void main() { vUv = uv; gl_Position = vec4( position.xy, 0.0, 1.0 ); }';

/** The sky as the tracer takes it: the view's sky uniforms (its light-giving one and its backdrop), the sun, how strong. */
export interface TraceSky {
  /** Uniform values of the sky that lights (Realism's environment sky). */
  light: Record<string, unknown>;
  /** Uniform values of the sky seen behind the model (Realism's backdrop), or null: a plain colour behind (backdrop). */
  backdrop: Record<string, unknown> | null;
  backdropColour: THREE.Color;
  /** The sky light's strength (the view's scene.environmentIntensity). */
  intensity: number;
  /** Towards the sun (z up) and its light (the DirectionalLight's colour × intensity), or none (night, no sun). */
  sun: { dir: THREE.Vector3; light: THREE.Color } | null;
}

export interface TraceLook {
  exposure: number;
  gains: readonly [number, number, number];
  toneMapping: THREE.ToneMapping;
}

/** Copies a sky's uniform values (THREE.Vector3 copied, numbers set). */
function setUniforms(sky: Sky, values: Record<string, unknown>): void {
  const u = sky.material.uniforms;
  for (const [k, v] of Object.entries(values)) {
    if (!u[k]) continue;
    const cur = u[k].value as { copy?: (x: unknown) => void };
    if (cur && typeof cur.copy === 'function' && v && typeof v === 'object') cur.copy(v);
    else u[k].value = v;
  }
  u.sunDisc.value = 0;
}

export class PathTraceView {
  readonly pt: WebGLPathTracer;
  readonly camera = new PhysicalCamera(45, 1, 0.1, 5000);
  readonly ortho = new THREE.OrthographicCamera(-1, 1, 1, -1, -5000, 5000);
  private scene: THREE.Scene | null = null;
  private envKey = '';
  private env: THREE.DataTexture | null = null;
  private backdrop: THREE.DataTexture | null = null;
  private cubeTarget = new THREE.WebGLCubeRenderTarget(256, { type: THREE.HalfFloatType });
  private cubeCamera = new THREE.CubeCamera(0.1, 1000, this.cubeTarget);
  private skyScene = new THREE.Scene();
  private sky = new Sky();
  private unwrap = new FullScreenQuad(new THREE.ShaderMaterial({
    uniforms: { cube: { value: null } },
    vertexShader: VERTEX,
    fragmentShader: `uniform samplerCube cube; varying vec2 vUv;
      void main() { float th = ( vUv.x - 0.5 ) * 6.283185307; float ph = ( 1.0 - vUv.y ) * 3.141592654;
        vec3 d = vec3( sin( ph ) * cos( th ), cos( ph ), sin( ph ) * sin( th ) );
        gl_FragColor = vec4( textureCube( cube, d ).rgb, 1.0 ); }`,
    depthTest: false, depthWrite: false,
  }));
  // The guides of the denoiser, and its passes.
  private albedo = new THREE.WebGLRenderTarget(1, 1, { type: THREE.HalfFloatType });
  private normals = new THREE.WebGLRenderTarget(1, 1, { type: THREE.HalfFloatType });
  private depth = new THREE.WebGLRenderTarget(1, 1, { type: THREE.FloatType });
  private guide = new THREE.WebGLRenderTarget(1, 1, { type: THREE.FloatType, minFilter: THREE.NearestFilter, magFilter: THREE.NearestFilter });
  // Read by the output pass, which may draw them larger (the live view at half a Retina screen's pixels): smooth.
  private ping = new THREE.WebGLRenderTarget(1, 1, { type: THREE.HalfFloatType, minFilter: THREE.LinearFilter, magFilter: THREE.LinearFilter });
  private pong = new THREE.WebGLRenderTarget(1, 1, { type: THREE.HalfFloatType, minFilter: THREE.LinearFilter, magFilter: THREE.LinearFilter });
  private guidesFor = -1;
  private resets = 0;
  private albedoMats = new Map<THREE.Material, THREE.Material>();
  private normalMats = new Map<THREE.Material, THREE.Material>();
  private depthMat = new THREE.ShaderMaterial({
    vertexShader: 'varying float vZ; void main() { vec4 mv = modelViewMatrix * vec4( position, 1.0 ); vZ = -mv.z; gl_Position = projectionMatrix * mv; }',
    fragmentShader: 'varying float vZ; void main() { gl_FragColor = vec4( vZ, vZ, vZ, 1.0 ); }',
    side: THREE.DoubleSide,
  });
  private pack = new FullScreenQuad(new THREE.ShaderMaterial({
    uniforms: { tNormal: { value: null }, tDepth: { value: null } },
    vertexShader: VERTEX,
    fragmentShader: `uniform sampler2D tNormal; uniform sampler2D tDepth; varying vec2 vUv;
      void main() { vec4 n = texture2D( tNormal, vUv ); float z = texture2D( tDepth, vUv ).r;
        gl_FragColor = n.a > 0.0 && z > 0.0 ? vec4( normalize( n.xyz * 2.0 - 1.0 ), z ) : vec4( 0.0 ); }`,
    depthTest: false, depthWrite: false,
  }));
  private demod = new FullScreenQuad(new THREE.ShaderMaterial({
    uniforms: { tColor: { value: null }, tAlbedo: { value: null }, floorAlbedo: { value: ALBEDO_FLOOR }, texel: { value: new THREE.Vector2() }, spread: { value: SPREAD } },
    vertexShader: VERTEX, fragmentShader: DEMODULATE_FRAGMENT, depthTest: false, depthWrite: false,
  }));
  private noise = new FullScreenQuad(new THREE.ShaderMaterial({
    uniforms: { tIllum: { value: null }, texel: { value: new THREE.Vector2() } },
    vertexShader: VERTEX, fragmentShader: NOISE_FRAGMENT, depthTest: false, depthWrite: false,
  }));
  private atrous = new FullScreenQuad(new THREE.ShaderMaterial({
    uniforms: { tIllum: { value: null }, tGuide: { value: null }, texel: { value: new THREE.Vector2() }, stride: { value: 1 }, sigmaN: { value: 48 }, sigmaZ: { value: 0.01 }, sigmaL: { value: 2 }, floorL: { value: 0.04 } },
    vertexShader: VERTEX, fragmentShader: ATROUS_FRAGMENT, depthTest: false, depthWrite: false,
  }));
  private out = new FullScreenQuad(this.outputMaterial(THREE.AgXToneMapping));
  private outTone: THREE.ToneMapping = THREE.AgXToneMapping;

  constructor(private readonly renderer: THREE.WebGLRenderer) {
    // The class warns once that WebGPU is its future: Lapin draws with WebGL.
    const warn = console.warn;
    console.warn = () => undefined;
    try { this.pt = new WebGLPathTracer(renderer); } finally { console.warn = warn; }
    const pt = this.pt;
    pt.renderToCanvas = false;
    pt.rasterizeScene = false;
    pt.renderDelay = 0;
    pt.minSamples = 0;
    pt.fadeDuration = 0;
    pt.dynamicLowRes = false;
    pt.tiles.set(2, 2);
    pt.renderScale = this.liveScale();
    pt.bounces = 5;
    pt.transmissiveBounces = 8;
    pt.filterGlossyFactor = 0.5;
    pt.multipleImportanceSampling = true;
    pt.textureSize.set(1024, 1024);
    this.camera.up.set(0, 0, 1);
    this.ortho.up.set(0, 0, 1);
    this.sky.scale.setScalar(100);
    this.skyScene.add(this.sky);
  }

  /** The picture's output pass: the light (or the denoised light times its albedo), balanced, exposed, tone-mapped, in sRGB. */
  private outputMaterial(tone: THREE.ToneMapping): THREE.ShaderMaterial {
    const fn = tone === THREE.NeutralToneMapping ? 'NeutralToneMapping' : tone === THREE.NoToneMapping ? 'LinearToneMapping' : 'AgXToneMapping';
    return new THREE.ShaderMaterial({
      uniforms: { tColor: { value: null }, tAlbedo: { value: null }, remodulate: { value: 0 }, floorAlbedo: { value: ALBEDO_FLOOR }, gains: { value: new THREE.Vector3(1, 1, 1) }, uExposure: { value: 1 } },
      vertexShader: VERTEX,
      fragmentShader: `#define toneMappingExposure uExposure
        #include <tonemapping_pars_fragment>
        uniform sampler2D tColor; uniform sampler2D tAlbedo; uniform float remodulate; uniform float floorAlbedo; uniform vec3 gains;
        varying vec2 vUv;
        vec3 lapinSrgb( vec3 c ) { return mix( c * 12.92, pow( c, vec3( 0.41666 ) ) * 1.055 - 0.055, step( 0.0031308, c ) ); }
        void main() {
          vec3 c = texture2D( tColor, vUv ).rgb;
          if ( remodulate > 0.5 ) c *= max( texture2D( tAlbedo, vUv ).rgb, vec3( floorAlbedo ) );
          c = max( c * gains, vec3( 0.0 ) );
          c = ${fn}( c );
          gl_FragColor = vec4( lapinSrgb( clamp( c, 0.0, 1.0 ) ), 1.0 );
        }`,
      depthTest: false, depthWrite: false, toneMapped: false,
    });
  }

  get samples(): number { return this.pt.samples; }
  get compiling(): boolean { return Boolean((this.pt as unknown as { isCompiling?: boolean }).isCompiling); }

  /** The model's scene, made by buildTraceScene; its BVH built here (ms). */
  setScene(scene: THREE.Scene, camera: THREE.PerspectiveCamera | THREE.OrthographicCamera): number {
    const t0 = performance.now();
    this.scene = scene;
    this.syncCamera(camera);
    this.skyOn = null;
    this.applySky();
    this.withoutClipping(() => this.pt.setScene(scene, this.active));
    this.guidesFor = -1;
    return performance.now() - t0;
  }

  private active: THREE.PerspectiveCamera | THREE.OrthographicCamera = this.camera;
  private skyOn: THREE.Scene | null = null;
  private dofKey = '';
  private skyIn: TraceSky | null = null;

  /** The sky and sun (laid out again only when they change). */
  setSky(sky: TraceSky): void {
    this.skyIn = sky;
    if (this.scene) this.applySky();
  }

  private applySky(): void {
    const sky = this.skyIn;
    const scene = this.scene;
    if (!sky || !scene) return;
    const key = JSON.stringify([sky.light, sky.backdrop, sky.backdropColour.getHex(), sky.sun ? [sky.sun.dir.toArray(), sky.sun.light.toArray()] : null, sky.intensity.toFixed(5)]);
    // The same sky on the same scene: nothing to do (sending it again would start the picture over).
    if (key === this.envKey && this.skyOn === scene) return;
    this.skyOn = scene;
    if (key !== this.envKey) {
      this.envKey = key;
      this.env?.dispose();
      this.backdrop?.dispose();
      const sunLight = sky.sun ? sky.sun.light.clone().multiplyScalar(1 / Math.max(1e-6, sky.intensity)) : null;
      this.env = this.equirect(sky.light, sky.sun && sunLight ? { dir: sky.sun.dir, light: sunLight } : null);
      this.backdrop = sky.backdrop ? this.equirect(sky.backdrop, sky.sun ? { dir: sky.sun.dir, light: sky.sun.light } : null) : null;
    }
    scene.environment = this.env;
    scene.environmentIntensity = sky.intensity;
    scene.background = this.backdrop ?? sky.backdropColour;
    scene.backgroundIntensity = 1;
    this.pt.updateEnvironment();
  }

  /** A sky drawn into the cube and laid out (2048 × 1024 floats), the sun's disc added. */
  private equirect(values: Record<string, unknown>, sun: { dir: THREE.Vector3; light: THREE.Color } | null): THREE.DataTexture {
    const W = 2048, H = 1024;
    setUniforms(this.sky, values);
    const r = this.renderer;
    const keepTarget = r.getRenderTarget(), keepTone = r.toneMapping;
    r.toneMapping = THREE.NoToneMapping;
    this.withoutClipping(() => this.cubeCamera.update(r, this.skyScene));
    const target = new THREE.WebGLRenderTarget(W, H, { type: THREE.FloatType });
    (this.unwrap.material as THREE.ShaderMaterial).uniforms.cube.value = this.cubeTarget.texture;
    r.setRenderTarget(target);
    this.unwrap.render(r);
    const data = new Float32Array(W * H * 4);
    r.readRenderTargetPixels(target, 0, 0, W, H, data);
    r.setRenderTarget(keepTarget);
    r.toneMapping = keepTone;
    target.dispose();
    if (sun) addSun(data, W, H, sun.dir, sun.light);
    const tex = new THREE.DataTexture(data, W, H, THREE.RGBAFormat, THREE.FloatType);
    tex.mapping = THREE.EquirectangularReflectionMapping;
    tex.wrapS = THREE.RepeatWrapping;
    tex.wrapT = THREE.ClampToEdgeWrapping;
    tex.magFilter = THREE.LinearFilter;
    tex.minFilter = THREE.LinearFilter;
    tex.needsUpdate = true;
    return tex;
  }

  /** The view's camera copied; the depth of field set (fStop null: none). Returns whether it moved. */
  syncCamera(view: THREE.PerspectiveCamera | THREE.OrthographicCamera, dof?: { fStop: number; focus: number } | null, aspect?: number): boolean {
    const was = this.active;
    const before = this.active.matrixWorld.clone(), beforeP = this.active.projectionMatrix.clone();
    if ((view as THREE.OrthographicCamera).isOrthographicCamera) {
      const o = view as THREE.OrthographicCamera;
      this.ortho.copy(o);
      if (aspect) { const h = (o.top - o.bottom) / 2, mid = (o.left + o.right) / 2; this.ortho.left = mid - h * aspect; this.ortho.right = mid + h * aspect; }
      this.ortho.updateProjectionMatrix();
      this.ortho.updateMatrixWorld(true);
      this.active = this.ortho;
    } else {
      const p = view as THREE.PerspectiveCamera;
      const c = this.camera;
      c.position.copy(p.position); c.quaternion.copy(p.quaternion); c.up.copy(p.up);
      c.fov = p.fov; c.near = p.near; c.far = p.far; c.zoom = p.zoom;
      c.aspect = aspect ?? p.aspect;
      c.fStop = dof ? Math.max(0.5, dof.fStop) : 1e9;
      c.focusDistance = dof ? Math.max(0.05, dof.focus) : 25;
      c.apertureBlades = dof ? 6 : 0;
      c.updateProjectionMatrix();
      c.updateMatrixWorld(true);
      this.active = c;
    }
    const dofKey = `${(this.active as PhysicalCamera).fStop ?? ''}:${(this.active as PhysicalCamera).focusDistance ?? ''}`;
    const moved = this.active !== was || !before.equals(this.active.matrixWorld) || !beforeP.equals(this.active.projectionMatrix) || dofKey !== this.dofKey;
    this.dofKey = dofKey;
    // Sending the camera starts the picture over: only when it changed.
    if (moved) this.pt.setCamera(this.active);
    return moved;
  }

  /** The distance to what the ray from the camera through a point of the screen (NDC) meets, or null. */
  distanceAt(ndc: THREE.Vector2): number | null {
    const bvh = (this.pt as unknown as { _generator: { bvh: { raycastFirst(r: THREE.Ray, side: THREE.Side): { distance: number } | null } | null } })._generator.bvh;
    if (!bvh || !(bvh as { raycastFirst?: unknown }).raycastFirst) return null;
    const ray = new THREE.Raycaster();
    ray.setFromCamera(ndc, this.active);
    const hit = bvh.raycastFirst(ray.ray, THREE.DoubleSide);
    if (!hit) return null;
    // Along the camera's axis (the focus plane is square to it).
    const forward = new THREE.Vector3(0, 0, -1).applyQuaternion(this.active.quaternion);
    return hit.distance * ray.ray.direction.dot(forward);
  }

  /** The lights changed (their objects in the scene): sent to the tracer again. */
  updateLights(): void { this.pt.updateLights(); }

  reset(): void { this.pt.reset(); this.resets++; }

  /** One step of the tracer (a tile of a sample). */
  step(): void {
    this.withoutClipping(() => this.pt.renderSample());
  }

  /** The size the tracer renders at, and whether it follows the canvas (the live view) or is fixed (a still image). */
  setSize(size: { width: number; height: number } | null): void {
    const pt = this.pt as unknown as { synchronizeRenderSize: boolean; _pathTracer: { setSize(w: number, h: number): void } };
    if (!size) { pt.synchronizeRenderSize = true; this.pt.renderScale = this.liveScale(); return; }
    pt.synchronizeRenderSize = false;
    pt._pathTracer.setSize(size.width, size.height);
  }

  /** The live view's picture at the screen's CSS pixels (half the device's on a Retina screen: four times the samples a second); stills at their own size. */
  private liveScale(): number { return 1 / Math.max(1, this.renderer.getPixelRatio()); }

  /** The tiles a sample is cut into: more for a large image, so no frame holds the GPU long. */
  setTiles(n: number): void { this.pt.tiles.set(n, n); }

  /**
   * The picture drawn to the screen (target null) or into a target: denoised (guides rendered once per restart), balanced,
   * exposed, tone-mapped. `color` replaces the tracer's light (a picture denoised elsewhere: OIDN).
   */
  present(look: TraceLook, denoise: boolean, target: THREE.WebGLRenderTarget | null = null, color?: THREE.Texture): void {
    const r = this.renderer;
    const tex = this.pt.target.texture;
    const { width: w, height: h } = this.pt.target as THREE.WebGLRenderTarget;
    const keep = r.getRenderTarget();
    let source: THREE.Texture = color ?? tex;
    let remodulate = 0;
    this.withoutClipping(() => {
      if (denoise && !color && this.samples > 0) {
        this.renderGuides(w, h);
        this.ping.setSize(w, h); this.pong.setSize(w, h);
        const dm = this.demod.material as THREE.ShaderMaterial;
        dm.uniforms.tColor.value = tex; dm.uniforms.tAlbedo.value = this.albedo.texture; dm.uniforms.texel.value.set(1 / w, 1 / h);
        r.setRenderTarget(this.pong); this.demod.render(r);
        const nm = this.noise.material as THREE.ShaderMaterial;
        nm.uniforms.tIllum.value = this.pong.texture; nm.uniforms.texel.value.set(1 / w, 1 / h);
        r.setRenderTarget(this.ping); this.noise.render(r);
        const am = this.atrous.material as THREE.ShaderMaterial;
        const p = paramsFor(this.samples);
        am.uniforms.tGuide.value = this.guide.texture;
        am.uniforms.texel.value.set(1 / w, 1 / h);
        am.uniforms.sigmaN.value = p.sigmaN; am.uniforms.sigmaZ.value = p.sigmaZ; am.uniforms.sigmaL.value = p.sigmaL; am.uniforms.floorL.value = p.floorL;
        let from = this.ping, to = this.pong;
        for (const s of STEPS) {
          am.uniforms.tIllum.value = from.texture;
          am.uniforms.stride.value = s;
          r.setRenderTarget(to); this.atrous.render(r);
          [from, to] = [to, from];
        }
        source = from.texture;
        remodulate = 1;
      }
      if (this.outTone !== look.toneMapping) {
        (this.out.material as THREE.Material).dispose();
        this.out.material = this.outputMaterial(look.toneMapping);
        this.outTone = look.toneMapping;
      }
      const om = this.out.material as THREE.ShaderMaterial;
      om.uniforms.tColor.value = source;
      om.uniforms.tAlbedo.value = this.albedo.texture;
      om.uniforms.remodulate.value = remodulate;
      om.uniforms.gains.value.set(look.gains[0], look.gains[1], look.gains[2]);
      om.uniforms.uExposure.value = look.exposure;
      r.setRenderTarget(target);
      this.out.render(r);
    });
    r.setRenderTarget(keep);
  }

  /** The denoiser's guides from the tracer's camera: albedo, normals (their maps' too), depth; glass left out (what is behind it shows). */
  private renderGuides(w: number, h: number): void {
    if (this.guidesFor === this.resets && this.albedo.width === w && this.albedo.height === h) return;
    this.guidesFor = this.resets;
    const scene = this.scene;
    if (!scene) return;
    const r = this.renderer;
    for (const t of [this.albedo, this.normals, this.depth, this.guide]) t.setSize(w, h);
    const keepBg = scene.background, keepEnv = scene.environment, keepClear = r.getClearColor(new THREE.Color()), keepAlpha = r.getClearAlpha();
    scene.background = null;
    scene.environment = null;
    const swaps: { mesh: THREE.Mesh; material: THREE.Material | THREE.Material[]; visible: boolean }[] = [];
    scene.traverse((o) => { const m = o as THREE.Mesh; if (m.isMesh) swaps.push({ mesh: m, material: m.material, visible: m.visible }); });
    const through = (m: THREE.Material) => ((m as THREE.MeshPhysicalMaterial).transmission ?? 0) > 0;
    const pass = (target: THREE.WebGLRenderTarget, of: (m: THREE.Material) => THREE.Material, clear: number) => {
      for (const s of swaps) {
        const mats = Array.isArray(s.material) ? s.material : [s.material];
        s.mesh.visible = s.visible && !mats.every(through);
        s.mesh.material = Array.isArray(s.material) ? s.material.map(of) : of(s.material);
      }
      r.setRenderTarget(target);
      r.setClearColor(clear, 0);
      r.clear();
      r.render(scene, this.active);
    };
    const albedoOf = (m: THREE.Material) => {
      let a = this.albedoMats.get(m);
      if (!a) {
        const s = m as THREE.MeshStandardMaterial;
        a = through(m) ? new THREE.MeshBasicMaterial({ visible: false }) : new THREE.MeshBasicMaterial({ color: s.color, map: s.map ?? null, side: s.side, transparent: s.transparent, opacity: s.opacity, alphaTest: s.alphaTest, vertexColors: s.vertexColors });
        this.albedoMats.set(m, a);
      }
      return a;
    };
    const normalOf = (m: THREE.Material) => {
      let a = this.normalMats.get(m);
      if (!a) {
        const s = m as THREE.MeshStandardMaterial;
        a = new THREE.MeshNormalMaterial({ side: s.side, normalMap: s.normalMap ?? null, normalScale: s.normalScale?.clone() ?? new THREE.Vector2(1, 1), alphaTest: s.alphaTest });
        this.normalMats.set(m, a);
      }
      return a;
    };
    pass(this.albedo, albedoOf, 0x000000);
    pass(this.normals, normalOf, 0x000000);
    pass(this.depth, () => this.depthMat, 0x000000);
    for (const s of swaps) { s.mesh.material = s.material; s.mesh.visible = s.visible; }
    scene.background = keepBg;
    scene.environment = keepEnv;
    r.setClearColor(keepClear, keepAlpha);
    const pm = this.pack.material as THREE.ShaderMaterial;
    pm.uniforms.tNormal.value = this.normals.texture;
    pm.uniforms.tDepth.value = this.depth.texture;
    r.setRenderTarget(this.guide);
    this.pack.render(r);
  }

  /** The guides and the light as float arrays (RGB, RGB, XYZ), for a denoiser off the GPU (OIDN). */
  readForDenoise(): { width: number; height: number; color: Float32Array; albedo: Float32Array; normal: Float32Array } {
    const { width: w, height: h } = this.pt.target as THREE.WebGLRenderTarget;
    this.withoutClipping(() => this.renderGuides(w, h));
    const r = this.renderer;
    const read = (t: THREE.WebGLRenderTarget) => { const d = new Float32Array(w * h * 4); r.readRenderTargetPixels(t, 0, 0, w, h, d); return d; };
    const rgb = (d: Float32Array) => { const o = new Float32Array(w * h * 3); for (let i = 0; i < w * h; i++) { o[3 * i] = d[4 * i]; o[3 * i + 1] = d[4 * i + 1]; o[3 * i + 2] = d[4 * i + 2]; } return o; };
    // The albedo target is half float: read through a float copy.
    const albedoF = new THREE.WebGLRenderTarget(w, h, { type: THREE.FloatType });
    const copy = new FullScreenQuad(new THREE.ShaderMaterial({ uniforms: { t: { value: this.albedo.texture } }, vertexShader: VERTEX, fragmentShader: 'uniform sampler2D t; varying vec2 vUv; void main() { gl_FragColor = texture2D( t, vUv ); }' }));
    const keep = r.getRenderTarget();
    r.setRenderTarget(albedoF); copy.render(r); r.setRenderTarget(keep);
    const color = rgb(read(this.pt.target as THREE.WebGLRenderTarget));
    const albedo = rgb(read(albedoF));
    const normal = rgb(read(this.guide));
    albedoF.dispose(); copy.dispose(); (copy.material as THREE.Material).dispose();
    return { width: w, height: h, color, albedo, normal };
  }

  /** The tracer and the view share one renderer: the view's clipping planes (sections) are its own, not the tracer's quads'. */
  private withoutClipping(f: () => void): void {
    const r = this.renderer;
    const planes = r.clippingPlanes;
    r.clippingPlanes = [];
    try { f(); } finally { r.clippingPlanes = planes; }
  }

  dispose(): void {
    this.pt.dispose();
    this.env?.dispose();
    this.backdrop?.dispose();
    this.cubeTarget.dispose();
    for (const t of [this.albedo, this.normals, this.depth, this.guide, this.ping, this.pong]) t.dispose();
    for (const m of [...this.albedoMats.values(), ...this.normalMats.values()]) m.dispose();
    for (const q of [this.unwrap, this.pack, this.demod, this.noise, this.atrous, this.out]) { q.dispose(); (q.material as THREE.Material).dispose(); }
    this.depthMat.dispose();
    this.sky.geometry.dispose();
    this.sky.material.dispose();
  }
}
