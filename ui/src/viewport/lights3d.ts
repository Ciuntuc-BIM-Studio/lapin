/**
 * Artificial light in the 3D view (docs/plan-render.md, S2): the light fixtures, lamps and glowing materials the engine
 * lists (`light.sources`) drawn as three's lights in the medium and realistic views; the drawing is lit as it always was.
 *
 * - Units: physically based, as three's lights are (a point's and a spot's intensity in candela, a panel's in nits), scaled
 *   into the view's light by LUX_UNIT (the view's sun of 3.2 stands for daylight's ~100 000 lux), so a lamp is as faint
 *   beside the sun as it is outdoors at noon; the night's exposure (render3d.ts) brings it up.
 * - Colour: the colour temperature on Planck's locus (Krystek's approximation, as the engine's Kelvin.cs).
 * - Distribution: a spot is a cone (its beam full, a soft edge to its field); an IES or built-in profile is a spot whose
 *   cone is the profile's field and, for the lights drawn with shadows, whose light is shaped by the profile's table laid
 *   on it as a projected map (three's spot light map); a profile that reaches behind (a pendant's) is a point.
 * - The budget, live: the lights nearest and brightest first, a few with shadows, more without, none past a radius; the
 *   lights are made in pools sized by the model's lights in steps (three compiles its shaders by the number of lights),
 *   unused ones dark; a light's shadow is drawn again only when the model or the light changes, not as the camera moves.
 */

import * as THREE from 'three';
import { create } from 'zustand';
import { RectAreaLightUniformsLib } from 'three/examples/jsm/lights/RectAreaLightUniformsLib.js';
import type { RenderProfile } from './render3d';

/** The view's lights now, for its corner: how many the model has on, how many are drawn, how many with shadows. */
export const useLightBudget = create<{ total: number; drawn: number; shadowed: number }>(() => ({ total: 0, drawn: 0, shadowed: 0 }));

/** One light as the engine lists it (Lights.cs, light.sources). */
export interface LightSourceDto {
  id: string;
  kind: 'point' | 'spot' | 'area';
  lumens: number;
  kelvin: number;
  beam: number;
  field: number;
  width: number;
  length: number;
  profile: string | null;
  on: boolean;
  position: [number, number, number];
  direction: [number, number, number];
  across: [number, number, number];
  material?: string | null;
}

/** A light profile's table (light.profile): relative intensity, gamma rows × C columns, peak 1. */
export interface LightProfileDto {
  key: string;
  name: string;
  scale: number;
  beam: number;
  field: number;
  vertical: number;
  horizontal: number;
  table: number[];
}

/** The view's light per lux: its sun's 3.2 for daylight's 100 000 lux. */
export const LUX_UNIT = 3.2 / 100_000;

// ─── Colour ─────────────────────────────────────────────────────────────

/** The Planckian locus's chromaticity (CIE 1931 x, y), Krystek's rational approximation, 1000–12000 K. */
export function planckXy(kelvin: number): [number, number] {
  const t = Math.min(12000, Math.max(1000, Number.isFinite(kelvin) ? kelvin : 6500));
  const u = (0.860117757 + 1.54118254e-4 * t + 1.28641212e-7 * t * t) / (1 + 8.42420235e-4 * t + 7.08145163e-7 * t * t);
  const v = (0.317398726 + 4.22806245e-5 * t + 4.20481691e-8 * t * t) / (1 - 2.89741816e-5 * t + 1.61456053e-7 * t * t);
  const d = 2 * u - 8 * v + 4;
  return [(3 * u) / d, (2 * v) / d];
}

/** A colour temperature as linear sRGB, its largest channel 1. */
export function kelvinRgb(kelvin: number): [number, number, number] {
  const [x, y] = planckXy(kelvin);
  const X = x / y, Z = (1 - x - y) / y;
  const r = Math.max(0, 3.2404542 * X - 1.5371385 - 0.4985314 * Z);
  const g = Math.max(0, -0.969266 * X + 1.8760108 + 0.041556 * Z);
  const b = Math.max(0, 0.0556434 * X - 0.2040259 + 1.0572252 * Z);
  const top = Math.max(r, g, b);
  return top > 0 ? [r / top, g / top, b / top] : [1, 1, 1];
}

