import { describe, expect, it } from 'vitest';
import * as THREE from 'three';
import { addSun, buildTraceScene, clipGeometry, curveTexture, directionOf, exposureFactor, POINT_RADIUS, profileCurve, resolutionOf, sideOf, traceLights, traceMaterial, TraceMaterials, MAX_RADIANCE } from './pathTrace';
import { LUX_UNIT, panelNits, pointCandela, profileCandela, spotCandela, type LightProfileDto, type LightSourceDto } from './lights3d';
import { exposureGain, NIGHT_GAIN } from './render3d';
import { ALBEDO_FLOOR, denoiseCpu, mse, paramsFor, type Picture } from './denoise';
import { finished, progressOf, readout, readSettings, DEFAULT_SETTINGS } from './renderMode';

const box = () => new THREE.BoxGeometry(1, 1, 1);
const tris = (g: THREE.BufferGeometry) => (g.getIndex()?.count ?? g.getAttribute('position').count) / 3;

/** An element as the view draws it (Viewport3D meshOf): faces, edges, a pick-only face, a part with its coarser copy. */
function element(): THREE.Group {
  const g = new THREE.Group();
  const wall = new THREE.Mesh(box(), new THREE.MeshStandardMaterial({ color: 0xc08060 }));
  wall.position.set(2, 0, 0);
  g.add(wall);
  g.add(new THREE.LineSegments(new THREE.EdgesGeometry(box()), new THREE.LineBasicMaterial()));
  const hit = new THREE.Mesh(new THREE.PlaneGeometry(1, 1), new THREE.MeshBasicMaterial({ visible: false }));
  hit.userData.pickOnly = true;
  g.add(hit);
  // A heavy part (lod.ts): its finest copy (a sphere of many triangles) and a coarse one, the coarse one shown now.
  const fine = new THREE.Group(); fine.userData.lodLevel = true; fine.visible = false;
  fine.add(new THREE.Mesh(new THREE.SphereGeometry(0.5, 32, 16), new THREE.MeshStandardMaterial()));
  const coarse = new THREE.Group(); coarse.userData.lodLevel = true;
  coarse.add(new THREE.Mesh(new THREE.SphereGeometry(0.5, 6, 4), new THREE.MeshStandardMaterial()));
  g.add(fine, coarse);
  return g;
}

const src = (id: string, extra: Partial<LightSourceDto> = {}): LightSourceDto => ({
  id, kind: 'point', lumens: 1000, kelvin: 3000, beam: 30, field: 60, width: 0.6, length: 0.6, profile: null, on: true,
  position: [1, 2, 2.5], direction: [0, 0, -1], across: [1, 0, 0], ...extra,
});

