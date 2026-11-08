/**
 * Point clouds in the 3D view (docs/point-clouds.md): each linked cloud's octree as the engine indexed it, streamed by
 * the camera. Every frame (on demand) the nodes in view are chosen from the root down, nearest to the eye first, a node's
 * children taken while its sample is coarser on screen than a point, until the point budget is spent; the nodes not yet
 * here are asked for in batches (`pointcloud.tiles`, raw arrays), and kept in a cache twice the budget, the least recently
 * drawn let go first. Each tile is one THREE.Points in a shared shader: coloured by RGB, intensity, elevation or class,
 * classes hidden, halftone, its size in pixels; clipped by the view's planes as everything else.
 */

import * as THREE from 'three';
import { pixelsPerMetre, type DisplayQuality, type LodCamera } from './lod';
import type { Vec3 } from './camera';

/** A node as the hierarchy lists it: name, points, box in the project (min, max), the spacing its sample keeps (m). */
export type NodeRow = [string, number, number, number, number, number, number, number, number];

export interface Hierarchy {
  id: string; points: number; nodes: NodeRow[]; box: number[];
  intensityRange: [number, number]; color: boolean; intensity: boolean; classification: boolean;
}

export interface Tile { link: string; name: string; positions: Float32Array; colors: Int32Array; values: Int32Array }

export type ColorMode = 'rgb' | 'intensity' | 'elevation' | 'classification';

/** How one cloud is drawn now: from its link and the view's graphics. */
export interface CloudSpec {
  id: string;
  /** Changes when what the tiles are made of changes (its placement, its file): the tiles are asked for again. */
  rev: string;
  shown: boolean;
  halftone: boolean;
  colorMode: ColorMode;
  pointSize: number;
  /** The ASPRS classes hidden (0–31; above 31 counts as 31). */
  hiddenClasses: readonly number[];
}

/** The points drawn at most, all clouds together, by the project's display quality (fewer in a browser). */
export function pointBudget(q: DisplayQuality, browser = false): number {
  const n = q === 'fast' ? 1_500_000 : q === 'precise' ? 6_000_000 : 3_000_000;
  return browser ? Math.round(n / 2) : n;
}

/** The ASPRS LAS 1.4 standard classes' colours (as most viewers draw them), the rest grey. */
export const CLASS_COLORS: Record<number, string> = {
  0: '#9e9e9e', 1: '#bdbdbd', 2: '#a0522d', 3: '#9ccc65', 4: '#43a047', 5: '#1b5e20', 6: '#e53935', 7: '#ff00ff', 8: '#ffeb3b',
  9: '#1e88e5', 10: '#8d6e63', 11: '#616161', 12: '#ffa726', 13: '#00acc1', 14: '#fdd835', 15: '#7e57c2', 16: '#26a69a', 17: '#ab47bc', 18: '#ec407a',
};

/** The classes' names (ASPRS LAS 1.4 R15, table 17), for the dialogs. */
export const CLASS_NAMES: Record<number, string> = {
  0: 'Never classified', 1: 'Unclassified', 2: 'Ground', 3: 'Low vegetation', 4: 'Medium vegetation', 5: 'High vegetation', 6: 'Building',
  7: 'Low point (noise)', 8: 'Model key point', 9: 'Water', 10: 'Rail', 11: 'Road surface', 12: 'Overlap', 13: 'Wire guard', 14: 'Wire conductor',
  15: 'Transmission tower', 16: 'Wire connector', 17: 'Bridge deck', 18: 'High noise',
};

interface Choice { names: string[]; points: number }

/**
 * The nodes to draw: from the root, nearest first, each in the frustum; a node's children too while its spacing covers
 * more than `pointPx` on screen, until `budget` points are taken. Pure, for the tests.
 */