/** The gains a white balance at `kelvin` puts on the picture: a light of that temperature comes out white, the brightness kept. */
export function whiteBalanceGains(kelvin: number): [number, number, number] {
  const [r, g, b] = kelvinRgb(kelvin);
  const [r0, g0, b0] = kelvinRgb(6504);
  const gains: [number, number, number] = [r0 / r, g0 / g, b0 / b];
  const lum = 0.2126 * gains[0] + 0.7152 * gains[1] + 0.0722 * gains[2];
  return [gains[0] / lum, gains[1] / lum, gains[2] / lum];
}

// ─── Units ──────────────────────────────────────────────────────────────

/** An even (point) light's intensity: Φ / 4π candela. */
export const pointCandela = (lumens: number) => lumens / (4 * Math.PI);

/** A spot's intensity, its lumens spread evenly in its field (full angle, degrees): Φ / (2π (1 − cos(θ/2))) candela. */
export const spotCandela = (lumens: number, fieldDeg: number) => lumens / (2 * Math.PI * (1 - Math.cos((Math.min(179, Math.max(1, fieldDeg)) * Math.PI) / 360)));

/** A panel's luminance, its lumens out of one face of w × l metres as a Lambertian emitter: Φ / (π w l) nits. */
export const panelNits = (lumens: number, w: number, l: number) => lumens / (Math.PI * Math.max(1e-4, w) * Math.max(1e-4, l));

/** A profile's peak intensity: the even light's Φ / 4π times the profile's scale (LightProfile.Scale). */
export const profileCandela = (lumens: number, scale: number) => (lumens / (4 * Math.PI)) * scale;

// ─── The budget ─────────────────────────────────────────────────────────

export type Pool = 'spotShadow' | 'pointShadow' | 'spot' | 'point' | 'rect';

/** How many lights a profile draws of each kind (pools made once: three compiles its shaders by the count). */
export interface LightCaps { spotShadow: number; pointShadow: number; spot: number; point: number; rect: number; radius: number }

/**
 * A profile's budget. A WebGL frame lights every pixel by every light and a light with shadows draws the model once more
 * (a point six times), and a GPU has 16 texture units shared with the materials' maps: fewer than the plan's 8/64 and
 * 16/256 (those are the path tracer's, S4); the medium view a laptop's, the realistic a desktop's.
 */
export const CAPS: Record<RenderProfile, LightCaps> = {
  drawing: { spotShadow: 0, pointShadow: 0, spot: 0, point: 0, rect: 0, radius: 0 },
  medium: { spotShadow: 2, pointShadow: 0, spot: 8, point: 8, rect: 2, radius: 30 },
  realistic: { spotShadow: 4, pointShadow: 0, spot: 16, point: 16, rect: 4, radius: 60 },
};

/** The pool a light goes to by its kind and profile, with shadows or not. */
export function poolOf(s: LightSourceDto, profile: LightProfileDto | undefined, shadow: boolean): Pool {
  if (s.kind === 'area' && !s.profile) return 'rect';
  const cone = s.kind === 'spot' || (profile != null && profile.field > 0 && profile.field < 180);
  return cone ? (shadow ? 'spotShadow' : 'spot') : (shadow ? 'pointShadow' : 'point');
}

export interface Chosen { source: LightSourceDto; pool: Pool }
export interface Choice { chosen: Chosen[]; total: number; drawn: number; shadowed: number }

/**
 * The lights drawn now (the budget): those on and in reach, by how much light they bring to the eye (lumens over the
 * distance squared, at least a metre, from the eye or the point looked at), the first ones with shadows where their pool has
 * room, then without, the rest left.
 */