describe('the path tracer’s scene is the view’s (docs/plan-render.md, S4)', () => {
  it('takes the faces the view draws, a heavy part’s finest copy, no lines, no pick-only faces', () => {
    const { scene, stats } = buildTraceScene({ roots: [element()] });
    const fineTris = tris(new THREE.SphereGeometry(0.5, 32, 16));
    expect(stats.triangles).toBe(12 + fineTris);
    expect(stats.meshes).toBe(2);
    expect(scene.children.every((c) => (c as THREE.Mesh).isMesh)).toBe(true);
  });

  it('leaves out what the view hides', () => {
    const g = element();
    g.children[0].visible = false;
    expect(buildTraceScene({ roots: [g] }).stats.meshes).toBe(1);
    g.visible = false;
    expect(buildTraceScene({ roots: [g] }).stats.meshes).toBe(0);
  });

  it('places every instance of a shared mesh, in its look’s colour', () => {
    const geometry = box();
    const material = new THREE.MeshStandardMaterial({ color: 0xffffff });
    const instances = [0, 1, 2].map((i) => ({ geometry, material, matrix: new THREE.Matrix4().makeTranslation(i * 3, 0, 0), colour: new THREE.Color(i === 1 ? 0xff0000 : 0x808080) }));
    const { scene, stats } = buildTraceScene({ roots: [], instances });
    expect(stats.triangles).toBe(36);
    const meshes = scene.children as THREE.Mesh[];
    expect(meshes[1].matrix.elements[12]).toBe(3);
    expect((meshes[1].material as THREE.MeshStandardMaterial).color.getHex()).toBe(0xff0000);
    // Two colours, two of the tracer's materials; the same colour shares one.
    expect(stats.materials).toBe(2);
  });

  it('cuts what a section cuts, triangle by triangle, and drops what lies wholly behind it', () => {
    const g = new THREE.Group();
    g.add(new THREE.Mesh(box(), new THREE.MeshStandardMaterial()));
    const far = new THREE.Mesh(box(), new THREE.MeshStandardMaterial());
    far.position.set(5, 0, 0);
    g.add(far);
    // Keeps x ≤ 0.2 (three's clipping keeps dot(n, p) + c ≥ 0).
    const plane = new THREE.Plane(new THREE.Vector3(-1, 0, 0), 0.2);
    const { scene, stats } = buildTraceScene({ roots: [g], planes: [plane] });
    expect(stats.meshes).toBe(1);
    expect(stats.clipped).toBe(1);
    const cut = (scene.children[0] as THREE.Mesh).geometry;
    const pos = cut.getAttribute('position');
    for (let i = 0; i < pos.count; i++) expect(pos.getX(i)).toBeLessThanOrEqual(0.2 + 1e-6);
    expect(pos.count).toBeGreaterThan(0);
    expect(cut.userData.traceOwned).toBe(true);
  });

  it('clips with normals and uvs carried along', () => {
    const g = new THREE.PlaneGeometry(2, 2);
    const out = clipGeometry(g, new THREE.Matrix4(), [new THREE.Plane(new THREE.Vector3(1, 0, 0), 0)])!;
    expect(out.getAttribute('uv')).toBeTruthy();
    expect(out.getAttribute('normal').getZ(0)).toBeCloseTo(1);
    const p = out.getAttribute('position');
    let area = 0;
    for (let i = 0; i < p.count; i += 3) {
      const a = new THREE.Vector3().fromBufferAttribute(p, i), b = new THREE.Vector3().fromBufferAttribute(p, i + 1), c = new THREE.Vector3().fromBufferAttribute(p, i + 2);
      area += b.sub(a).cross(c.sub(a)).length() / 2;
    }
    expect(area).toBeCloseTo(2, 5);
    expect(sideOf(new THREE.Box3(new THREE.Vector3(1, 0, 0), new THREE.Vector3(2, 1, 1)), [new THREE.Plane(new THREE.Vector3(-1, 0, 0), 0)])).toBe('out');
  });

  it('keeps a material’s own look: glass lets light through, a lamp glows, a selection’s highlight stays out', () => {
    const glass = new THREE.MeshPhysicalMaterial({ color: 0xddeeff, transparent: true, opacity: 0.3, roughness: 0.2, ior: 1.52 });
    glass.userData.glass = { transmission: 0.95 };
    const g = traceMaterial(glass) as THREE.MeshPhysicalMaterial;
    expect(g.transmission).toBeCloseTo(0.95);
    expect(g.transparent).toBe(false);
    expect(g.opacity).toBe(1);
    expect(g.ior).toBeCloseTo(1.52);
    const lamp = new THREE.MeshStandardMaterial({ emissive: 0x00ff00, emissiveIntensity: 0.45 });
    lamp.userData.emissive = { hex: 0xfff0c0, intensity: 4 };
    const l = traceMaterial(lamp);
    expect(l.emissive.getHex()).toBe(0xfff0c0);
    expect(l.emissiveIntensity).toBe(4);
    // Selected, without a glow of its own: nothing glows.
    const wall = new THREE.MeshStandardMaterial({ emissive: 0x3366ff, emissiveIntensity: 0.45 });
    expect(traceMaterial(wall).emissiveIntensity).toBe(0);
    // The S1 roughness map, which the drawing profile takes off, is the tracer's always.
    const rough = new THREE.Texture();
    const brick = new THREE.MeshStandardMaterial();
    brick.userData.surface = { roughness: rough, aoStrength: 1 };
    expect(traceMaterial(brick).roughnessMap).toBe(rough);
    // Not physically based: a rough standard material of its colour.
    expect(traceMaterial(new THREE.MeshBasicMaterial({ color: 0x123456 })).color.getHex()).toBe(0x123456);
  });

  it('copies a material once per version and colour, and lets go of the ones no scene uses', () => {
    const cache = new TraceMaterials();
    const m = new THREE.MeshStandardMaterial({ color: 0xffffff });
    expect(cache.of(m)).toBe(cache.of(m));
    const before = cache.of(m);
    m.color.set(0xff0000);
    expect(cache.of(m)).not.toBe(before);
    cache.prune(new Set([cache.of(m)]));
    expect(cache.size).toBe(1);
  });

  it('lets a light fixture’s own body and glass pass the shadow rays, as the view does', () => {
    const g = new THREE.Group();
    const shade = new THREE.Mesh(box(), new THREE.MeshStandardMaterial());
    shade.castShadow = false;
    g.add(shade);
    const { scene } = buildTraceScene({ roots: [g] });
    expect(((scene.children[0] as THREE.Mesh).material as THREE.Material & { castShadow: boolean }).castShadow).toBe(false);
  });

  it('puts a ground under the model where the view has one', () => {
    const meadow = new THREE.MeshStandardMaterial({ color: 0xa3ab86 });
    const { scene } = buildTraceScene({ roots: [], ground: { z: 0, material: meadow } });
    expect(scene.children[0].userData.ground).toBe(true);
  });
});