export function chooseNodes(nodes: readonly NodeRow[], cam: LodCamera, inView: (row: NodeRow) => boolean, budget: number, pointPx: number): Choice {
  const children = new Map<string, NodeRow[]>();
  let root: NodeRow | null = null;
  for (const n of nodes) {
    if (n[0] === 'r') { root = n; continue; }
    const parent = n[0].slice(0, -1);
    const l = children.get(parent);
    if (l) l.push(n); else children.set(parent, [n]);
  }
  const out: Choice = { names: [], points: 0 };
  if (!root) return out;
  const ppmOf = (n: NodeRow) => {
    const c: [number, number, number] = [(n[2] + n[5]) / 2, (n[3] + n[6]) / 2, (n[4] + n[7]) / 2];
    const r = Math.hypot(n[5] - n[2], n[6] - n[3], n[7] - n[4]) / 2;
    return pixelsPerMetre(cam, c, r);
  };
  // A queue by how large a node's spacing is on screen, largest first.
  const queue: { n: NodeRow; px: number }[] = [{ n: root, px: Infinity }];
  while (queue.length) {
    let best = 0;
    for (let i = 1; i < queue.length; i++) if (queue[i].px > queue[best].px) best = i;
    const { n } = queue[best];
    queue[best] = queue[queue.length - 1];
    queue.pop();
    if (!inView(n)) continue;
    if (out.points + n[1] > budget && out.names.length > 0) break;
    out.names.push(n[0]);
    out.points += n[1];
    const px = n[8] * ppmOf(n);
    if (px <= pointPx * 0.75) continue;
    for (const c of children.get(n[0]) ?? []) queue.push({ n: c, px: c[8] * ppmOf(c) });
  }
  return out;
}

const VERT = /* glsl */ `
#include <common>
#include <clipping_planes_pars_vertex>
attribute vec3 rgb;
attribute float intensity;
attribute float cls;
uniform int mode;
uniform float size;
uniform vec2 zRange;
uniform vec2 iRange;
uniform vec3 classColors[32];
uniform float classShown[32];
uniform float halftone;
varying vec3 vColor;
vec3 ramp(float t) {
  t = clamp(t, 0.0, 1.0);
  vec3 a = vec3(0.17, 0.32, 0.75), b = vec3(0.1, 0.7, 0.85), c = vec3(0.3, 0.75, 0.3), d = vec3(0.95, 0.85, 0.2), e = vec3(0.85, 0.2, 0.15);
  if (t < 0.25) return mix(a, b, t / 0.25);
  if (t < 0.5) return mix(b, c, (t - 0.25) / 0.25);
  if (t < 0.75) return mix(c, d, (t - 0.5) / 0.25);
  return mix(d, e, (t - 0.75) / 0.25);
}
void main() {
  int k = int(min(cls, 31.0));
  #include <begin_vertex>
  #include <project_vertex>
  #include <clipping_planes_vertex>
  if (classShown[k] < 0.5) { gl_Position = vec4(2.0, 2.0, 2.0, 1.0); gl_PointSize = 0.0; return; }
  vec3 col;
  if (mode == 0) col = rgb;
  else if (mode == 1) { float t = (intensity * 65535.0 - iRange.x) / max(iRange.y - iRange.x, 1.0); col = vec3(pow(clamp(t, 0.0, 1.0), 0.6)); }
  else if (mode == 2) col = ramp((position.z - zRange.x) / max(zRange.y - zRange.x, 1e-3));
  else col = classColors[k];
  vColor = mix(col, vec3(0.62), halftone);
  gl_PointSize = size;
}`;

const FRAG = /* glsl */ `
#include <clipping_planes_pars_fragment>
varying vec3 vColor;
uniform float opacity;
void main() {
  #include <clipping_planes_fragment>
  vec2 d = gl_PointCoord - 0.5;
  if (dot(d, d) > 0.25) discard;
  gl_FragColor = vec4(vColor, opacity);
}`;

const MODE: Record<ColorMode, number> = { rgb: 0, intensity: 1, elevation: 2, classification: 3 };

function material(): THREE.ShaderMaterial {
  return new THREE.ShaderMaterial({
    vertexShader: VERT, fragmentShader: FRAG, clipping: true, transparent: false,
    uniforms: {
      mode: { value: 0 }, size: { value: 2 }, zRange: { value: new THREE.Vector2(0, 10) }, iRange: { value: new THREE.Vector2(0, 65535) },
      classColors: { value: Array.from({ length: 32 }, (_, k) => new THREE.Color(CLASS_COLORS[k] ?? '#9e9e9e')) },
      classShown: { value: new Array(32).fill(1) }, halftone: { value: 0 }, opacity: { value: 1 },
    },
  });
}

