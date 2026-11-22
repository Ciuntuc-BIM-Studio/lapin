/**
 * How the 3D view renders (docs/plan-visualisation.md, stage 1): as a drawing
 * (the sheet's palette and flat light, as Lapin always drew), medium (the
 * sun's shadows, ambient occlusion, light from the sky) or realistic (the sky
 * behind the model on a ground, tone mapping, glass that lets light
 * through). The viewer's choice, kept on this computer, not in the project:
 * a slower machine draws a colleague's project as well as it can.
 *
 * The passes are three's own (examples/jsm, MIT): GTAOPass, and its r182 Sky (sky.ts); no other
 * dependency. BimGo's numbers set them where they apply (an occlusion radius
 * of about 0.6 m, shadow maps by quality). A glow round lights waits for a
 * pass that blooms only what glows (BimGo writes the emissive colour to its
 * own target): blooming the whole picture hazes a sunlit wall and the sky.
 */

import * as THREE from 'three';
import { create } from 'zustand';
import { EffectComposer } from 'three/examples/jsm/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/examples/jsm/postprocessing/RenderPass.js';
import { GTAOPass } from 'three/examples/jsm/postprocessing/GTAOPass.js';
import { OutputPass } from 'three/examples/jsm/postprocessing/OutputPass.js';
import { ShaderPass } from 'three/examples/jsm/postprocessing/ShaderPass.js';
import { Sky } from './sky';
import { whiteBalanceGains } from './lights3d';

export type RenderProfile = 'drawing' | 'medium' | 'realistic';
export const PROFILES: readonly RenderProfile[] = ['drawing', 'medium', 'realistic'];

const KEY = 'lapin.render3d';

function stored(): RenderProfile {
  try {
    const v = localStorage.getItem(KEY);
    return PROFILES.includes(v as RenderProfile) ? (v as RenderProfile) : 'drawing';
  } catch { return 'drawing'; }
}

export const useRenderProfile = create<{ profile: RenderProfile }>(() => ({ profile: stored() }));

export function setRenderProfile(profile: RenderProfile): void {
  useRenderProfile.setState({ profile });
  try { localStorage.setItem(KEY, profile); } catch { /* a private window keeps it for the session */ }
}

/** What a profile draws. */
export interface ProfileSpec {
  /** The sun always shines (the project's sun now, when no sun study says otherwise). */
  sun: boolean;
  /** The sun's shadow map (px a side). */
  shadowMap: number;
  ao: boolean;
  /** The sky behind the model (perspective views). */
  sky: boolean;
  /** The sky's light on every face (image-based), and how strong. */
  skyLight: number;
  /** A ground under the model where it has no terrain, the grid put away. */
  ground: boolean;
  /** Glass lets light through (transmission) instead of only showing what is behind it. */
  glass: boolean;
  toneMapping: THREE.ToneMapping;
  exposure: number;
}

export const SPECS: Record<RenderProfile, ProfileSpec> = {
  drawing: { sun: false, shadowMap: 2048, ao: false, sky: false, skyLight: 0, ground: false, glass: false, toneMapping: THREE.NoToneMapping, exposure: 1 },
  medium: { sun: true, shadowMap: 2048, ao: true, sky: false, skyLight: 0.55, ground: false, glass: false, toneMapping: THREE.NeutralToneMapping, exposure: 1 },
  realistic: { sun: true, shadowMap: 4096, ao: true, sky: true, skyLight: 0.5, ground: true, glass: true, toneMapping: THREE.AgXToneMapping, exposure: 0.85 },
};

/**
 * The camera's look in the medium and realistic views (docs/plan-render.md, S2), kept on this computer as the profile is:
 * an exposure compensation (EV stops over the automatic exposure, which opens up after sunset) and a white balance
 * (automatic: daylight by day, 4000 K by night, so warm lamps stay warm without turning orange; or a temperature).
 */
export interface CameraLook { ev: number; wb: 'auto' | number }
const LOOK_KEY = 'lapin.render3d.look';

function storedLook(): CameraLook {
  try {
    const v = JSON.parse(localStorage.getItem(LOOK_KEY) ?? 'null') as Partial<CameraLook> | null;
    const ev = typeof v?.ev === 'number' && Number.isFinite(v.ev) ? Math.max(-4, Math.min(4, v.ev)) : 0;
    const wb = typeof v?.wb === 'number' && v.wb >= 2000 && v.wb <= 12000 ? v.wb : 'auto';
    return { ev, wb };
  } catch { return { ev: 0, wb: 'auto' }; }
}

export const useCameraLook = create<CameraLook>(() => storedLook());

export function setCameraLook(patch: Partial<CameraLook>): void {
  useCameraLook.setState(patch);
  try { localStorage.setItem(LOOK_KEY, JSON.stringify(useCameraLook.getState())); } catch { /* kept for the session */ }
}

