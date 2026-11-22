/**
 * The path tracer's scene (docs/plan-render.md, S4): built from exactly what the 3D view draws, so a rendered image shows
 * the view and nothing else (one resolver: what the view shows is what its looks, phases, layers, hidden elements and
 * storeys left visible, decided before anything reaches here).
 *
 * - Geometry: the elements' own meshes (a heavy part's finest copy, never the coarser ones the screen uses far away: the
 *   one rule for curves, Lapin.Geometry/Chords), the instances of shared meshes one by one, the caps of a section, the
 *   view's ground; cut by the view's clipping planes (a section shown, a section box, the cut above the storeys shown),
 *   triangle by triangle, since the tracer has no clipping of its own. Lines, points and labels are the screen's.
 * - Materials: the view's own (colour and images, roughness and metalness, the S1 normal and roughness maps, glass with
 *   its transmission and index of refraction, a glow), cloned so the view's selection highlight and phase tints stay out;
 *   anything not physically based becomes a rough MeshStandardMaterial of its colour.
 * - Lights: every light the engine lists (light.sources, Lights.cs), not the live view's budget, in the same units as the
 *   live view (lights3d.ts): candela for a point or a spot, nits for a panel, scaled by LUX_UNIT. A point is a small
 *   sphere-like source (a spot of even distribution with a radius, for soft shadows), a profile (IES or built in) a spot
 *   whose light follows the profile's table round its axis, a panel a rectangle that emits from its face.
 *
 * Pure three.js (no WebGL), so its counts are tested in vitest; pathTraceView.ts renders it.
 */

import * as THREE from 'three';
import { LUX_UNIT, kelvinRgb, panelNits, pointCandela, profileCandela, spotCandela, type LightProfileDto, type LightSourceDto } from './lights3d';

/** One instance of a shared mesh, as the instance layer draws it (instancing.ts, traceInstances). */
export interface TraceInstance { geometry: THREE.BufferGeometry; material: THREE.Material; matrix: THREE.Matrix4; colour: THREE.Color | null }

export interface TraceInput {
  /** The elements' objects shown (and the caps, the bars in 3D): their visible meshes are taken, a heavy part's finest copy. */
  roots: readonly THREE.Object3D[];
  instances?: readonly TraceInstance[];
  /** The view's clipping planes: what lies on a plane's negative side is cut away, as three's clipping does. */
  planes?: readonly THREE.Plane[];
  lights?: readonly LightSourceDto[];
  profiles?: ReadonlyMap<string, LightProfileDto>;
  /** A ground under the model (the realistic view's meadow, or a plain one), at this height; none where the model has terrain. */
  ground?: { z: number; material: THREE.Material } | null;
}

export interface TraceStats { meshes: number; triangles: number; materials: number; lights: number; clipped: number }

/** A point light's radius in the tracer: a bulb's few centimetres, so its shadows are soft as a real lamp's. */
export const POINT_RADIUS = 0.04;
/** A spot's radius: a downlight's lens. */
export const SPOT_RADIUS = 0.03;

const isLine = (o: THREE.Object3D) => (o as THREE.Line).isLine === true || (o as THREE.Points).isPoints === true || (o as THREE.Sprite).isSprite === true;

/** What a mesh's material is drawn with in the tracer: physically based, without the view's highlight or tint. */
export class TraceMaterials {
  private cache = new Map<string, THREE.Material>();

  /** The tracer's copy of a material (and a colour an instance or a look gives it), made once per version. */
  of(m: THREE.Material, colour: THREE.Color | null = null, castShadow = true): THREE.Material {
    const base = m as THREE.MeshStandardMaterial;
    const hex = colour ? colour.getHex() : base.color ? base.color.getHex() : 0xffffff;
    const opacity = base.opacity ?? 1;
    const key = `${m.uuid}:${m.version}:${hex}:${opacity.toFixed(3)}:${base.transparent ? 1 : 0}:${castShadow ? 1 : 0}`;
    const hit = this.cache.get(key);
    if (hit) return hit;
    const out = traceMaterial(m, colour);
    // What casts no shadow in the view (glass, a light fixture's own body round its lamp) lets the tracer's shadow rays through too.
    (out as THREE.Material & { castShadow?: boolean }).castShadow = castShadow;
    this.cache.set(key, out);
    return out;
  }

  get size(): number { return this.cache.size; }