describe('every light, in the live view’s units', () => {
  it('a point: Φ / 4π candela, a bulb’s radius for soft shadows, even every way', () => {
    const [l] = traceLights([src('a')], new Map()) as (THREE.SpotLight & { radius: number; iesMap: THREE.DataTexture })[];
    expect(l.intensity).toBeCloseTo(pointCandela(1000) * LUX_UNIT, 12);
    expect(l.userData.unit).toBe('cd');
    expect(l.radius).toBe(POINT_RADIUS);
    expect((l.iesMap.image.data as Float32Array)[4 * 90]).toBe(1);
    expect((l.iesMap.image.data as Float32Array)[4 * 180]).toBe(1);
    expect(l.userData.source).toBe('a');
  });

  it('a spot: its lumens in its field, its beam full', () => {
    const [l] = traceLights([src('s', { kind: 'spot', beam: 30, field: 60 })], new Map()) as THREE.SpotLight[];
    expect(l.intensity).toBeCloseTo(spotCandela(1000, 60) * LUX_UNIT, 12);
    expect(l.angle).toBeCloseTo(Math.PI / 6);
    expect(l.penumbra).toBeCloseTo(0.5);
    expect(l.target.position.z).toBeCloseTo(1.5);
  });

  it('a panel: nits out of its face', () => {
    const [l] = traceLights([src('p', { kind: 'area', width: 0.6, length: 1.2 })], new Map()) as THREE.RectAreaLight[];
    expect(l.isRectAreaLight).toBe(true);
    expect(l.intensity).toBeCloseTo(panelNits(1000, 0.6, 1.2) * LUX_UNIT, 12);
    expect(l.width).toBeCloseTo(0.6);
    expect(l.height).toBeCloseTo(1.2);
    expect(l.userData.unit).toBe('nit');
  });

  it('a profile: its peak candela, its table round the axis as the tracer’s distribution', () => {
    // 3 gamma rows (0°, 90°, 180°) × 2 C columns: full down, half sideways (one column only), nothing up.
    const p: LightProfileDto = { key: 'k', name: 'test', scale: 2.5, beam: 40, field: 90, vertical: 3, horizontal: 2, table: [1, 1, 1, 0, 0, 0] };
    const curve = profileCurve(p);
    expect(curve[0]).toBe(1);
    expect(curve[90]).toBeCloseTo(0.5);
    expect(curve[180]).toBe(0);
    const [l] = traceLights([src('d', { kind: 'spot', profile: 'k' })], new Map([['k', p]])) as (THREE.SpotLight & { iesMap: THREE.Texture })[];
    expect(l.intensity).toBeCloseTo(profileCandela(1000, 2.5) * LUX_UNIT, 12);
    expect(l.iesMap).toBeTruthy();
    expect(curveTexture(curve).image.width).toBe(181);
  });

  it('none that is off or gives no light', () => {
    expect(traceLights([src('a', { on: false }), src('b', { lumens: 0 })], new Map())).toHaveLength(0);
  });

  it('the scene counts them', () => {
    expect(buildTraceScene({ roots: [], lights: [src('a'), src('b', { kind: 'spot' })] }).stats.lights).toBe(2);
  });
});