/** How far into the night the sun's height (degrees) is: 0 by day (2° up and higher), 1 from nautical dusk (−10°) on, smooth between. */
export function nightOf(altitude: number | null): number {
  if (altitude == null || !Number.isFinite(altitude)) return 0;
  const t = Math.min(1, Math.max(0, (2 - altitude) / 12));
  return t * t * (3 - 2 * t);
}

/** The automatic exposure's gain over the day's: 1 by day, NIGHT_GAIN at night (about nine and a half stops, a room under lamps instead of the sun), in stops between. */
export const NIGHT_GAIN = 768;
export const exposureGain = (night: number): number => Math.pow(NIGHT_GAIN, Math.min(1, Math.max(0, night)));

/** The white balance's temperature: the user's, else daylight by day and 4000 K at night. */
export function whiteBalanceKelvin(wb: CameraLook['wb'], night: number): number {
  if (typeof wb === 'number') return wb;
  return 6504 + (4000 - 6504) * Math.min(1, Math.max(0, night));
}

/** The sun's light by its height (degrees): warm and low at sunrise and sunset, white at noon (BimGo's two ends). */
export function sunColour(altitude: number, out = new THREE.Color()): THREE.Color {
  const t = THREE.MathUtils.smoothstep(altitude, 2, 30);
  return out.setRGB(1, 0.62 + (0.97 - 0.62) * t, 0.36 + (0.92 - 0.36) * t, THREE.SRGBColorSpace);
}

/** The occlusion's reach in the model (BimGo's 0.6 m) and how far it spreads on screen at most, in device pixels. */
export const AO_RADIUS = 0.6;
export const AO_MAX_PX = 96;

/**
 * The occlusion's reach for a camera (forum topic 162): 0.6 m, but never more than AO_MAX_PX device pixels on screen at
 * the distance looked at. The pass samples its depth within that reach round every pixel: zoomed in close, 0.6 m covered
 * the whole screen and every sample missed the GPU's texture cache, so a frame took several times as long as at a
 * building's width (a slower graphics chip could barely turn the view). `pxPerMetre` is the screen's device pixels a
 * metre covers there.
 */
export function aoRadius(pxPerMetre: number): number {
  if (!Number.isFinite(pxPerMetre) || pxPerMetre <= 0) return AO_RADIUS;
  return Math.min(AO_RADIUS, AO_MAX_PX / pxPerMetre);
}

/** The device pixels a metre covers at `distance` from a perspective camera (fov in degrees) or in an orthographic one (`metresHigh`). */
export function devicePxPerMetre(heightPx: number, view: { fovDeg: number; distance: number } | { metresHigh: number }): number {
  if ('metresHigh' in view) return heightPx / Math.max(view.metresHigh, 1e-9);
  return heightPx / (2 * Math.max(view.distance, 1e-6) * Math.tan((view.fovDeg * Math.PI) / 360));
}

/**
 * The occlusion pass, made blind to what is seen through (glass, faded phases): it would darken what is behind
 * them. GTAOPass hides lines and points from its depth and normals by a method three renamed between releases
 * (overrideVisibility, then _overrideVisibility); whichever it has is wrapped.
 */
function occlusion(scene: THREE.Scene, camera: THREE.Camera, w: number, h: number): GTAOPass {
  const pass = new GTAOPass(scene, camera, w, h);
  const p = pass as unknown as Record<string, unknown> & { _visibilityCache: THREE.Object3D[] };
  const name = typeof p._overrideVisibility === 'function' ? '_overrideVisibility' : 'overrideVisibility';
  const base = p[name] as () => void;
  p[name] = () => {
    base.call(pass);
    scene.traverse((o) => {
      const m = (o as THREE.Mesh).material as THREE.Material | THREE.Material[] | undefined;
      if (!o.visible || !(o as THREE.Mesh).isMesh || !m) return;
      const through = (x: THREE.Material) => x.transparent || ((x as THREE.MeshPhysicalMaterial).transmission ?? 0) > 0;
      if (Array.isArray(m) ? m.every(through) : through(m)) { o.visible = false; p._visibilityCache.push(o); }
    });
  };
  return pass;
}

/**
 * The renderer's passes for a profile: the drawing renders straight, the others through a composer (the
 * scene, its occlusion, its glow, the tone mapping), with the sky in the scene and its light as the
 * environment. Made once per 3D view; a profile change only switches what runs.
 */