  /** The copies no scene uses any more let go (a look's tint changed, an element went). */
  prune(used: ReadonlySet<THREE.Material>): void {
    for (const [k, m] of this.cache) if (!used.has(m)) { m.dispose(); this.cache.delete(k); }
  }

  dispose(): void { for (const m of this.cache.values()) m.dispose(); this.cache.clear(); }
}

/** A material as the tracer takes it (see TraceMaterials). */
export function traceMaterial(m: THREE.Material, colour: THREE.Color | null = null): THREE.MeshStandardMaterial {
  const src = m as THREE.MeshStandardMaterial & Partial<THREE.MeshPhysicalMaterial>;
  let out: THREE.MeshStandardMaterial;
  if (src.isMeshStandardMaterial) {
    out = src.clone();
  } else {
    // Basic, Lambert, Phong (an overlay's, an old part's): its colour and image, rough, opaque or see-through as it is.
    out = new THREE.MeshStandardMaterial({ color: src.color ?? 0xffffff, map: src.map ?? null, roughness: 0.9, metalness: 0, transparent: src.transparent, opacity: src.opacity, side: src.side, alphaTest: src.alphaTest });
  }
  if (colour) out.color.copy(colour);
  // The material's own glow (a lamp shade), never the selection's.
  const glow = m.userData.emissive as { hex: number; intensity: number } | undefined;
  out.emissive = new THREE.Color(glow?.hex ?? 0);
  out.emissiveIntensity = glow?.intensity ?? 0;
  // Glass lets the light through, as in the realistic view (materialLook.ts, lightThrough).
  const glass = m.userData.glass as { transmission: number } | undefined;
  if (glass && (out as THREE.MeshPhysicalMaterial).isMeshPhysicalMaterial) {
    const p = out as THREE.MeshPhysicalMaterial;
    p.transmission = glass.transmission;
    p.transparent = false;
    p.opacity = 1;
    // A pane is drawn as its faces: thin-walled (no volume to bend the light through), as its thickness 0 says.
    p.thickness = 0;
    p.roughness = Math.min(p.roughness, 0.05);
  }
  // The S1 surface maps (roughness), which the drawing profile takes off: the tracer always has them. The occlusion map is
  // the screen's stand-in for light that does not reach a corner; the tracer finds that light itself.
  const surface = m.userData.surface as { roughness?: THREE.Texture } | undefined;
  if (surface?.roughness) out.roughnessMap = surface.roughness;
  out.aoMap = null;
  out.envMap = null;
  // Both faces: a face wound the wrong way (an imported mesh's, a triangulation's) would let the tracer's rays through to
  // what is behind it, where the screen draws it as it is.
  out.side = THREE.DoubleSide;
  out.polygonOffset = false;
  return out;
}

/**
 * Triangles on the kept side of every plane (dot(n, p) + c ≥ 0), in world space, with their normals and uvs interpolated
 * where a plane cuts them (Sutherland–Hodgman on each triangle). Returns null when nothing is left.
 */