/** A tile as a THREE.Points: its positions, its colour bytes, intensity (0–1) and class. */
export function tileObject(t: Tile, mat: THREE.Material): THREE.Points {
  const n = t.colors.length;
  const rgb = new Uint8Array(3 * n), intensity = new Uint16Array(n), cls = new Uint8Array(n);
  for (let i = 0; i < n; i++) {
    const c = t.colors[i], v = t.values[i];
    rgb[3 * i] = (c >> 16) & 255; rgb[3 * i + 1] = (c >> 8) & 255; rgb[3 * i + 2] = c & 255;
    intensity[i] = v & 0xffff; cls[i] = (v >> 16) & 255;
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.BufferAttribute(t.positions.slice(), 3));
  g.setAttribute('rgb', new THREE.BufferAttribute(rgb, 3, true));
  g.setAttribute('intensity', new THREE.BufferAttribute(intensity, 1, true));
  g.setAttribute('cls', new THREE.BufferAttribute(cls, 1, false));
  g.computeBoundingBox();
  g.computeBoundingSphere();
  const o = new THREE.Points(g, mat);
  o.frustumCulled = false;
  o.userData = { pointTile: t.name, pointCloud: t.link };
  // Picked and snapped through `pointsNear`, never by three's raycaster (a million points a click).
  o.raycast = () => undefined;
  return o;
}

interface Cloud {
  spec: CloudSpec;
  hierarchy: Hierarchy | null;
  /** Asking for the hierarchy (or polling the index's status): not again until it lands. */
  asking: boolean;
  status: { state: string; stage?: string; done?: number; error?: string } | null;
  group: THREE.Group;
  mat: THREE.ShaderMaterial;
  tiles: Map<string, { object: THREE.Points; used: number }>;
  loading: Set<string>;
  boxes: Map<string, THREE.Box3>;
}

/** What the layer asks of the engine. */
export interface CloudSource {
  status(id: string): Promise<{ state: string; stage?: string; done?: number; error?: string } | null>;
  hierarchy(id: string): Promise<Hierarchy | null>;
  tiles(id: string, names: string[]): Promise<Tile[] | null>;
}

const POLL_MS = 600;
const BATCH = 12;
const IN_FLIGHT = 3;

/** The 3D view's clouds: one group a cloud, its tiles under it. */
export class PointCloudLayer {
  readonly group = new THREE.Group();
  private readonly clouds = new Map<string, Cloud>();
  private frameNo = 0;
  private flying = 0;
  private lastFrustum = new THREE.Frustum();
  private budget = 3_000_000;
  /** Called when a cloud's state changes (its index's progress, its points drawn), for the panels. */
  onStatus: (id: string, status: { state: string; stage?: string; done?: number; error?: string; drawn?: number }) => void = () => undefined;

  constructor(private readonly source: CloudSource, private readonly invalidate: () => void) {
    this.group.name = 'point-clouds';
  }

  /** The clouds as the links and the view now have them. */
  sync(specs: readonly CloudSpec[]): void {
    const keep = new Set(specs.map((s) => s.id));
    for (const [id, c] of this.clouds) if (!keep.has(id)) this.drop(id, c);
    for (const spec of specs) {
      let c = this.clouds.get(spec.id);
      if (c && c.spec.rev !== spec.rev) { this.drop(spec.id, c); c = undefined; }
      if (!c) {
        const group = new THREE.Group();
        group.name = `point-cloud:${spec.id}`;
        c = { spec, hierarchy: null, asking: false, status: null, group, mat: material(), tiles: new Map(), loading: new Set(), boxes: new Map() };
        this.clouds.set(spec.id, c);
        this.group.add(group);
        void this.ask(c);
      }
      c.spec = spec;
      c.group.visible = spec.shown;
      this.style(c);
    }
    this.invalidate();
  }