export function chooseLights(sources: readonly LightSourceDto[], eye: readonly [number, number, number], caps: LightCaps,
  profiles: ReadonlyMap<string, LightProfileDto> = new Map(), focus?: readonly [number, number, number]): Choice {
  const lit = sources.filter((s) => s.on && s.lumens > 0);
  // How far a light is from what is seen: the eye, or the point looked at (an orbit's target), whichever is nearer.
  const far = (p: readonly number[], q: readonly number[]) => (p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2 + (p[2] - q[2]) ** 2;
  const score = (s: LightSourceDto) => {
    const d2 = Math.min(far(s.position, eye), focus ? far(s.position, focus) : Infinity);
    return { s, d2, w: s.lumens / Math.max(1, d2) };
  };
  const ranked = lit.map(score).filter((x) => x.d2 <= caps.radius * caps.radius).sort((a, b) => b.w - a.w || a.s.id.localeCompare(b.s.id));
  const room: Record<Pool, number> = { spotShadow: caps.spotShadow, pointShadow: caps.pointShadow, spot: caps.spot, point: caps.point, rect: caps.rect };
  const chosen: Chosen[] = [];
  for (const { s } of ranked) {
    const p = s.profile ? profiles.get(s.profile) : undefined;
    const withShadow = poolOf(s, p, true);
    if (withShadow !== 'rect' && room[withShadow] > 0) { room[withShadow]--; chosen.push({ source: s, pool: withShadow }); continue; }
    const plain = poolOf(s, p, false);
    if (room[plain] > 0) { room[plain]--; chosen.push({ source: s, pool: plain }); continue; }
    // A panel past its pool's room lights as a point (its light still comes, without its shape).
    if (plain === 'rect' && room.point > 0) { room.point--; chosen.push({ source: s, pool: 'point' }); }
  }
  return { chosen, total: lit.length, drawn: chosen.length, shadowed: chosen.filter((c) => c.pool === 'spotShadow' || c.pool === 'pointShadow').length };
}

// ─── The lights ─────────────────────────────────────────────────────────

/** A profile's table as the projected map a shadowed spot lays on its light: the cone seen through the spot's frustum (tan space). */
export function profileMap(p: LightProfileDto, coneDeg: number, size = 64): THREE.DataTexture {
  const data = new Uint8Array(size * size * 4);
  const tanA = Math.tan((Math.min(85, coneDeg) * Math.PI) / 180);
  const sample = (gamma: number, c: number) => {
    const gv = Math.min(180, Math.max(0, gamma)) / (180 / (p.vertical - 1));
    const v0 = Math.min(Math.floor(gv), p.vertical - 2), fv = gv - v0;
    let cc = c % 360; if (cc < 0) cc += 360;
    const ch = cc / (360 / p.horizontal);
    const h0 = Math.floor(ch) % p.horizontal, h1 = (h0 + 1) % p.horizontal, fh = ch - Math.floor(ch);
    const t = p.table, H = p.horizontal;
    const top = t[v0 * H + h0] + (t[v0 * H + h1] - t[v0 * H + h0]) * fh;
    const bottom = t[(v0 + 1) * H + h0] + (t[(v0 + 1) * H + h1] - t[(v0 + 1) * H + h0]) * fh;
    return top + (bottom - top) * fv;
  };
  for (let j = 0; j < size; j++) {
    for (let i = 0; i < size; i++) {
      const u = ((i + 0.5) / size) * 2 - 1, v = ((j + 0.5) / size) * 2 - 1;
      const gamma = (Math.atan(Math.hypot(u, v) * tanA) * 180) / Math.PI;
      const c = (Math.atan2(v, u) * 180) / Math.PI;
      const x = Math.round(Math.min(1, Math.max(0, sample(gamma, c))) * 255);
      const k = (j * size + i) * 4;
      data[k] = data[k + 1] = data[k + 2] = x; data[k + 3] = 255;
    }
  }
  const tex = new THREE.DataTexture(data, size, size, THREE.RGBAFormat);
  tex.colorSpace = THREE.NoColorSpace;
  tex.magFilter = THREE.LinearFilter;
  tex.minFilter = THREE.LinearFilter;
  tex.needsUpdate = true;
  return tex;
}

const WHITE = (() => { const t = new THREE.DataTexture(new Uint8Array([255, 255, 255, 255]), 1, 1, THREE.RGBAFormat); t.needsUpdate = true; return t; })();

/**
 * The view's artificial lights: pools made for a profile, filled each frame from the budget's choice. `update` takes the
 * sources (and their profiles), the eye and the exposure's gain; returns the choice (the corner's count).
 */
export class LightRig {
  readonly group = new THREE.Group();
  private pools: Record<Pool, THREE.Light[]> = { spotShadow: [], pointShadow: [], spot: [], point: [], rect: [] };
  private profile: RenderProfile = 'drawing';
  private maps = new Map<string, THREE.DataTexture>();
  last: Choice = { chosen: [], total: 0, drawn: 0, shadowed: 0 };

  constructor() { this.group.name = 'artificial-lights'; }

  private sizes = '';
  /** The shadows are drawn again only when what they fall on or the light changes (the model, the light's choice), not as the camera moves. */
  private dirty = true;

  /** The model changed: the lights' shadows are drawn again next frame. */
  markDirty(): void { this.dirty = true; }

  /**
   * The pools for a profile and the model's lights: as many of each kind as the model has, in steps (2, 4, 8, 16: three
   * compiles its shaders again when the count changes), up to the profile's budget; none for the drawing or a model without lights.
   */
  setProfile(profile: RenderProfile, sources: readonly LightSourceDto[], profiles: ReadonlyMap<string, LightProfileDto> = new Map()): void {
    const caps = CAPS[profile];
    const need: Record<Pool, number> = { spotShadow: 0, pointShadow: 0, spot: 0, point: 0, rect: 0 };
    for (const s of sources) {
      if (!s.on || s.lumens <= 0) continue;
      const p = s.profile ? profiles.get(s.profile) : undefined;
      need[poolOf(s, p, false)]++;
      const shadow = poolOf(s, p, true);
      if (shadow !== 'rect') need[shadow]++;
    }
    const step = (n: number) => (n <= 0 ? 0 : n <= 2 ? 2 : n <= 4 ? 4 : n <= 8 ? 8 : 16);
    const size: Record<Pool, number> = {
      spotShadow: Math.min(caps.spotShadow, step(need.spotShadow)), pointShadow: Math.min(caps.pointShadow, need.pointShadow),
      spot: Math.min(caps.spot, step(Math.max(0, need.spot - caps.spotShadow))), point: Math.min(caps.point, step(Math.max(0, need.point - caps.pointShadow) + Math.max(0, need.rect - caps.rect))),
      rect: Math.min(caps.rect, step(need.rect)),
    };
    const key = `${profile}:${size.spotShadow},${size.pointShadow},${size.spot},${size.point},${size.rect}`;
    if (key === this.sizes) return;
    this.clear();
    this.sizes = key;
    if (Object.values(size).every((n) => n === 0)) return;
    this.profile = profile;
    if (size.rect > 0) RectAreaLightUniformsLib.init();
    this.dirty = true;
    const make = (pool: Pool, n: number) => {
      for (let i = 0; i < n; i++) {
        let l: THREE.Light;
        if (pool === 'rect') l = new THREE.RectAreaLight(0xffffff, 0, 1, 1);
        else if (pool === 'spot' || pool === 'spotShadow') {
          const s = new THREE.SpotLight(0xffffff, 0, 0, Math.PI / 4, 0.3, 2);
          this.group.add(s.target);
          if (pool === 'spotShadow') { s.castShadow = true; s.map = WHITE; s.shadow.mapSize.set(1024, 1024); s.shadow.bias = -0.0004; s.shadow.normalBias = 0.02; s.shadow.camera.near = 0.05; s.shadow.autoUpdate = false; }
          l = s;
        } else {
          const p = new THREE.PointLight(0xffffff, 0, 0, 2);
          if (pool === 'pointShadow') { p.castShadow = true; p.shadow.mapSize.set(512, 512); p.shadow.bias = -0.0005; p.shadow.normalBias = 0.02; p.shadow.camera.near = 0.05; p.shadow.autoUpdate = false; }
          l = p;
        }
        l.userData.pool = pool;
        this.pools[pool].push(l);
        this.group.add(l);
      }
    };
    make('spotShadow', size.spotShadow); make('pointShadow', size.pointShadow); make('spot', size.spot); make('point', size.point); make('rect', size.rect);
  }

  get active(): boolean { return this.profile !== 'drawing'; }

  private mapOf(p: LightProfileDto, cone: number): THREE.DataTexture {
    const key = `${p.key}@${cone.toFixed(1)}`;
    let t = this.maps.get(key);
    if (!t) { t = profileMap(p, cone); this.maps.set(key, t); }
    return t;
  }

  /** The lights for this frame: chosen (from the eye and the point looked at), placed and set. */
  update(sources: readonly LightSourceDto[], profiles: ReadonlyMap<string, LightProfileDto>, eye: readonly [number, number, number], focus?: readonly [number, number, number]): Choice {
    if (!this.active) return (this.last = { chosen: [], total: sources.filter((s) => s.on).length, drawn: 0, shadowed: 0 });
    const pooled: LightCaps = { ...CAPS[this.profile], spotShadow: this.pools.spotShadow.length, pointShadow: this.pools.pointShadow.length, spot: this.pools.spot.length, point: this.pools.point.length, rect: this.pools.rect.length };
    const choice = chooseLights(sources, eye, pooled, profiles, focus);
    const used: Record<Pool, number> = { spotShadow: 0, pointShadow: 0, spot: 0, point: 0, rect: 0 };
    for (const { source: s, pool } of choice.chosen) {
      const l = this.pools[pool][used[pool]++];
      const [r, g, b] = kelvinRgb(s.kelvin);
      l.color.setRGB(r, g, b, THREE.LinearSRGBColorSpace);
      l.position.set(s.position[0], s.position[1], s.position[2]);
      const p = s.profile ? profiles.get(s.profile) : undefined;
      if (l instanceof THREE.RectAreaLight) {
        l.width = s.width; l.height = s.length;
        l.up.set(s.across[0], s.across[1], s.across[2]);
        l.lookAt(s.position[0] + s.direction[0], s.position[1] + s.direction[1], s.position[2] + s.direction[2]);
        l.intensity = panelNits(s.lumens, s.width, s.length) * LUX_UNIT;
      } else if (l instanceof THREE.SpotLight) {
        l.target.position.set(s.position[0] + s.direction[0], s.position[1] + s.direction[1], s.position[2] + s.direction[2]);
        l.target.updateMatrixWorld();
        if (p) {
          const cone = Math.min(85, Math.max(5, p.field / 2));
          l.angle = (cone * Math.PI) / 180;
          l.penumbra = p.beam > 0 ? Math.min(1, Math.max(0.05, 1 - p.beam / Math.max(p.field, 1))) : 0.6;
          l.intensity = profileCandela(s.lumens, p.scale) * LUX_UNIT;
          if (l.castShadow) l.map = this.mapOf(p, cone);
        } else {
          const field = Math.min(170, Math.max(2, s.field));
          l.angle = (field * Math.PI) / 360;
          l.penumbra = Math.min(1, Math.max(0, 1 - s.beam / field));
          l.intensity = spotCandela(s.lumens, field) * LUX_UNIT;
          if (l.castShadow) l.map = WHITE;
        }
        if (l.castShadow) this.shade(l, s);
      } else if (l instanceof THREE.PointLight) {
        // A panel drawn as a point sends its light down a half sphere: twice an even light's, seen from below.
        l.intensity = (s.kind === 'area' && !p ? 2 : 1) * pointCandela(s.lumens) * LUX_UNIT;
        if (l.castShadow) this.shade(l, s);
      }
    }
    this.dirty = false;
    for (const pool of Object.keys(this.pools) as Pool[])
      for (let i = used[pool]; i < this.pools[pool].length; i++) {
        const l = this.pools[pool][i];
        l.intensity = 0;
        l.userData.shaded = '';
      }
    return (this.last = choice);
  }

  /** A shadowed light's map drawn again when its light moved to another source or the model changed. */
  private shade(l: THREE.SpotLight | THREE.PointLight, s: LightSourceDto): void {
    const key = `${s.id}@${s.position.join(',')}`;
    if (!this.dirty && l.userData.shaded === key) return;
    l.userData.shaded = key;
    l.shadow.camera.far = 30;
    l.shadow.needsUpdate = true;
  }

  clear(): void {
    for (const pool of Object.values(this.pools)) for (const l of pool) { l.dispose(); this.group.remove(l); if (l instanceof THREE.SpotLight) this.group.remove(l.target); }
    this.pools = { spotShadow: [], pointShadow: [], spot: [], point: [], rect: [] };
    this.profile = 'drawing';
    this.sizes = '';
  }

  dispose(): void {
    this.clear();
    for (const t of this.maps.values()) t.dispose();
    this.maps.clear();
  }
}