export function clipGeometry(geometry: THREE.BufferGeometry, matrix: THREE.Matrix4, planes: readonly THREE.Plane[]): THREE.BufferGeometry | null {
  const pos = geometry.getAttribute('position') as THREE.BufferAttribute | undefined;
  if (!pos) return null;
  const nrm = geometry.getAttribute('normal') as THREE.BufferAttribute | undefined;
  const uv = geometry.getAttribute('uv') as THREE.BufferAttribute | undefined;
  const index = geometry.getIndex();
  const normalMatrix = new THREE.Matrix3().getNormalMatrix(matrix);
  const outP: number[] = [], outN: number[] = [], outU: number[] = [];
  const groupsIn = geometry.groups.length ? geometry.groups : [{ start: 0, count: index ? index.count : pos.count, materialIndex: 0 }];
  const groupsOut: { start: number; count: number; materialIndex: number }[] = [];
  type V = { p: THREE.Vector3; n: THREE.Vector3; u: THREE.Vector2 };
  const vert = (i: number): V => ({
    p: new THREE.Vector3().fromBufferAttribute(pos, i).applyMatrix4(matrix),
    n: nrm ? new THREE.Vector3().fromBufferAttribute(nrm, i).applyMatrix3(normalMatrix).normalize() : new THREE.Vector3(),
    u: uv ? new THREE.Vector2().fromBufferAttribute(uv, i) : new THREE.Vector2(),
  });
  const lerp = (a: V, b: V, t: number): V => ({ p: a.p.clone().lerp(b.p, t), n: a.n.clone().lerp(b.n, t).normalize(), u: a.u.clone().lerp(b.u, t) });
  for (const g of groupsIn) {
    const first = outP.length / 3;
    const end = Math.min(g.start + g.count, index ? index.count : pos.count);
    for (let k = g.start; k + 2 < end; k += 3) {
      const ia = index ? index.getX(k) : k, ib = index ? index.getX(k + 1) : k + 1, ic = index ? index.getX(k + 2) : k + 2;
      let poly: V[] = [vert(ia), vert(ib), vert(ic)];
      for (const plane of planes) {
        const next: V[] = [];
        for (let i = 0; i < poly.length; i++) {
          const a = poly[i], b = poly[(i + 1) % poly.length];
          const da = plane.distanceToPoint(a.p), db = plane.distanceToPoint(b.p);
          if (da >= 0) next.push(a);
          if ((da >= 0) !== (db >= 0)) next.push(lerp(a, b, da / (da - db)));
        }
        poly = next;
        if (poly.length < 3) break;
      }
      for (let i = 1; i + 1 < poly.length; i++) for (const v of [poly[0], poly[i], poly[i + 1]]) {
        outP.push(v.p.x, v.p.y, v.p.z); outN.push(v.n.x, v.n.y, v.n.z); outU.push(v.u.x, v.u.y);
      }
    }
    const count = outP.length / 3 - first;
    if (count > 0) groupsOut.push({ start: first, count, materialIndex: g.materialIndex ?? 0 });
  }
  if (!outP.length) return null;
  const out = new THREE.BufferGeometry();
  out.setAttribute('position', new THREE.Float32BufferAttribute(outP, 3));
  if (nrm) out.setAttribute('normal', new THREE.Float32BufferAttribute(outN, 3));
  else out.computeVertexNormals();
  if (uv) out.setAttribute('uv', new THREE.Float32BufferAttribute(outU, 2));
  if (geometry.groups.length) for (const g of groupsOut) out.addGroup(g.start, g.count, g.materialIndex);
  out.userData.traceOwned = true;
  return out;
}

/** How a mesh's world box lies to the planes: wholly kept, wholly cut away, or crossed by one. */
export function sideOf(box: THREE.Box3, planes: readonly THREE.Plane[]): 'in' | 'out' | 'cut' {
  let cut = false;
  const corners = [0, 1, 2, 3, 4, 5, 6, 7].map((i) => new THREE.Vector3(i & 1 ? box.max.x : box.min.x, i & 2 ? box.max.y : box.min.y, i & 4 ? box.max.z : box.min.z));
  for (const p of planes) {
    let inside = 0;
    for (const c of corners) if (p.distanceToPoint(c) >= 0) inside++;
    if (inside === 0) return 'out';
    if (inside < 8) cut = true;
  }
  return cut ? 'cut' : 'in';
}

const triangles = (g: THREE.BufferGeometry) => Math.floor((g.getIndex()?.count ?? g.getAttribute('position')?.count ?? 0) / 3);

/** The meshes a root draws: the visible ones, a heavy part's finest copy in place of whichever its distance chose. */
export function meshesOf(root: THREE.Object3D, out: THREE.Mesh[] = []): THREE.Mesh[] {
  const visit = (o: THREE.Object3D, forced: boolean) => {
    // A level of detail: the finest (the part itself) always, the coarser copies never.
    if (o.userData.lodLevel) {
      const parent = o.parent;
      const finest = parent ? parent.children.find((c) => c.userData.lodLevel) === o : true;
      if (!finest) return;
      forced = true;
    } else if (!o.visible) return;
    if ((o as THREE.Mesh).isMesh && !isLine(o) && !o.userData.pickOnly && !(o as THREE.InstancedMesh).isInstancedMesh) {
      const mesh = o as THREE.Mesh;
      const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
      if (mats.some((m) => m && m.visible && m.colorWrite !== false && !(m as THREE.ShadowMaterial).isShadowMaterial)) out.push(mesh);
    }
    for (const c of o.children) visit(c, forced);
  };
  visit(root, false);
  return out;
}

/**
 * The tracer's scene: the view's meshes (cut by its planes) with the tracer's materials, the instances, the ground and the
 * lights. Matrices are world matrices fixed in place (matrixAutoUpdate off).
 */