describe('the sky’s sun', () => {
  it('gives a face towards it the light asked, whatever the map’s texels there', () => {
    const W = 512, H = 256;
    for (const dir of [new THREE.Vector3(0.3, -0.5, 0.8), new THREE.Vector3(0, -1, 0.05)]) {
      const data = new Float32Array(W * H * 4);
      const light = new THREE.Color(3, 2.8, 2.5);
      expect(addSun(data, W, H, dir, light, 1)).toBeGreaterThan(0);
      let e = 0;
      const v = new THREE.Vector3();
      const d = dir.clone().normalize();
      for (let j = 0; j < H; j++) for (let i = 0; i < W; i++) {
        const r = data[4 * (j * W + i)];
        if (!r) continue;
        const sin = Math.sin((1 - (j + 0.5) / H) * Math.PI);
        e += r * sin * (2 * Math.PI / W) * (Math.PI / H) * directionOf((i + 0.5) / W, (j + 0.5) / H, v).dot(d);
      }
      expect(e).toBeGreaterThan(2.99);
      expect(e).toBeLessThan(3.01);
    }
  });

  it('is kept within half floats', () => {
    const W = 2048, H = 1024;
    const data = new Float32Array(W * H * 4);
    addSun(data, W, H, new THREE.Vector3(0.2, 0.3, 0.9), new THREE.Color(8, 8, 8), 0.27);
    let top = 0; for (let i = 0; i < data.length; i += 4) top = Math.max(top, data[i]);
    expect(top).toBeLessThanOrEqual(MAX_RADIANCE * 1.05);
  });
});

describe('the camera’s look', () => {
  it('exposure: each EV doubles, the night opens it as the live view does', () => {
    expect(exposureFactor(0.85, 1, 0)).toBeCloseTo(0.85);
    expect(exposureFactor(0.85, 1, 1)).toBeCloseTo(1.7);
    expect(exposureFactor(0.85, 1, -2)).toBeCloseTo(0.2125);
    expect(exposureFactor(1, exposureGain(1), 0)).toBeCloseTo(NIGHT_GAIN);
  });

  it('sizes: the window, 1080p and 4K in the view’s proportion, one’s own held to 8192', () => {
    const view = { width: 1600, height: 900 };
    expect(resolutionOf('window', view)).toEqual({ width: 1600, height: 900 });
    expect(resolutionOf('1080p', view)).toEqual({ width: 1920, height: 1080 });
    expect(resolutionOf('4k', view)).toEqual({ width: 3840, height: 2160 });
    expect(resolutionOf('custom', view, { width: 20000, height: 100 })).toEqual({ width: 8192, height: 100 });
    expect(resolutionOf('1080p', { width: 900, height: 1600 }).height).toBe(1920);
  });
});