export class Realism {
  profile: RenderProfile = 'drawing';
  readonly sky = new Sky();
  private composer: EffectComposer | null = null;
  private renderPass: RenderPass | null = null;
  private occlusion: GTAOPass | null = null;
  private balance: ShaderPass | null = null;
  private size = new THREE.Vector2(1, 1);
  private envSky = new Sky();
  private envScene = new THREE.Scene();
  private env: THREE.WebGLRenderTarget | null = null;
  private envKey = '';
  private pmrem: THREE.PMREMGenerator | null = null;

  constructor(private readonly renderer: THREE.WebGLRenderer, private readonly scene: THREE.Scene) {
    for (const s of [this.sky, this.envSky]) {
      const u = s.material.uniforms;
      u.up.value.set(0, 0, 1);
      u.turbidity.value = 4;
      u.rayleigh.value = 1.2;
      u.mieCoefficient.value = 0.004;
      u.mieDirectionalG.value = 0.8;
    }
    // Inside a box 2 km round the eye, under the far plane (5 km); never picked, never cut by a section.
    this.sky.scale.setScalar(4000);
    this.sky.name = 'sky';
    this.sky.visible = false;
    this.sky.raycast = () => undefined;
    this.sky.renderOrder = -1;
    scene.add(this.sky);
    this.envSky.scale.setScalar(100);
    this.envScene.add(this.envSky);
  }

  get spec(): ProfileSpec { return SPECS[this.profile]; }

  setProfile(profile: RenderProfile): void {
    this.profile = profile;
    const spec = this.spec;
    this.renderer.toneMapping = spec.toneMapping;
    if (profile !== 'drawing' && !this.composer) this.build();
    if (this.occlusion) this.occlusion.enabled = spec.ao;
    this.applyLook();
    this.applyEnvironment();
  }

  /** How far into the night it is (nightOf): the exposure opens, the sky and its light fade, the stars come out. */
  night = 0;

  setNight(night: number): void {
    this.night = this.profile === 'drawing' ? 0 : night;
    this.applyLook();
    this.applyEnvironment();
  }

  /** The exposure's gain over the day's now (the automatic exposure's, before the user's compensation). */
  get gain(): number { return exposureGain(this.night); }

  /** The camera's look: the exposure (the profile's, the night's gain, the user's stops), the white balance, the night sky. */
  applyLook(): void {
    const spec = this.spec;
    const look = useCameraLook.getState();
    const drawing = this.profile === 'drawing';
    this.renderer.toneMappingExposure = drawing ? spec.exposure : spec.exposure * this.gain * Math.pow(2, look.ev);
    if (this.balance) {
      const [r, g, b] = whiteBalanceGains(whiteBalanceKelvin(look.wb, this.night));
      this.balance.uniforms.gains.value.set(r, g, b);
      this.balance.enabled = Math.abs(r - 1) + Math.abs(g - 1) + Math.abs(b - 1) > 1e-3;
    }
    const n = this.night, gain = this.gain;
    const u = this.sky.material.uniforms;
    u.skyGain.value = 1 / gain;
    u.nightAdd.value.set(0.006, 0.009, 0.022).multiplyScalar(n / gain);
    u.starLevel.value = n > 0.6 ? (0.5 * (n - 0.6)) / 0.4 / gain : 0;
  }

  /** The sun the sky and its light are made for (towards it, z up), or none (the sky's light goes). */
  setSun(dir: readonly [number, number, number] | null): void {
    const d = dir ?? [0.4, -0.6, 0.7];
    for (const s of [this.sky, this.envSky]) s.material.uniforms.sunPosition.value.set(d[0], d[1], d[2]);
    this.applyEnvironment(dir ? d : null);
  }

  /**
   * The sky as the path tracer takes it (pathTraceView.ts, docs/plan-render.md S4): the light-giving sky's and the backdrop's
   * uniforms as they are now (the backdrop only where the profile shows the sky), and the sky light's strength.
   */
  traceSky(): { light: Record<string, unknown>; backdrop: Record<string, unknown> | null; intensity: number } {
    const values = (s: Sky) => {
      const out: Record<string, unknown> = {};
      for (const [k, u] of Object.entries(s.material.uniforms)) {
        const v = u.value as { clone?: () => unknown } | number;
        out[k] = typeof v === 'object' && v && typeof v.clone === 'function' ? v.clone() : v;
      }
      return out;
    };
    return {
      light: values(this.envSky),
      backdrop: this.spec.sky ? values(this.sky) : null,
      intensity: this.scene.environment ? this.scene.environmentIntensity : 0,
    };
  }