export function buildTraceScene(input: TraceInput, materials = new TraceMaterials()): { scene: THREE.Scene; stats: TraceStats } {
  const scene = new THREE.Scene();
  const planes = input.planes ?? [];
  const stats: TraceStats = { meshes: 0, triangles: 0, materials: 0, lights: 0, clipped: 0 };
  const used = new Set<THREE.Material>();
  const box = new THREE.Box3();
  const put = (geometry: THREE.BufferGeometry, material: THREE.Material | THREE.Material[], matrix: THREE.Matrix4, colour: THREE.Color | null, castShadow: boolean) => {
    const mats = (Array.isArray(material) ? material : [material]).map((m) => materials.of(m, colour, castShadow));
    let g = geometry;
    let world = matrix;
    if (planes.length) {
      if (!g.boundingBox) g.computeBoundingBox();
      box.copy(g.boundingBox!).applyMatrix4(matrix);
      const side = sideOf(box, planes);
      if (side === 'out') return;
      if (side === 'cut') {
        const cut = clipGeometry(g, matrix, planes);
        if (!cut) return;
        g = cut;
        world = new THREE.Matrix4();
        stats.clipped++;
      }
    }
    const mesh = new THREE.Mesh(g, mats.length === 1 ? mats[0] : mats);
    mesh.matrixAutoUpdate = false;
    mesh.matrix.copy(world);
    mesh.castShadow = castShadow;
    scene.add(mesh);
    for (const m of mats) used.add(m);
    stats.meshes++;
    stats.triangles += triangles(g);
  };
  for (const root of input.roots) {
    root.updateWorldMatrix(true, true);
    for (const mesh of meshesOf(root)) put(mesh.geometry, mesh.material, mesh.matrixWorld, null, mesh.castShadow);
  }
  for (const inst of input.instances ?? []) put(inst.geometry, inst.material, inst.matrix, inst.colour, true);
  if (input.ground) {
    const g = new THREE.PlaneGeometry(4000, 4000);
    const ground = new THREE.Mesh(g, materials.of(input.ground.material));
    ground.matrixAutoUpdate = false;
    // 3 cm under the given height: a floor's top at the same height let the tracer's rays through to the ground.
    ground.matrix.makeTranslation(0, 0, input.ground.z - 0.03);
    ground.userData.ground = true;
    scene.add(ground);
    used.add(ground.material as THREE.Material);
  }
  const lights = traceLights(input.lights ?? [], input.profiles ?? new Map());
  for (const l of lights) scene.add(l);
  stats.lights = lights.length;
  stats.materials = used.size;
  materials.prune(used);
  return { scene, stats };
}

// ─── Lights ─────────────────────────────────────────────────────────────

/**
 * A light profile's table as the tracer's distribution: intensity by the angle from the light's axis (0–180°, one row,
 * averaged round the axis: the tracer's profiles are rotationally symmetric), peak 1. 181 samples, a degree each.
 */
export function profileCurve(p: LightProfileDto, samples = 181): Float32Array {
  const out = new Float32Array(samples);
  const H = Math.max(1, p.horizontal), V = Math.max(2, p.vertical);
  for (let i = 0; i < samples; i++) {
    const gamma = (i / (samples - 1)) * 180;
    const gv = gamma / (180 / (V - 1));
    const v0 = Math.min(Math.floor(gv), V - 2), f = gv - v0;
    let sum = 0;
    for (let h = 0; h < H; h++) sum += p.table[v0 * H + h] + (p.table[(v0 + 1) * H + h] - p.table[v0 * H + h]) * f;
    out[i] = Math.max(0, sum / H);
  }
  const top = Math.max(...out);
  if (top > 0) for (let i = 0; i < samples; i++) out[i] /= top;
  return out;
}

/** A curve as the texture the tracer's IES lookup reads (its red channel along the first row). */
export function curveTexture(curve: Float32Array): THREE.DataTexture {
  const data = new Float32Array(curve.length * 4);
  for (let i = 0; i < curve.length; i++) { data[4 * i] = data[4 * i + 1] = data[4 * i + 2] = curve[i]; data[4 * i + 3] = 1; }
  const t = new THREE.DataTexture(data, curve.length, 1, THREE.RGBAFormat, THREE.FloatType);
  t.magFilter = THREE.LinearFilter;
  t.minFilter = THREE.LinearFilter;
  t.needsUpdate = true;
  return t;
}