describe('Lapin’s denoiser (denoise.ts)', () => {
  /** Two walls meeting at a corner (normals apart), one textured brick red and white, a shadow's edge across both, noise of a few samples. */
  function scene(noise: number, seed = 7): { clean: Float32Array; noisy: Picture } {
    const W = 64, H = 48, n = W * H;
    let s = seed;
    const rnd = () => ((s = (s * 16807) % 2147483647) / 2147483647);
    const clean = new Float32Array(n * 3), color = new Float32Array(n * 3), albedo = new Float32Array(n * 3), guide = new Float32Array(n * 4);
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const i = y * W + x;
      const left = x < W / 2;
      // Brick courses: a texture in the albedo the denoiser must keep.
      const mortar = y % 6 === 0;
      const a = left ? (mortar ? [0.7, 0.7, 0.68] : [0.55, 0.22, 0.15]) : [0.8, 0.8, 0.8];
      const light = (left ? 1.2 : 0.6) * (y < H * 0.6 ? 1 : 0.25);
      for (let c = 0; c < 3; c++) {
        albedo[3 * i + c] = a[c];
        clean[3 * i + c] = a[c] * light;
        // Path-tracing noise: the light's estimate wrong by a share, more where it is dimmer.
        color[3 * i + c] = a[c] * light * Math.max(0, 1 + noise * (rnd() * 2 - 1) * 1.7);
      }
      guide[4 * i] = left ? 1 : 0; guide[4 * i + 1] = left ? 0 : 1; guide[4 * i + 2] = 0; guide[4 * i + 3] = 5 + x * 0.01;
    }
    return { clean, noisy: { width: W, height: H, color, albedo, guide } };
  }

  it('lowers the error of a noisy picture against the clean one, by much', () => {
    const { clean, noisy } = scene(0.8);
    const before = mse(noisy.color, clean);
    const after = mse(denoiseCpu(noisy, paramsFor(4)), clean);
    expect(after).toBeLessThan(before * 0.25);
  });

  it('keeps the texture, the corner and the shadow’s edge', () => {
    const { clean, noisy } = scene(0.3);
    const out = denoiseCpu(noisy, paramsFor(32));
    const W = 64;
    const at = (a: Float32Array, x: number, y: number) => a[3 * (y * W + x)];
    // A mortar joint (y = 12) stays lighter than the brick beside it (y = 13): the albedo is put back after.
    expect(at(out, 10, 12) / at(out, 10, 13)).toBeCloseTo(at(clean, 10, 12) / at(clean, 10, 13), 1);
    // Across the corner (x 31 | 32) and the shadow (y 28 | 29) the light is not smeared: the rows' and columns' means
    // (the noise averaged out) keep the contrast the clean picture has, within a fifth.
    const mean = (a: Float32Array, xs: number[], ys: number[]) => { let t = 0; for (const x of xs) for (const y of ys) t += at(a, x, y); return t / (xs.length * ys.length); };
    const range = (a: number, b: number) => Array.from({ length: b - a + 1 }, (_, i) => a + i);
    const contrast = (a: Float32Array, xa: number[], ya: number[], xb: number[], yb: number[]) => mean(a, xa, ya) / mean(a, xb, yb);
    const rows: [number[], number[], number[], number[]] = [range(34, 62), [28], range(34, 62), [29]];
    expect(contrast(out, ...rows) / contrast(clean, ...rows)).toBeGreaterThan(0.8);
    const cols: [number[], number[], number[], number[]] = [[31], range(1, 26), [32], range(1, 26)];
    expect(contrast(out, ...cols) / contrast(clean, ...cols)).toBeGreaterThan(0.8);
    expect(contrast(out, ...cols) / contrast(clean, ...cols)).toBeLessThan(1.25);
  });

  it('leaves a converged picture nearly as it is', () => {
    const { clean, noisy } = scene(0.01);
    const out = denoiseCpu(noisy, paramsFor(4096));
    expect(mse(out, clean)).toBeLessThan(1e-4);
  });

  it('takes out a firefly (one path that found a lamp by luck)', () => {
    const { clean, noisy } = scene(0.01);
    const i = 3 * (20 * 64 + 40);
    noisy.color[i] *= 60; noisy.color[i + 1] *= 60; noisy.color[i + 2] *= 60;
    const out = denoiseCpu(noisy, paramsFor());
    expect(Math.abs(out[i] - clean[i]) / clean[i]).toBeLessThan(0.1);
  });

  it('passes the sky (no surface met) through untouched', () => {
    const { noisy } = scene(0.5);
    noisy.guide[3] = 0;
    const out = denoiseCpu(noisy, paramsFor(1));
    for (let c = 0; c < 3; c++) expect(out[c]).toBeCloseTo(noisy.color[c] / Math.max(noisy.albedo[c], ALBEDO_FLOOR) * Math.max(noisy.albedo[c], ALBEDO_FLOOR), 6);
  });
});

describe('the Render settings (renderMode.ts)', () => {
  it('read back checked: a bad value falls back to its default', () => {
    expect(readSettings(null)).toEqual(DEFAULT_SETTINGS);
    const s = readSettings({ resolution: '8k', samples: -5, fStop: 2, ev: 9, wb: 50000, limit: 'time', seconds: 1 });
    expect(s.resolution).toBe('window');
    expect(s.samples).toBe(1);
    expect(s.fStop).toBe(2);
    expect(s.ev).toBe(6);
    expect(s.wb).toBe('view');
    expect(s.limit).toBe('time');
    expect(s.seconds).toBe(5);
  });

  it('a picture is done by its samples or its time', () => {
    expect(finished({ limit: 'samples', samples: 256, seconds: 60 }, 255, 1000)).toBe(false);
    expect(finished({ limit: 'samples', samples: 256, seconds: 60 }, 256, 1)).toBe(true);
    expect(finished({ limit: 'time', samples: 256, seconds: 60 }, 10_000, 59)).toBe(false);
    expect(progressOf({ limit: 'time', samples: 256, seconds: 60 }, 0, 30)).toBeCloseTo(0.5);
    expect(readout(128.6, 62)).toBe('128 samples · 1:02');
  });
});