  private style(c: Cloud): void {
    const u = c.mat.uniforms;
    u.mode.value = MODE[c.spec.colorMode] ?? 0;
    u.size.value = Math.max(1, c.spec.pointSize) * (typeof window !== 'undefined' ? window.devicePixelRatio || 1 : 1);
    u.halftone.value = c.spec.halftone ? 0.6 : 0;
    const shown = new Array(32).fill(1);
    for (const k of c.spec.hiddenClasses) shown[Math.min(31, Math.max(0, k))] = 0;
    u.classShown.value = shown;
    if (c.hierarchy) {
      u.zRange.value.set(c.hierarchy.box[2], c.hierarchy.box[5]);
      u.iRange.value.set(c.hierarchy.intensityRange[0], c.hierarchy.intensityRange[1]);
    }
    c.mat.needsUpdate = true;
  }

  private drop(id: string, c: Cloud): void {
    for (const t of c.tiles.values()) t.object.geometry.dispose();
    c.mat.dispose();
    this.group.remove(c.group);
    this.clouds.delete(id);
  }

  /** The index's state, and its hierarchy once ready (polled while it is being made). */
  private async ask(c: Cloud): Promise<void> {
    if (c.asking) return;
    c.asking = true;
    try {
      const st = await this.source.status(c.spec.id);
      if (this.clouds.get(c.spec.id) !== c) return;
      c.status = st;
      this.onStatus(c.spec.id, st ?? { state: 'failed' });
      if (st?.state === 'ready') {
        const h = await this.source.hierarchy(c.spec.id);
        if (this.clouds.get(c.spec.id) !== c || !h) return;
        c.hierarchy = h;
        c.boxes.clear();
        for (const n of h.nodes) c.boxes.set(n[0], new THREE.Box3(new THREE.Vector3(n[2], n[3], n[4]), new THREE.Vector3(n[5], n[6], n[7])));
        this.style(c);
        this.invalidate();
      } else if (st?.state === 'indexing') {
        setTimeout(() => { c.asking = false; void this.ask(c); }, POLL_MS);
        return;
      }
    } finally {
      if (c.hierarchy || c.status?.state !== 'indexing') c.asking = false;
    }
  }

  /** The box round every cloud shown (for Zoom to fit), null when none is ready. */
  box(): THREE.Box3 | null {
    const b = new THREE.Box3();
    for (const c of this.clouds.values()) if (c.hierarchy && c.spec.shown) b.union(new THREE.Box3(new THREE.Vector3(...c.hierarchy.box.slice(0, 3)), new THREE.Vector3(...c.hierarchy.box.slice(3, 6))));
    return b.isEmpty() ? null : b;
  }

  /** A frame: the nodes in view chosen for every cloud, those here shown, those missing asked for. */
  update(camera: THREE.Camera, lod: LodCamera, quality: DisplayQuality, browser = false): void {
    this.frameNo++;
    this.budget = pointBudget(quality, browser);
    camera.updateMatrixWorld();
    const m = new THREE.Matrix4().multiplyMatrices(camera.projectionMatrix, camera.matrixWorldInverse);
    this.lastFrustum.setFromProjectionMatrix(m);
    const shown = [...this.clouds.values()].filter((c) => c.spec.shown && c.hierarchy);
    let left = this.budget;
    for (const c of shown) {
      const share = Math.max(100_000, Math.round(left / Math.max(1, shown.length - shown.indexOf(c))));
      const choice = chooseNodes(c.hierarchy!.nodes, lod, (n) => this.lastFrustum.intersectsBox(c.boxes.get(n[0])!), share, c.spec.pointSize);
      left -= Math.min(choice.points, share);
      const wanted = new Set(choice.names);
      let drawn = 0;
      for (const [name, t] of c.tiles) {
        t.object.visible = wanted.has(name);
        if (t.object.visible) { t.used = this.frameNo; drawn += t.object.geometry.getAttribute('position').count; }
      }
      const missing = choice.names.filter((n) => !c.tiles.has(n) && !c.loading.has(n));
      this.fetch(c, missing);
      this.evict(c);
      this.onStatus(c.spec.id, { state: 'ready', drawn });
    }
  }