/** An even distribution: a point light drawn as a small spot whose light goes every way. */
let EVEN: THREE.DataTexture | null = null;
const evenTexture = () => (EVEN ??= curveTexture(new Float32Array(181).fill(1)));

/** The tracer's spot (three-gpu-pathtracer's PhysicalSpotLight is a SpotLight with an iesMap and a radius). */
function spot(colour: THREE.Color, candela: number, radius: number): THREE.SpotLight & { iesMap: THREE.Texture | null; radius: number } {
  const s = new THREE.SpotLight(colour, candela, 0, Math.PI / 4, 0, 2) as THREE.SpotLight & { iesMap: THREE.Texture | null; radius: number };
  s.iesMap = null;
  s.radius = radius;
  return s;
}

/**
 * Every light on (the engine's list) as the tracer's lights, in the live view's units: a point, a spot of its cone, a
 * profile, a panel. Each object carries `userData.source` (its id) and `userData.unit` (what its intensity is in).
 */
export function traceLights(sources: readonly LightSourceDto[], profiles: ReadonlyMap<string, LightProfileDto>): THREE.Light[] {
  const out: THREE.Light[] = [];
  const curves = new Map<string, THREE.DataTexture>();
  for (const s of sources) {
    if (!s.on || !(s.lumens > 0)) continue;
    const [r, g, b] = kelvinRgb(s.kelvin);
    const colour = new THREE.Color().setRGB(r, g, b, THREE.LinearSRGBColorSpace);
    const at = new THREE.Vector3(...s.position);
    const aim = new THREE.Vector3(...s.direction);
    if (aim.lengthSq() < 1e-9) aim.set(0, 0, -1);
    aim.normalize();
    const p = s.profile ? profiles.get(s.profile) : undefined;
    let light: THREE.Light;
    if (s.kind === 'area' && !p) {
      const panel = new THREE.RectAreaLight(colour, panelNits(s.lumens, s.width, s.length) * LUX_UNIT, Math.max(0.01, s.width), Math.max(0.01, s.length));
      panel.position.copy(at);
      panel.up.set(s.across[0], s.across[1], s.across[2]);
      panel.lookAt(at.clone().add(aim));
      panel.userData.unit = 'nit';
      light = panel;
    } else if (p) {
      const l = spot(colour, profileCandela(s.lumens, p.scale) * LUX_UNIT, SPOT_RADIUS);
      let tex = curves.get(p.key);
      if (!tex) { tex = curveTexture(profileCurve(p)); curves.set(p.key, tex); }
      l.iesMap = tex;
      // Its cone only places the lens behind the light (the profile shapes the light): a right angle keeps it there.
      l.angle = Math.PI / 2 - 1e-3;
      l.position.copy(at);
      l.target.position.copy(at.clone().add(aim));
      l.userData.unit = 'cd';
      light = l;
    } else if (s.kind === 'spot') {
      const field = Math.min(170, Math.max(2, s.field));
      const l = spot(colour, spotCandela(s.lumens, field) * LUX_UNIT, SPOT_RADIUS);
      l.angle = (field * Math.PI) / 360;
      l.penumbra = Math.min(1, Math.max(0, 1 - s.beam / field));
      l.position.copy(at);
      l.target.position.copy(at.clone().add(aim));
      l.userData.unit = 'cd';
      light = l;
    } else {
      // A point (or a panel with a profile drawn as one): even every way, a bulb's radius; a panel's light out of one face only.
      const l = spot(colour, (s.kind === 'area' ? 2 : 1) * pointCandela(s.lumens) * LUX_UNIT, POINT_RADIUS);
      l.iesMap = s.kind === 'area' ? curveTexture(new Float32Array(181).map((_, i) => (i <= 90 ? 1 : 0))) : evenTexture();
      l.angle = Math.PI / 2 - 1e-3;
      l.position.copy(at);
      l.target.position.copy(at.clone().add(aim));
      l.userData.unit = 'cd';
      light = l;
    }
    light.userData.source = s.id;
    light.updateMatrixWorld(true);
    if ((light as THREE.SpotLight).isSpotLight) { const t = (light as THREE.SpotLight).target; t.updateMatrixWorld(true); }
    out.push(light);
  }
  return out;
}