  private applyEnvironment(dir?: readonly [number, number, number] | null): void {
    const spec = this.spec;
    if (!spec.skyLight) { this.scene.environment = null; return; }
    const sun = this.envSky.material.uniforms.sunPosition.value as THREE.Vector3;
    const key = dir === null ? 'none' : `${sun.x.toFixed(3)},${sun.y.toFixed(3)},${sun.z.toFixed(3)}`;
    if (key !== this.envKey || !this.env) {
      this.pmrem ??= new THREE.PMREMGenerator(this.renderer);
      this.env?.dispose();
      this.env = this.pmrem.fromScene(this.envScene, 0, 0.1, 1000);
      this.envKey = key;
    }
    this.scene.environment = this.env.texture;
    // At night the sky's light fades with it to a moonless glow (its own radiance falls too, the exposure brings it up).
    this.scene.environmentIntensity = spec.skyLight * ((1 - this.night) + (this.night * 0.15) / this.gain);
  }

  private build(): void {
    const { x: w, y: h } = this.size;
    const target = new THREE.WebGLRenderTarget(w, h, { type: THREE.HalfFloatType, samples: 4 });
    const composer = new EffectComposer(this.renderer, target);
    const camera = new THREE.PerspectiveCamera();
    this.renderPass = new RenderPass(this.scene, camera);
    this.occlusion = occlusion(this.scene, camera, w, h);
    // About BimGo's 0.6 m: a corner's shade reaches a hand's breadth or two out of it.
    this.occlusion.updateGtaoMaterial({ radius: AO_RADIUS, distanceExponent: 1, thickness: 1, scale: 1, samples: 16 });
    this.occlusion.blendIntensity = 0.9;
    composer.addPass(this.renderPass);
    composer.addPass(this.occlusion);
    // The white balance, on the linear picture before its tone mapping (off while its gains are 1).
    this.balance = new ShaderPass({
      name: 'WhiteBalance',
      uniforms: { tDiffuse: { value: null }, gains: { value: new THREE.Vector3(1, 1, 1) } },
      vertexShader: 'varying vec2 vUv; void main() { vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }',
      fragmentShader: 'uniform sampler2D tDiffuse; uniform vec3 gains; varying vec2 vUv; void main() { vec4 c = texture2D(tDiffuse, vUv); gl_FragColor = vec4(c.rgb * gains, c.a); }',
    });
    this.balance.enabled = false;
    composer.addPass(this.balance);
    composer.addPass(new OutputPass());
    this.composer = composer;
    this.resize(w, h);
  }

  resize(width: number, height: number): void {
    this.size.set(Math.max(1, width), Math.max(1, height));
    if (!this.composer) return;
    this.composer.setPixelRatio(this.renderer.getPixelRatio());
    this.composer.setSize(this.size.x, this.size.y);
  }

  /** The scene drawn from a camera, by the profile; `distance`: how far the camera looks (to the orbit's target), for the occlusion's reach. */
  render(camera: THREE.PerspectiveCamera | THREE.OrthographicCamera, distance?: number): void {
    const spec = this.spec;
    this.sky.visible = spec.sky && (camera as THREE.PerspectiveCamera).isPerspectiveCamera === true;
    if (this.sky.visible) this.sky.position.copy(camera.position);
    if (this.profile === 'drawing' || !this.composer || !this.renderPass || !this.occlusion) {
      this.renderer.render(this.scene, camera);
      return;
    }
    this.renderPass.camera = camera;
    if (this.occlusion.camera !== camera) {
      this.occlusion.camera = camera;
      // The occlusion reads depth by the camera's kind, compiled in.
      const perspective = (camera as THREE.PerspectiveCamera).isPerspectiveCamera ? 1 : 0;
      for (const m of [this.occlusion.gtaoMaterial, this.occlusion.depthRenderMaterial]) {
        if (m.defines.PERSPECTIVE_CAMERA !== perspective) { m.defines.PERSPECTIVE_CAMERA = perspective; m.needsUpdate = true; }
      }
    }
    // Its reach held to a few dozen pixels on screen, its falloff with it (the shade keeps its look, only smaller when close).
    const heightPx = this.size.y * this.renderer.getPixelRatio();
    const ortho = camera as THREE.OrthographicCamera;
    const ppm = ortho.isOrthographicCamera
      ? devicePxPerMetre(heightPx, { metresHigh: (ortho.top - ortho.bottom) / ortho.zoom })
      : distance !== undefined ? devicePxPerMetre(heightPx, { fovDeg: (camera as THREE.PerspectiveCamera).fov, distance }) : NaN;
    const r = aoRadius(ppm);
    const u = this.occlusion.gtaoMaterial.uniforms;
    u.radius.value = r;
    u.thickness.value = r / AO_RADIUS;
    this.composer.render();
  }

  dispose(): void {
    this.scene.remove(this.sky);
    this.sky.geometry.dispose();
    this.sky.material.dispose();
    this.envSky.geometry.dispose();
    this.envSky.material.dispose();
    this.env?.dispose();
    this.pmrem?.dispose();
    this.composer?.dispose();
    this.occlusion?.dispose();
  }
}