  private fetch(c: Cloud, names: string[]): void {
    while (names.length && this.flying < IN_FLIGHT) {
      const batch = names.splice(0, BATCH);
      for (const n of batch) c.loading.add(n);
      this.flying++;
      void this.source.tiles(c.spec.id, batch).then((tiles) => {
        if (this.clouds.get(c.spec.id) !== c) return;
        for (const t of tiles ?? []) {
          if (c.tiles.has(t.name)) continue;
          const o = tileObject(t, c.mat);
          o.visible = false;
          c.group.add(o);
          c.tiles.set(t.name, { object: o, used: 0 });
        }
      }).catch(() => undefined).finally(() => {
        for (const n of batch) c.loading.delete(n);
        this.flying--;
        // Drawn with the tiles now here, which chooses again and asks for the next ones.
        this.invalidate();
      });
    }
  }

  /** Tiles beyond twice the budget let go, the least recently drawn first. */
  private evict(c: Cloud): void {
    let total = 0;
    for (const t of c.tiles.values()) total += t.object.geometry.getAttribute('position').count;
    if (total <= this.budget * 2) return;
    const old = [...c.tiles.entries()].filter(([, t]) => !t.object.visible).sort((a, b) => a[1].used - b[1].used);
    for (const [name, t] of old) {
      if (total <= this.budget * 2) break;
      total -= t.object.geometry.getAttribute('position').count;
      t.object.geometry.dispose();
      c.group.remove(t.object);
      c.tiles.delete(name);
    }
  }

  /** How many points are drawn now, all clouds. */
  drawn(): number {
    let n = 0;
    for (const c of this.clouds.values()) if (c.group.visible) for (const t of c.tiles.values()) if (t.object.visible) n += t.object.geometry.getAttribute('position').count;
    return n;
  }

  /**
   * The drawn points near the cursor's ray, nearest the eye first (at most `max`), for the snaps: within `reach(t)` metres
   * of the ray at a distance t along it (the cursor's tolerance in pixels as metres there), kept by the view's cuts.
   */
  pointsNear(ray: { origin: Vec3; dir: Vec3 }, reach: (t: number) => number, max = 8, kept: (p: Vec3) => boolean = () => true): Vec3[] {
    const out: { p: Vec3; t: number }[] = [];
    const { origin: o, dir: d } = ray;
    const sphere = new THREE.Sphere();
    for (const c of this.clouds.values()) {
      if (!c.group.visible) continue;
      const hidden = new Set(c.spec.hiddenClasses);
      for (const t of c.tiles.values()) {
        if (!t.object.visible) continue;
        const g = t.object.geometry;
        // A tile whose sphere the ray passes far from is not walked.
        sphere.copy(g.boundingSphere!);
        const wx = sphere.center.x - o.x, wy = sphere.center.y - o.y, wz = sphere.center.z - o.z;
        const tc = wx * d.x + wy * d.y + wz * d.z;
        const dc = Math.sqrt(Math.max(0, wx * wx + wy * wy + wz * wz - tc * tc));
        if (tc + sphere.radius < 0 || dc > sphere.radius + reach(Math.max(tc + sphere.radius, 0))) continue;
        const pos = g.getAttribute('position').array as Float32Array;
        const cls = g.getAttribute('cls').array as Uint8Array;
        for (let i = 0; i < cls.length; i++) {
          const x = pos[3 * i] - o.x, y = pos[3 * i + 1] - o.y, z = pos[3 * i + 2] - o.z;
          const along = x * d.x + y * d.y + z * d.z;
          if (along <= 0) continue;
          const r = reach(along);
          if (x * x + y * y + z * z - along * along > r * r) continue;
          if (hidden.size && hidden.has(Math.min(31, cls[i]))) continue;
          const p = { x: pos[3 * i], y: pos[3 * i + 1], z: pos[3 * i + 2] };
          if (!kept(p)) continue;
          out.push({ p, t: along });
        }
      }
    }
    out.sort((a, b) => a.t - b.t);
    return out.slice(0, max).map((q) => q.p);
  }

  dispose(): void {
    for (const [id, c] of this.clouds) this.drop(id, c);
  }
}