// ─── The sky ────────────────────────────────────────────────────────────

/** The sun's disc in the tracer's sky: its angular radius, degrees (a real sun's is 0.27°). */
export const SUN_RADIUS_DEG = 0.4;
/** The brightest the sun's disc is laid in (half floats end at 65 504). */
export const MAX_RADIANCE = 30_000;

/** The tracer's equirectangular layout (three-gpu-pathtracer's equirectUvToDirection): +Y at the pole. */
export function directionOf(u: number, v: number, out: THREE.Vector3): THREE.Vector3 {
  const theta = (u - 0.5) * 2 * Math.PI, phi = (1 - v) * Math.PI;
  return out.set(Math.sin(phi) * Math.cos(theta), Math.cos(phi), Math.sin(phi) * Math.sin(theta));
}

/** A disc of the sun laid into an equirectangular map (RGBA floats), its irradiance on a face towards it `light`. */
export function addSun(data: Float32Array, width: number, height: number, dir: THREE.Vector3, light: THREE.Color, radiusDeg = SUN_RADIUS_DEG): number {
  // Its radiance kept within half floats' range (the tracer keeps its sky so): a disc too bright for it is made larger.
  const peak = Math.max(light.r, light.g, light.b);
  const least = Math.sqrt(peak / MAX_RADIANCE / Math.PI) * (180 / Math.PI);
  radiusDeg = Math.max(radiusDeg, least);
  const cosR = Math.cos((radiusDeg * Math.PI) / 180);
  const d = dir.clone().normalize();
  const v = new THREE.Vector3();
  const hits: number[] = [];
  let solid = 0;
  const dTheta = (2 * Math.PI) / width, dPhi = Math.PI / height;
  // Only the rows the disc can reach.
  const centreV = 1 - Math.acos(Math.max(-1, Math.min(1, d.y))) / Math.PI;
  const rowSpan = Math.ceil(((radiusDeg * 2) / 180) * height) + 2;
  const j0 = Math.max(0, Math.floor(centreV * height) - rowSpan), j1 = Math.min(height - 1, Math.floor(centreV * height) + rowSpan);
  for (let j = j0; j <= j1; j++) {
    const vv = (j + 0.5) / height;
    const sinPhi = Math.sin((1 - vv) * Math.PI);
    for (let i = 0; i < width; i++) {
      directionOf((i + 0.5) / width, vv, v);
      if (v.dot(d) < cosR) continue;
      hits.push(j * width + i);
      solid += sinPhi * dTheta * dPhi;
    }
  }
  if (!hits.length || solid <= 0) return 0;
  // Its radiance so that what it gives a face towards it is the light asked (whatever the texels' sizes there).
  const r = light.r / solid, g = light.g / solid, b = light.b / solid;
  for (const k of hits) { data[4 * k] += r; data[4 * k + 1] += g; data[4 * k + 2] += b; }
  return hits.length;
}

// ─── The camera's look ──────────────────────────────────────────────────

/**
 * The exposure the tracer's picture is shown at: the profile's, times the night's automatic gain, times the user's
 * stops (2^EV), exactly as the live view's (render3d.ts, Realism.applyLook).
 */
export const exposureFactor = (profileExposure: number, gain: number, ev: number): number => profileExposure * gain * Math.pow(2, ev);

/** The size an image is rendered at: the window's, 1080p, 4K or the user's own, with the view's proportion where asked. */
export type ResolutionChoice = 'window' | '1080p' | '4k' | 'custom';

export function resolutionOf(choice: ResolutionChoice, view: { width: number; height: number }, custom?: { width: number; height: number }): { width: number; height: number } {
  const aspect = view.width / Math.max(1, view.height);
  const byHeight = (h: number) => ({ width: Math.round(h * aspect / 2) * 2, height: h });
  const clamp = (n: number) => Math.max(16, Math.min(8192, Math.round(n)));
  switch (choice) {
    case '1080p': return aspect >= 1 ? { width: 1920, height: Math.round(1920 / aspect / 2) * 2 } : byHeight(1920);
    case '4k': return aspect >= 1 ? { width: 3840, height: Math.round(3840 / aspect / 2) * 2 } : byHeight(3840);
    case 'custom': return { width: clamp(custom?.width ?? view.width), height: clamp(custom?.height ?? view.height) };
    default: return { width: clamp(view.width), height: clamp(view.height) };
  }
}
